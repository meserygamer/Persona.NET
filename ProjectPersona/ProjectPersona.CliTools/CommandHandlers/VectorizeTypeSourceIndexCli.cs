using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectPersona.Application.Services.RagSearchService;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.CliTools.CommandBuilders;
using ProjectPersona.CliTools.Validation;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.LanceDB;
using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.SourceIndex.Sqlite;
using ProjectPersona.SourceIndex.Sqlite.Schema;

namespace ProjectPersona.CliTools.CommandHandlers;

/// <summary>
/// CLI-режим векторизации type-source-index в LanceDB без Roslyn.
/// </summary>
internal static class VectorizeTypeSourceIndexCli
{
    /// <summary>
    /// Векторизует индекс исходников по параметрам командной строки.
    /// </summary>
    /// <param name="options">Параметры подкоманды.</param>
    public static async Task RunAsync(VectorizeTypeSourceIndexOptions options)
    {
        Console.WriteLine("Building RAG vector index from type source index...");

        await RunWithVectorizerAsync(
                options,
                (vectorizer, progress) => vectorizer.VectorizeTypeSourceIndexAsync(CancellationToken.None, progress))
            .ConfigureAwait(false);

        Console.WriteLine($"RAG vector index written to {options.LanceDbPath}");
    }

    /// <summary>
    /// Проверяет параметры, поднимает контейнер с векторизатором и выполняет действие, затем освобождает модель и хранилище.
    /// </summary>
    /// <param name="options">Общие параметры векторизации.</param>
    /// <param name="action">Действие над векторизатором с отчётом прогресса в консоль.</param>
    internal static async Task RunWithVectorizerAsync(
        VectorizeTypeSourceIndexOptions options,
        Func<TypeSourceIndexVectorizerV2, IProgress<VectorizationProgress>, ValueTask> action)
    {
        var llamaSettings = LlamaSettingsFile.Load(options.LlamaOptionsPath);
        await new LlamaSettingsInputValidator().ValidateAsync(llamaSettings).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        services.AddSqliteTypeSourceIndex(_ => Path.Combine(options.IndexPath, SqliteIndexFormat.DatabaseFileName));

        services.AddSingleton(llamaSettings);

        services.AddSingleton(_ =>
        {
            return new LanceDbSettings
            {
                PathToLanceDb = options.LanceDbPath,
                TableName = options.TableName ?? string.Empty,
                VectorDimension = options.VectorDimension
            };
        });

        services.AddRagTextSearch();
        services.AddSingleton<TypeSourceIndexHolder>();
        services.AddTypeSourceIndexVectorizer();

        // await using: провайдер освобождает и асинхронно освобождаемые пайпы, и хранилище с моделью.
        await using var provider = services.BuildServiceProvider();
        var vectorStore = provider.GetRequiredService<IVectorStore>();

        try
        {
            var progress = new ConsoleVectorizationProgress();
            await action(provider.GetRequiredService<TypeSourceIndexVectorizerV2>(), progress)
                .ConfigureAwait(false);
        }
        finally
        {
            if (provider.GetService<LlamaModelSessionHolder>() is IDisposable sessionHolder)
                sessionHolder.Dispose();
            vectorStore.Dispose();
        }
    }

    /// <summary>
    /// Печатает процент векторизации в одну строку консоли.
    /// </summary>
    private sealed class ConsoleVectorizationProgress : IProgress<VectorizationProgress>
    {
        private int _lastPercent = -1;

        /// <inheritdoc />
        public void Report(VectorizationProgress value)
        {
            var percent = value.Percent;
            if (!value.IsComplete && percent == _lastPercent)
                return;

            _lastPercent = percent;
            Console.Write($"\rVectorizing: {percent}% ({value.Processed}/{value.Total})");

            if (value.IsComplete)
                Console.WriteLine();
        }
    }
}
