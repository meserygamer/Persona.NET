using Microsoft.CodeAnalysis;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Naming;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Строит FQN типа в формате type-catalog из символов Roslyn.
/// </summary>
internal static class NamedTypeFqn
{
    /// <summary>
    /// Строит FQN именованного типа Roslyn без списка type parameters.
    /// </summary>
    /// <param name="named">Именованный тип.</param>
    /// <returns>FQN в формате каталога.</returns>
    public static string From(INamedTypeSymbol named)
    {
        var topLevel = GetTopLevelContainingType(named);
        var namespaceName = GetNamespace(topLevel.ContainingNamespace) ?? string.Empty;
        var typePath = GetTypePath(named);
        return TypeFqn.Build(namespaceName, typePath);
    }

    private static string GetTypePath(INamedTypeSymbol named)
    {
        var parts = new Stack<string>();
        for (var current = named; current is not null; current = current.ContainingType)
            parts.Push(current.Name);

        return string.Join('.', parts);
    }

    private static INamedTypeSymbol GetTopLevelContainingType(INamedTypeSymbol named)
    {
        var current = named;
        while (current.ContainingType is not null)
            current = current.ContainingType;

        return current;
    }

    private static string? GetNamespace(INamespaceSymbol? namespaceSymbol)
    {
        if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
            return null;

        return namespaceSymbol.ToDisplayString();
    }
}
