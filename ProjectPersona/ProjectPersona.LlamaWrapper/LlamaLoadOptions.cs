namespace ProjectPersona.LlamaWrapper;

/// <summary>
/// Параметры загрузки GGUF и контекста inference для эмбеддингов.
/// </summary>
public sealed record LlamaLoadOptions
{
    /// <summary>
    /// Каталог с <c>llama.dll</c> и зависимостями; <see langword="null"/> — автопоиск.
    /// </summary>
    public string? NativeLibraryDirectory { get; init; }

    /// <summary>
    /// <c>n_gpu_layers</c> при загрузке GGUF.
    /// <see langword="null"/> — значение <c>llama_model_default_params</c>.
    /// </summary>
    public int? NGpuLayers { get; init; }

    /// <summary>
    /// <c>n_ctx</c>. <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NCtx { get; init; }

    /// <summary>
    /// <c>n_batch</c>. <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NBatch { get; init; }

    /// <summary>
    /// <c>n_ubatch</c>. <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NUbatch { get; init; }

    /// <summary>
    /// <c>n_seq_max</c>. <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NSeqMax { get; init; }

    /// <summary>
    /// <c>n_threads</c>. <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public int? NThreads { get; init; }

    /// <summary>
    /// <c>n_threads_batch</c>. <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public int? NThreadsBatch { get; init; }

    /// <summary>
    /// Список устройств оффлоада через запятую, как <c>--device</c> в llama.cpp.
    /// <c>none</c> — не оффлоадить. <see langword="null"/> — все доступные устройства
    /// (<c>llama_model_params.devices == NULL</c>).
    /// </summary>
    public string? Devices { get; init; }

    /// <summary>
    /// <c>flash_attn_type</c>, как <c>--flash-attn</c>: <c>on</c>, <c>off</c> или <c>auto</c>
    /// (также <c>enabled</c>/<c>true</c>/<c>1</c>, <c>disabled</c>/<c>false</c>/<c>0</c>, <c>-1</c>).
    /// <see langword="null"/> — значение <c>llama_context_default_params</c> (<c>auto</c>).
    /// </summary>
    public string? FlashAttn { get; init; }

    /// <summary>
    /// Ожидаемая размерность эмбеддинга (например из LanceDB); <c>0</c> — не проверять.
    /// </summary>
    public int ExpectedEmbeddingDimension { get; init; }
}
