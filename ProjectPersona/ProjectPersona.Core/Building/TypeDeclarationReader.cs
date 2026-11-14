using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Обходит синтаксические деревья Roslyn и формирует снимки типов для построения каталога.
/// </summary>
internal static class TypeDeclarationReader
{
    /// <summary>
    /// Обрабатывает члены верхнего уровня и вложенные члены единицы компиляции или пространства имён, регистрируя найденные типы.
    /// </summary>
    /// <param name="members">Объявления членов для обхода.</param>
    /// <param name="assembly">Идентичность компиляции сборки, которой принадлежит содержащий документ.</param>
    /// <param name="namespaceName">Текущий префикс пространства имён или пустая строка для глобального пространства имён.</param>
    /// <param name="builder">Построитель каталога, принимающий разобранные снимки типов.</param>
    /// <param name="semanticModel">Семантическая модель документа или <see langword="null"/>, если компиляция недоступна.</param>
    /// <param name="documentationContext">Контекст XML-документации для компиляции проекта или <see langword="null"/>.</param>
    public static void ProcessMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        AssemblyIdentity assembly,
        string namespaceName,
        CatalogBuilder builder,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext = null)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    ProcessMembers(
                        namespaceDeclaration.Members,
                        assembly,
                        CombineNamespace(namespaceName, namespaceDeclaration.Name.ToString()),
                        builder,
                        semanticModel,
                        documentationContext);
                    break;

                case TypeDeclarationSyntax typeDeclaration:
                    builder.AddType(
                        assembly,
                        namespaceName,
                        BuildTypeSnapshot(typeDeclaration, isNested: false, semanticModel, documentationContext));
                    break;

                case DelegateDeclarationSyntax delegateDeclaration:
                    builder.AddType(
                        assembly,
                        namespaceName,
                        BuildDelegateSnapshot(delegateDeclaration, isNested: false, semanticModel, documentationContext));
                    break;

                case EnumDeclarationSyntax enumDeclaration:
                    builder.AddType(
                        assembly,
                        namespaceName,
                        BuildEnumSnapshot(enumDeclaration, isNested: false, semanticModel, documentationContext));
                    break;
            }
        }
    }

    private static TypeSnapshot BuildTypeSnapshot(
        TypeDeclarationSyntax typeDeclaration,
        bool isNested,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext)
    {
        var isInterface = typeDeclaration is InterfaceDeclarationSyntax;
        var typeContext = isNested ? AccessibilityContext.NestedType : AccessibilityContext.TopLevelType;
        var memberContext = isInterface ? AccessibilityContext.InterfaceMember : AccessibilityContext.ClassMember;
        var modifiers = typeDeclaration.Modifiers;
        var declaredSymbol = GetDeclaredNamedType(typeDeclaration, semanticModel);

        var nestedTypes = typeDeclaration.Members
            .Select(member => TryBuildNestedTypeSnapshot(member, semanticModel, documentationContext))
            .Where(static type => type is not null)
            .Cast<TypeSnapshot>()
            .ToList();

        return new TypeSnapshot(
            typeDeclaration.Identifier.Text,
            GetTypeKind(typeDeclaration),
            AccessibilityResolver.GetAccessibility(modifiers, typeContext),
            AccessibilityResolver.HasExplicitAccessibilityModifier(modifiers),
            XmlDocumentationReader.GetXmlDocumentation(typeDeclaration, declaredSymbol, documentationContext),
            null,
            AttributeListReader.Read(typeDeclaration.AttributeLists, semanticModel),
            AttributeListReader.ReadBaseTypes(typeDeclaration.BaseList, declaredSymbol, semanticModel),
            ReadTypeParameters(
                typeDeclaration.TypeParameterList,
                typeDeclaration.ConstraintClauses,
                declaredSymbol,
                semanticModel),
            ReadTypeModifiers(modifiers, declaredSymbol, isInterface),
            BuildMembers(typeDeclaration.Members, memberContext, semanticModel, documentationContext),
            nestedTypes);
    }

    private static TypeSnapshot BuildDelegateSnapshot(
        DelegateDeclarationSyntax delegateDeclaration,
        bool isNested,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext)
    {
        var typeContext = isNested ? AccessibilityContext.NestedType : AccessibilityContext.TopLevelType;
        var modifiers = delegateDeclaration.Modifiers;
        var declaredSymbol = GetDeclaredNamedType(delegateDeclaration, semanticModel);

        return new TypeSnapshot(
            delegateDeclaration.Identifier.Text,
            CodeTypeKind.Delegate,
            AccessibilityResolver.GetAccessibility(modifiers, typeContext),
            AccessibilityResolver.HasExplicitAccessibilityModifier(modifiers),
            XmlDocumentationReader.GetXmlDocumentation(delegateDeclaration, declaredSymbol, documentationContext),
            BuildSignature(delegateDeclaration.ReturnType, delegateDeclaration.ParameterList, semanticModel),
            AttributeListReader.Read(delegateDeclaration.AttributeLists, semanticModel),
            [],
            ReadTypeParameters(
                delegateDeclaration.TypeParameterList,
                delegateDeclaration.ConstraintClauses,
                declaredSymbol,
                semanticModel),
            ReadTypeModifiers(modifiers, declaredSymbol, isInterface: false),
            [],
            []);
    }

    private static TypeSnapshot BuildEnumSnapshot(
        EnumDeclarationSyntax enumDeclaration,
        bool isNested,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext)
    {
        var typeContext = isNested ? AccessibilityContext.NestedType : AccessibilityContext.TopLevelType;
        var modifiers = enumDeclaration.Modifiers;
        var declaredSymbol = GetDeclaredNamedType(enumDeclaration, semanticModel);

        return new TypeSnapshot(
            enumDeclaration.Identifier.Text,
            CodeTypeKind.Enum,
            AccessibilityResolver.GetAccessibility(modifiers, typeContext),
            AccessibilityResolver.HasExplicitAccessibilityModifier(modifiers),
            XmlDocumentationReader.GetXmlDocumentation(enumDeclaration, declaredSymbol, documentationContext),
            null,
            AttributeListReader.Read(enumDeclaration.AttributeLists, semanticModel),
            AttributeListReader.ReadBaseTypes(enumDeclaration.BaseList, declaredSymbol, semanticModel),
            [],
            ReadTypeModifiers(modifiers, declaredSymbol, isInterface: false),
            BuildEnumMembers(enumDeclaration.Members, semanticModel, documentationContext),
            []);
    }

    private static TypeSnapshot? TryBuildNestedTypeSnapshot(
        MemberDeclarationSyntax member,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext) =>
        member switch
        {
            TypeDeclarationSyntax typeDeclaration => BuildTypeSnapshot(typeDeclaration, isNested: true, semanticModel, documentationContext),
            DelegateDeclarationSyntax delegateDeclaration => BuildDelegateSnapshot(delegateDeclaration, isNested: true, semanticModel, documentationContext),
            EnumDeclarationSyntax enumDeclaration => BuildEnumSnapshot(enumDeclaration, isNested: true, semanticModel, documentationContext),
            _ => null
        };

    private static CodeTypeKind GetTypeKind(TypeDeclarationSyntax typeDeclaration) =>
        typeDeclaration switch
        {
            ClassDeclarationSyntax => CodeTypeKind.Class,
            StructDeclarationSyntax => CodeTypeKind.Struct,
            InterfaceDeclarationSyntax => CodeTypeKind.Interface,
            RecordDeclarationSyntax record => record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                ? CodeTypeKind.RecordStruct
                : CodeTypeKind.Record,
            _ => CodeTypeKind.Class
        };

    private static IReadOnlyList<DeclaredMember> BuildMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        AccessibilityContext memberContext,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext) =>
        members
            .SelectMany(member => BuildDeclaredMembers(member, memberContext, semanticModel, documentationContext))
            .ToList();

    private static IEnumerable<DeclaredMember> BuildDeclaredMembers(
        MemberDeclarationSyntax member,
        AccessibilityContext memberContext,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
                yield return CreateMember(
                    method.Identifier.Text,
                    MemberKind.Method,
                    AccessibilityResolver.GetAccessibility(method.Modifiers, memberContext),
                    method,
                    BuildSignature(method.ReturnType, method.ParameterList, semanticModel),
                    method.AttributeLists,
                    method.Modifiers,
                    semanticModel,
                    documentationContext,
                    explicitInterface: ReadExplicitInterface(method.ExplicitInterfaceSpecifier, semanticModel));
                break;

            case ConstructorDeclarationSyntax constructor:
                yield return CreateMember(
                    constructor.Identifier.Text,
                    MemberKind.Constructor,
                    AccessibilityResolver.GetAccessibility(constructor.Modifiers, memberContext),
                    constructor,
                    BuildSignature(null, constructor.ParameterList, semanticModel),
                    constructor.AttributeLists,
                    constructor.Modifiers,
                    semanticModel,
                    documentationContext);
                break;

            case OperatorDeclarationSyntax operatorDeclaration:
                yield return CreateMember(
                    $"operator {operatorDeclaration.OperatorToken.Text}",
                    MemberKind.Operator,
                    AccessibilityResolver.GetAccessibility(operatorDeclaration.Modifiers, memberContext),
                    operatorDeclaration,
                    BuildSignature(operatorDeclaration.ReturnType, operatorDeclaration.ParameterList, semanticModel),
                    operatorDeclaration.AttributeLists,
                    operatorDeclaration.Modifiers,
                    semanticModel,
                    documentationContext,
                    explicitInterface: ReadExplicitInterface(operatorDeclaration.ExplicitInterfaceSpecifier, semanticModel));
                break;

            case ConversionOperatorDeclarationSyntax conversionOperator:
            {
                var convertedType = TypeRefResolver.FromSyntax(conversionOperator.Type, semanticModel);
                yield return CreateMember(
                    $"{conversionOperator.ImplicitOrExplicitKeyword.Text} operator {convertedType.Display}",
                    MemberKind.Operator,
                    AccessibilityResolver.GetAccessibility(conversionOperator.Modifiers, memberContext),
                    conversionOperator,
                    BuildSignature(conversionOperator.Type, conversionOperator.ParameterList, semanticModel),
                    conversionOperator.AttributeLists,
                    conversionOperator.Modifiers,
                    semanticModel,
                    documentationContext,
                    explicitInterface: ReadExplicitInterface(conversionOperator.ExplicitInterfaceSpecifier, semanticModel));
                break;
            }

            case PropertyDeclarationSyntax property:
            {
                var propertyAccessibility = AccessibilityResolver.GetAccessibility(property.Modifiers, memberContext);
                yield return CreateMember(
                    property.Identifier.Text,
                    MemberKind.Property,
                    propertyAccessibility,
                    property,
                    BuildSignature(property.Type, null, semanticModel),
                    property.AttributeLists,
                    property.Modifiers,
                    semanticModel,
                    documentationContext,
                    AccessorListReader.Read(property, propertyAccessibility),
                    ReadExplicitInterface(property.ExplicitInterfaceSpecifier, semanticModel));
                break;
            }

            case IndexerDeclarationSyntax indexer:
            {
                var indexerAccessibility = AccessibilityResolver.GetAccessibility(indexer.Modifiers, memberContext);
                yield return CreateMember(
                    "this",
                    MemberKind.Indexer,
                    indexerAccessibility,
                    indexer,
                    BuildSignature(indexer.Type, indexer.ParameterList, semanticModel),
                    indexer.AttributeLists,
                    indexer.Modifiers,
                    semanticModel,
                    documentationContext,
                    AccessorListReader.Read(indexer, indexerAccessibility),
                    ReadExplicitInterface(indexer.ExplicitInterfaceSpecifier, semanticModel));
                break;
            }

            case EventDeclarationSyntax eventDeclaration:
                yield return CreateMember(
                    eventDeclaration.Identifier.Text,
                    MemberKind.Event,
                    AccessibilityResolver.GetAccessibility(eventDeclaration.Modifiers, memberContext),
                    eventDeclaration,
                    BuildSignature(eventDeclaration.Type, null, semanticModel),
                    eventDeclaration.AttributeLists,
                    eventDeclaration.Modifiers,
                    semanticModel,
                    documentationContext,
                    explicitInterface: ReadExplicitInterface(eventDeclaration.ExplicitInterfaceSpecifier, semanticModel));
                break;

            case EventFieldDeclarationSyntax eventField:
            {
                var accessibility = AccessibilityResolver.GetAccessibility(eventField.Modifiers, memberContext);
                var attributes = AttributeListReader.Read(eventField.AttributeLists, semanticModel);
                var memberModifiers = MemberModifierReader.Read(eventField.Modifiers);
                var signature = BuildSignature(eventField.Declaration.Type, null, semanticModel);
                foreach (var variable in eventField.Declaration.Variables)
                {
                    var declaredSymbol = semanticModel?.GetDeclaredSymbol(variable);
                    yield return new DeclaredMember(
                        variable.Identifier.Text,
                        MemberKind.Event,
                        accessibility,
                        XmlDocumentationReader.GetXmlDocumentation(eventField, declaredSymbol, documentationContext),
                        signature,
                        attributes,
                        memberModifiers,
                        [],
                        ExplicitInterface: null);
                }

                break;
            }

            case FieldDeclarationSyntax field:
            {
                var accessibility = AccessibilityResolver.GetAccessibility(field.Modifiers, memberContext);
                var attributes = AttributeListReader.Read(field.AttributeLists, semanticModel);
                var memberModifiers = MemberModifierReader.Read(field.Modifiers);
                var signature = BuildSignature(field.Declaration.Type, null, semanticModel);
                foreach (var variable in field.Declaration.Variables)
                {
                    var declaredSymbol = semanticModel?.GetDeclaredSymbol(variable);
                    yield return new DeclaredMember(
                        variable.Identifier.Text,
                        MemberKind.Field,
                        accessibility,
                        XmlDocumentationReader.GetXmlDocumentation(field, declaredSymbol, documentationContext),
                        signature,
                        attributes,
                        memberModifiers,
                        [],
                        ExplicitInterface: null);
                }

                break;
            }
        }
    }

    private static IReadOnlyList<DeclaredMember> BuildEnumMembers(
        SeparatedSyntaxList<EnumMemberDeclarationSyntax> members,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext) =>
        members
            .Select(member => CreateMember(
                member.Identifier.Text,
                MemberKind.EnumMember,
                AccessibilityResolver.GetAccessibility(member.Modifiers, AccessibilityContext.EnumMember),
                member,
                null,
                member.AttributeLists,
                member.Modifiers,
                semanticModel,
                documentationContext))
            .ToList();

    private static DeclaredMember CreateMember(
        string name,
        MemberKind kind,
        AccessibilityLevel accessibility,
        SyntaxNode xmlSource,
        FunctionSignature? signature,
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxTokenList modifiers,
        SemanticModel? semanticModel,
        XmlDocumentationContext? documentationContext,
        IReadOnlyList<string>? accessors = null,
        TypeRef? explicitInterface = null) =>
        new(
            name,
            kind,
            accessibility,
            XmlDocumentationReader.GetXmlDocumentation(
                xmlSource,
                semanticModel?.GetDeclaredSymbol(xmlSource),
                documentationContext),
            signature,
            AttributeListReader.Read(attributeLists, semanticModel),
            MemberModifierReader.Read(modifiers),
            accessors ?? [],
            explicitInterface);

    private static TypeRef? ReadExplicitInterface(
        ExplicitInterfaceSpecifierSyntax? specifier,
        SemanticModel? semanticModel) =>
        specifier is null ? null : TypeRefResolver.FromSyntax(specifier.Name, semanticModel);

    private static FunctionSignature BuildSignature(
        TypeSyntax? returnType,
        BaseParameterListSyntax? parameterList,
        SemanticModel? semanticModel) =>
        new(
            returnType is null ? null : TypeRefResolver.FromSyntax(returnType, semanticModel),
            parameterList?.Parameters.Select(parameter => BuildParameterInfo(parameter, semanticModel)).ToList() ?? []);

    private static ParameterInfo BuildParameterInfo(ParameterSyntax parameter, SemanticModel? semanticModel)
    {
        var modifiers = parameter.Modifiers.Count == 0
            ? (IReadOnlyList<string>)[]
            : parameter.Modifiers.Select(static modifier => modifier.Text).ToList();

        return new ParameterInfo(
            parameter.Identifier.Text,
            TypeRefResolver.FromSyntax(parameter.Type, semanticModel),
            modifiers,
            parameter.Default?.Value?.ToString().Trim());
    }

    private static IReadOnlyList<TypeParameterInfo> ReadTypeParameters(
        TypeParameterListSyntax? typeParameterList,
        SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
        INamedTypeSymbol? declaredSymbol,
        SemanticModel? semanticModel)
    {
        if (declaredSymbol is { TypeParameters.Length: > 0 })
            return declaredSymbol.TypeParameters.Select(TypeRefResolver.FromTypeParameter).ToList();

        if (typeParameterList is null || typeParameterList.Parameters.Count == 0)
            return [];

        var constraintsByName = constraintClauses
            .GroupBy(static clause => clause.Name.Identifier.Text, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);

        return typeParameterList.Parameters
            .Select(parameter =>
            {
                constraintsByName.TryGetValue(parameter.Identifier.Text, out var clause);
                return TypeRefResolver.FromSyntax(parameter, clause, semanticModel);
            })
            .ToList();
    }

    private static IReadOnlyList<string> ReadTypeModifiers(
        SyntaxTokenList modifiers,
        INamedTypeSymbol? declaredSymbol,
        bool isInterface)
    {
        if (declaredSymbol is not null)
        {
            var fromSymbol = new List<string>();
            if (declaredSymbol.IsAbstract && !isInterface && declaredSymbol.TypeKind != TypeKind.Interface)
                fromSymbol.Add("abstract");
            if (declaredSymbol.IsSealed &&
                declaredSymbol.TypeKind is not (TypeKind.Struct or TypeKind.Enum or TypeKind.Delegate))
                fromSymbol.Add("sealed");
            if (declaredSymbol.IsStatic)
                fromSymbol.Add("static");
            if (declaredSymbol.IsReadOnly)
                fromSymbol.Add("readonly");
            if (declaredSymbol.IsRefLikeType)
                fromSymbol.Add("ref");
            return fromSymbol;
        }

        return ReadTypeModifiersFromSyntax(modifiers);
    }

    private static IReadOnlyList<string> ReadTypeModifiersFromSyntax(SyntaxTokenList modifiers)
    {
        var result = new List<string>();
        foreach (var modifier in modifiers)
        {
            switch (modifier.Kind())
            {
                case SyntaxKind.AbstractKeyword:
                case SyntaxKind.SealedKeyword:
                case SyntaxKind.StaticKeyword:
                case SyntaxKind.ReadOnlyKeyword:
                case SyntaxKind.RefKeyword:
                    result.Add(modifier.Text);
                    break;
            }
        }

        return result;
    }

    private static INamedTypeSymbol? GetDeclaredNamedType(SyntaxNode node, SemanticModel? semanticModel) =>
        semanticModel?.GetDeclaredSymbol(node) as INamedTypeSymbol;

    private static string CombineNamespace(string parentNamespace, string segment) =>
        string.IsNullOrEmpty(parentNamespace) ? segment : $"{parentNamespace}.{segment}";
}
