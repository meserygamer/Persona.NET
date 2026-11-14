using System.Runtime.InteropServices;
using System.Text;

namespace ProjectPersona.LlamaWrapper.Native;

/// <summary>
/// P/Invoke к <c>llama.dll</c> сборки b12359 (Windows x64, ABI MSVC).
/// </summary>
internal static class LlamaNative
{
    private const int ModelParamsSize = 80;
    private const int ContextParamsSize = 176;
    private const int PoolingTypeNone = 0;
    private const int LogLevelError = 4;
    private const int FlashAttnAuto = -1;
    private const int FlashAttnDisabled = 0;
    private const int FlashAttnEnabled = 1;

    private static readonly object Gate = new();
    private static readonly StringBuilder NativeErrors = new();
    private static readonly LlamaLogCallback LogCallback = OnLog;

    private static bool _backendReady;
    private static string? _loadedDirectory;

    static LlamaNative()
    {
        var modelParamsSize = Marshal.SizeOf<LlamaModelParams>();
        if (modelParamsSize != ModelParamsSize)
        {
            throw new InvalidOperationException(
                $"Ожидался размер llama_model_params {ModelParamsSize} байт, получен {modelParamsSize}.");
        }

        var contextParamsSize = Marshal.SizeOf<LlamaContextParams>();
        if (contextParamsSize != ContextParamsSize)
        {
            throw new InvalidOperationException(
                $"Ожидался размер llama_context_params {ContextParamsSize} байт, получен {contextParamsSize}.");
        }
    }

    /// <summary>
    /// Загружает словарь из GGUF и возвращает указатели модели и <c>llama_vocab</c>.
    /// </summary>
    /// <param name="modelPath">Путь к файлу GGUF.</param>
    /// <param name="nativeLibraryDirectory">Каталог с <c>llama.dll</c> или <see langword="null"/> для автопоиска.</param>
    /// <returns>Ненулевые указатели модели и словаря. Словарь жив, пока модель не освобождена.</returns>
    /// <exception cref="ArgumentException"><paramref name="modelPath"/> пустой.</exception>
    /// <exception cref="FileNotFoundException">Нет файла модели или <c>llama.dll</c>.</exception>
    /// <exception cref="InvalidOperationException">ABI <c>llama_model_params</c> не совпал или модель не открылась.</exception>
    /// <summary>
    /// Загружает GGUF и возвращает модель со словарём.
    /// </summary>
    /// <param name="modelPath">Путь к файлу GGUF.</param>
    /// <param name="nativeLibraryDirectory">Каталог с <c>llama.dll</c> или <see langword="null"/> для автопоиска.</param>
    /// <param name="vocabOnly">Только словарь, без весов.</param>
    /// <param name="nGpuLayers">
    /// <c>n_gpu_layers</c> при полной загрузке.
    /// <see langword="null"/> — значение <c>llama_model_default_params</c>.
    /// Игнорируется при <paramref name="vocabOnly"/>.
    /// </param>
    /// <param name="devices">
    /// Список устройств оффлоада. <see langword="null"/> — все доступные устройства.
    /// Игнорируется при <paramref name="vocabOnly"/>.
    /// </param>
    public static (IntPtr Model, IntPtr Vocab) LoadModel(
        string modelPath,
        string? nativeLibraryDirectory,
        bool vocabOnly,
        int? nGpuLayers = null,
        string? devices = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Путь к GGUF пустой.", nameof(modelPath));

        var fullModelPath = Path.GetFullPath(modelPath);
        if (!File.Exists(fullModelPath))
            throw new FileNotFoundException("Файл GGUF не найден.", fullModelPath);

        var nativeDirectory = ResolveNativeDirectory(nativeLibraryDirectory);

        lock (Gate)
        {
            EnsureBackend(nativeDirectory);
            ClearErrors();

            var parameters = llama_model_default_params();
            EnsureModelParamsCompatible(parameters);
            parameters.VocabOnly = (byte)(vocabOnly ? 1 : 0);
            if (!vocabOnly && nGpuLayers is int gpuLayers)
                parameters.NGpuLayers = gpuLayers;

            var deviceList = IntPtr.Zero;
            try
            {
                if (!vocabOnly && !string.IsNullOrWhiteSpace(devices))
                {
                    deviceList = CreateDeviceList(devices);
                    parameters.Devices = deviceList;
                }

                var model = llama_model_load_from_file(fullModelPath, parameters);
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        $"llama.dll не открыла модель '{fullModelPath}'. {ReadErrors()}".Trim());
                }

                var vocab = llama_model_get_vocab(model);
                if (vocab == IntPtr.Zero)
                {
                    llama_model_free(model);
                    throw new InvalidOperationException($"У модели '{fullModelPath}' нет словаря.");
                }

                return (model, vocab);
            }
            finally
            {
                if (deviceList != IntPtr.Zero)
                    Marshal.FreeHGlobal(deviceList);
            }
        }
    }

    /// <summary>
    /// Проверяет список устройств и значение flash attention, не загружая GGUF.
    /// </summary>
    /// <param name="modelPath">Путь к GGUF для поиска <c>llama.dll</c>, если заданы устройства.</param>
    /// <param name="nativeLibraryDirectory">Каталог <c>llama.dll</c> или <see langword="null"/>.</param>
    /// <param name="devices">Список устройств или <see langword="null"/>.</param>
    /// <param name="flashAttn">Значение flash attention или <see langword="null"/>.</param>
    public static void ValidateSelection(
        string modelPath,
        string? nativeLibraryDirectory,
        string? devices,
        string? flashAttn)
    {
        if (!string.IsNullOrWhiteSpace(flashAttn))
            ParseFlashAttn(flashAttn);

        if (string.IsNullOrWhiteSpace(devices))
            return;

        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Путь к GGUF пустой.", nameof(modelPath));

        var nativeDirectory = ResolveNativeDirectory(nativeLibraryDirectory);

        lock (Gate)
        {
            EnsureBackend(nativeDirectory);
            var deviceList = CreateDeviceList(devices);
            Marshal.FreeHGlobal(deviceList);
        }
    }

    /// <summary>Создаёт контекст inference для эмбеддингов.</summary>
    public static IntPtr CreateEmbeddingContext(IntPtr model, in LlamaContextParams parameters)
    {
        if (model == IntPtr.Zero)
            throw new ArgumentException("Модель не загружена.", nameof(model));

        lock (Gate)
        {
            ClearErrors();
            var context = llama_init_from_model(model, parameters);
            if (context == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"llama.dll не создала контекст. {ReadErrors()}".Trim());
            }

            return context;
        }
    }

    /// <summary>Освобождает контекст, созданный <see cref="CreateEmbeddingContext"/>.</summary>
    public static void FreeContext(IntPtr context)
    {
        if (context == IntPtr.Zero)
            return;

        lock (Gate)
            llama_free(context);
    }

    /// <summary>Размерность выходного эмбеддинга модели.</summary>
    public static int ModelEmbeddingDimension(IntPtr model) =>
        llama_model_n_embd_out(model);

    /// <summary>Строковое значение метаданных GGUF по ключу.</summary>
    /// <param name="model">Загруженная модель.</param>
    /// <param name="key">Ключ метаданных, например <c>general.name</c>.</param>
    /// <returns>Значение; <see langword="null"/>, если ключа нет.</returns>
    public static string? ModelMetadataValue(IntPtr model, string key)
    {
        var buffer = new byte[256];
        var length = llama_model_meta_val_str(model, key, buffer, (nuint)buffer.Length);
        if (length < 0)
            return null;

        // Функция возвращает полную длину значения, даже если оно не поместилось в буфер.
        if (length >= buffer.Length)
        {
            buffer = new byte[length + 1];
            length = llama_model_meta_val_str(model, key, buffer, (nuint)buffer.Length);
            if (length < 0)
                return null;
        }

        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    /// <summary>Фактический <c>n_ctx</c> созданного контекста.</summary>
    public static uint ContextSize(IntPtr context) => llama_n_ctx(context);

    /// <summary>Тип pooling контекста.</summary>
    public static int PoolingType(IntPtr context) => (int)llama_pooling_type(context);

    /// <summary>Очищает KV-память контекста перед очередным батчем эмбеддингов.</summary>
    public static void ClearMemory(IntPtr context, bool dataOnly = true)
    {
        var memory = llama_get_memory(context);
        if (memory == IntPtr.Zero)
            return;

        llama_memory_clear(memory, dataOnly);
    }

    /// <summary>Выделяет batch на стороне native.</summary>
    public static LlamaBatch BatchInit(int maxTokens, int maxSeqPerToken) =>
        llama_batch_init(maxTokens, 0, maxSeqPerToken);

    /// <summary>Освобождает batch, созданный <see cref="BatchInit"/>.</summary>
    public static void BatchFree(LlamaBatch batch) => llama_batch_free(batch);

    /// <summary>Запускает decode батча токенов.</summary>
    public static int Decode(IntPtr context, LlamaBatch batch) =>
        llama_decode(context, batch);

    /// <summary>Копирует эмбеддинг последовательности в managed-массив.</summary>
    public static float[] CopySequenceEmbedding(IntPtr context, int sequenceId, int dimension)
    {
        var pointer = llama_get_embeddings_seq(context, sequenceId);
        if (pointer == IntPtr.Zero)
            throw new InvalidOperationException($"Не удалось получить эмбеддинг для seq_id={sequenceId}.");

        var vector = new float[dimension];
        Marshal.Copy(pointer, vector, 0, dimension);
        return vector;
    }

    /// <summary>Копирует эмбеддинг токена по индексу в батче.</summary>
    public static float[] CopyTokenEmbedding(IntPtr context, int batchIndex, int dimension)
    {
        var pointer = llama_get_embeddings_ith(context, batchIndex);
        if (pointer == IntPtr.Zero)
            throw new InvalidOperationException($"Не удалось получить эмбеддинг для токена {batchIndex}.");

        var vector = new float[dimension];
        Marshal.Copy(pointer, vector, 0, dimension);
        return vector;
    }

    /// <summary>Собирает параметры контекста для эмбеддингов из <see cref="LlamaLoadOptions"/>.</summary>
    public static LlamaContextParams BuildEmbeddingContextParams(LlamaLoadOptions options)
    {
        var parameters = llama_context_default_params();
        EnsureContextParamsCompatible(parameters);

        if (options.NCtx is uint nCtx)
            parameters.NCtx = nCtx;

        if (options.NBatch is uint nBatch)
            parameters.NBatch = nBatch;

        if (options.NUbatch is uint nUbatch)
            parameters.NUbatch = nUbatch;

        if (options.NSeqMax is uint nSeqMax)
            parameters.NSeqMax = nSeqMax;

        if (options.NThreads is int nThreads)
            parameters.NThreads = nThreads;

        if (options.NThreadsBatch is int nThreadsBatch)
            parameters.NThreadsBatch = nThreadsBatch;

        if (!string.IsNullOrWhiteSpace(options.FlashAttn))
            parameters.FlashAttnType = ParseFlashAttn(options.FlashAttn);

        parameters.Embeddings = true;

        return parameters;
    }

    /// <summary>Обнуляет счётчик токенов в batch.</summary>
    public static void BatchReset(ref LlamaBatch batch) => batch.NTokens = 0;

    /// <summary>Добавляет токен в batch.</summary>
    public static void BatchAddToken(ref LlamaBatch batch, int token, int position, int sequenceId, bool outputEmbedding)
    {
        var index = batch.NTokens;
        Marshal.WriteInt32(batch.Token, index * sizeof(int), token);
        Marshal.WriteInt32(batch.Pos, index * sizeof(int), position);
        Marshal.WriteInt32(batch.NSeqId, index * sizeof(int), 1);

        var seqRow = Marshal.ReadIntPtr(batch.SeqId, index * IntPtr.Size);
        Marshal.WriteInt32(seqRow, 0, sequenceId);
        Marshal.WriteByte(batch.Logits, index, (byte)(outputEmbedding ? 1 : 0));
        batch.NTokens++;
    }

    /// <summary>Освобождает модель, загруженную <see cref="LoadModel"/>.</summary>
    /// <param name="model">Указатель модели. <see cref="IntPtr.Zero"/> игнорируется.</param>
    public static void FreeModel(IntPtr model)
    {
        if (model == IntPtr.Zero)
            return;

        lock (Gate)
            llama_model_free(model);
    }

    /// <summary>
    /// Токенизирует UTF-8 текст. При нехватке буфера возвращает отрицательную длину, как <c>llama_tokenize</c>.
    /// </summary>
    public static int Tokenize(
        IntPtr vocab,
        byte[] text,
        int textLength,
        int[] tokens,
        bool addSpecial,
        bool parseSpecial)
    {
        return llama_tokenize(vocab, text, textLength, tokens, tokens.Length, addSpecial, parseSpecial);
    }

    /// <summary>
    /// Пишет piece токена в буфер без завершающего нуля.
    /// Отрицательный результат — требуемая длина буфера.
    /// </summary>
    public static int TokenToPiece(IntPtr vocab, int token, byte[] buffer)
    {
        return llama_token_to_piece(vocab, token, buffer, buffer.Length, 0, true);
    }

    /// <summary>
    /// Собирает NULL-terminated список <c>ggml_backend_dev_t</c>, как <c>parse_device_list</c> в llama.cpp.
    /// <c>none</c> — список из одного NULL (оффлоада нет). CPU-устройства недопустимы.
    /// </summary>
    private static IntPtr CreateDeviceList(string value)
    {
        var names = value.Split(',');
        for (var i = 0; i < names.Length; i++)
            names[i] = names[i].Trim();

        if (names.Length == 1 && names[0] == "none")
            return AllocateDeviceList([]);

        var pointers = new IntPtr[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            if (names[i].Length == 0)
                throw new ArgumentException("В списке devices есть пустое имя.");

            var device = ggml_backend_dev_by_name(names[i]);
            if (device == IntPtr.Zero || IsCpuDeviceName(names[i]))
            {
                throw new ArgumentException(
                    $"Недопустимое устройство '{names[i]}'. Доступны для оффлоада: {DescribeOffloadDevices()}.");
            }

            pointers[i] = device;
        }

        return AllocateDeviceList(pointers);
    }

    /// <summary>Выделяет native-массив устройств с завершающим NULL.</summary>
    private static IntPtr AllocateDeviceList(IntPtr[] devices)
    {
        var list = Marshal.AllocHGlobal((devices.Length + 1) * IntPtr.Size);
        for (var i = 0; i < devices.Length; i++)
            Marshal.WriteIntPtr(list, i * IntPtr.Size, devices[i]);

        Marshal.WriteIntPtr(list, devices.Length * IntPtr.Size, IntPtr.Zero);
        return list;
    }

    /// <summary>Имена устройств оффлоада, без CPU, как <c>--list-devices</c>.</summary>
    private static string DescribeOffloadDevices()
    {
        var names = new List<string>();
        var count = ggml_backend_dev_count();
        for (nuint index = 0; index < count; index++)
        {
            var device = ggml_backend_dev_get(index);
            if (device == IntPtr.Zero)
                continue;

            var name = Marshal.PtrToStringUTF8(BackendDeviceName(device));
            if (string.IsNullOrEmpty(name) || IsCpuDeviceName(name))
                continue;

            names.Add(name);
        }

        return names.Count == 0 ? "нет" : string.Join(", ", names);
    }

    /// <summary>Имя CPU-устройства ggml; в <c>--device</c> его указывать нельзя.</summary>
    private static bool IsCpuDeviceName(string name) =>
        name.Equals("CPU", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Разбирает <c>--flash-attn</c> в <c>llama_flash_attn_type</c>.
    /// </summary>
    /// <returns><c>1</c> on, <c>0</c> off, <c>-1</c> auto.</returns>
    private static int ParseFlashAttn(string value)
    {
        switch (value.Trim())
        {
            case "on":
            case "enabled":
            case "true":
            case "1":
                return FlashAttnEnabled;
            case "off":
            case "disabled":
            case "false":
            case "0":
                return FlashAttnDisabled;
            case "auto":
            case "-1":
                return FlashAttnAuto;
            default:
                throw new ArgumentException(
                    $"Неизвестное значение flash_attn '{value}'. Допустимо: on, off, auto.");
        }
    }

    private static void EnsureBackend(string nativeDirectory)
    {
        if (_loadedDirectory is not null &&
            !string.Equals(_loadedDirectory, nativeDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"llama.dll уже загружена из '{_loadedDirectory}'.");
        }

        if (_backendReady)
            return;

        PrepareDependencySearch(nativeDirectory);
        var llamaHandle = LoadNativeLibrary(nativeDirectory);
        NativeLibrary.SetDllImportResolver(typeof(LlamaNative).Assembly, (libraryName, _, _) =>
        {
            if (IsLlamaLibrary(libraryName))
                return llamaHandle;

            if (IsGgmlLibrary(libraryName) && _ggmlHandle != IntPtr.Zero)
                return _ggmlHandle;

            return IntPtr.Zero;
        });

        llama_log_set(LogCallback, IntPtr.Zero);
        llama_backend_init();
        ggml_backend_load_all_from_path(nativeDirectory);
        _loadedDirectory = nativeDirectory;
        _backendReady = true;
    }

    private static IntPtr _ggmlHandle;
    private static IntPtr _ggmlBaseHandle;
    private static GgmlBackendDevName? _backendDeviceName;

    private static IntPtr LoadNativeLibrary(string nativeDirectory)
    {
        // libomp нужен ggml-cpu-*.dll, которые ggml подгружает уже после llama.dll.
        var openMp = Path.Combine(nativeDirectory, "libomp.dll");
        if (File.Exists(openMp))
            NativeLibrary.Load(openMp);

        _ggmlBaseHandle = NativeLibrary.Load(Path.Combine(nativeDirectory, "ggml-base.dll"));
        _ggmlHandle = NativeLibrary.Load(Path.Combine(nativeDirectory, "ggml.dll"));
        BindBackendDeviceName();

        var cpuBackend = Path.Combine(nativeDirectory, "ggml-cpu-x64.dll");
        if (File.Exists(cpuBackend))
            NativeLibrary.Load(cpuBackend);

        LoadBackendIfPresent(nativeDirectory, "ggml-vulkan.dll");
        LoadBackendIfPresent(nativeDirectory, "ggml-cuda.dll");

        return NativeLibrary.Load(Path.Combine(nativeDirectory, "llama.dll"));
    }

    /// <summary>
    /// Привязывает <c>ggml_backend_dev_name</c>.
    /// В b11986 символ экспортирует <c>ggml-base.dll</c>; в более старых сборках он оставался в <c>ggml.dll</c>.
    /// </summary>
    private static void BindBackendDeviceName()
    {
        if (NativeLibrary.TryGetExport(_ggmlBaseHandle, "ggml_backend_dev_name", out var export) ||
            NativeLibrary.TryGetExport(_ggmlHandle, "ggml_backend_dev_name", out export))
        {
            _backendDeviceName = Marshal.GetDelegateForFunctionPointer<GgmlBackendDevName>(export);
            return;
        }

        throw new EntryPointNotFoundException(
            "В ggml-base.dll и ggml.dll нет точки входа ggml_backend_dev_name.");
    }

    /// <summary>Имя устройства ggml. Вызывается только после <see cref="BindBackendDeviceName"/>.</summary>
    private static IntPtr BackendDeviceName(IntPtr device)
    {
        var callback = _backendDeviceName ?? throw new InvalidOperationException(
            "ggml_backend_dev_name не привязан.");
        return callback(device);
    }

    /// <summary>
    /// Грузит GPU-бэкенд, если файл лежит рядом с <c>llama.dll</c>.
    /// При ошибке называет отсутствующую зависимость, а не глотает её в <c>ggml_backend_load_all_from_path</c>.
    /// </summary>
    private static void LoadBackendIfPresent(string nativeDirectory, string fileName)
    {
        var path = Path.Combine(nativeDirectory, fileName);
        if (!File.Exists(path))
            return;

        try
        {
            NativeLibrary.Load(path);
        }
        catch (DllNotFoundException exception)
        {
            var missing = TryFindMissingDependency(path);
            if (missing is null)
                throw;

            throw new DllNotFoundException(
                $"Не удалось загрузить {fileName}: не найдена зависимость '{missing}'. " +
                DependencyHint(missing) +
                "Положите её в каталог с llama.dll или в PATH.",
                exception);
        }
    }

    /// <summary>Подсказка, если не хватает рантайма CUDA.</summary>
    private static string DependencyHint(string missing)
    {
        if (missing.Contains("cublas", StringComparison.OrdinalIgnoreCase) ||
            missing.Contains("cudart", StringComparison.OrdinalIgnoreCase) ||
            missing.Contains("nvrtc", StringComparison.OrdinalIgnoreCase))
        {
            return "Нужна среда CUDA той же мажорной версии, что и сборка llama. ";
        }

        return string.Empty;
    }

    /// <summary>
    /// Первая импортируемая DLL, которой нет ни рядом с модулем, ни в путях загрузчика.
    /// </summary>
    private static string? TryFindMissingDependency(string dllPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(dllPath);
            foreach (var name in ReadImportedDllNames(dllPath))
            {
                if (name.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (directory is not null && File.Exists(Path.Combine(directory, name)))
                    continue;

                if (NativeLibrary.TryLoad(name, out _))
                    continue;

                return name;
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>Имена DLL из import directory PE32+.</summary>
    private static List<string> ReadImportedDllNames(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> dos = stackalloc byte[64];
        if (stream.Read(dos) < 64 || dos[0] != (byte)'M' || dos[1] != (byte)'Z')
            return [];

        var pe = BitConverter.ToInt32(dos.Slice(0x3C, 4));
        stream.Position = pe;
        Span<byte> peHeader = stackalloc byte[24];
        if (stream.Read(peHeader) < 24 || BitConverter.ToUInt32(peHeader) != 0x4550)
            return [];

        var sections = BitConverter.ToUInt16(peHeader.Slice(6, 2));
        var optionalSize = BitConverter.ToUInt16(peHeader.Slice(20, 2));
        Span<byte> optional = stackalloc byte[optionalSize];
        if (optionalSize < 128 || stream.Read(optional) < optionalSize)
            return [];

        if (BitConverter.ToUInt16(optional) != 0x20B)
            return [];

        var importRva = BitConverter.ToUInt32(optional.Slice(120, 4));
        if (importRva == 0)
            return [];

        var mapped = new (uint VirtualAddress, uint VirtualSize, uint Raw, uint RawSize)[sections];
        Span<byte> section = stackalloc byte[40];
        for (var i = 0; i < sections; i++)
        {
            if (stream.Read(section) < 40)
                return [];

            mapped[i] = (
                BitConverter.ToUInt32(section.Slice(12, 4)),
                BitConverter.ToUInt32(section.Slice(8, 4)),
                BitConverter.ToUInt32(section.Slice(20, 4)),
                BitConverter.ToUInt32(section.Slice(16, 4)));
        }

        var names = new List<string>();
        var descriptor = RvaToOffset(importRva, mapped);
        Span<byte> raw = stackalloc byte[20];
        while (descriptor >= 0)
        {
            stream.Position = descriptor;
            if (stream.Read(raw) < 20)
                break;

            var nameRva = BitConverter.ToUInt32(raw.Slice(12, 4));
            if (nameRva == 0)
                break;

            var nameOffset = RvaToOffset(nameRva, mapped);
            if (nameOffset < 0)
                break;

            stream.Position = nameOffset;
            names.Add(ReadAsciiZ(stream));
            descriptor += 20;
        }

        return names;
    }

    private static int RvaToOffset(
        uint rva,
        (uint VirtualAddress, uint VirtualSize, uint Raw, uint RawSize)[] sections)
    {
        foreach (var section in sections)
        {
            var span = Math.Max(section.VirtualSize, section.RawSize);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + span)
                return (int)(section.Raw + (rva - section.VirtualAddress));
        }

        return -1;
    }

    private static string ReadAsciiZ(Stream stream)
    {
        var bytes = new List<byte>();
        int value;
        while ((value = stream.ReadByte()) > 0)
            bytes.Add((byte)value);

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static void PrepareDependencySearch(string nativeDirectory)
    {
        SetDllDirectory(nativeDirectory);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var prefix = nativeDirectory + Path.PathSeparator;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", prefix + path);
    }

    private static string ResolveNativeDirectory(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            throw new ArgumentException("Не задан каталог с llama.dll (LlamaSettings.NativeLibraryDirectory).", nameof(configured));

        return RequireLlamaDll(Path.GetFullPath(configured));
    }

    private static string RequireLlamaDll(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "llama.dll")))
            throw new FileNotFoundException("В каталоге нет llama.dll.", directory);

        return directory;
    }

    /// <summary>
    /// Сверяет значения <c>llama_model_default_params</c> со сборкой b11669.
    /// Совпадение полей значит, что смещения <c>llama_model_params</c> прочитаны верно.
    /// </summary>
    private static void EnsureModelParamsCompatible(in LlamaModelParams parameters)
    {
        if (parameters.NGpuLayers == -1 &&
            parameters.SplitMode == 1 &&
            parameters.LoadMode == -1 &&
            parameters.LazyMode == 1 &&
            parameters.MainGpu == 0 &&
            parameters.Devices == IntPtr.Zero &&
            parameters.TensorBuftOverrides == IntPtr.Zero &&
            parameters.TensorSplit == IntPtr.Zero &&
            parameters.ProgressCallback == IntPtr.Zero &&
            parameters.ProgressCallbackUserData == IntPtr.Zero &&
            parameters.KvOverrides == IntPtr.Zero &&
            parameters.VocabOnly == 0 &&
            parameters.CheckTensors == 0 &&
            parameters.UseExtraBufts == 1 &&
            parameters.NoHost == 0 &&
            parameters.NoAlloc == 0 &&
            parameters.LoadMtp == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Раскладка llama_model_params не совпала с llama.dll b11669. " +
            $"n_gpu_layers={parameters.NGpuLayers}, split={parameters.SplitMode}, load={parameters.LoadMode}, " +
            $"lazy={parameters.LazyMode}, main_gpu={parameters.MainGpu}, vocab_only={parameters.VocabOnly}, " +
            $"check_tensors={parameters.CheckTensors}, use_extra_bufts={parameters.UseExtraBufts}, " +
            $"no_host={parameters.NoHost}, no_alloc={parameters.NoAlloc}, load_mtp={parameters.LoadMtp}.");
    }

    /// <summary>
    /// Сверяет поля <c>llama_context_default_params</c> со сборкой b12359 (кроме <c>n_threads</c>).
    /// </summary>
    private static void EnsureContextParamsCompatible(in LlamaContextParams parameters)
    {
        if (parameters.NCtx == 512 &&
            parameters.NBatch == 2048 &&
            parameters.NUbatch == 512 &&
            parameters.NSeqMax == 1 &&
            parameters.PoolingType == -1 &&
            parameters.OffloadKqv &&
            parameters.OpOffload)
        {
            return;
        }

        throw new InvalidOperationException(
            "Раскладка llama_context_params не совпала с llama.dll b12359. " +
            $"n_ctx={parameters.NCtx}, n_batch={parameters.NBatch}, n_ubatch={parameters.NUbatch}, " +
            $"n_seq_max={parameters.NSeqMax}, pooling={parameters.PoolingType}.");
    }

    /// <summary>Используется при pooling NONE вместо sequence embedding.</summary>
    internal static bool IsPoolingNone(int poolingType) => poolingType == PoolingTypeNone;

    private static void ClearErrors()
    {
        lock (NativeErrors)
            NativeErrors.Clear();
    }

    private static string ReadErrors()
    {
        lock (NativeErrors)
            return NativeErrors.ToString().Trim();
    }

    private static void OnLog(int level, IntPtr text, IntPtr userData)
    {
        if (level < LogLevelError || text == IntPtr.Zero)
            return;

        var message = Marshal.PtrToStringUTF8(text);
        if (string.IsNullOrEmpty(message))
            return;

        lock (NativeErrors)
        {
            if (NativeErrors.Length > 4000)
                NativeErrors.Clear();

            NativeErrors.Append(message);
        }
    }

    private static bool IsLlamaLibrary(string libraryName) =>
        libraryName.Equals("llama", StringComparison.OrdinalIgnoreCase) ||
        libraryName.Equals("llama.dll", StringComparison.OrdinalIgnoreCase);

    private static bool IsGgmlLibrary(string libraryName) =>
        libraryName.Equals("ggml", StringComparison.OrdinalIgnoreCase) ||
        libraryName.Equals("ggml.dll", StringComparison.OrdinalIgnoreCase);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LlamaLogCallback(int level, IntPtr text, IntPtr userData);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void llama_log_set(LlamaLogCallback callback, IntPtr userData);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void llama_backend_init();

    [DllImport("ggml", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void ggml_backend_load_all_from_path(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? dirPath);

    [DllImport("ggml", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nuint ggml_backend_dev_count();

    [DllImport("ggml", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr ggml_backend_dev_get(nuint index);

    [DllImport("ggml", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr ggml_backend_dev_by_name(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GgmlBackendDevName(IntPtr device);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern LlamaModelParams llama_model_default_params();

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr llama_model_load_from_file(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pathModel,
        LlamaModelParams parameters);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void llama_model_free(IntPtr model);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr llama_model_get_vocab(IntPtr model);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int llama_tokenize(
        IntPtr vocab,
        byte[] text,
        int textLen,
        int[] tokens,
        int nTokensMax,
        [MarshalAs(UnmanagedType.U1)] bool addSpecial,
        [MarshalAs(UnmanagedType.U1)] bool parseSpecial);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int llama_token_to_piece(
        IntPtr vocab,
        int token,
        byte[] buffer,
        int length,
        int lstrip,
        [MarshalAs(UnmanagedType.U1)] bool special);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern LlamaContextParams llama_context_default_params();

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr llama_init_from_model(IntPtr model, LlamaContextParams parameters);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void llama_free(IntPtr context);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint llama_n_ctx(IntPtr context);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int llama_model_n_embd_out(IntPtr model);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int llama_model_meta_val_str(
        IntPtr model,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        byte[] buffer,
        nuint bufferSize);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern LlamaPoolingType llama_pooling_type(IntPtr context);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr llama_get_memory(IntPtr context);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void llama_memory_clear(IntPtr memory, [MarshalAs(UnmanagedType.U1)] bool dataOnly);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern LlamaBatch llama_batch_init(int nTokens, int embd, int nSeqMax);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void llama_batch_free(LlamaBatch batch);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int llama_decode(IntPtr context, LlamaBatch batch);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr llama_get_embeddings_seq(IntPtr context, int seqId);

    [DllImport("llama", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr llama_get_embeddings_ith(IntPtr context, int index);

    [StructLayout(LayoutKind.Sequential)]
    internal struct LlamaBatch
    {
        public int NTokens;
        public IntPtr Token;
        public IntPtr Embd;
        public IntPtr Pos;
        public IntPtr NSeqId;
        public IntPtr SeqId;
        public IntPtr Logits;
    }

    private enum LlamaPoolingType
    {
        None = 0
    }

    /// <summary>
    /// Копия <c>llama_context_params</c> из <c>include/llama.h</c> b12359 для win-x64.
    /// В b11669/b11986 есть <c>defrag_thold</c> и нет <c>moe_cache_cpu_share</c> — раскладка другая.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ContextParamsSize)]
    internal struct LlamaContextParams
    {
        [FieldOffset(0)] public uint NCtx;
        [FieldOffset(4)] public uint NBatch;
        [FieldOffset(8)] public uint NUbatch;
        [FieldOffset(12)] public uint NSeqMax;
        [FieldOffset(16)] public uint NRsSeq;
        [FieldOffset(20)] public uint NOutputsMax;
        [FieldOffset(24)] public uint NOutputsMaxPerSeq;
        [FieldOffset(28)] public int NThreads;
        [FieldOffset(32)] public int NThreadsBatch;
        [FieldOffset(36)] public int CtxType;
        [FieldOffset(40)] public int RopeScalingType;
        [FieldOffset(44)] public int PoolingType;
        [FieldOffset(48)] public int AttentionType;
        [FieldOffset(52)] public int FlashAttnType;
        [FieldOffset(56)] public float RopeFreqBase;
        [FieldOffset(60)] public float RopeFreqScale;
        [FieldOffset(64)] public float YarnExtFactor;
        [FieldOffset(68)] public float YarnAttnFactor;
        [FieldOffset(72)] public float YarnBetaFast;
        [FieldOffset(76)] public float YarnBetaSlow;
        [FieldOffset(80)] public uint YarnOrigCtx;
        [FieldOffset(88)] public IntPtr CbEval;
        [FieldOffset(96)] public IntPtr CbEvalUserData;
        [FieldOffset(104)] public int TypeK;
        [FieldOffset(108)] public int TypeV;
        [FieldOffset(112)] public UIntPtr MoeCacheSize;
        [FieldOffset(120)] public float MoeCacheCpuShare;
        [FieldOffset(128)] public IntPtr AbortCallback;
        [FieldOffset(136)] public IntPtr AbortCallbackData;
        [FieldOffset(144)] public bool Embeddings;
        [FieldOffset(145)] public bool OffloadKqv;
        [FieldOffset(146)] public bool NoPerf;
        [FieldOffset(147)] public bool OpOffload;
        [FieldOffset(148)] public bool SwaFull;
        [FieldOffset(149)] public bool KvUnified;
        [FieldOffset(152)] public IntPtr Samplers;
        [FieldOffset(160)] public UIntPtr NSamplers;
        [FieldOffset(168)] public IntPtr CtxOther;
    }

    /// <summary>
    /// Копия <c>llama_model_params</c> из <c>include/llama.h</c> b11669 для win-x64.
    /// Хвост из <c>bool</c> упакован по байту; после <c>main_gpu</c> четыре байта выравнивания.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ModelParamsSize)]
    private struct LlamaModelParams
    {
        [FieldOffset(0)] public IntPtr Devices;
        [FieldOffset(8)] public IntPtr TensorBuftOverrides;
        [FieldOffset(16)] public int NGpuLayers;
        [FieldOffset(20)] public int SplitMode;
        [FieldOffset(24)] public int LoadMode;
        [FieldOffset(28)] public int LazyMode;
        [FieldOffset(32)] public int MainGpu;
        [FieldOffset(40)] public IntPtr TensorSplit;
        [FieldOffset(48)] public IntPtr ProgressCallback;
        [FieldOffset(56)] public IntPtr ProgressCallbackUserData;
        [FieldOffset(64)] public IntPtr KvOverrides;
        [FieldOffset(72)] public byte VocabOnly;
        [FieldOffset(73)] public byte CheckTensors;
        [FieldOffset(74)] public byte UseExtraBufts;
        [FieldOffset(75)] public byte NoHost;
        [FieldOffset(76)] public byte NoAlloc;
        [FieldOffset(77)] public byte LoadMtp;
    }
}
