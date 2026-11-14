using System.Text.Json.Serialization;

namespace ProjectPersona.RagTextSearch.Llama.Tokenizer;

/// <summary> Токен Llama модели. </summary>
public record LlamaModelToken
{
    /// <summary> Id токена. </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary> Кусок текста представленный токеном. </summary>
    [JsonPropertyName("piece")]
    public string Piece { get; set; } = string.Empty;
};