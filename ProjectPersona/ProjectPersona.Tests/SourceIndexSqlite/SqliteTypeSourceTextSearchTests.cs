using Microsoft.Data.Sqlite;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Searching;
using ProjectPersona.SourceIndex.Sqlite.Writing;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.Tests.SourceIndexSqlite;

/// <summary>Поиск подстроки <see cref="SqliteTypeSourceTextSearch"/> по FTS5 trigram на реальном файле SQLite.</summary>
public sealed class SqliteTypeSourceTextSearchTests : IAsyncLifetime
{
    private const string OrderSource = "public class Order { public int Total() => 1; public int Count() => Total() + Total(); }";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pp-fts-" + Guid.NewGuid().ToString("N"));
    private SqliteTypeSourceIndexOptions _options = null!;

    public async Task InitializeAsync()
    {
        _options = new SqliteTypeSourceIndexOptions { DatabasePath = Path.Combine(_directory, "index.db") };

        var snapshot = new TypeSourceIndexSnapshot(
            [
                Declaration("Shop", "net10.0", "Shop.Order", OrderSource, Slice(OrderSource, "Total()", "public int Total() => 1;"),
                    Slice(OrderSource, "Count()", "public int Count() => Total() + Total();")),
                Declaration("Shop", "net10.0", "Shop.Customer", "public class Customer { public string NAME; }"),
                Declaration("Billing", "net10.0", "Billing.Invoice", "public class Invoice { public int Total; }")
            ],
            new Dictionary<string, UsedByFqnEntry>());

        await new SqliteTypeSourceIndexWriter(_options).WriteAsync(snapshot);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        return Task.CompletedTask;
    }

    /// <summary>Подстрока находится и в объявлении типа, и в срезах членов; позиции вхождений не перекрываются.</summary>
    [Fact]
    public void Search_ReturnsTypeAndMemberDocuments_WithAllOccurrences()
    {
        var matches = new SqliteTypeSourceTextSearch(_options).Search("Total()", assemblyName: "Shop");

        Assert.Equal(3, matches.Count);
        Assert.All(matches, match => Assert.Equal("Shop.Order", match.Type));

        var type = Assert.Single(matches, match => match.MemberName is null);
        Assert.Equal(3, type.Indexes.Count);

        var count = Assert.Single(matches, match => match.MemberName == "Count()");
        Assert.Equal(2, count.Indexes.Count);
        Assert.Equal(count.Text.IndexOf("Total()", StringComparison.Ordinal), count.Indexes[0]);
    }

    /// <summary>Без фильтра ищет по всем сборкам, фильтр сужает до одной.</summary>
    [Fact]
    public void Search_FiltersByAssembly()
    {
        var search = new SqliteTypeSourceTextSearch(_options);

        var all = search.Search("int Total");
        var billing = search.Search("int Total", "Billing");

        Assert.Contains(all, match => match.AssemblyName == "Shop");
        Assert.Contains(all, match => match.AssemblyName == "Billing");
        Assert.All(billing, match => Assert.Equal("Billing", match.AssemblyName));
    }

    /// <summary>Регистр учитывается, пока не задан <c>ignoreCase</c>: FTS5 отдаёт надмножество, сужает точное сравнение.</summary>
    [Fact]
    public void Search_RespectsIgnoreCase()
    {
        var search = new SqliteTypeSourceTextSearch(_options);

        Assert.Empty(search.Search("name;"));
        Assert.Single(search.Search("NAME;"));
        Assert.Single(search.Search("name;", ignoreCase: true));
    }

    /// <summary>Спецсимволы синтаксиса FTS5 в запросе трактуются как обычный текст.</summary>
    [Theory]
    [InlineData("\"Order\"")]
    [InlineData("Order OR")]
    [InlineData("Order*")]
    [InlineData("(Total")]
    [InlineData("-Total")]
    [InlineData("col:Total")]
    public void Search_TreatsFtsSyntaxAsLiteralText(string query)
    {
        var exception = Record.Exception(() => new SqliteTypeSourceTextSearch(_options).Search(query));

        Assert.Null(exception);
    }

    /// <summary>Подстрока без совпадений даёт пустой список.</summary>
    [Fact]
    public void Search_ReturnsEmpty_WhenNothingMatches() =>
        Assert.Empty(new SqliteTypeSourceTextSearch(_options).Search("NoSuchText"));

    /// <summary>Отсутствие файла индекса — <see cref="InvalidOperationException"/> с подсказкой, как его создать.</summary>
    [Fact]
    public void Search_Throws_WhenIndexFileMissing()
    {
        var missing = new SqliteTypeSourceIndexOptions { DatabasePath = Path.Combine(_directory, "missing.db") };

        var exception = Assert.Throws<InvalidOperationException>(() => new SqliteTypeSourceTextSearch(missing).Search("Total"));

        Assert.Contains("generate-type-source-index", exception.Message);
    }

    private static TypeSourceIndexedMatch Declaration(
        string assembly,
        string tfm,
        string type,
        string source,
        params TypeSourceMemberSlice[] members) =>
        new(new TypeSourceMatch(assembly, type, tfm, source, [], null), members);

    private static TypeSourceMemberSlice Slice(string source, string name, string fragment) =>
        new(name, source.IndexOf(fragment, StringComparison.Ordinal), fragment.Length, []);
}
