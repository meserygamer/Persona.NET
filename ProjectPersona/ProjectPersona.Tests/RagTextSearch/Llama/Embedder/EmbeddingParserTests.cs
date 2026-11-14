using ProjectPersona.RagTextSearch.Llama.Embedder;

namespace ProjectPersona.Tests.RagTextSearch.Llama.Embedder;

/// <summary>
/// <see cref="EmbeddingParser.TryParseEmbeddings"/>: разбор OpenAI-совместимого ответа без исключений наружу.
/// </summary>
public class EmbeddingParserTests
{
    /// <summary>Пустое тело не является ответом эмбеддинг-API.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void TryParseEmbeddings_ReturnsFalse_WhenJsonIsEmpty(string? json)
    {
        var ok = EmbeddingParser.TryParseEmbeddings(json, out var embeddings, out var error);

        Assert.False(ok);
        Assert.Empty(embeddings);
        Assert.Equal("Пустой ответ.", error);
    }

    /// <summary>Синтаксически битый JSON возвращает текст ошибки и пустой набор векторов.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("{]")]
    [InlineData("{\"data\":[")]
    [InlineData("{\"data\":[1,]}")]
    public void TryParseEmbeddings_ReturnsFalse_WhenJsonIsInvalid(string json)
    {
        var ok = EmbeddingParser.TryParseEmbeddings(json, out var embeddings, out var error);

        Assert.False(ok);
        Assert.Empty(embeddings);
        Assert.StartsWith("Некорректный JSON:", error);
    }

    /// <summary>Корень ответа обязан быть объектом.</summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"data\"")]
    public void TryParseEmbeddings_ReturnsFalse_WhenRootIsNotObject(string json)
    {
        AssertFailure(json, "Корень JSON не является объектом.");
    }

    /// <summary>Поле <c>data</c> обязательно и должно быть массивом. Имя чувствительно к регистру.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Data\":[{\"embedding\":[1]}]}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"data\":1}")]
    public void TryParseEmbeddings_ReturnsFalse_WhenDataIsMissingOrNotArray(string json)
    {
        AssertFailure(json, "Поле 'data' отсутствует или не является массивом.");
    }

    /// <summary>Пустой <c>data</c> не считается успешным ответом.</summary>
    [Fact]
    public void TryParseEmbeddings_ReturnsFalse_WhenDataIsEmpty()
    {
        AssertFailure("{\"data\":[]}", "Массив 'data' пуст.");
    }

    /// <summary>У каждого элемента <c>data</c> должно быть поле <c>embedding</c>.</summary>
    [Theory]
    [InlineData("{\"data\":[null]}", 0)]
    [InlineData("{\"data\":[1]}", 0)]
    [InlineData("{\"data\":[{\"index\":0}]}", 0)]
    [InlineData("{\"data\":[{\"embedding\":[1]},{\"index\":1}]}", 1)]
    public void TryParseEmbeddings_ReturnsFalse_WhenItemHasNoEmbedding(string json, int position)
    {
        AssertFailure(json, $"Элемент data[{position}] не содержит поля 'embedding'.");
    }

    /// <summary>Строка вместо массива — типичный ответ с <c>encoding_format=base64</c>.</summary>
    [Fact]
    public void TryParseEmbeddings_ReturnsFalse_WhenEmbeddingIsNotArray()
    {
        AssertFailure(
            "{\"data\":[{\"embedding\":\"AAAA\"}]}",
            "data[0].embedding не является массивом (возможно, запрошен encoding_format=base64).");
    }

    /// <summary>Пустой вектор не принимается.</summary>
    [Fact]
    public void TryParseEmbeddings_ReturnsFalse_WhenEmbeddingIsEmpty()
    {
        AssertFailure("{\"data\":[{\"embedding\":[]}]}", "data[0].embedding пуст.");
    }

    /// <summary>В векторе допустимы только числа. Индекс в сообщении — позиция в массиве.</summary>
    [Theory]
    [InlineData("{\"data\":[{\"embedding\":[true]}]}", 0, 0)]
    [InlineData("{\"data\":[{\"embedding\":[null]}]}", 0, 0)]
    [InlineData("{\"data\":[{\"embedding\":[\"1\"]}]}", 0, 0)]
    [InlineData("{\"data\":[{\"embedding\":[{}]}]}", 0, 0)]
    [InlineData("{\"data\":[{\"embedding\":[1,\"x\"]}]}", 0, 1)]
    [InlineData("{\"data\":[{\"embedding\":[1]},{\"embedding\":[false]}]}", 1, 0)]
    public void TryParseEmbeddings_ReturnsFalse_WhenEmbeddingValueIsNotNumber(string json, int position, int component)
    {
        AssertFailure(json, $"data[{position}].embedding[{component}] не является числом.");
    }

    /// <summary>Один вектор в формате OpenAI, лишние поля игнорируются.</summary>
    [Fact]
    public void TryParseEmbeddings_ParsesOpenAiEmbeddingResponse()
    {
        const string json = """
            {
              "object": "list",
              "data": [
                {
                  "object": "embedding",
                  "index": 0,
                  "embedding": [-1.5, 0, 0.5, 100]
                }
              ],
              "model": "embedding-gemma",
              "usage": { "prompt_tokens": 3, "total_tokens": 3 }
            }
            """;

        AssertSuccess(json, [-1.5f, 0, 0.5f, 100]);
    }

    /// <summary>Несколько векторов без <c>index</c> сохраняют порядок элементов <c>data</c>.</summary>
    [Fact]
    public void TryParseEmbeddings_KeepsDataOrder_WhenIndexIsMissing()
    {
        const string json = """
            {"data":[{"embedding":[1,2]},{"embedding":[3,4,5]}]}
            """;

        AssertSuccess(json, [1, 2], [3, 4, 5]);
    }

    /// <summary>Векторы выстраиваются по <c>index</c>, а не по порядку в массиве.</summary>
    [Fact]
    public void TryParseEmbeddings_OrdersVectorsByIndex()
    {
        const string json = """
            {
              "data": [
                {"index": 2, "embedding": [20]},
                {"index": 0, "embedding": [0.25]},
                {"index": 1, "embedding": [-4]}
              ]
            }
            """;

        AssertSuccess(json, [0.25f], [-4], [20]);
    }

    /// <summary>
    /// <c>index</c> не <see cref="int"/> (дробь, строка, число вне <see cref="int"/>) заменяется позицией в <c>data</c>.
    /// Одинаковые индексы сохраняют исходный порядок.
    /// </summary>
    [Fact]
    public void TryParseEmbeddings_UsesPosition_WhenIndexIsNotInt32()
    {
        const string json = """
            {
              "data": [
                {"index": 1.5, "embedding": [15]},
                {"index": "1", "embedding": [10]},
                {"index": 0, "embedding": [0]},
                {"index": 9999999999, "embedding": [99]},
                {"embedding": [40]}
              ]
            }
            """;

        AssertSuccess(json, [15], [0], [10], [99], [40]);
    }

    /// <summary>Отрицательный <c>index</c> уходит в начало результата.</summary>
    [Fact]
    public void TryParseEmbeddings_SortsNegativeIndexFirst()
    {
        const string json = """
            {"data":[{"index":1,"embedding":[1]},{"index":-1,"embedding":[2]}]}
            """;

        AssertSuccess(json, [2], [1]);
    }

    /// <summary>Экспоненциальная запись читается как число вектора.</summary>
    [Fact]
    public void TryParseEmbeddings_ParsesExponentialComponents()
    {
        AssertSuccess("{\"data\":[{\"embedding\":[1e2,-5E-1]}]}", [100, -0.5f]);
    }

    /// <summary>Неуспешный разбор не отдаёт частично собранные векторы.</summary>
    [Fact]
    public void TryParseEmbeddings_ReturnsEmptyEmbeddings_WhenLaterItemIsInvalid()
    {
        const string json = """
            {"data":[{"embedding":[1,2,3]},{"embedding":"base64"}]}
            """;

        AssertFailure(json, "data[1].embedding не является массивом (возможно, запрошен encoding_format=base64).");
    }

    /// <summary>Успешный разбор: <paramref name="expected"/> — векторы в порядке <c>index</c>.</summary>
    private static void AssertSuccess(string json, params float[][] expected)
    {
        var ok = EmbeddingParser.TryParseEmbeddings(json, out var embeddings, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expected, embeddings);
    }

    /// <summary>Неуспешный разбор с точным текстом причины и пустым набором векторов.</summary>
    private static void AssertFailure(string json, string expectedError)
    {
        var ok = EmbeddingParser.TryParseEmbeddings(json, out var embeddings, out var error);

        Assert.False(ok);
        Assert.Empty(embeddings);
        Assert.Equal(expectedError, error);
    }
}
