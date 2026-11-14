using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.Tests.RagTextSearch.Manifest;

internal static class ManifestTestData
{
    public const int PipelineVersion = 1;

    public static EmbeddingProfile Profile() => new(
        new EmbeddingFingerprint("embeddinggemma", "gemma-embedding", "7", 768, 328_000_000, 1),
        new EmbeddingBuildParameters(512, null, null),
        "title: {0} | text: {1}",
        "task: code retrieval | query: {0}");

    public static VectorStoreManifest Manifest(EmbeddingProfile? profile = null)
    {
        profile ??= Profile();
        return new VectorStoreManifest
        {
            FormatVersion = VectorStoreManifest.CurrentFormatVersion,
            PipelineVersion = PipelineVersion,
            VectorDimension = profile.Fingerprint.EmbeddingDimension,
            EmbeddingFingerprint = profile.Fingerprint,
            BuildParameters = profile.BuildParameters,
            DocumentQueryTemplate = profile.DocumentQueryTemplate,
            SearchQueryTemplate = profile.SearchQueryTemplate
        };
    }
}
