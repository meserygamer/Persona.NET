using ProjectPersona.LlamaWrapper;

namespace ProjectPersona.RagTextSearch.Llama;

/// <summary>
/// Ленивая загрузка одной <see cref="LlamaModelSession"/> на процесс для токенизации и эмбеддингов.
/// </summary>
public sealed class LlamaModelSessionHolder : IDisposable
{
    private readonly object _gate = new();
    private readonly LlamaSettings _settings;
    private readonly int _expectedEmbeddingDimension;

    private LlamaModelSession? _session;

    /// <summary>
    /// Создаёт holder с настройками Llama и ожидаемой размерностью вектора LanceDB.
    /// </summary>
    /// <param name="settings">Параметры модели и контекста.</param>
    /// <param name="expectedEmbeddingDimension">Размерность из LanceDB; <c>0</c> — не проверять.</param>
    public LlamaModelSessionHolder(LlamaSettings settings, int expectedEmbeddingDimension = 0)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _expectedEmbeddingDimension = expectedEmbeddingDimension;
    }

    /// <summary>
    /// Возвращает загруженную сессию, создавая её при первом обращении.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены до начала загрузки GGUF.</param>
    public LlamaModelSession GetSession(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_session is not null)
            return _session;

        lock (_gate)
        {
            if (_session is not null)
                return _session;

            var modelPath = ResolveModelPath(_settings);
            var options = new LlamaLoadOptions
            {
                NativeLibraryDirectory = string.IsNullOrWhiteSpace(_settings.NativeLibraryDirectory)
                    ? null
                    : _settings.NativeLibraryDirectory.Trim(),
                NGpuLayers = _settings.NGpuLayers,
                NCtx = _settings.NCtx,
                NBatch = _settings.NBatch,
                NUbatch = _settings.NUbatch,
                NSeqMax = _settings.NSeqMax,
                NThreads = _settings.NThreads,
                NThreadsBatch = _settings.NThreadsBatch,
                Devices = string.IsNullOrWhiteSpace(_settings.Devices) ? null : _settings.Devices.Trim(),
                FlashAttn = string.IsNullOrWhiteSpace(_settings.FlashAttn) ? null : _settings.FlashAttn.Trim(),
                ExpectedEmbeddingDimension = _expectedEmbeddingDimension
            };

            _session = LlamaModelSession.Load(modelPath, options);
            return _session;
        }
    }

    /// <summary>
    /// Возвращает фактический <c>n_ctx</c> загруженного контекста.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены до начала загрузки GGUF.</param>
    /// <returns>Число токенов контекста после <c>llama_init_from_model</c>.</returns>
    public uint GetContextTokenLimit(CancellationToken cancellationToken = default) =>
        GetSession(cancellationToken).ContextTokenLimit;

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _session?.Dispose();
            _session = null;
        }
    }

    private static string ResolveModelPath(LlamaSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ModelPath))
            return settings.ModelPath.Trim();

        throw new ArgumentException(
            "Для локальной модели укажите путь к GGUF в LlamaSettings.ModelPath.");
    }
}
