using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Building;
using ProjectPersona.SourceIndex;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Определяет имена членов объявления по тем же правилам, что <see cref="TypeDeclarationReader"/>.
/// </summary>
internal static class TypeSourceMemberNameResolver
{
    /// <summary>
    /// Каталожное имя оператора преобразования по символу метода.
    /// </summary>
    /// <param name="method">Символ <see cref="MethodKind.Conversion"/>.</param>
    public static string FormatConversionOperatorCatalogName(IMethodSymbol method)
    {
        var returnType = TypeRefResolver.FromSymbol(method.ReturnType).Display;
        var keyword = method.Name == "op_Implicit" ? "implicit" : "explicit";
        return $"{keyword} operator {returnType}";
    }

    /// <summary>
    /// Возвращает имена каталога для синтаксиса члена типа.
    /// </summary>
    /// <param name="member">Объявление члена или член перечисления.</param>
    /// <param name="semanticModel">Семантическая модель документа или <see langword="null"/>.</param>
    /// <returns>Имена для сопоставления с запросом <c>memberNames</c>.</returns>
    public static IEnumerable<string> GetNames(MemberDeclarationSyntax member, SemanticModel? semanticModel)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
                if (method.ExplicitInterfaceSpecifier is { } explicitMethodInterface)
                {
                    var interfaceDisplay = TypeRefResolver.FromSyntax(explicitMethodInterface.Name, semanticModel).Display;
                    yield return $"{interfaceDisplay}.{method.Identifier.Text}";
                    break;
                }

                yield return method.Identifier.Text;
                break;

            case ConstructorDeclarationSyntax constructor:
                yield return constructor.Identifier.Text;
                break;

            case OperatorDeclarationSyntax operatorDeclaration:
                yield return $"operator {operatorDeclaration.OperatorToken.Text}";
                break;

            case ConversionOperatorDeclarationSyntax conversionOperator:
            {
                var convertedType = TypeRefResolver.FromSyntax(conversionOperator.Type, semanticModel);
                yield return $"{conversionOperator.ImplicitOrExplicitKeyword.Text} operator {convertedType.Display}";
                break;
            }

            case PropertyDeclarationSyntax property:
                if (property.ExplicitInterfaceSpecifier is { } explicitPropertyInterface)
                {
                    var interfaceDisplay = TypeRefResolver.FromSyntax(explicitPropertyInterface.Name, semanticModel).Display;
                    yield return $"{interfaceDisplay}.{property.Identifier.Text}";
                    break;
                }

                yield return property.Identifier.Text;
                break;

            case IndexerDeclarationSyntax indexer:
                if (indexer.ExplicitInterfaceSpecifier is { } explicitIndexerInterface)
                {
                    var interfaceDisplay = TypeRefResolver.FromSyntax(explicitIndexerInterface.Name, semanticModel).Display;
                    yield return $"{interfaceDisplay}.this";
                    break;
                }

                yield return "this";
                break;

            case EventDeclarationSyntax eventDeclaration:
                if (eventDeclaration.ExplicitInterfaceSpecifier is { } explicitEventInterface)
                {
                    var interfaceDisplay = TypeRefResolver.FromSyntax(explicitEventInterface.Name, semanticModel).Display;
                    yield return $"{interfaceDisplay}.{eventDeclaration.Identifier.Text}";
                    break;
                }

                yield return eventDeclaration.Identifier.Text;
                break;

            case EventFieldDeclarationSyntax eventField:
                foreach (var variable in eventField.Declaration.Variables)
                    yield return variable.Identifier.Text;
                break;

            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                    yield return variable.Identifier.Text;
                break;

            case EnumMemberDeclarationSyntax enumMember:
                yield return enumMember.Identifier.Text;
                break;
        }
    }
}
