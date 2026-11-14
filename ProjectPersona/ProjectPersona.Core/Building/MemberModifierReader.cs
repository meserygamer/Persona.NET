using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Извлекает модификаторы члена, кроме модификаторов доступа.
/// </summary>
internal static class MemberModifierReader
{
    public static IReadOnlyList<string> Read(SyntaxTokenList modifiers) =>
        modifiers
            .Where(static modifier => !IsAccessibilityModifier(modifier.Kind()))
            .Select(static modifier => modifier.Text)
            .ToList();

    private static bool IsAccessibilityModifier(SyntaxKind kind) =>
        kind switch
        {
            SyntaxKind.PublicKeyword => true,
            SyntaxKind.PrivateKeyword => true,
            SyntaxKind.ProtectedKeyword => true,
            SyntaxKind.InternalKeyword => true,
            SyntaxKind.FileKeyword => true,
            _ => false
        };
}
