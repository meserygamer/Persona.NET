using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Расширения <see cref="IVectorStore"/>. </summary>
public static class VectorStoreExtensions
{
    /// <summary> Прочитать манифест, без которого хранилище нельзя использовать. </summary>
    /// <param name="store"> Хранилище векторов. </param>
    /// <param name="missingMessage"> Текст исключения, если манифеста нет. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Манифест хранилища. </returns>
    /// <exception cref="InvalidOperationException"> Манифеста нет. </exception>
    public static async ValueTask<VectorStoreManifest> ReadRequiredManifestAsync(
        this IVectorStore store,
        string missingMessage,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        return await store.ReadManifestAsync(ct).ConfigureAwait(false)
               ?? throw new InvalidOperationException(missingMessage);
    }
}
