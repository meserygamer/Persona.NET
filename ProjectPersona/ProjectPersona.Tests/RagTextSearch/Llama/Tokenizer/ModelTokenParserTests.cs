using System.Text;
using ProjectPersona.RagTextSearch.Llama.Tokenizer;

namespace ProjectPersona.Tests.RagTextSearch.Llama.Tokenizer;

/// <summary>
/// <see cref="ModelTokenParser.ReadTokensAsync"/>: объекты <c>tokens</c> с <c>id</c> и строкой <c>piece</c>.
/// </summary>
public class ModelTokenParserTests
{
    /// <summary>Пустой поток — ответ токенизатора не пришёл.</summary>
    [Fact]
    public async Task ReadTokensAsync_Throws_WhenStreamIsEmpty()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ModelTokenParser.ReadTokensAsync(new MemoryStream()));

        Assert.Equal("Пустой ответ токенизатора.", ex.Message);
    }

    /// <summary>Отрицательная начальная ёмкость отклоняется до чтения потока.</summary>
    [Fact]
    public async Task ReadTokensAsync_Throws_WhenInitialCapacityIsNegative()
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ModelTokenParser.ReadTokensAsync(new MemoryStream(), -1));

        Assert.Equal("initialCapacity", ex.ParamName);
        Assert.Contains("Начальная ёмкость не может быть отрицательной.", ex.Message);
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
    [InlineData("{\"a\":1}")]
    [InlineData("{\"Tokens\":[{\"id\":1,\"piece\":\"a\"}]}")]
    [InlineData("{\"nested\":{\"tokens\":[{\"id\":1,\"piece\":\"a\"}]}}")]
    public async Task ReadTokensAsync_ThrowsMissingTokens_WhenDocumentHasNoTokensProperty(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Поле 'tokens' отсутствует.", ex.Message);
    }

    /// <summary>Поток оборвался после начала <c>tokens</c>, массив объектов так и не закрыт.</summary>
    [Theory]
    [InlineData("{\"tokens\":")]
    [InlineData("{\"tokens\":[")]
    [InlineData("{\"tokens\":[{\"id\":1")]
    [InlineData("{\"tokens\":[{\"id\":1,")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":\"ab")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":\"a\"}")]
    public async Task ReadTokensAsync_ThrowsTruncated_WhenTokensValueIsCutOff(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Ответ токенизатора обрезан.", ex.Message);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    /// <summary>Синтаксически битый JSON заворачивается в <see cref="InvalidOperationException"/>.</summary>
    [Theory]
    [InlineData("{]")]
    [InlineData("{")]
    [InlineData(" ")]
    [InlineData("{\"tokens\":[{\"id\":1,}]}")]
    [InlineData("\"abc")]
    public async Task ReadTokensAsync_WrapsJsonException_WhenJsonIsInvalid(string json)
    {
        var ex = await ReadFails(json);

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
    public async Task ReadTokensAsync_Throws_WhenTokensIsNotArray(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Поле 'tokens' не является массивом.", ex.Message);
    }

    /// <summary>В массиве <c>tokens</c> допустимы только объекты.</summary>
    [Theory]
    [InlineData("{\"tokens\":[1]}")]
    [InlineData("{\"tokens\":[\"x\"]}")]
    [InlineData("{\"tokens\":[true]}")]
    [InlineData("{\"tokens\":[null]}")]
    [InlineData("{\"tokens\":[[]]}")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":\"a\"},1]}")]
    public async Task ReadTokensAsync_Throws_WhenTokensArrayContainsNonObject(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Массив 'tokens' должен содержать только объекты.", ex.Message);
    }

    /// <summary>У объекта нет <c>id</c>, <c>piece</c> или обоих. Регистр имён имеет значение.</summary>
    [Theory]
    [InlineData("{\"tokens\":[{}]}")]
    [InlineData("{\"tokens\":[{\"id\":1}]}")]
    [InlineData("{\"tokens\":[{\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"Id\":1,\"Piece\":\"a\"}]}")]
    public async Task ReadTokensAsync_Throws_WhenIdOrPieceIsMissing(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Элемент массива 'tokens' должен содержать 'id' и 'piece'.", ex.Message);
    }

    /// <summary><c>id</c> — целое число, включая ноль как отдельный флаг, а не «поле отсутствует».</summary>
    [Theory]
    [InlineData("{\"tokens\":[{\"id\":1.5,\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"id\":true,\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"id\":null,\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"id\":\"9\",\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"id\":{},\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"id\":[],\"piece\":\"a\"}]}")]
    [InlineData("{\"tokens\":[{\"id\":9223372036854775808,\"piece\":\"a\"}]}")]
    public async Task ReadTokensAsync_Throws_WhenIdIsNotInt64(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Поле 'id' должно быть целым числом.", ex.Message);
    }

    /// <summary><c>piece</c> — строка. Массив байт тоже не принимается.</summary>
    [Theory]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":1}]}")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":true}]}")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":null}]}")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":{}}]}")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":[]}]}")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":[195,161]}]}")]
    public async Task ReadTokensAsync_Throws_WhenPieceIsNotString(string json)
    {
        var ex = await ReadFails(json);

        Assert.Equal("Поле 'piece' должно быть строкой.", ex.Message);
    }

    /// <summary>
    /// Один незакрытый JSON-токен длиннее арендованного буфера (8192 байта) не копится целиком.
    /// </summary>
    [Fact]
    public async Task ReadTokensAsync_Throws_WhenSingleJsonTokenExceedsBuffer()
    {
        var json = "{\"tokens\":[{\"id\":1,\"piece\":\"" + new string('a', 8192);
        var ex = await ReadFails(json);

        Assert.Equal("Слишком длинный токен JSON в ответе токенизатора.", ex.Message);
    }

    /// <summary>Пустой массив <c>tokens</c> — пустой результат. Нулевая ёмкость списка этому не мешает.</summary>
    [Fact]
    public async Task ReadTokensAsync_ReturnsEmpty_WhenTokensArrayIsEmpty()
    {
        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8("{\"tokens\":[ ]}"), 0);

        Assert.Empty(tokens);
    }

    /// <summary>Ответ сервиса с <c>with_pieces</c>: порядок, пробелы в кусках и служебные токены сохраняются.</summary>
    [Fact]
    public async Task ReadTokensAsync_ReadsServicePayloadInOrder()
    {
        const string json = """
            {
              "tokens": [
                { "id": 2, "piece": "<bos>" },
                { "id": 21787, "piece": "ping" },
                { "id": 1, "piece": "<eos>" },
                { "id": 9259, "piece": "Hello" },
                { "id": 236764, "piece": "," },
                { "id": 1902, "piece": " world" },
                { "id": 236888, "piece": "!" },
                { "id": 3250, "piece": "title" },
                { "id": 236787, "piece": ":" },
                { "id": 6824, "piece": " Program" },
                { "id": 236761, "piece": "." },
                { "id": 5188, "piece": "cs" },
                { "id": 1109, "piece": " |" },
                { "id": 1816, "piece": " text" },
                { "id": 236787, "piece": ":" },
                { "id": 6400, "piece": " Main" },
                { "id": 825, "piece": "()" }
              ]
            }
            """;

        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal(
            [
                Token(2, "<bos>"),
                Token(21787, "ping"),
                Token(1, "<eos>"),
                Token(9259, "Hello"),
                Token(236764, ","),
                Token(1902, " world"),
                Token(236888, "!"),
                Token(3250, "title"),
                Token(236787, ":"),
                Token(6824, " Program"),
                Token(236761, "."),
                Token(5188, "cs"),
                Token(1109, " |"),
                Token(1816, " text"),
                Token(236787, ":"),
                Token(6400, " Main"),
                Token(825, "()")
            ],
            tokens);
    }

    /// <summary>
    /// <c>piece</c> может идти раньше <c>id</c>. Ноль, отрицательный id, пустая строка и экранирование сохраняются.
    /// Повтор поля берёт последнее значение. Соседние поля корня игнорируются.
    /// </summary>
    [Fact]
    public async Task ReadTokensAsync_ReadsFieldsInAnyOrderAndDecodesStrings()
    {
        const string json = """
            {
              "model": "llama",
              "tokens": [
                { "piece": " world", "id": 0 },
                { "id": -5, "piece": "" },
                { "id": 1, "id": 9223372036854775807, "piece": "a", "piece": "a\"b\n<" }
              ]
            }
            """;

        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal(
            [
                Token(0, " world"),
                Token(-5, ""),
                Token(long.MaxValue, "a\"b\n<")
            ],
            tokens);
    }

    /// <summary>Неизвестные свойства пропускаются, в том числе вложенные объект и массив с чужими <c>id</c>.</summary>
    [Fact]
    public async Task ReadTokensAsync_SkipsUnknownProperties()
    {
        const string json = """
            {
              "tokens": [
                {
                  "id": 1,
                  "meta": { "id": 9, "piece": "bad", "inner": [1, { "x": true }] },
                  "flag": false,
                  "piece": "good"
                },
                { "piece": "p", "tags": ["a", { "id": 3 }], "id": 4 }
              ]
            }
            """;

        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal([Token(1, "good"), Token(4, "p")], tokens);
    }

    /// <summary>Свойство <c>tokens</c> внутри вложенного объекта не читается: глубина не равна 1.</summary>
    [Fact]
    public async Task ReadTokensAsync_IgnoresTokensPropertyNestedDeeperThanRoot()
    {
        const string json = "{\"a\":{\"tokens\":[{\"id\":1,\"piece\":\"no\"}]},\"tokens\":[{\"id\":9,\"piece\":\"yes\"}]}";

        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal([Token(9, "yes")], tokens);
    }

    /// <summary>После первого массива <c>tokens</c> разбор заканчивается, повтор ключа не читается.</summary>
    [Fact]
    public async Task ReadTokensAsync_ReadsOnlyFirstTokensArray()
    {
        const string json = "{\"tokens\":[{\"id\":1,\"piece\":\"a\"}],\"tokens\":[{\"id\":2,\"piece\":\"b\"},{\"id\":3,\"piece\":\"c\"}]}";

        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal([Token(1, "a")], tokens);
    }

    /// <summary>Конец массива достаточен для ответа: закрытие объекта и хвост после него не читаются.</summary>
    [Theory]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":\"a\"},{\"id\":2,\"piece\":\"b\"}]", 1L, "a", 2)]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":\"a\"}]}NOT JSON", 1L, "a", 1)]
    [InlineData("{\"tokens\":[{\"id\":4,\"piece\":\"b\"}]}", 4L, "b", 1)]
    public async Task ReadTokensAsync_StopsAtEndOfTokensArray(string json, long firstId, string firstPiece, int expectedCount)
    {
        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal(expectedCount, tokens.Length);
        Assert.Equal(Token(firstId, firstPiece), tokens[0]);
    }

    /// <summary>Конец массива в первом чтении возвращает токены без повторного чтения потока.</summary>
    [Fact]
    public async Task ReadTokensAsync_DoesNotReadPastCompletedTokensArray()
    {
        await using var stream = new OneShotStream(Encoding.UTF8.GetBytes(
            "{\"tokens\":[{\"id\":7,\"piece\":\"x\"}],\"extra\":true}"));

        var tokens = await ModelTokenParser.ReadTokensAsync(stream);

        Assert.Equal([Token(7, "x")], tokens);
        Assert.False(stream.ReadPastFirstBuffer);
    }

    /// <summary>Поток по одному байту не режет многозначный <c>id</c> и многобайтный <c>piece</c>.</summary>
    [Theory]
    [InlineData("{\"tokens\":[{\"id\":21787,\"piece\":\"ping\"}]}", 21787L, "ping")]
    [InlineData("{\"tokens\":[{\"id\":1,\"piece\":\"привет\"}]}", 1L, "привет")]
    [InlineData("{\"tokens\":[{\"id\":2,\"piece\":\"🤗\"}]}", 2L, "🤗")]
    public async Task ReadTokensAsync_Reads_WhenStreamYieldsOneByte(string json, long id, string piece)
    {
        await using var stream = new OneByteStream(Encoding.UTF8.GetBytes(json));

        var tokens = await ModelTokenParser.ReadTokensAsync(stream);

        Assert.Equal([Token(id, piece)], tokens);
        Assert.Equal(1, stream.MaxBytesReturned);
    }

    /// <summary>Результат не зависит от того, какими кусками приходит тело, в том числе на границе UTF-8.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(64)]
    public async Task ReadTokensAsync_Reads_WhenStreamIsChunked(int chunkSize)
    {
        const string json = "{\"before\":[1],\"tokens\":[{\"id\":10,\"piece\":\"привет\"},{\"id\":20,\"piece\":\" world\"}],\"after\":true}";
        await using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(json), chunkSize);

        var tokens = await ModelTokenParser.ReadTokensAsync(stream);

        Assert.Equal([Token(10, "привет"), Token(20, " world")], tokens);
    }

    /// <summary>Обрыв объекта виден и когда тело приходит короткими кусками.</summary>
    [Fact]
    public async Task ReadTokensAsync_ThrowsTruncated_WhenChunkedStreamEndsInsideTokens()
    {
        await using var stream = new ChunkedStream(Encoding.UTF8.GetBytes("{\"tokens\":[{\"id\":10,\"piece\":\"ab"), 5);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ModelTokenParser.ReadTokensAsync(stream));

        Assert.Equal("Ответ токенизатора обрезан.", ex.Message);
    }

    /// <summary>Массив длиннее буфера чтения (8192 байта) читается целиком, с переносом хвоста между чтениями.</summary>
    [Fact]
    public async Task ReadTokensAsync_Reads_WhenArrayCrossesReadBuffer()
    {
        const int tokenCount = 400;
        var parts = new string[tokenCount];
        for (var i = 0; i < tokenCount; i++)
            parts[i] = "{\"id\":" + i + ",\"piece\":\"t" + i + "\"}";

        var json = "{\"tokens\":[" + string.Join(',', parts) + "]}";
        Assert.True(Encoding.UTF8.GetByteCount(json) > 8192);

        var tokens = await ModelTokenParser.ReadTokensAsync(Utf8(json));

        Assert.Equal(tokenCount, tokens.Length);
        Assert.Equal(Token(0, "t0"), tokens[0]);
        Assert.Equal(Token(150, "t150"), tokens[150]);
        Assert.Equal(Token(tokenCount - 1, "t" + (tokenCount - 1)), tokens[^1]);
    }

    /// <summary>Поток UTF-8 JSON из строки.</summary>
    private static MemoryStream Utf8(string json) => new(Encoding.UTF8.GetBytes(json));

    /// <summary>Объект токена для сравнения.</summary>
    private static LlamaModelToken Token(long id, string piece) => new() { Id = id, Piece = piece };

    /// <summary>Читает JSON и ожидает <see cref="InvalidOperationException"/>.</summary>
    private static async Task<InvalidOperationException> ReadFails(string json) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => ModelTokenParser.ReadTokensAsync(Utf8(json)));
}
