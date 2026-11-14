using System.Text;
using ProjectPersona.RagTextSearch.Llama.Tokenizer;

namespace ProjectPersona.Tests.RagTextSearch.Llama.Tokenizer;

/// <summary>
/// <see cref="TokenizerParser.CountTokensAsync"/>: число элементов <c>tokens</c> в UTF-8 JSON без сохранения идентификаторов.
/// </summary>
public class TokenizerParserTests
{
    /// <summary>Пустой поток — ответ токенизатора не пришёл.</summary>
    [Fact]
    public async Task CountTokensAsync_Throws_WhenStreamIsEmpty()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(new MemoryStream()));

        Assert.Equal("Пустой ответ токенизатора.", ex.Message);
    }

    /// <summary>
    /// Законченный JSON без свойства <c>tokens</c> на глубине 1.
    /// Имя свойства чувствительно к регистру.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("-3")]
    [InlineData("1.5")]
    [InlineData("1e2")]
    [InlineData("{\"a\":1}")]
    [InlineData("{\"Tokens\":[1,2,3]}")]
    [InlineData("{\"nested\":{\"tokens\":[1,2]}}")]
    public async Task CountTokensAsync_ThrowsMissingTokens_WhenDocumentHasNoTokensProperty(string json)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(Utf8(json)));

        Assert.Equal("Поле 'tokens' отсутствует.", ex.Message);
    }

    /// <summary>Поток оборвался после начала <c>tokens</c>, массив так и не закрыт.</summary>
    [Theory]
    [InlineData("{\"tokens\":")]
    [InlineData("{\"tokens\":[")]
    [InlineData("{\"tokens\":[1")]
    [InlineData("{\"tokens\":[1,")]
    public async Task CountTokensAsync_ThrowsTruncated_WhenTokensValueIsCutOff(string json)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(Utf8(json)));

        Assert.Equal("Ответ токенизатора обрезан.", ex.Message);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    /// <summary>Синтаксически битый JSON заворачивается в <see cref="InvalidOperationException"/>.</summary>
    [Theory]
    [InlineData("{]")]
    [InlineData("{")]
    [InlineData(" ")]
    [InlineData("{\"tokens\":[1,]}")]
    [InlineData("\"abc")]
    public async Task CountTokensAsync_WrapsJsonException_WhenJsonIsInvalid(string json)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(Utf8(json)));

        Assert.StartsWith("Некорректный JSON токенизатора:", ex.Message);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    /// <summary>Значение <c>tokens</c> обязано быть массивом.</summary>
    [Theory]
    [InlineData("{\"tokens\":null}")]
    [InlineData("{\"tokens\":1}")]
    [InlineData("{\"tokens\":true}")]
    [InlineData("{\"tokens\":\"x\"}")]
    [InlineData("{\"tokens\":{}}")]
    public async Task CountTokensAsync_Throws_WhenTokensIsNotArray(string json)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(Utf8(json)));

        Assert.Equal("Поле 'tokens' не является массивом.", ex.Message);
    }

    /// <summary>В массиве <c>tokens</c> допустимы только числа.</summary>
    [Theory]
    [InlineData("{\"tokens\":[\"x\"]}")]
    [InlineData("{\"tokens\":[true]}")]
    [InlineData("{\"tokens\":[false]}")]
    [InlineData("{\"tokens\":[null]}")]
    [InlineData("{\"tokens\":[{}]}")]
    [InlineData("{\"tokens\":[[]]}")]
    [InlineData("{\"tokens\":[1,\"x\"]}")]
    public async Task CountTokensAsync_Throws_WhenTokensArrayContainsNonNumber(string json)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(Utf8(json)));

        Assert.Equal("Массив 'tokens' должен содержать только числа.", ex.Message);
    }

    /// <summary>
    /// Один незакрытый JSON-токен длиннее арендованного буфера (8192 байта) не копится целиком.
    /// </summary>
    [Fact]
    public async Task CountTokensAsync_Throws_WhenSingleJsonTokenExceedsBuffer()
    {
        var json = "\"" + new string('a', 8192);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(Utf8(json)));

        Assert.Equal("Слишком длинный токен JSON в ответе токенизатора.", ex.Message);
    }

    /// <summary>Пустой массив <c>tokens</c> — ноль токенов.</summary>
    [Fact]
    public async Task CountTokensAsync_ReturnsZero_WhenTokensArrayIsEmpty()
    {
        var count = await TokenizerParser.CountTokensAsync(Utf8("{\"tokens\":[ ]}"));

        Assert.Equal(0, count);
    }

    /// <summary>Считаются целые, дробные и экспоненциальные числа; соседние поля игнорируются.</summary>
    [Fact]
    public async Task CountTokensAsync_CountsNumbersInTokensArray()
    {
        const string json = """
            {
              "model": "llama",
              "tokens": [-1, 0, 1.5, 1e2, 2E-1],
              "truncated": false
            }
            """;

        var count = await TokenizerParser.CountTokensAsync(Utf8(json));

        Assert.Equal(5, count);
    }

    /// <summary>Свойство <c>tokens</c> внутри вложенного объекта не считается: глубина не равна 1.</summary>
    [Fact]
    public async Task CountTokensAsync_IgnoresTokensPropertyNestedDeeperThanRoot()
    {
        const string json = "{\"a\":{\"tokens\":[1,2,3]},\"tokens\":[9,8]}";

        var count = await TokenizerParser.CountTokensAsync(Utf8(json));

        Assert.Equal(2, count);
    }

    /// <summary>После первого массива <c>tokens</c> разбор заканчивается, повтор ключа не читается.</summary>
    [Fact]
    public async Task CountTokensAsync_CountsOnlyFirstTokensArray()
    {
        const string json = "{\"tokens\":[1,2],\"tokens\":[3,4,5]}";

        var count = await TokenizerParser.CountTokensAsync(Utf8(json));

        Assert.Equal(2, count);
    }

    /// <summary>Конец массива достаточен для ответа: закрытие объекта и хвост после него не читаются.</summary>
    [Theory]
    [InlineData("{\"tokens\":[1,2,3]", 3)]
    [InlineData("{\"tokens\":[1,2,3]}NOT JSON", 3)]
    [InlineData("{\"tokens\":[4,5]}", 2)]
    public async Task CountTokensAsync_StopsAtEndOfTokensArray(string json, int expected)
    {
        var count = await TokenizerParser.CountTokensAsync(Utf8(json));

        Assert.Equal(expected, count);
    }

    /// <summary>Конец массива в первом чтении возвращает счётчик без повторного чтения потока.</summary>
    [Fact]
    public async Task CountTokensAsync_DoesNotReadPastCompletedTokensArray()
    {
        await using var stream = new OneShotStream(Encoding.UTF8.GetBytes("{\"tokens\":[7,8,9],\"extra\":true}"));

        var count = await TokenizerParser.CountTokensAsync(stream);

        Assert.Equal(3, count);
        Assert.False(stream.ReadPastFirstBuffer);
    }

    /// <summary>
    /// Поток по одному байту не режет многозначное число: <c>48</c> — один элемент, <c>4,8</c> — два.
    /// </summary>
    [Theory]
    [InlineData("{\"tokens\":[48]}", 1)]
    [InlineData("{\"tokens\":[4,8]}", 2)]
    [InlineData("{\"tokens\":[48,7]}", 2)]
    public async Task CountTokensAsync_Counts_WhenStreamYieldsOneByte(string json, long expected)
    {
        await using var stream = new OneByteStream(Encoding.UTF8.GetBytes(json));

        var count = await TokenizerParser.CountTokensAsync(stream);

        Assert.Equal(expected, count);
        Assert.Equal(1, stream.MaxBytesReturned);
    }

    /// <summary>Счётчик не зависит от того, какими кусками приходит тело ответа.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(64)]
    public async Task CountTokensAsync_Counts_WhenStreamIsChunked(int chunkSize)
    {
        const string json = "{\"before\":[1,2],\"tokens\":[10,20,30],\"after\":true}";
        await using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(json), chunkSize);

        var count = await TokenizerParser.CountTokensAsync(stream);

        Assert.Equal(3, count);
    }

    /// <summary>Обрыв массива <c>tokens</c> виден и когда тело приходит короткими кусками.</summary>
    [Fact]
    public async Task CountTokensAsync_ThrowsTruncated_WhenChunkedStreamEndsInsideTokens()
    {
        await using var stream = new ChunkedStream(Encoding.UTF8.GetBytes("{\"tokens\":[10,20"), 5);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TokenizerParser.CountTokensAsync(stream));

        Assert.Equal("Ответ токенизатора обрезан.", ex.Message);
    }

    /// <summary>Массив длиннее буфера чтения (8192 байта) считается целиком, с переносом хвоста между чтениями.</summary>
    [Fact]
    public async Task CountTokensAsync_Counts_WhenArrayCrossesReadBuffer()
    {
        const int tokenCount = 3000;
        var json = "{\"tokens\":[" + string.Join(',', Enumerable.Range(0, tokenCount)) + "]}";
        Assert.True(Encoding.UTF8.GetByteCount(json) > 8192);

        var count = await TokenizerParser.CountTokensAsync(Utf8(json));

        Assert.Equal(tokenCount, count);
    }

    /// <summary>Поток UTF-8 JSON из строки.</summary>
    private static MemoryStream Utf8(string json) => new(Encoding.UTF8.GetBytes(json));
}
