using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ProjectPersona.CliTools;

/// <summary>
/// Открывает MSBuild-решение для CLI-генерации каталога или индекса исходников.
/// </summary>
internal sealed class MsBuildSolutionSession : IDisposable
{
    private MsBuildSolutionSession(MSBuildWorkspace workspace, Solution solution)
    {
        Workspace = workspace;
        Solution = solution;
    }

    /// <summary>
    /// Рабочая область MSBuild, которую нужно удерживать, пока используется <see cref="Solution"/>.
    /// </summary>
    public MSBuildWorkspace Workspace { get; }

    /// <summary>
    /// Загруженное решение.
    /// </summary>
    public Solution Solution { get; }

    /// <summary>
    /// Регистрирует MSBuild Locator и открывает <paramref name="solutionPath"/>.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу <c>.sln</c>.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Сессия с открытым решением.</returns>
    public static async Task<MsBuildSolutionSession> OpenAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(static e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                Console.Error.WriteLine($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}");
        });

        try
        {
            var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new MsBuildSolutionSession(workspace, solution);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Workspace.Dispose();
}
