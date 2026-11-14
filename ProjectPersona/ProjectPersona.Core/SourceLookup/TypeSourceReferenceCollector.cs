using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Building;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Собирает уникальные семантические ссылки на типы и члены внутри объявления типа.
/// </summary>
internal static class TypeSourceReferenceCollector
{
    private static readonly IReadOnlyList<TypeSourceReference> Empty = [];

    /// <summary>
    /// Собирает уникальные ссылки внутри объявления типа, не заходя в тела вложенных типов.
    /// </summary>
    /// <param name="declaration">Объявление типа, делегата или перечисления.</param>
    /// <param name="semanticModel">Семантическая модель документа или <see langword="null"/>.</param>
    /// <param name="declaringFqn">FQN объявляемого типа (исключается из ссылок).</param>
    /// <returns>Отсортированный список уникальных ссылок.</returns>
    public static IReadOnlyList<TypeSourceReference> Collect(
        MemberDeclarationSyntax declaration,
        SemanticModel? semanticModel,
        string declaringFqn)
    {
        if (semanticModel is null)
            return Empty;

        var references = new Dictionary<string, TypeSourceReference>(StringComparer.Ordinal);

        foreach (var node in declaration.DescendantNodes(descendIntoTrivia: false))
        {
            if (IsInsideNestedTypeDeclaration(node, declaration))
                continue;

            switch (node)
            {
                case TypeSyntax typeSyntax:
                    if (typeSyntax.Parent is TypeSyntax)
                        break;

                    if (!IsTypePosition(typeSyntax))
                    {
                        if (typeSyntax is IdentifierNameSyntax or GenericNameSyntax)
                            AddMemberReference(references, typeSyntax, semanticModel, typeSyntax);

                        break;
                    }

                    AddTypeReference(references, typeSyntax, semanticModel, declaringFqn);
                    break;

                case InvocationExpressionSyntax invocation:
                    AddInvocationReference(references, invocation, semanticModel);
                    break;

                case ObjectCreationExpressionSyntax objectCreation:
                    AddTypeReference(references, objectCreation.Type, semanticModel, declaringFqn);
                    if (semanticModel.GetSymbolInfo(objectCreation).Symbol is IMethodSymbol constructor)
                        AddMember(references, GetDisplayText(objectCreation), constructor, semanticModel, objectCreation);
                    break;

                case MemberAccessExpressionSyntax memberAccess:
                    if (IsInvocationCallee(memberAccess))
                        break;

                    AddMemberReference(references, memberAccess, semanticModel, memberAccess);
                    break;

                case ElementAccessExpressionSyntax elementAccess:
                    AddMemberReference(references, elementAccess, semanticModel, elementAccess);
                    break;

                case MemberBindingExpressionSyntax memberBinding:
                    AddMemberReference(references, memberBinding, semanticModel, memberBinding);
                    break;

                case ConditionalAccessExpressionSyntax conditionalAccess
                    when conditionalAccess.WhenNotNull is MemberBindingExpressionSyntax conditionalBinding:
                    AddMemberReference(references, conditionalBinding, semanticModel, conditionalBinding);
                    break;

                case BinaryExpressionSyntax binary:
                    AddOperatorReference(references, binary, semanticModel);
                    break;

                case PrefixUnaryExpressionSyntax prefixUnary:
                    AddOperatorReference(references, prefixUnary, semanticModel);
                    break;

                case AssignmentExpressionSyntax assignment
                    when !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    AddOperatorReference(references, assignment, semanticModel);
                    break;
            }
        }

        return references.Values
            .OrderBy(static reference => reference.Kind)
            .ThenBy(static reference => reference.Fqn, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Name, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Signature, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsInsideNestedTypeDeclaration(SyntaxNode node, MemberDeclarationSyntax rootDeclaration)
    {
        for (var current = node.Parent; current is not null && current != rootDeclaration; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax or DelegateDeclarationSyntax or EnumDeclarationSyntax)
                return true;
        }

        return false;
    }

    private static bool IsInvocationCallee(SyntaxNode node)
    {
        var current = node;
        while (current.Parent is MemberAccessExpressionSyntax memberAccess &&
               memberAccess.Expression == current)
            current = memberAccess;

        return current.Parent is InvocationExpressionSyntax invocation &&
               invocation.Expression == current;
    }

    private static bool IsTypePosition(TypeSyntax typeSyntax) =>
        typeSyntax.Parent switch
        {
            InvocationExpressionSyntax invocation when invocation.Expression == typeSyntax => false,
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name == typeSyntax => false,
            MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == typeSyntax => false,
            MemberBindingExpressionSyntax memberBinding when memberBinding.Name == typeSyntax => false,
            ElementAccessExpressionSyntax elementAccess when elementAccess.Expression == typeSyntax => false,
            ExplicitInterfaceSpecifierSyntax => false,
            CastExpressionSyntax cast when cast.Expression == typeSyntax => false,
            AssignmentExpressionSyntax assignment
                when assignment.Left == typeSyntax || assignment.Right == typeSyntax => false,
            ReturnStatementSyntax returnStatement when returnStatement.Expression == typeSyntax => false,
            ArgumentSyntax argument when argument.Expression == typeSyntax => false,
            EqualsValueClauseSyntax clause when clause.Value == typeSyntax => false,
            ArrowExpressionClauseSyntax arrow when arrow.Expression == typeSyntax => false,
            YieldStatementSyntax yield when yield.Expression == typeSyntax => false,
            ThrowStatementSyntax throwStatement when throwStatement.Expression == typeSyntax => false,
            IfStatementSyntax ifStatement when ifStatement.Condition == typeSyntax => false,
            WhileStatementSyntax whileStatement when whileStatement.Condition == typeSyntax => false,
            DoStatementSyntax doStatement when doStatement.Condition == typeSyntax => false,
            ForStatementSyntax forStatement when forStatement.Condition == typeSyntax => false,
            SwitchStatementSyntax switchStatement when switchStatement.Expression == typeSyntax => false,
            SwitchExpressionSyntax switchExpression when switchExpression.GoverningExpression == typeSyntax =>
                false,
            BinaryExpressionSyntax binary when binary.Left == typeSyntax || binary.Right == typeSyntax => false,
            PrefixUnaryExpressionSyntax prefix when prefix.Operand == typeSyntax => false,
            PostfixUnaryExpressionSyntax postfix when postfix.Operand == typeSyntax => false,
            ConditionalExpressionSyntax conditional
                when conditional.Condition == typeSyntax
                    || conditional.WhenTrue == typeSyntax
                    || conditional.WhenFalse == typeSyntax => false,
            InterpolationSyntax interpolation when interpolation.Expression == typeSyntax => false,
            _ => true
        };

    private static void AddOperatorReference(
        Dictionary<string, TypeSourceReference> references,
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(expression);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        if (symbol is not IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } method)
            return;

        AddMember(references, expression.ToString().Trim(), method, semanticModel, expression);
    }

    private static void AddInvocationReference(
        Dictionary<string, TypeSourceReference> references,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var symbol = GetEffectiveSymbol(semanticModel.GetSymbolInfo(invocation), semanticModel, invocation);
        if (symbol is null)
            return;

        AddMember(
            references,
            GetDisplayText(invocation.Expression),
            symbol,
            semanticModel,
            invocation,
            includeSkippedContainingType: true);
    }

    private static void AddTypeReference(
        Dictionary<string, TypeSourceReference> references,
        TypeSyntax typeSyntax,
        SemanticModel semanticModel,
        string declaringFqn)
    {
        var display = GetTypeDisplayText(typeSyntax);
        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;

        if (type is null || type.TypeKind == TypeKind.Error)
        {
            AddUnresolved(references, display);
            return;
        }

        type = UnwrapType(type);

        if (!TryGetNamedType(type, out var namedType))
            return;

        if (ShouldSkipType(namedType, declaringFqn) && !IsExplicitCastTarget(typeSyntax))
            return;

        AddType(references, display, namedType);
    }

    private static bool IsExplicitCastTarget(TypeSyntax typeSyntax) =>
        typeSyntax.Parent is CastExpressionSyntax cast && cast.Type == typeSyntax;

    private static void AddMemberReference(
        Dictionary<string, TypeSourceReference> references,
        SyntaxNode contextNode,
        SemanticModel semanticModel,
        SyntaxNode symbolNode,
        bool includeSkippedContainingType = false)
    {
        var symbol = GetEffectiveSymbol(semanticModel.GetSymbolInfo(symbolNode), semanticModel, symbolNode);
        if (symbol is null)
            return;

        AddMember(
            references,
            GetDisplayText(symbolNode),
            symbol,
            semanticModel,
            symbolNode,
            includeSkippedContainingType);
    }

    private static void AddType(
        Dictionary<string, TypeSourceReference> references,
        string display,
        INamedTypeSymbol namedType)
    {
        namedType = namedType.OriginalDefinition;
        var fqn = NamedTypeFqn.From(namedType);
        var reference = new TypeSourceReference(
            TypeSourceReferenceKind.Type,
            display,
            fqn,
            namedType.Name,
            GetAssemblyName(namedType),
            string.Empty);

        references[CreateKey(reference)] = reference;
    }

    private static void AddMember(
        Dictionary<string, TypeSourceReference> references,
        string display,
        ISymbol symbol,
        SemanticModel semanticModel,
        SyntaxNode symbolNode,
        bool includeSkippedContainingType = false)
    {
        symbol = symbol.OriginalDefinition;
        var refinedSymbol = RefineSymbolForReceiver(symbol, semanticModel, symbolNode);
        if (refinedSymbol is null)
            return;

        symbol = refinedSymbol;

        var receiverType = TryGetReceiverNamedType(semanticModel, symbolNode);

        switch (symbol)
        {
            case IMethodSymbol method:
                AddMethod(references, display, method, includeSkippedContainingType);
                break;

            case IPropertySymbol property:
                AddProperty(references, display, property, receiverType);
                break;

            case IFieldSymbol field:
                AddField(references, display, field, receiverType);
                break;

            case IEventSymbol eventSymbol:
                AddEvent(references, display, eventSymbol, receiverType);
                break;
        }
    }

    private static void AddMethod(
        Dictionary<string, TypeSourceReference> references,
        string display,
        IMethodSymbol method,
        bool includeSkippedContainingType)
    {
        if (method.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction)
            return;

        var containingType = GetContainingNamedType(method);
        if (containingType is null || containingType.TypeKind == TypeKind.TypeParameter)
            return;

        if (!includeSkippedContainingType && ShouldSkipType(containingType, declaringFqn: null))
            return;

        var kind = method.MethodKind == MethodKind.Constructor
            ? TypeSourceReferenceKind.Constructor
            : TypeSourceReferenceKind.Method;

        var memberName = method.MethodKind == MethodKind.Conversion
            ? TypeSourceMemberNameResolver.FormatConversionOperatorCatalogName(method)
            : method.Name;

        var reference = new TypeSourceReference(
            kind,
            display,
            NamedTypeFqn.From(containingType),
            memberName,
            GetAssemblyName(containingType),
            FormatCallableSignature(method));

        references[CreateKey(reference)] = reference;
    }

    private static void AddProperty(
        Dictionary<string, TypeSourceReference> references,
        string display,
        IPropertySymbol property,
        INamedTypeSymbol? receiverType)
    {
        var containingType = GetReferenceContainingType(property, receiverType);
        if (containingType is null || ShouldSkipType(containingType, declaringFqn: null))
            return;

        var kind = property.IsIndexer
            ? TypeSourceReferenceKind.Indexer
            : TypeSourceReferenceKind.Property;

        var reference = new TypeSourceReference(
            kind,
            display,
            NamedTypeFqn.From(containingType),
            property.Name,
            GetAssemblyName(containingType),
            property.IsIndexer ? FormatCallableSignature(property) : string.Empty);

        references[CreateKey(reference)] = reference;
    }

    private static void AddField(
        Dictionary<string, TypeSourceReference> references,
        string display,
        IFieldSymbol field,
        INamedTypeSymbol? receiverType)
    {
        var containingType = GetReferenceContainingType(field, receiverType);
        if (containingType is null || ShouldSkipType(containingType, declaringFqn: null))
            return;

        var kind = field.ContainingType.TypeKind == TypeKind.Enum
            ? TypeSourceReferenceKind.EnumMember
            : TypeSourceReferenceKind.Field;

        var reference = new TypeSourceReference(
            kind,
            display,
            NamedTypeFqn.From(containingType),
            field.Name,
            GetAssemblyName(containingType),
            string.Empty);

        references[CreateKey(reference)] = reference;
    }

    private static void AddEvent(
        Dictionary<string, TypeSourceReference> references,
        string display,
        IEventSymbol eventSymbol,
        INamedTypeSymbol? receiverType)
    {
        var containingType = GetReferenceContainingType(eventSymbol, receiverType);
        if (containingType is null || ShouldSkipType(containingType, declaringFqn: null))
            return;

        var reference = new TypeSourceReference(
            TypeSourceReferenceKind.Event,
            display,
            NamedTypeFqn.From(containingType),
            eventSymbol.Name,
            GetAssemblyName(containingType),
            string.Empty);

        references[CreateKey(reference)] = reference;
    }

    private static void AddUnresolved(Dictionary<string, TypeSourceReference> references, string display)
    {
        if (string.IsNullOrWhiteSpace(display))
            return;

        var reference = new TypeSourceReference(
            TypeSourceReferenceKind.Unresolved,
            display,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

        references[CreateKey(reference)] = reference;
    }

    private static ITypeSymbol UnwrapType(ITypeSymbol type)
    {
        while (true)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    type = array.ElementType;
                    continue;

                case IPointerTypeSymbol pointer:
                    type = pointer.PointedAtType;
                    continue;

                case INamedTypeSymbol { IsGenericType: true, OriginalDefinition: { } original }
                    when !SymbolEqualityComparer.Default.Equals(type, original):
                    type = original;
                    continue;

                default:
                    return type;
            }
        }
    }

    private static bool TryGetNamedType(ITypeSymbol type, out INamedTypeSymbol namedType)
    {
        type = UnwrapType(type);

        if (type is INamedTypeSymbol named)
        {
            namedType = named;
            return true;
        }

        namedType = null!;
        return false;
    }

    private static bool ShouldSkipType(INamedTypeSymbol namedType, string? declaringFqn)
    {
        namedType = namedType.OriginalDefinition;

        if (namedType.TypeKind == TypeKind.TypeParameter)
            return true;

        if (namedType.SpecialType is not SpecialType.None and not SpecialType.System_Nullable_T)
            return true;

        if (declaringFqn is not null &&
            string.Equals(NamedTypeFqn.From(namedType), declaringFqn, StringComparison.Ordinal))
            return true;

        return false;
    }

    private static INamedTypeSymbol? GetContainingNamedType(ISymbol symbol)
    {
        if (symbol is IMethodSymbol { ReducedFrom: { } reducedFrom })
            return GetContainingNamedType(reducedFrom);

        var containing = symbol.ContainingType;
        if (containing is null || containing.TypeKind == TypeKind.Error)
            return null;

        return containing.OriginalDefinition;
    }

    private static string GetAssemblyName(INamedTypeSymbol namedType) =>
        namedType.ContainingAssembly?.Name ?? string.Empty;

    /// <summary>
    /// Сигнатура объявляемого члена: список параметров метода, оператора, конструктора или индексатора.
    /// </summary>
    /// <param name="member">Объявление члена.</param>
    /// <param name="semanticModel">Семантическая модель или <see langword="null"/>.</param>
    /// <returns>Компактная сигнатура либо пустая строка.</returns>
    internal static string FormatDeclaringSignature(MemberDeclarationSyntax member, SemanticModel? semanticModel)
    {
        if (semanticModel?.GetDeclaredSymbol(member) is not ISymbol symbol)
            return string.Empty;

        return symbol switch
        {
            IMethodSymbol method => FormatCallableSignature(method),
            IPropertySymbol { IsIndexer: true } property => FormatCallableSignature(property),
            _ => string.Empty
        };
    }

    private static string FormatCallableSignature(IMethodSymbol method)
    {
        var builder = new StringBuilder();
        builder.Append('(');
        for (var index = 0; index < method.Parameters.Length; index++)
        {
            if (index > 0)
                builder.Append(", ");

            builder.Append(TypeRefResolver.FromSymbol(method.Parameters[index].Type).Display);
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static string FormatCallableSignature(IPropertySymbol property)
    {
        if (property.Parameters.Length == 0)
            return "[]";

        var builder = new StringBuilder();
        builder.Append('[');
        for (var index = 0; index < property.Parameters.Length; index++)
        {
            if (index > 0)
                builder.Append(", ");

            builder.Append(TypeRefResolver.FromSymbol(property.Parameters[index].Type).Display);
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string GetTypeDisplayText(TypeSyntax typeSyntax) =>
        typeSyntax switch
        {
            NameSyntax nameSyntax => nameSyntax.ToString().Trim(),
            _ => typeSyntax.ToString().Trim()
        };

    private static string GetDisplayText(SyntaxNode node) =>
        node switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.Text,
            _ => node.ToString().Trim()
        };

    private static string CreateKey(TypeSourceReference reference) =>
        $"{reference.Kind}|{reference.Fqn}|{reference.Name}|{reference.Signature}";

    private static ISymbol? GetEffectiveSymbol(
        SymbolInfo symbolInfo,
        SemanticModel semanticModel,
        SyntaxNode symbolNode)
    {
        if (symbolInfo.Symbol is { } symbol)
            return symbol;

        var candidates = symbolInfo.CandidateSymbols;
        if (candidates.IsEmpty)
            return null;

        var receiverType = TryGetReceiverNamedType(semanticModel, symbolNode);
        if (receiverType is not null)
        {
            foreach (var candidate in candidates)
            {
                if (IsMemberDeclaredOnType(candidate, receiverType))
                    return candidate;
            }
        }

        return candidates[0];
    }

    private static ISymbol? RefineSymbolForReceiver(
        ISymbol symbol,
        SemanticModel semanticModel,
        SyntaxNode symbolNode)
    {
        var receiverType = TryGetReceiverNamedType(semanticModel, symbolNode);
        symbol = PreferMemberDeclaredOnReceiver(symbol, receiverType);

        var containingType = GetContainingNamedType(symbol);
        if (containingType is null || !ShouldSkipType(containingType, declaringFqn: null))
            return symbol;

        if (receiverType is null)
            return symbol;

        var onReceiver = FindEquivalentMemberOnType(receiverType, symbol);
        if (onReceiver is not null)
        {
            containingType = GetContainingNamedType(onReceiver);
            if (containingType is not null && !ShouldSkipType(containingType, declaringFqn: null))
                return onReceiver;
        }

        return symbol;
    }

    private static INamedTypeSymbol? GetReferenceContainingType(ISymbol member, INamedTypeSymbol? receiverType)
    {
        var declaredType = GetContainingNamedType(member);
        if (receiverType is null || declaredType is null || ShouldSkipType(receiverType, declaringFqn: null))
            return declaredType;

        if (!TypeExposesMember(receiverType, member))
            return declaredType;

        return receiverType.OriginalDefinition;
    }

    private static bool TypeExposesMember(INamedTypeSymbol type, ISymbol member)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (DeclaresCorrespondingMember(current, member))
                return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (DeclaresCorrespondingMember(iface, member))
                return true;
        }

        return false;
    }

    private static bool DeclaresCorrespondingMember(INamedTypeSymbol type, ISymbol member)
    {
        foreach (var candidate in type.GetMembers(member.Name))
        {
            if (MembersCorrespond(member, candidate))
                return true;
        }

        return false;
    }

    private static ISymbol PreferMemberDeclaredOnReceiver(ISymbol symbol, INamedTypeSymbol? receiverType)
    {
        if (receiverType is null)
            return symbol;

        foreach (var member in receiverType.GetMembers(symbol.Name))
        {
            if (MembersCorrespond(symbol, member))
                return member;
        }

        return symbol;
    }

    private static bool IsMemberDeclaredOnType(ISymbol symbol, INamedTypeSymbol receiverType)
    {
        foreach (var member in receiverType.GetMembers(symbol.Name))
        {
            if (MembersCorrespond(symbol, member))
                return true;
        }

        return false;
    }

    private static ISymbol? FindEquivalentMemberOnType(INamedTypeSymbol receiverType, ISymbol symbol)
    {
        foreach (var member in receiverType.GetMembers(symbol.Name))
        {
            if (!MembersCorrespond(symbol, member))
                continue;

            var containing = GetContainingNamedType(member);
            if (containing is not null && !ShouldSkipType(containing, declaringFqn: null))
                return member;
        }

        return null;
    }

    private static bool MembersCorrespond(ISymbol left, ISymbol right)
    {
        if (left.Kind != right.Kind)
            return false;

        return left switch
        {
            IMethodSymbol leftMethod when right is IMethodSymbol rightMethod =>
                leftMethod.MethodKind == rightMethod.MethodKind &&
                leftMethod.Parameters.Length == rightMethod.Parameters.Length,
            IPropertySymbol leftProperty when right is IPropertySymbol rightProperty =>
                leftProperty.Parameters.Length == rightProperty.Parameters.Length,
            _ => true
        };
    }

    private static INamedTypeSymbol? TryGetReceiverNamedType(SemanticModel semanticModel, SyntaxNode symbolNode)
    {
        var initializerReceiver = TryGetObjectInitializerReceiver(semanticModel, symbolNode);
        if (initializerReceiver is not null)
            return initializerReceiver;

        var receiverNode = symbolNode switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            ElementAccessExpressionSyntax elementAccess => elementAccess.Expression,
            InvocationExpressionSyntax invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
                ElementAccessExpressionSyntax elementAccess => elementAccess.Expression,
                _ => invocation.Expression
            },
            _ => symbolNode
        };

        if (receiverNode is ExpressionSyntax receiverExpression)
            receiverNode = UnwrapReceiverExpression(receiverExpression);

        var typeInfo = semanticModel.GetTypeInfo(receiverNode);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;
        if (type is null || type.TypeKind == TypeKind.Error)
            return null;

        return TryGetNamedType(UnwrapType(type), out var namedType) ? namedType : null;
    }

    private static INamedTypeSymbol? TryGetObjectInitializerReceiver(
        SemanticModel semanticModel,
        SyntaxNode symbolNode)
    {
        if (symbolNode.Parent is not AssignmentExpressionSyntax assignment || assignment.Left != symbolNode)
            return null;

        if (assignment.Parent is not InitializerExpressionSyntax initializer)
            return null;

        if (initializer.Parent is not BaseObjectCreationExpressionSyntax creation)
            return null;

        var typeInfo = semanticModel.GetTypeInfo(creation);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;
        if ((type is null || type.TypeKind == TypeKind.Error) &&
            creation is ObjectCreationExpressionSyntax objectCreation)
        {
            typeInfo = semanticModel.GetTypeInfo(objectCreation.Type);
            type = typeInfo.Type ?? typeInfo.ConvertedType;
        }

        if (type is null || type.TypeKind == TypeKind.Error)
            return null;

        return TryGetNamedType(UnwrapType(type), out var namedType) ? namedType : null;
    }

    private static ExpressionSyntax UnwrapReceiverExpression(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;

                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                default:
                    return expression;
            }
        }
    }
}
