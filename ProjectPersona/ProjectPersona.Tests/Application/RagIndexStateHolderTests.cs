using Microsoft.Extensions.Logging.Abstractions;
using ProjectPersona.Application.Services.RagSearchService;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Manifest;
using ProjectPersona.Tests.RagTextSearch.Manifest;

namespace ProjectPersona.Tests.Application;

public class RagIndexStateHolderTests
{
    [Fact]
    public async Task NoTable_IsMissing()
    {
        var state = await CheckAsync(new FakeVectorStore { Exists = false });

        Assert.Equal(RagIndexStatus.Missing, state.Status);
        Assert.False(state.IsSearchable);
    }

    [Fact]
    public async Task NoManifest_IsInvalid()
    {
        var state = await CheckAsync(new FakeVectorStore { Manifest = null });

        Assert.Equal(RagIndexStatus.Invalid, state.Status);
        Assert.Contains("no manifest", state.Error);
    }

    [Fact]
    public async Task OtherModel_IsInvalid()
    {
        var profile = ManifestTestData.Profile();
        var store = new FakeVectorStore
        {
            Manifest = ManifestTestData.Manifest(profile with
            {
                Fingerprint = profile.Fingerprint with { ModelName = "other" }
            })
        };

        var state = await CheckAsync(store);

        Assert.Equal(RagIndexStatus.Invalid, state.Status);
        Assert.Contains("model name", state.Error);
    }

    [Fact]
    public async Task Updating_IsSearchableWithWarning()
    {
        var store = new FakeVectorStore { Manifest = ManifestTestData.Manifest().WithUpdating([("A", "net8.0")]) };

        var state = await CheckAsync(store);

        Assert.Equal(RagIndexStatus.Updating, state.Status);
        Assert.True(state.IsSearchable);
        Assert.Contains("A:net8.0", Assert.Single(state.Warnings));
    }

    [Fact]
    public async Task Ready_IsSearchableWithoutWarnings()
    {
        var state = await CheckAsync(new FakeVectorStore { Manifest = ManifestTestData.Manifest() });

        Assert.Equal(RagIndexStatus.Ready, state.Status);
        Assert.Empty(state.Warnings);
    }

    [Fact]
    public async Task Check_RunsOnce()
    {
        var store = new FakeVectorStore { Manifest = ManifestTestData.Manifest() };
        var holder = new RagIndexStateHolder(store, new FakeProfileProvider(), NullLogger<RagIndexStateHolder>.Instance);

        await holder.GetAsync();
        await holder.GetAsync();

        Assert.Equal(1, store.ManifestReads);
    }

    private static Task<RagIndexState> CheckAsync(FakeVectorStore store) =>
        new RagIndexStateHolder(store, new FakeProfileProvider(), NullLogger<RagIndexStateHolder>.Instance).GetAsync();

    private sealed class FakeProfileProvider : IEmbeddingProfileProvider
    {
        public EmbeddingProfile GetProfile(CancellationToken ct = default) => ManifestTestData.Profile();
    }

    private sealed class FakeVectorStore : IVectorStore
    {
        public bool Exists { get; init; } = true;
        public VectorStoreManifest? Manifest { get; init; }
        public int ManifestReads { get; private set; }

        public ValueTask<bool> StoreExistsAsync(CancellationToken ct = default) => ValueTask.FromResult(Exists);

        public ValueTask<VectorStoreManifest?> ReadManifestAsync(CancellationToken ct = default)
        {
            ManifestReads++;
            return ValueTask.FromResult(Manifest);
        }

        public ValueTask AddVectorsAsync(IEnumerable<VectorData>? vectors, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public ValueTask ClearStoreAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask ClearAssembliesAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
            CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<VectorData[]> SearchNearestVectorsAsync(float[] vector, int maximumVectors,
            CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask OptimizeAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask WriteManifestAsync(VectorStoreManifest manifest, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public ValueTask DeleteManifestAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask MarkUpdatingAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
            CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask MarkReadyAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies,
            CancellationToken ct = default) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
