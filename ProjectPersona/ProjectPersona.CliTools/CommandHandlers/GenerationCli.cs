using Microsoft.CodeAnalysis;
using ProjectPersona.CliTools.CommandBuilders;
using ProjectPersona.Core;
using ProjectPersona.Core.Building;
using ProjectPersona.Core.SourceLookup;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Schema;
using ProjectPersona.SourceIndex.Sqlite.Writing;

namespace ProjectPersona.CliTools.CommandHandlers;

/// <summary>
/// CLI-режимы генерации type-catalog и type-source-index без запуска MCP.
/// </summary>
internal static class GenerationCli
{
    /// <summary>
    /// Генерирует markdown type-catalog для решения.
    /// </summary>
    /// <param name="options">Параметры подкоманды.</param>
    public static async Task GenerateTypeCatalogAsync(GenerateTypeCatalogOptions options)
    {
        await RunWithSolutionAsync(options.SolutionPath, solution => WriteTypeCatalogAsync(solution, options.OutputPath))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Генерирует SQLite type-source-index для решения.
    /// </summary>
    /// <param name="options">Параметры подкоманды.</param>
    public static async Task GenerateTypeSourceIndexAsync(GenerateTypeSourceIndexOptions options)
    {
        await RunWithSolutionAsync(options.SolutionPath, solution => WriteTypeSourceIndexAsync(solution, options.OutputPath))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Генерирует type-catalog и type-source-index за одно открытие решения.
    /// </summary>
    /// <param name="options">Параметры подкоманды.</param>
    public static async Task GenerateTypeArtifactsAsync(GenerateTypeArtifactsOptions options)
    {
        await RunWithSolutionAsync(options.SolutionPath, async solution =>
            {
                await WriteTypeCatalogAsync(solution, options.CatalogOutputPath).ConfigureAwait(false);
                await WriteTypeSourceIndexAsync(solution, options.IndexOutputPath).ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    /// <summary> Открывает MSBuild-решение один раз и выполняет <paramref name="action"/>. </summary>
    /// <param name="solutionPath"> Путь к файлу <c>.sln</c>. </param>
    /// <param name="action"> Действие над загруженным <see cref="Solution"/>. </param>
    private static async Task RunWithSolutionAsync(string solutionPath, Func<Solution, Task> action)
    {
        using var session = await MsBuildSolutionSession.OpenAsync(solutionPath).ConfigureAwait(false);
        await action(session.Solution).ConfigureAwait(false);
    }

    /// <summary> Записать каталог типов. </summary>
    /// <param name="solution"> Решение. </param>
    /// <param name="outputPath"> Путь по которому произойдёт запись. </param>
    private static async Task WriteTypeCatalogAsync(Solution solution, string outputPath)
    {
        var outputDirectory = ResolveDirectory(outputPath, "type-catalog");
        Console.WriteLine("Building type catalog...");
        var catalog = await TypeCatalog.CreateAsync(solution).ConfigureAwait(false);
        Console.WriteLine($"Assemblies in catalog: {catalog.AssemblyGroups.Count}");
        await TypeCatalogMarkdownWriter.WriteAsync(catalog, outputDirectory).ConfigureAwait(false);
        Console.WriteLine($"Type catalog written to {outputDirectory}");
    }

    /// <summary> Записать индекс исходников. </summary>
    /// <param name="solution"> Решение. </param>
    /// <param name="outputPath"> Путь к папке, куда будет записана база. </param>
    private static async Task WriteTypeSourceIndexAsync(Solution solution, string outputPath)
    {
        var outputDirectory = ResolveDirectory(outputPath, "type-source-index");
        Console.WriteLine("Building type source index...");
        var index = await TypeSourceIndex.CreateAsync(solution).ConfigureAwait(false);
        var databasePath = Path.Combine(outputDirectory, SqliteIndexFormat.DatabaseFileName);
        var writer = new SqliteTypeSourceIndexWriter(new SqliteTypeSourceIndexOptions { DatabasePath = databasePath });
        var result = await writer.WriteAsync(index.ToSnapshot()).ConfigureAwait(false);
        Console.WriteLine(
            $"Type source index written to {databasePath}: " +
            $"{result.Assemblies} assemblies, {result.Declarations} declarations, {result.MemberSlices} member slices, " +
            $"{result.References} references, {result.UsedBy} used-by edges");
    }

    /// <summary> Разрешить папку. </summary>
    /// <param name="configured"> Сконфигурированный путь. </param>
    /// <param name="defaultFolderName"> Путь по умолчанию. </param>
    /// <returns> При отсутствии сконфигурированного пути произойдёт разрешение в путь по умолчанию. </returns>
    private static string ResolveDirectory(string? configured, string defaultFolderName)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return Path.Combine(AppContext.BaseDirectory, defaultFolderName);
    }
}
