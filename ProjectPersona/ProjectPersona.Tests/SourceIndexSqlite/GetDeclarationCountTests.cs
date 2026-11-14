using Microsoft.Data.Sqlite;
using ProjectPersona.Core.SourceLookup;
using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Reading;
using ProjectPersona.SourceIndex.Sqlite.Writing;
using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.Tests.SourceIndexSqlite;

/// <summary>Подсчёт деклараций <see cref="ITypeSourceLookup.GetDeclarationCount"/> одинаков в SQLite и в памяти.</summary>
public sealed class GetDeclarationCountTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pp-count-" + Guid.NewGuid().ToString("N"));
    private ITypeSourceLookup _sqlite = null!;
    private ITypeSourceLookup _memory = null!;

    public async Task InitializeAsync()
    {
        var options = new SqliteTypeSourceIndexOptions { DatabasePath = Path.Combine(_directory, "index.db") };
        var matches = new[]
        {
            Declaration("Shop", "net8.0", "Shop.Order"),
            Declaration("Shop", "net10.0", "Shop.Order"),
            Declaration("Shop", "net10.0", "Shop.Customer"),
            Declaration("Billing", "net10.0", "Billing.Invoice")
        };

        await new SqliteTypeSourceIndexWriter(options).WriteAsync(
            new TypeSourceIndexSnapshot(matches, new Dictionary<string, UsedByFqnEntry>()));
        _sqlite = await new SqliteTypeSourceLookupFactory(options).OpenAsync();

        var builder = new TypeSourceIndexBuilder();
        foreach (var match in matches)
            builder.Add(match);
        _memory = builder.Build();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        return Task.CompletedTask;
    }

    /// <summary>Без фильтра считаются все декларации.</summary>
    [Fact]
    public void Count_WithoutFilter_ReturnsAll()
    {
        Assert.Equal(4, _sqlite.GetDeclarationCount(null));
        Assert.Equal(4, _memory.GetDeclarationCount(null));
    }

    /// <summary>Пустой фильтр — ноль деклараций.</summary>
    [Fact]
    public void Count_WithEmptyFilter_ReturnsZero()
    {
        Assert.Equal(0, _sqlite.GetDeclarationCount([]));
        Assert.Equal(0, _memory.GetDeclarationCount([]));
    }

    /// <summary>TFM <see langword="null"/> или пустой означает любой TFM сборки — в обеих реализациях.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Count_NullOrBlankTfm_MatchesAnyTfm(string? tfm)
    {
        (string, string?)[] filter = [("Shop", tfm)];

        Assert.Equal(3, _sqlite.GetDeclarationCount(filter));
        Assert.Equal(3, _memory.GetDeclarationCount(filter));
    }

    /// <summary>Заданный TFM сужает результат.</summary>
    [Fact]
    public void Count_WithTfm_NarrowsResult()
    {
        (string, string?)[] filter = [("Shop", "net10.0")];

        Assert.Equal(2, _sqlite.GetDeclarationCount(filter));
        Assert.Equal(2, _memory.GetDeclarationCount(filter));
    }

    /// <summary>Дубликаты и пересекающиеся фильтры не удваивают счёт.</summary>
    [Fact]
    public void Count_OverlappingFilters_DoNotDoubleCount()
    {
        (string, string?)[] filter = [("Shop", null), ("Shop", "net10.0"), ("Shop", null), ("Billing", "net10.0")];

        Assert.Equal(4, _sqlite.GetDeclarationCount(filter));
        Assert.Equal(4, _memory.GetDeclarationCount(filter));
    }

    /// <summary>Неизвестная сборка даёт ноль.</summary>
    [Fact]
    public void Count_UnknownAssembly_ReturnsZero()
    {
        (string, string?)[] filter = [("Missing", null)];

        Assert.Equal(0, _sqlite.GetDeclarationCount(filter));
        Assert.Equal(0, _memory.GetDeclarationCount(filter));
    }

    private static TypeSourceIndexedMatch Declaration(string assembly, string tfm, string type) =>
        new(new TypeSourceMatch(assembly, type, tfm, "public class X { }", [], null), []);
}
