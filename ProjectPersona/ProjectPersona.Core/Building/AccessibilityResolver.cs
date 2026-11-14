using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Определяет модификаторы доступа C# по синтаксическим токенам Roslyn.
/// </summary>
internal static class AccessibilityResolver
{
    /// <summary>
    /// Определяет эффективный уровень доступа объявления по списку модификаторов и синтаксическому контексту.
    /// </summary>
    /// <param name="modifiers">Токены модификаторов объявления.</param>
    /// <param name="context">
    /// Синтаксический контекст для применения доступа по умолчанию в C#, когда явный модификатор отсутствует.
    /// </param>
    /// <returns>Определённый уровень доступа.</returns>
    public static AccessibilityLevel GetAccessibility(
        SyntaxTokenList modifiers,
        AccessibilityContext context)
    {
        var hasPublic = false;
        var hasPrivate = false;
        var hasProtected = false;
        var hasInternal = false;
        var hasFile = false;

        foreach (var modifier in modifiers)
        {
            switch (modifier.Kind())
            {
                case SyntaxKind.PublicKeyword:
                    hasPublic = true;
                    break;
                case SyntaxKind.PrivateKeyword:
                    hasPrivate = true;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    hasProtected = true;
                    break;
                case SyntaxKind.InternalKeyword:
                    hasInternal = true;
                    break;
                case SyntaxKind.FileKeyword:
                    hasFile = true;
                    break;
            }
        }

        if (hasFile)
            return AccessibilityLevel.File;

        if (hasProtected && hasInternal)
            return AccessibilityLevel.ProtectedInternal;

        if (hasPrivate && hasProtected)
            return AccessibilityLevel.PrivateProtected;

        if (hasPublic)
            return AccessibilityLevel.Public;

        if (hasProtected)
            return AccessibilityLevel.Protected;

        if (hasInternal)
            return AccessibilityLevel.Internal;

        if (hasPrivate)
            return AccessibilityLevel.Private;

        return context switch
        {
            AccessibilityContext.TopLevelType => AccessibilityLevel.Internal,
            AccessibilityContext.NestedType => AccessibilityLevel.Private,
            AccessibilityContext.InterfaceMember => AccessibilityLevel.Public,
            AccessibilityContext.EnumMember => AccessibilityLevel.Public,
            _ => AccessibilityLevel.Private
        };
    }

    public static bool HasExplicitAccessibilityModifier(SyntaxTokenList modifiers) =>
        modifiers.Any(static modifier => modifier.Kind() switch
        {
            SyntaxKind.PublicKeyword => true,
            SyntaxKind.PrivateKeyword => true,
            SyntaxKind.ProtectedKeyword => true,
            SyntaxKind.InternalKeyword => true,
            SyntaxKind.FileKeyword => true,
            _ => false
        });
}

internal enum AccessibilityContext
{
    TopLevelType,
    NestedType,
    ClassMember,
    InterfaceMember,
    EnumMember
}
