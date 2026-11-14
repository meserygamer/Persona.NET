using Microsoft.Extensions.DependencyInjection;
using ProjectPersona.Application.Services.RagSearchService;
using ProjectPersona.CliTools.CommandBuilders;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.LanceDB;

namespace ProjectPersona.CliTools.CommandHandlers;

/// <summary>
/// CLI-режим частичной перевекторизации: обновляет в LanceDB векторы только указанных сборок.
/// </summary>
internal static class RevectorizeAssembliesCli
{
    /// <summary>
    /// Перевекторизует указанные сборки по параметрам командной строки.
    /// </summary>
    /// <param name="options">Параметры подкоманды.</param>
    public static async Task RunAsync(RevectorizeAssembliesOptions options)
    {
        var assemblies = ParseAssemblies(options.Assemblies);
        var vectorDimension = await ResolveVectorDimensionAsync(options).ConfigureAwait(false);
        var common = new VectorizeTypeSourceIndexOptions(
            options.IndexPath,
            options.LanceDbPath,
            vectorDimension,
            options.TableName,
            options.LlamaOptionsPath);

        Console.WriteLine($"Re-vectorizing {assemblies.Count} assembly(ies) from type source index...");

        await VectorizeTypeSourceIndexCli.RunWithVectorizerAsync(
                common,
                (vectorizer, progress) => vectorizer.VectorizeAssembliesAsync(assemblies, CancellationToken.None, progress))
            .ConfigureAwait(false);

        Console.WriteLine($"RAG vector index updated in {options.LanceDbPath}");
    }

    /// <summary> Получить размерность векторов и валидировать её. </summary>
    /// <param name="options"> Параметры подкоманды. </param>
    /// <returns> Размерность векторов базы. </returns>
    /// <exception cref="InvalidOperationException"> Базы или манифеста нет, либо размерность не совпала. </exception>
    private static async Task<int> ResolveVectorDimensionAsync(RevectorizeAssembliesOptions options)
    {
        // Размерность ещё неизвестна, а манифест её не использует: подставляется значение по умолчанию.
        await using var provider = BuildVectorStoreProvider(options);
        var store = provider.GetRequiredService<IVectorStore>();

        if (!await store.StoreExistsAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                $"LanceDB not found: {options.LanceDbPath}. Run `vectorize-type-source-index` first.");
        }

        var manifest = await store.ReadRequiredManifestAsync(
                $"LanceDB table '{options.TableName ?? string.Empty}' has no manifest: it was never fully vectorized or the last " +
                "full vectorization was interrupted. Run `vectorize-type-source-index` first.")
            .ConfigureAwait(false);

        if (options.VectorDimension is { } expected && expected != manifest.VectorDimension)
        {
            throw new InvalidOperationException(
                $"--vector-dimension {expected} does not match the LanceDB manifest ({manifest.VectorDimension}). " +
                "Omit the option to use the manifest value.");
        }

        return manifest.VectorDimension;
    }

    /// <summary> Собрать контейнер с хранилищем векторов без модели: для чтения манифеста она не нужна. </summary>
    /// <param name="options"> Параметры подкоманды. </param>
    /// <returns> Провайдер; его нужно освободить после использования. </returns>
    private static ServiceProvider BuildVectorStoreProvider(RevectorizeAssembliesOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new LanceDbSettings
        {
            PathToLanceDb = options.LanceDbPath,
            TableName = options.TableName ?? string.Empty
        });
        services.AddRagTextSearch();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Разбирает уже проверенные значения <c>--assembly</c> вида <c>Name</c> или <c>Name:tfm</c>.
    /// </summary>
    /// <param name="values">Значения опции.</param>
    /// <returns>Пары «код сборки – TFM»; TFM равен <see langword="null"/>, если не указан.</returns>
    private static List<(string Assembly, string? TargetFramework)> ParseAssemblies(IReadOnlyCollection<string> values)
    {
        var result = new List<(string Assembly, string? TargetFramework)>(values.Count);
        foreach (var value in values)
        {
            var separator = value.LastIndexOf(':');
            var name = (separator < 0 ? value : value[..separator]).Trim();
            var tfm = separator < 0 ? null : value[(separator + 1)..].Trim();

            result.Add((name, string.IsNullOrEmpty(tfm) ? null : tfm));
        }

        return result;
    }
}
