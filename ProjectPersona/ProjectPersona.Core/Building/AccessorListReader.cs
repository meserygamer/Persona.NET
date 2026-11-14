using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Извлекает компактный снимок аксессоров свойства или индексера без тел.
/// </summary>
internal static class AccessorListReader
{
    public static IReadOnlyList<string> Read(
        PropertyDeclarationSyntax property,
        AccessibilityLevel defaultAccessibility)
    {
        if (property.ExpressionBody is not null)
            return ["get..."];

        return property.AccessorList is { } accessorList
            ? Read(accessorList, defaultAccessibility)
            : [];
    }

    public static IReadOnlyList<string> Read(
        IndexerDeclarationSyntax indexer,
        AccessibilityLevel defaultAccessibility)
    {
        if (indexer.ExpressionBody is not null)
            return ["get..."];

        return indexer.AccessorList is { } accessorList
            ? Read(accessorList, defaultAccessibility)
            : [];
    }

    private static IReadOnlyList<string> Read(
        AccessorListSyntax accessorList,
        AccessibilityLevel defaultAccessibility)
    {
        if (accessorList.Accessors.Count == 0)
            return [];

        return accessorList.Accessors
            .Select(accessor => FormatAccessor(accessor, defaultAccessibility))
            .ToList();
    }

    private static string FormatAccessor(
        AccessorDeclarationSyntax accessor,
        AccessibilityLevel defaultAccessibility)
    {
        var keyword = accessor.Keyword.Text;
        if (HasImplementation(accessor))
            keyword += "...";

        if (!AccessibilityResolver.HasExplicitAccessibilityModifier(accessor.Modifiers))
            return keyword;

        var accessorAccessibility = AccessibilityResolver.GetAccessibility(
            accessor.Modifiers,
            AccessibilityContext.ClassMember);

        if (accessorAccessibility == defaultAccessibility)
            return keyword;

        return $"{FormatAccessibility(accessorAccessibility)} {keyword}";
    }

    private static bool HasImplementation(AccessorDeclarationSyntax accessor) =>
        accessor.Body is not null || accessor.ExpressionBody is not null;

    private static string FormatAccessibility(AccessibilityLevel accessibility) =>
        accessibility switch
        {
            AccessibilityLevel.Public => "public",
            AccessibilityLevel.Private => "private",
            AccessibilityLevel.Protected => "protected",
            AccessibilityLevel.Internal => "internal",
            AccessibilityLevel.ProtectedInternal => "protected internal",
            AccessibilityLevel.PrivateProtected => "private protected",
            AccessibilityLevel.File => "file",
            _ => accessibility.ToString().ToLowerInvariant()
        };
}
