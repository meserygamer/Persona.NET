using System.Text.Json.Serialization;

namespace ProjectPersona.RagTextSearch.Llama.Tokenizer;

/// <summary> Запрос к Llama токенайзеру. </summary>
public record LlamaTokenizerQuery
{
    /// <summary> Ключ вызываемой модели (Используется только когда API в режиме роутера). </summary>
    [JsonPropertyName("model")] 
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; set; }

    /// <summary> Контент. </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Добавлять ли специальные токены (например, BOS — Beginning of Sequence) в начало.
    /// По умолчанию: false.
    /// </summary>
    [JsonPropertyName("add_special")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AddSpecial { get; set; }

    /// <summary>
    /// Обрабатывать ли специальные токены как управляющие. Если false, они интерпретируются как обычный текст.
    /// По умолчанию: true.
    /// </summary>
    [JsonPropertyName("parse_special")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ParseSpecial { get; set; }

    /// <summary>
    /// Возвращать ли текстовые подстроки (pieces) вместе с числовыми идентификаторами токенов.
    /// По умолчанию: false.
    /// </summary>
    [JsonPropertyName("with_pieces")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? WithPieces { get; set; }
}