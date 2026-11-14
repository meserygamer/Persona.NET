using Microsoft.Extensions.Configuration;
using ProjectPersona.RagTextSearch.LanceDB;

namespace ProjectPersona.Tests.RagTextSearch.LanceDB;

public class LanceDbSettingsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyValues_FallBackToDefaults(string blank)
    {
        var defaults = new LanceDbSettings();

        var settings = new LanceDbSettings
        {
            PathToLanceDb = blank,
            TableName = blank,
            IndexVectorName = blank
        };

        Assert.Equal(defaults.PathToLanceDb, settings.PathToLanceDb);
        Assert.Equal(defaults.TableName, settings.TableName);
        Assert.Equal(defaults.IndexVectorName, settings.IndexVectorName);
    }

    [Fact]
    public void WithExpression_BlankValue_KeepsDefault()
    {
        var settings = new LanceDbSettings() with { TableName = "" };

        Assert.Equal("type_source_index", settings.TableName);
    }

    [Fact]
    public void ConfigurationBinding_EmptyStrings_UseDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["pathToLanceDb"] = "",
                ["tableName"] = "",
                ["indexVectorName"] = " ",
                ["vectorDimension"] = "768"
            })
            .Build();

        var settings = config.Get<LanceDbSettings>()!;

        Assert.Equal(new LanceDbSettings().PathToLanceDb, settings.PathToLanceDb);
        Assert.Equal("type_source_index", settings.TableName);
        Assert.Equal("vector_index", settings.IndexVectorName);
        Assert.Equal(768, settings.VectorDimension);
    }

    [Fact]
    public void CustomValues_AreKeptAndTrimmed()
    {
        var settings = new LanceDbSettings { TableName = " my_table " };

        Assert.Equal("my_table", settings.TableName);
    }
}
