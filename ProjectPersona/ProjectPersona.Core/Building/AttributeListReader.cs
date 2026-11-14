using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Извлекает атрибуты и базовые типы с разрешением имён через <see cref="TypeRefResolver"/>.
/// </summary>
internal static class AttributeListReader
{
    /// <summary>
    /// Читает атрибуты из списков атрибутов в порядке объявления.
    /// </summary>
    public static IReadOnlyList<AttributeInfo> Read(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel? semanticModel)
    {
        if (attributeLists.Count == 0)
            return [];

        var result = new List<AttributeInfo>();
        foreach (var list in attributeLists)
        {
            var target = list.Target?.Identifier.Text;
            foreach (var attribute in list.Attributes)
                result.Add(TypeRefResolver.FromAttribute(attribute, semanticModel, target));
        }

        return result;
    }

    /// <summary>
    /// Читает непосредственные базовые типы из списка наследования.
    /// </summary>
    public static IReadOnlyList<BaseTypeRef> ReadBaseTypes(
        BaseListSyntax? baseList,
        INamedTypeSymbol? declaredType,
        SemanticModel? semanticModel)
    {
        if (baseList is null || baseList.Types.Count == 0)
            return [];

        return baseList.Types
            .Select(type =>
            {
                var typeRef = TypeRefResolver.FromSyntax(type.Type, semanticModel);
                return new BaseTypeRef(typeRef, ClassifyBase(type.Type, declaredType, semanticModel));
            })
            .ToList();
    }

    private static BaseTypeKind ClassifyBase(
        TypeSyntax typeSyntax,
        INamedTypeSymbol? declaredType,
        SemanticModel? semanticModel)
    {
        if (declaredType?.TypeKind == TypeKind.Enum)
            return BaseTypeKind.EnumUnderlying;

        if (semanticModel is null)
            return BaseTypeKind.Class;

        var symbol = semanticModel.GetTypeInfo(typeSyntax).Type;
        if (symbol is null || symbol.TypeKind == TypeKind.Error)
            return BaseTypeKind.Class;

        return symbol.TypeKind == TypeKind.Interface
            ? BaseTypeKind.Interface
            : BaseTypeKind.Class;
    }
}
