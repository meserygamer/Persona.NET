using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Entities;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Создаёт каталог объявленных типов, извлечённых из исходных файлов C#, сгруппированных по сборкам, целевым фреймворкам и пространствам имён.
/// </summary>
public sealed class TypeCatalog
{
    private TypeCatalog(IReadOnlyList<AssemblyGroup> assemblyGroups)
    {
        AssemblyGroups = assemblyGroups;
    }

    /// <summary>
    /// Компиляции сборок в каталоге; каждая содержит пространства имён и объявленные типы.
    /// Для multi-targeting одна сборка представлена несколькими элементами — по одному на TFM.
    /// </summary>
    public IReadOnlyList<AssemblyGroup> AssemblyGroups { get; }

    /// <summary>
    /// Создаёт каталог типов из всех C#-проектов решения Roslyn.
    /// </summary>
    /// <param name="solution">Решение, C#-документы которого сканируются.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Каталог типов, сгруппированных по сборкам, целевым фреймворкам и пространствам имён.</returns>
    public static Task<TypeCatalog> CreateAsync(Solution solution, CancellationToken cancellationToken = default) =>
        CreateAsync(solution.Projects, cancellationToken);

    /// <summary>
    /// Создаёт каталог типов, сканируя синтаксические деревья C#-документов указанных проектов.
    /// </summary>
    /// <param name="projects">Проекты, C#-документы которых сканируются. Проекты не на C# пропускаются.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Каталог типов, сгруппированных по сборкам, целевым фреймворкам и пространствам имён.</returns>
    public static async Task<TypeCatalog> CreateAsync(
        IEnumerable<Project> projects,
        CancellationToken cancellationToken = default)
    {
        var builder = new CatalogBuilder();

        foreach (var project in projects.Where(p => p.Language == LanguageNames.CSharp))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = AssemblyIdentity.Create(project.AssemblyName, TargetFrameworkResolver.Get(project));
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
                continue;

            var documentationContext = new XmlDocumentationContext(compilation);

            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var tree = semanticModel?.SyntaxTree
                    ?? await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (tree is null)
                    continue;

                var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                if (root is not CompilationUnitSyntax compilationUnit)
                    continue;

                TypeDeclarationReader.ProcessMembers(
                    compilationUnit.Members,
                    assembly,
                    string.Empty,
                    builder,
                    semanticModel,
                    documentationContext);
            }
        }

        return new TypeCatalog(builder.Build());
    }
}
