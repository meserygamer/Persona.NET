using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.Tests.RagTextSearch.Manifest;

public class VectorStoreManifestTests
{
    [Fact]
    public void WithUpdating_UnionsWithPendingAndNormalizesEmptyTfm()
    {
        var manifest = ManifestTestData.Manifest()
            .WithUpdating([("A", "net8.0")])
            .WithUpdating([("A", "net8.0"), ("B", ""), ("C", null)]);

        Assert.Equal(VectorStoreState.Updating, manifest.State);
        Assert.Equal(
            [new PendingAssembly("A", "net8.0"), new PendingAssembly("B", null), new PendingAssembly("C", null)],
            manifest.PendingAssemblies);
    }

    [Fact]
    public void WithCompleted_RemovesOnlyOwnAssemblies_AndStaysUpdating()
    {
        var manifest = ManifestTestData.Manifest()
            .WithUpdating([("X", null), ("Y", null)])
            .WithUpdating([("Z", null)])
            .WithCompleted([("Z", null)]);

        Assert.Equal(VectorStoreState.Updating, manifest.State);
        Assert.Equal([new PendingAssembly("X", null), new PendingAssembly("Y", null)], manifest.PendingAssemblies);
    }

    [Fact]
    public void WithCompleted_AllPendingDone_BecomesReady()
    {
        var manifest = ManifestTestData.Manifest()
            .WithUpdating([("X", "net8.0")])
            .WithCompleted([("X", "net8.0")]);

        Assert.Equal(VectorStoreState.Ready, manifest.State);
        Assert.Empty(manifest.PendingAssemblies);
    }

    [Fact]
    public void WithCompleted_AllTfms_ClosesSpecificTfm()
    {
        var manifest = ManifestTestData.Manifest()
            .WithUpdating([("X", "net8.0"), ("X", "netstandard2.0")])
            .WithCompleted([("X", null)]);

        Assert.Equal(VectorStoreState.Ready, manifest.State);
    }

    [Fact]
    public void WithCompleted_SpecificTfm_DoesNotCloseAllTfms()
    {
        var manifest = ManifestTestData.Manifest()
            .WithUpdating([("X", null)])
            .WithCompleted([("X", "net8.0")]);

        Assert.Equal(VectorStoreState.Updating, manifest.State);
        Assert.Equal([new PendingAssembly("X", null)], manifest.PendingAssemblies);
    }

    [Fact]
    public void Json_RoundTrip_KeepsAllFields()
    {
        var manifest = ManifestTestData.Manifest().WithUpdating([("A", "net8.0"), ("B", null)]);

        var json = VectorStoreManifestJson.Serialize(manifest);
        var restored = VectorStoreManifestJson.Deserialize(json);

        Assert.Contains("\"state\":\"Updating\"", json);
        Assert.Contains("\"embedding_fingerprint\"", json);
        Assert.Equal(manifest.EmbeddingFingerprint, restored.EmbeddingFingerprint);
        Assert.Equal(manifest.BuildParameters, restored.BuildParameters);
        Assert.Equal(manifest.SearchQueryTemplate, restored.SearchQueryTemplate);
        Assert.Equal(manifest.State, restored.State);
        Assert.Equal(manifest.PendingAssemblies, restored.PendingAssemblies);
        Assert.Equal(manifest.VectorDimension, restored.VectorDimension);
    }

    [Fact]
    public void Json_Corrupted_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => VectorStoreManifestJson.Deserialize("{\"format_version\":"));
    }
}
