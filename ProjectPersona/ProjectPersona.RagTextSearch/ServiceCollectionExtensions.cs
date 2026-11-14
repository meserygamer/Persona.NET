using Microsoft.Extensions.DependencyInjection;
using ProjectPersona.RagTextSearch.LanceDB;
using ProjectPersona.RagTextSearch.LanceDB.Data;
using ProjectPersona.RagTextSearch.LanceDB.Structure;
using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.RagTextSearch.Llama.Embedder;
using ProjectPersona.RagTextSearch.Llama.TextSplitter;
using ProjectPersona.RagTextSearch.Llama.Tokenizer;
using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.RagTextSearch;

/// <summary>
/// Регистрация RAG-компонентов (Llama + LanceDB) в DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует <see cref="LlamaModelSessionHolder"/>, токенизатор, эмбеддер, разделитель текста,
    /// источник описания эмбеддера, слои LanceDB (подключение, структура, данные, манифест) и <see cref="IVectorStore"/>.
    /// Требует, чтобы в контейнере уже были зарегистрированы <see cref="LlamaSettings"/> и <see cref="LanceDbSettings"/>.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    public static IServiceCollection AddRagTextSearch(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(sp =>
        {
            var llama = sp.GetRequiredService<LlamaSettings>();
            var lance = sp.GetRequiredService<LanceDbSettings>();
            return new LlamaModelSessionHolder(llama, lance.VectorDimension);
        });
        services.AddSingleton<ITextTokenizer, LlamaTextTokenizer>();
        services.AddSingleton<ITextEmbedder, LlamaTextEmbedder>();
        services.AddSingleton<ITextSplitter, LlamaTextSplitter>();
        services.AddSingleton<IEmbeddingProfileProvider, LlamaEmbeddingProfileProvider>();
        services.AddSingleton<AsyncReaderWriterLock>();
        services.AddSingleton<LanceDbConnectionProvider>();
        services.AddSingleton<LanceDbTableManager>();
        services.AddSingleton<VectorDataArrowMapper>();
        services.AddSingleton<LanceDbVectorRepository>();
        services.AddSingleton<LanceDbManifestRepository>();
        services.AddSingleton<IVectorStore, LanceDbVectorStore>();
        return services;
    }
}
