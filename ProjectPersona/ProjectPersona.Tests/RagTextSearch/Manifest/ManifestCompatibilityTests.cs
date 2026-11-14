using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.Tests.RagTextSearch.Manifest;

public class ManifestCompatibilityTests
{
    [Fact]
    public void SameProfile_IsCompatibleWithoutWarnings()
    {
        var result = ManifestCompatibility.Check(
            ManifestTestData.Manifest(), ManifestTestData.PipelineVersion, ManifestTestData.Profile());

        Assert.True(result.IsCompatible);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void OtherModel_IsError()
    {
        var profile = ManifestTestData.Profile();
        profile = profile with { Fingerprint = profile.Fingerprint with { ModelName = "other", FileType = "2" } };

        var result = ManifestCompatibility.Check(ManifestTestData.Manifest(), ManifestTestData.PipelineVersion, profile);

        Assert.False(result.IsCompatible);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Contains("model name") && error.Contains("'other'"));
    }

    [Fact]
    public void OtherPipelineVersion_IsError()
    {
        var result = ManifestCompatibility.Check(
            ManifestTestData.Manifest(), ManifestTestData.PipelineVersion + 1, ManifestTestData.Profile());

        Assert.Contains(result.Errors, error => error.Contains("pipeline version"));
    }

    [Fact]
    public void OtherBuildParametersAndTemplates_AreWarnings()
    {
        var profile = ManifestTestData.Profile() with
        {
            BuildParameters = new EmbeddingBuildParameters(1024, 10, 32),
            DocumentQueryTemplate = "{0} {1}",
            SearchQueryTemplate = "query: {0}"
        };

        var result = ManifestCompatibility.Check(ManifestTestData.Manifest(), ManifestTestData.PipelineVersion, profile);

        Assert.True(result.IsCompatible);
        Assert.Equal(5, result.Warnings.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("document query template"));
    }

    [Fact]
    public void OtherFormatVersion_ReportsOnlyFormat()
    {
        var manifest = ManifestTestData.Manifest() with { FormatVersion = VectorStoreManifest.CurrentFormatVersion + 1 };
        var profile = ManifestTestData.Profile();
        profile = profile with { Fingerprint = profile.Fingerprint with { ModelName = "other" } };

        var result = ManifestCompatibility.Check(manifest, ManifestTestData.PipelineVersion, profile);

        var error = Assert.Single(result.Errors);
        Assert.Contains("format version", error);
    }
}
