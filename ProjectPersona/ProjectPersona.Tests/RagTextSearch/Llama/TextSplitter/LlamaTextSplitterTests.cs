using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.RagTextSearch.Llama.TextSplitter;

namespace ProjectPersona.Tests.RagTextSearch.Llama.TextSplitter;

/// <summary> <see cref="LlamaTextSplitter"/>: бюджет шаблона, окна и потокобезопасная инициализация. </summary>
public class LlamaTextSplitterTests
{
    private const string DefaultTitle = "Ns.SampleType";

    private static LlamaSettings DefaultSettings(long batchSize = 100) => new()
    {
        BatchSize = batchSize,
        DocumentQueryTemplate = "title: {0} | text: {1}",
        FreeSpaceClearance = 5
    };

    /// <summary> Пустой и <see langword="null"/> ввод не вызывают токенизатор. </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SplitTextAsync_ReturnsEmpty_WithoutTokenizing(string? text)
    {
        var tokenizer = new FakeTextTokenizer();
        var splitter = new LlamaTextSplitter(tokenizer, DefaultSettings());

        var result = await splitter.SplitTextAsync(text!, DefaultTitle);

        Assert.Empty(result);
        Assert.Equal(0, tokenizer.TokenizeCallCount);
        Assert.Equal(0, tokenizer.GetTokenCountCallCount);
    }

    /// <summary> Короткий текст возвращается одним фрагментом без нарезки. </summary>
    [Fact]
    public async Task SplitTextAsync_ReturnsSingleChunk_WhenTextFitsInWindow()
    {
        const string text = "hello";
        var tokenizer = new FakeTextTokenizer();
        var splitter = new LlamaTextSplitter(tokenizer, DefaultSettings());

        var result = await splitter.SplitTextAsync(text, DefaultTitle);

        Assert.Equal([text], result);
    }

    /// <summary> Длинный текст без перекрытия: каждый символ попадает хотя бы в один кусок. </summary>
    [Fact]
    public async Task SplitTextAsync_CoversAllCharacters_WhenOverlapIsZero()
    {
        var text = string.Create(200, 0, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = (char)('A' + i % 26);
        });
        var tokenizer = new FakeTextTokenizer();
        var settings = DefaultSettings() with { OverlapTokens = 0 };
        var splitter = new LlamaTextSplitter(tokenizer, settings);

        var chunks = await splitter.SplitTextAsync(text, DefaultTitle);

        Assert.True(chunks.Length > 1);
        var covered = new bool[text.Length];
        foreach (var chunk in chunks)
        {
            var index = 0;
            while (true)
            {
                index = text.IndexOf(chunk, index, StringComparison.Ordinal);
                if (index < 0)
                    break;
                for (var i = index; i < index + chunk.Length; i++)
                    covered[i] = true;
                index++;
            }
        }

        Assert.True(covered.All(static x => x));
    }

    /// <summary> При перекрытии объединение окон покрывает все символы. </summary>
    [Fact]
    public async Task SplitTextAsync_CoversAllCharacters_WhenOverlapIsPositive()
    {
        var text = new string('b', 200);
        var tokenizer = new FakeTextTokenizer();
        var settings = DefaultSettings() with { OverlapTokens = 10 };
        var splitter = new LlamaTextSplitter(tokenizer, settings);

        var chunks = await splitter.SplitTextAsync(text, DefaultTitle);
        var covered = new bool[text.Length];
        foreach (var chunk in chunks)
        {
            var index = 0;
            while (true)
            {
                index = text.IndexOf(chunk, index, StringComparison.Ordinal);
                if (index < 0)
                    break;
                for (var i = index; i < index + chunk.Length; i++)
                    covered[i] = true;
                index++;
            }
        }

        Assert.True(covered.All(static x => x));
    }

    /// <summary> Инициализация считает токены для <see cref="LlamaSettings.FormatDocument"/>, а не для сырого шаблона. </summary>
    [Fact]
    public async Task SplitTextAsync_CountsWrapperPrefix_OnInitialize()
    {
        const string title = "doc";
        var settings = DefaultSettings();
        var expectedWrapper = settings.FormatDocument(title, string.Empty);
        var tokenizer = new FakeTextTokenizer();
        var splitter = new LlamaTextSplitter(tokenizer, settings);

        await splitter.SplitTextAsync("x", title);

        Assert.Contains(expectedWrapper, tokenizer.GetTokenCountInputs);
        Assert.DoesNotContain(settings.DocumentQueryTemplate, tokenizer.GetTokenCountInputs);
    }

    /// <summary> Шаблон и запас, съедающие весь батч, дают исключение при первом разбиении. </summary>
    [Fact]
    public async Task SplitTextAsync_Throws_WhenWrapperConsumesBatch()
    {
        var tokenizer = new FakeTextTokenizer { SpecialTokenCount = 0 };
        var settings = new LlamaSettings
        {
            BatchSize = 10,
            DocumentQueryTemplate = "1234567890{0}{1}",
            FreeSpaceClearance = 0
        };
        var splitter = new LlamaTextSplitter(tokenizer, settings);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => splitter.SplitTextAsync("a", string.Empty));
        Assert.Contains("шаблон и запас", ex.Message);
    }

    /// <summary> Неаддитивный подсчёт сужает окно, обёрнутый фрагмент остаётся в бюджете. </summary>
    [Fact]
    public async Task SplitTextAsync_ShrinksChunk_WhenWrappedCountIsNonAdditive()
    {
        const string title = "h";
        const string template = "p:{0}|t:{1}";
        var settings = new LlamaSettings
        {
            BatchSize = 50,
            DocumentQueryTemplate = template,
            FreeSpaceClearance = 2,
            OverlapTokens = 0
        };
        var wrapper = settings.FormatDocument(title, string.Empty);
        var tokenizer = new FakeTextTokenizer
        {
            WrappedPrefix = wrapper,
            NonAdditivePerChar = 1
        };
        var splitter = new LlamaTextSplitter(tokenizer, settings);
        var text = new string('z', 80);

        var chunks = await splitter.SplitTextAsync(text, title);
        var maxAllowed = settings.BatchSize - settings.FreeSpaceClearance!.Value;

        foreach (var chunk in chunks)
        {
            var wrapped = settings.FormatDocument(title, chunk);
            var count = await tokenizer.GetTokenCountAsync(wrapped, addSpecialTokens: true);
            Assert.True(count <= maxAllowed);
        }
    }

    /// <summary> Параллельные вызовы с одним заголовком дают одинаковый результат без общего состояния. </summary>
    [Fact]
    public async Task SplitTextAsync_ReturnsSameChunks_WhenCalledConcurrently()
    {
        var splitter = new LlamaTextSplitter(new FakeTextTokenizer(), DefaultSettings());
        var text = new string('c', 150);
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => splitter.SplitTextAsync(text, DefaultTitle))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, chunks => Assert.Equal(results[0], chunks));
    }

    /// <summary> Сбой инициализации не кэшируется: повторный вызов после исправления проходит. </summary>
    [Fact]
    public async Task SplitTextAsync_RetriesInitialize_AfterFailure()
    {
        var tokenizer = new FakeTextTokenizer { FailGetTokenCountTimes = 1 };
        var splitter = new LlamaTextSplitter(tokenizer, DefaultSettings());

        await Assert.ThrowsAsync<InvalidOperationException>(() => splitter.SplitTextAsync("ok", DefaultTitle));
        var result = await splitter.SplitTextAsync("ok", DefaultTitle);

        Assert.Equal(["ok"], result);
    }

    /// <summary> Отмена до инициализации пробрасывает <see cref="OperationCanceledException"/>. </summary>
    [Fact]
    public async Task SplitTextAsync_Throws_WhenCancelledBeforeInit()
    {
        var tokenizer = new FakeTextTokenizer();
        var splitter = new LlamaTextSplitter(tokenizer, DefaultSettings());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            splitter.SplitTextAsync("x", DefaultTitle, cts.Token));
    }

    /// <summary> Отрицательное перекрытие в настройках отклоняется в конструкторе. </summary>
    [Fact]
    public void Constructor_Throws_WhenOverlapTokensNegative()
    {
        var tokenizer = new FakeTextTokenizer();
        var settings = DefaultSettings() with { OverlapTokens = -1 };

        Assert.Throws<ArgumentException>(() => new LlamaTextSplitter(tokenizer, settings));
    }

    /// <summary> Шаблон с одним аргументом отклоняется в конструкторе. </summary>
    [Fact]
    public void Constructor_Throws_WhenTemplateHasSingleArgument()
    {
        var tokenizer = new FakeTextTokenizer();
        var settings = DefaultSettings() with { DocumentQueryTemplate = "{0}" };

        Assert.Throws<ArgumentException>(() => new LlamaTextSplitter(tokenizer, settings));
    }

    /// <summary> <see langword="null"/> токенизатор отклоняется. </summary>
    [Fact]
    public void Constructor_Throws_WhenTokenizerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LlamaTextSplitter(null!, DefaultSettings()));
    }
}
