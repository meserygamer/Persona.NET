using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AssemblyIdentity = ProjectPersona.Core.Building.AssemblyIdentity;
using TargetFrameworkResolver = ProjectPersona.Core.Building.TargetFrameworkResolver;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Naming;
using ProjectPersona.SourceIndex.Projection;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Индекс исходного кода объявлений типов по FQN, построенный из Roslyn через <see cref="CreateAsync(Solution, CancellationToken)"/>. Сохранение и загрузка — в реализациях <see cref="ITypeSourceIndexWriter"/> и <see cref="ITypeSourceLookupFactory"/>.
/// </summary>
public sealed class TypeSourceIndex : ITypeSourceLookup
{
    private readonly Dictionary<string, FqnIndexEntry> _entries;
    private readonly Dictionary<string, UsedByFqnEntry> _usedByEntries;

    internal TypeSourceIndex(
        Dictionary<string, FqnBucket> buckets,
        Dictionary<string, UsedByFqnEntry> usedByEntries)
    {
        _entries = buckets.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToEntry(),
            StringComparer.Ordinal);
        _usedByEntries = usedByEntries;
    }

    /// <summary>
    /// Возвращает фрагменты исходного кода для указанного FQN.
    /// </summary>
    /// <param name="fqn">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
    /// <param name="assemblyName">Имя сборки для сужения результата или <see langword="null"/>, чтобы вернуть совпадения из всех сборок.</param>
    /// <param name="memberNames">Имена членов в формате каталога; <see langword="null"/> или пустой список — весь тип.</param>
    /// <returns>Список совпадений; пустой, если FQN не найден, тип отсутствует в указанной сборке или ни один член не совпал.</returns>
    public IReadOnlyList<TypeSourceMatch> Find(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null)
    {
        var normalizedFqn = TypeFqn.NormalizeQuery(fqn);
        if (normalizedFqn is null)
            return [];

        if (!_entries.TryGetValue(normalizedFqn, out var entry))
            return [];

        var source = string.IsNullOrWhiteSpace(assemblyName)
            ? entry.All
            : entry.ByAssembly.GetValueOrDefault(assemblyName.Trim());

        if (source is null)
            return [];

        return TypeSourceProjection.ProjectMatches(source, memberNames);
    }

    /// <inheritdoc />
    public IReadOnlyList<TypeSourceUsedByMatch> FindUsedBy(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null,
        int limit = TypeSourceUsedByLimit.DefaultLimit)
    {
        var normalizedFqn = TypeFqn.NormalizeQuery(fqn);
        if (normalizedFqn is null)
            return [];

        if (!_entries.TryGetValue(normalizedFqn, out var entry))
            return [];

        var targets = string.IsNullOrWhiteSpace(assemblyName)
            ? entry.All
            : entry.ByAssembly.GetValueOrDefault(assemblyName.Trim());

        if (targets is null)
            return [];

        _usedByEntries.TryGetValue(normalizedFqn, out var usedByEntry);
        return TypeSourceProjection.ProjectUsedBy(targets, usedByEntry, memberNames, limit);
    }

    /// <summary>
    /// Возвращает индекс потребителей для FQN или <see langword="null"/>.
    /// </summary>
    /// <param name="fqn">Нормализованный FQN цели.</param>
    internal UsedByFqnEntry? GetUsedByEntry(string fqn) =>
        _usedByEntries.GetValueOrDefault(fqn);

    /// <summary>
    /// Возвращает содержимое индекса в виде, который могут сохранить любые хранилища.
    /// </summary>
    /// <returns>Снимок объявлений и потребителей.</returns>
    public TypeSourceIndexSnapshot ToSnapshot() =>
        new(EnumerateIndexedMatches().ToList(), _usedByEntries);

    /// <summary> Перечисляет все индексированные совпадения для сериализации. </summary>
    /// <returns> Совпадения по всем FQN и сборкам. </returns>
    internal IEnumerable<TypeSourceIndexedMatch> EnumerateIndexedMatches()
    {
        foreach (var entry in _entries.Values)
        {
            foreach (var match in entry.All)
                yield return match;
        }
    }
    
    /// <summary> Перечисляет индексированные совпадения по сборкам. </summary>
    /// <param name="assemblies"> Пары сборки и TFM; пустой или <see langword="null"/> TFM — любой TFM сборки. </param>
    /// <returns> Совпадения по всем FQN, подходящие хотя бы под один фильтр; каждое не более одного раза. </returns>
    internal IEnumerable<TypeSourceIndexedMatch> EnumerateIndexedMatches(
        IEnumerable<(string AssemblyName, string? TargetFramework)> assemblies)
    {
        var filters = TypeSourceAssemblyFilter.Normalize(assemblies).ToHashSet();

        if (filters.Count == 0)
            yield break;

        foreach (var entry in _entries.Values)
        {
            foreach (var match in entry.All)
            {
                if (filters.Contains((match.Full.AssemblyName, null))
                    || filters.Contains((match.Full.AssemblyName, match.Full.TargetFramework)))
                    yield return match;
            }
        }
    }

    /// <summary> Получить число деклараций. </summary>
    /// <param name="assemblies"> Фильтр по сборкам. </param>
    /// <returns> Число деклараций. Если assemblies = null, то по всем сборкам. </returns>
    public long GetDeclarationCount(IEnumerable<(string AssemblyName, string? TargetFramework)>? assemblies)
    {
        return assemblies is null
            ? EnumerateIndexedMatches().LongCount()
            : EnumerateIndexedMatches(assemblies).LongCount();
    }

    /// <inheritdoc />
    public IEnumerable<TypeSourceDeclaration> EnumerateDeclarations()
    {
        foreach (var match in EnumerateIndexedMatches())
            yield return ToDeclaration(match);
    }

    /// <inheritdoc />
    public IEnumerable<TypeSourceDeclaration> EnumerateDeclarations(IEnumerable<(string AssemblyName, string? TargetFramework)> keys)
    {
        var filters = TypeSourceAssemblyFilter.Normalize(keys);

        foreach (var match in EnumerateIndexedMatches())
        {
            var isRequiredAssembly = filters.Any(filter => 
                filter.AssemblyName == match.Full.AssemblyName 
                && (filter.TargetFramework is null || filter.TargetFramework == match.Full.TargetFramework));
            
            if (isRequiredAssembly) 
                yield return ToDeclaration(match);
        }
    }

    internal static TypeSourceDeclaration ToDeclaration(TypeSourceIndexedMatch match)
    {
        var full = match.Full;
        var members = match.Members.Count == 0
            ? []
            : match.Members
                .Select(static slice => new TypeSourceMemberSpan(slice.Name, slice.Start, slice.Length))
                .ToList();

        return new TypeSourceDeclaration(
            full.AssemblyName,
            full.TargetFramework,
            full.Type,
            full.SourceCode!,
            members);
    }

    /// <summary>
    /// Создаёт индекс исходников из всех C#-проектов решения Roslyn.
    /// </summary>
    /// <param name="solution">Решение, C#-документы которого индексируются.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Индекс исходников типов.</returns>
    public static Task<TypeSourceIndex> CreateAsync(
        Solution solution,
        CancellationToken cancellationToken = default) =>
        CreateAsync(solution.Projects, cancellationToken);

    /// <summary>
    /// Создаёт индекс исходников, обходя синтаксические деревья C#-документов указанных проектов.
    /// </summary>
    /// <param name="projects">Проекты, C#-документы которых индексируются. Проекты не на C# пропускаются.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Индекс исходников типов.</returns>
    public static async Task<TypeSourceIndex> CreateAsync(
        IEnumerable<Project> projects,
        CancellationToken cancellationToken = default)
    {
        var builder = new TypeSourceIndexBuilder();

        foreach (var project in projects.Where(static project => project.Language == LanguageNames.CSharp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var assembly = AssemblyIdentity.Create(
                project.AssemblyName ?? string.Empty,
                TargetFrameworkResolver.Get(project));

            await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (tree is null)
                    continue;

                var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                if (root is not CompilationUnitSyntax compilationUnit)
                    continue;

                var sourceText = await tree.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                TypeSourceLocator.Locate(
                    compilationUnit,
                    sourceText,
                    assembly,
                    semanticModel,
                    builder);
            }
        }

        return builder.Build();
    }
}
