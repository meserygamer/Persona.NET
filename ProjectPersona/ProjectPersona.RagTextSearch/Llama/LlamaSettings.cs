using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.RagTextSearch.Llama;

/// <summary> Параметры Llama: локальный GGUF, эмбеддинги через <c>llama.dll</c>, шаблон документа и лимиты нарезки. </summary>
public record LlamaSettings : IValidatableObject
{
    /// <summary> Лимит токенов окна нарезки документа. Не задаёт параметры контекста llama. </summary>
    [Range(1, long.MaxValue, ErrorMessage = "{0} must be greater than zero.")]
    public long BatchSize { get; init; }

    /// <summary>
    /// <c>n_ctx</c>: размер контекста llama в токенах.
    /// <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NCtx { get; init; }

    /// <summary>
    /// <c>n_batch</c>: логический максимум токенов в одном <c>llama_decode</c>.
    /// <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NBatch { get; init; }

    /// <summary>
    /// <c>n_ubatch</c>: физический размер micro-batch.
    /// <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NUbatch { get; init; }

    /// <summary>
    /// <c>n_seq_max</c>: максимум последовательностей в одном decode.
    /// <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public uint? NSeqMax { get; init; }

    /// <summary> Путь к файлу GGUF с весами embedding-модели. </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "{0} is required.")]
    public string ModelPath { get; init; } = string.Empty;

    /// <summary>
    /// Каталог с <c>llama.dll</c> и её зависимостями. Обязателен, автопоиска нет.
    /// </summary>
    public string NativeLibraryDirectory { get; init; } = string.Empty;

    /// <summary>
    /// <c>n_gpu_layers</c> при загрузке GGUF.
    /// <see langword="null"/> — значение <c>llama_model_default_params</c>.
    /// </summary>
    public int? NGpuLayers { get; init; }

    /// <summary>
    /// <c>n_threads</c>: потоки генерации.
    /// <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public int? NThreads { get; init; }

    /// <summary>
    /// <c>n_threads_batch</c>: потоки обработки батча.
    /// <see langword="null"/> — значение <c>llama_context_default_params</c>.
    /// </summary>
    public int? NThreadsBatch { get; init; }

    /// <summary>
    /// Список устройств оффлоада через запятую, как <c>--device</c> в llama.cpp.
    /// <c>none</c> — не оффлоадить. <see langword="null"/> или пустая строка — все доступные устройства.
    /// </summary>
    public string? Devices { get; init; }

    /// <summary>
    /// <c>flash_attn_type</c>, как <c>--flash-attn</c>: <c>on</c>, <c>off</c> или <c>auto</c>.
    /// <see langword="null"/> или пустая строка — значение <c>llama_context_default_params</c> (<c>auto</c>).
    /// </summary>
    public string? FlashAttn { get; init; }
    
    /// <summary> Шаблон запроса на векторизацию документа: <c>{0}</c> — заголовок, <c>{1}</c> — текст фрагмента. </summary>
    public string DocumentQueryTemplate { get; init; } = "title: {0} | text: {1}";

    /// <summary> Шаблон промпта поискового запроса: <c>{0}</c> — текст запроса. </summary>
    public string SearchQueryTemplate { get; init; } = "task: code retrieval | query: {0}";

    /// <summary>
    /// Собирает промпт поискового запроса для эмбеддинга по <see cref="SearchQueryTemplate"/>.
    /// </summary>
    /// <param name="query">Текст поискового запроса.</param>
    /// <returns>Строка после <c>string.Format</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> равен <see langword="null"/>.</exception>
    public string FormatSearchQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return string.Format(CultureInfo.InvariantCulture, SearchQueryTemplate, query);
    }

    /// <summary>
    /// Собирает промпт документа для эмбеддинга по <see cref="DocumentQueryTemplate"/>.
    /// </summary>
    /// <param name="title">Заголовок (например FQN типа или члена).</param>
    /// <param name="text">Фрагмент исходного кода.</param>
    /// <returns>Строка после <c>string.Format</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="title"/> или <paramref name="text"/> равны <see langword="null"/>.
    /// </exception>
    public string FormatDocument(string title, string text)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(text);
        return string.Format(CultureInfo.InvariantCulture, DocumentQueryTemplate, title, text);
    }

    /// <summary> Запас свободного места под запрос при разбиении на мелкие части. </summary>
    [Range(0, long.MaxValue, ErrorMessage = "{0} cannot be negative.")]
    public long? FreeSpaceClearance { get; init; }

    /// <summary> Перекрытие окон сплиттера в токенах payload; <see langword="null"/> — 10% окна. </summary>
    [Range(0, long.MaxValue, ErrorMessage = "{0} cannot be negative.")]
    public long? OverlapTokens { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StringUtils.GetFormatArgumentsCount(DocumentQueryTemplate) != 2)
        {
            yield return new ValidationResult(
                $"{nameof(DocumentQueryTemplate)} must contain exactly two format placeholders ({{0}} and {{1}}).",
                [nameof(DocumentQueryTemplate)]);
        }
    }
}
