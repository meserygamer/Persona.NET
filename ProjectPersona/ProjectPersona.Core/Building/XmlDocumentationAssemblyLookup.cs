using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Ищет XML-документацию символа в <c>.xml</c>-файлах referenced-сборок (BCL и NuGet),
/// когда у метаданных компиляции нет провайдера документации.
/// </summary>
internal sealed class XmlDocumentationAssemblyLookup(Compilation compilation)
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _membersByXmlPath =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<IAssemblySymbol, IReadOnlyDictionary<string, string>> _membersByAssembly =
        new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Возвращает XML-фрагмент <c>&lt;member&gt;</c> для символа из документации сборки или <see langword="null"/>.
    /// </summary>
    /// <param name="symbol">Символ, для которого ищется документация.</param>
    /// <returns>XML члена или <see langword="null"/>, если файл или идентификатор не найдены.</returns>
    public string? FindMemberXml(ISymbol symbol)
    {
        foreach (var documentationId in EnumerateDocumentationIds(symbol))
        {
            foreach (var assembly in EnumerateAssemblies(symbol))
            {
                var members = GetOrLoadMembers(assembly);
                if (members.TryGetValue(documentationId, out var xml))
                    return xml;
            }
        }

        return null;
    }

    private IReadOnlyDictionary<string, string> GetOrLoadMembers(IAssemblySymbol assembly)
    {
        if (_membersByAssembly.TryGetValue(assembly, out var cached))
            return cached;

        var members = LoadMembers(assembly);
        _membersByAssembly[assembly] = members;
        return members;
    }

    private IReadOnlyDictionary<string, string> LoadMembers(IAssemblySymbol assembly)
    {
        if (compilation.GetMetadataReference(assembly) is not PortableExecutableReference { FilePath: { Length: > 0 } assemblyPath })
            return EmptyMembers();

        var xmlPath = FindXmlDocumentationPath(assemblyPath);
        if (xmlPath is null)
            return EmptyMembers();

        if (_membersByXmlPath.TryGetValue(xmlPath, out var shared))
            return shared;

        var loaded = LoadMembersFromFile(xmlPath);
        _membersByXmlPath[xmlPath] = loaded;
        return loaded;
    }

    private static IReadOnlyDictionary<string, string> LoadMembersFromFile(string xmlPath)
    {
        try
        {
            using var stream = File.OpenRead(xmlPath);
            var document = XDocument.Load(stream);
            var members = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var member in document.Descendants().Where(static element => element.Name.LocalName == "member"))
            {
                var name = member.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(name))
                    continue;

                members[name] = member.ToString(SaveOptions.DisableFormatting);
            }

            return members;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return EmptyMembers();
        }
    }

    private static string? FindXmlDocumentationPath(string assemblyPath)
    {
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");
        if (File.Exists(xmlPath))
            return xmlPath;

        var directory = Path.GetDirectoryName(assemblyPath);
        var fileName = Path.GetFileName(xmlPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            return null;

        foreach (var culture in new[] { "en", "en-us" })
        {
            var culturedPath = Path.Combine(directory, culture, fileName);
            if (File.Exists(culturedPath))
                return culturedPath;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDocumentationIds(ISymbol symbol)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddIds(seen, symbol.OriginalDefinition);
        AddIds(seen, symbol);
        return seen;
    }

    private static void AddIds(HashSet<string> seen, ISymbol symbol)
    {
        TryAddId(seen, symbol.GetDocumentationCommentId());
        TryAddId(seen, DocumentationCommentId.CreateDeclarationId(symbol));
        TryAddId(seen, DocumentationCommentId.CreateReferenceId(symbol));
    }

    private static void TryAddId(HashSet<string> seen, string? documentationId)
    {
        if (!string.IsNullOrWhiteSpace(documentationId))
            seen.Add(documentationId);
    }

    private static IEnumerable<IAssemblySymbol> EnumerateAssemblies(ISymbol symbol)
    {
        var seen = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
        TryAddAssembly(seen, symbol.ContainingAssembly);
        TryAddAssembly(seen, symbol.OriginalDefinition.ContainingAssembly);
        return seen;
    }

    private static void TryAddAssembly(HashSet<IAssemblySymbol> seen, IAssemblySymbol? assembly)
    {
        if (assembly is not null)
            seen.Add(assembly);
    }

    private static IReadOnlyDictionary<string, string> EmptyMembers() =>
        new Dictionary<string, string>(StringComparer.Ordinal);
}
