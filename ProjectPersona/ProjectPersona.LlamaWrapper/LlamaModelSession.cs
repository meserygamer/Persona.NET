using System.Runtime.InteropServices;
using ProjectPersona.LlamaWrapper.Native;

namespace ProjectPersona.LlamaWrapper;

/// <summary>
/// Одна загрузка GGUF: словарь, контекст эмбеддингов и токенизация через <see cref="Tokenizer"/>.
/// </summary>
public sealed class LlamaModelSession : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly LlamaTokenizer _tokenizer;
    private readonly int _embeddingDimension;
    private readonly int _poolingType;
    private readonly uint _contextTokenLimit;
    private readonly uint _maxDecodeTokens;
    private readonly uint _maxSequences;

    private IntPtr _model;
    private IntPtr _context;

    private LlamaModelSession(
        IntPtr model,
        IntPtr context,
        LlamaTokenizer tokenizer,
        int embeddingDimension,
        int poolingType,
        uint contextTokenLimit,
        uint maxBatchTokens,
        uint maxUbatchTokens,
        uint maxSequences)
    {
        _model = model;
        _context = context;
        _tokenizer = tokenizer;
        _embeddingDimension = embeddingDimension;
        _poolingType = poolingType;
        _contextTokenLimit = contextTokenLimit;
        _maxDecodeTokens = Math.Min(maxBatchTokens, maxUbatchTokens);
        _maxSequences = maxSequences;
    }

    /// <summary>Токенизатор, разделяющий словарь этой сессии.</summary>
    public LlamaTokenizer Tokenizer => _tokenizer;

    /// <summary>Размерность выходного эмбеддинга.</summary>
    public int EmbeddingDimension => _embeddingDimension;

    /// <summary>Фактический <c>n_ctx</c> контекста после <c>llama_init_from_model</c>.</summary>
    public uint ContextTokenLimit => _contextTokenLimit;

    /// <summary>Фактический тип pooling контекста (<c>llama_pooling_type</c>).</summary>
    public int PoolingType => _poolingType;

    /// <summary>
    /// Возвращает строковое значение метаданных GGUF.
    /// </summary>
    /// <param name="key">Ключ метаданных, например <c>general.name</c>.</param>
    /// <returns>Значение; <see langword="null"/>, если ключа в модели нет.</returns>
    /// <exception cref="ObjectDisposedException">Сессия освобождена.</exception>
    public string? GetMetadataValue(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        _lock.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            return LlamaNative.ModelMetadataValue(_model, key);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Проверяет <see cref="LlamaLoadOptions.Devices"/> и <see cref="LlamaLoadOptions.FlashAttn"/> до загрузки весов.
    /// </summary>
    /// <param name="modelPath">Путь к GGUF; каталог файла участвует в поиске <c>llama.dll</c>.</param>
    /// <param name="options">Параметры устройств и flash attention.</param>
    public static void Validate(string modelPath, LlamaLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        LlamaNative.ValidateSelection(modelPath, options.NativeLibraryDirectory, options.Devices, options.FlashAttn);
    }

    /// <summary>
    /// Загружает модель с весами и создаёт контекст для эмбеддингов.
    /// </summary>
    /// <param name="modelPath">Путь к GGUF.</param>
    /// <param name="options">Параметры native и контекста.</param>
    /// <returns>Сессия, которую нужно освободить через <see cref="Dispose"/>.</returns>
    public static LlamaModelSession Load(string modelPath, LlamaLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (model, vocab) = LlamaNative.LoadModel(
            modelPath,
            options.NativeLibraryDirectory,
            vocabOnly: false,
            options.NGpuLayers,
            options.Devices);

        try
        {
            var embeddingDimension = LlamaNative.ModelEmbeddingDimension(model);
            if (embeddingDimension <= 0)
            {
                throw new InvalidOperationException(
                    "Модель не поддерживает эмбеддинги (llama_model_n_embd_out <= 0).");
            }

            if (options.ExpectedEmbeddingDimension > 0 &&
                options.ExpectedEmbeddingDimension != embeddingDimension)
            {
                throw new InvalidOperationException(
                    $"Размерность эмбеддинга модели {embeddingDimension} не совпала с ожидаемой " +
                    $"{options.ExpectedEmbeddingDimension}.");
            }

            var contextParams = LlamaNative.BuildEmbeddingContextParams(options);
            var context = LlamaNative.CreateEmbeddingContext(model, contextParams);
            var poolingType = LlamaNative.PoolingType(context);
            var contextTokenLimit = LlamaNative.ContextSize(context);

            var maxBatch = contextParams.NBatch;
            var maxUbatch = contextParams.NUbatch;
            var maxSeq = contextParams.NSeqMax;

            var tokenizer = LlamaTokenizer.Attach(model, vocab);
            return new LlamaModelSession(
                model,
                context,
                tokenizer,
                embeddingDimension,
                poolingType,
                contextTokenLimit,
                maxBatch,
                maxUbatch,
                maxSeq);
        }
        catch
        {
            LlamaNative.FreeModel(model);
            throw;
        }
    }

    /// <summary>
    /// Строит L2-нормализованные эмбеддинги для текстов в порядке входа.
    /// </summary>
    /// <param name="texts">Промпты.</param>
    /// <param name="addSpecial">Добавлять BOS/EOS по настройкам словаря.</param>
    /// <param name="parseSpecial">Разбирать специальные токены в тексте.</param>
    /// <param name="cancellationToken">Токен отмены между батчами.</param>
    /// <returns>Массив векторов той же длины, что и <paramref name="texts"/>.</returns>
    public float[][] Embed(
        IReadOnlyList<string> texts,
        bool addSpecial = true,
        bool parseSpecial = true,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
            return [];

        var tokenIds = new int[texts.Count][];
        for (var i = 0; i < texts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tokenIds[i] = _tokenizer.GetTokenIds(texts[i], addSpecial, parseSpecial);
        }

        return EmbedTokens(tokenIds, cancellationToken);
    }

    /// <summary>
    /// Строит L2-нормализованные эмбеддинги для уже токенизированных текстов в порядке входа.
    /// Токенизация выполняется у вызывающего кода, поэтому блокировка сессии держится только на decode.
    /// </summary>
    /// <param name="tokenIds">Идентификаторы токенов каждого текста; пустой массив даёт нулевой вектор.</param>
    /// <param name="cancellationToken">Токен отмены между батчами.</param>
    /// <returns>Массив векторов той же длины, что и <paramref name="tokenIds"/>.</returns>
    public float[][] EmbedTokens(
        IReadOnlyList<int[]> tokenIds,
        CancellationToken cancellationToken = default)
    {
        if (tokenIds.Count == 0)
            return [];

        var results = new float[tokenIds.Count][];

        _lock.EnterWriteLock();
        try
        {
            ThrowIfDisposed();

            var batchCapacity = (int)Math.Min(_maxDecodeTokens, int.MaxValue);
            var batch = LlamaNative.BatchInit(batchCapacity, (int)_maxSequences);
            try
            {
                var batchEntries = new List<(int Index, int[] Ids)>();
                var tokensInBatch = 0;

                for (var i = 0; i < tokenIds.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var ids = tokenIds[i];
                    if (ids.Length == 0)
                    {
                        results[i] = new float[_embeddingDimension];
                        continue;
                    }

                    if (ids.Length > _maxDecodeTokens)
                    {
                        throw new InvalidOperationException(
                            $"Текст #{i} содержит {ids.Length} токенов, лимит одного decode " +
                            $"(min(n_batch, n_ubatch)) {_maxDecodeTokens}. Уменьшите фрагмент или поднимите n_ubatch.");
                    }

                    if (batchEntries.Count >= _maxSequences 
                        || tokensInBatch + ids.Length > _maxDecodeTokens)
                    {
                        RunEmbeddingBatch(batchEntries, results, ref batch);
                        batchEntries.Clear();
                        tokensInBatch = 0;
                    }

                    batchEntries.Add((i, ids));
                    tokensInBatch += ids.Length;
                }

                if (batchEntries.Count > 0)
                    RunEmbeddingBatch(batchEntries, results, ref batch);
            }
            finally
            {
                LlamaNative.BatchFree(batch);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            // Сначала отцепляем токенизатор: после этого он не читает словарь, и модель можно освобождать.
            _tokenizer.MarkDetached();

            if (_context != IntPtr.Zero)
            {
                LlamaNative.FreeContext(_context);
                _context = IntPtr.Zero;
            }

            if (_model != IntPtr.Zero)
            {
                LlamaNative.FreeModel(_model);
                _model = IntPtr.Zero;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void RunEmbeddingBatch(
        List<(int Index, int[] Ids)> entries,
        float[][] results,
        ref LlamaNative.LlamaBatch batch)
    {
        LlamaNative.BatchReset(ref batch);

        for (var seq = 0; seq < entries.Count; seq++)
        {
            var ids = entries[seq].Ids;
            for (var pos = 0; pos < ids.Length; pos++)
                LlamaNative.BatchAddToken(ref batch, ids[pos], pos, seq, outputEmbedding: true);
        }

        LlamaNative.ClearMemory(_context, dataOnly: true);
        var status = LlamaNative.Decode(_context, batch);
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"llama_decode вернул код {status} для батча из {entries.Count} последовательностей.");
        }

        for (var seq = 0; seq < entries.Count; seq++)
        {
            float[] vector;
            if (LlamaNative.IsPoolingNone(_poolingType))
            {
                var lastTokenIndex = batch.NTokens - 1;
                for (var t = batch.NTokens - 1; t >= 0; t--)
                {
                    var seqRow = Marshal.ReadIntPtr(batch.SeqId, t * IntPtr.Size);
                    if (Marshal.ReadInt32(seqRow, 0) == seq)
                    {
                        lastTokenIndex = t;
                        break;
                    }
                }

                vector = LlamaNative.CopyTokenEmbedding(_context, lastTokenIndex, _embeddingDimension);
            }
            else
            {
                vector = LlamaNative.CopySequenceEmbedding(_context, seq, _embeddingDimension);
            }

            LlamaEmbeddingMath.L2Normalize(vector);
            results[entries[seq].Index] = vector;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_model == IntPtr.Zero || _context == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(LlamaModelSession));
    }
}
