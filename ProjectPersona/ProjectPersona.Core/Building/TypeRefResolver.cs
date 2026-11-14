using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Строит <see cref="TypeRef"/> из синтаксиса и символов Roslyn: FQN при успешном резолве, иначе текст исходника.
/// </summary>
internal static class TypeRefResolver
{
    public static TypeRef FromSyntax(TypeSyntax? typeSyntax, SemanticModel? semanticModel)
    {
        if (typeSyntax is null)
            return TypeRef.Unresolved("var");

        var syntaxText = typeSyntax.ToString().Trim();
        if (semanticModel is null)
            return TypeRef.Unresolved(syntaxText);

        var symbol = semanticModel.GetTypeInfo(typeSyntax).Type;
        if (symbol is null || symbol.TypeKind == TypeKind.Error)
            return TypeRef.Unresolved(syntaxText);

        return FromSymbol(symbol);
    }

    public static TypeRef FromSymbol(ITypeSymbol symbol)
    {
        var isNullableAnnotation = symbol.NullableAnnotation == NullableAnnotation.Annotated
            && symbol.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

        switch (symbol)
        {
            case ITypeParameterSymbol typeParameter:
                return CreateTypeParameter(typeParameter.Name, isNullableAnnotation);

            case IArrayTypeSymbol array:
            {
                var element = FromSymbol(array.ElementType);
                var brackets = array.Rank == 1
                    ? "[]"
                    : "[" + new string(',', array.Rank - 1) + "]";
                var display = AppendNullable(element.Display + brackets, isNullableAnnotation);
                return new TypeRef(
                    TypeRefKind.Array,
                    element.Name,
                    element.Namespace,
                    [],
                    element,
                    array.Rank,
                    isNullableAnnotation,
                    display);
            }

            case IPointerTypeSymbol pointer:
            {
                var pointed = FromSymbol(pointer.PointedAtType);
                return new TypeRef(
                    TypeRefKind.Pointer,
                    pointed.Name,
                    pointed.Namespace,
                    [],
                    pointed,
                    ArrayRank: 0,
                    IsNullableAnnotation: false,
                    pointed.Display + "*");
            }

            case INamedTypeSymbol named:
                return FromNamedType(named, isNullableAnnotation);
        }

        if (symbol.TypeKind == TypeKind.Dynamic)
            return CreateDynamic(isNullableAnnotation);

        var fallbackDisplay = AppendNullable(symbol.ToDisplayString(FallbackFormat), isNullableAnnotation);
        return new TypeRef(
            TypeRefKind.Named,
            symbol.Name,
            GetNamespace(symbol.ContainingNamespace),
            [],
            ElementType: null,
            ArrayRank: 0,
            isNullableAnnotation,
            fallbackDisplay);
    }

    public static AttributeInfo FromAttribute(
        AttributeSyntax attribute,
        SemanticModel? semanticModel,
        string? target)
    {
        var argumentsDisplay = attribute.ArgumentList?.ToString().Trim() ?? string.Empty;
        var type = ResolveAttributeType(attribute, semanticModel);
        return new AttributeInfo(type, target, argumentsDisplay);
    }

    public static TypeParameterInfo FromTypeParameter(ITypeParameterSymbol typeParameter)
    {
        var constraints = typeParameter.ConstraintTypes
            .Select(FromSymbol)
            .ToList();

        return new TypeParameterInfo(
            typeParameter.Name,
            FormatVariance(typeParameter.Variance),
            constraints,
            typeParameter.HasReferenceTypeConstraint,
            typeParameter.HasValueTypeConstraint,
            typeParameter.HasUnmanagedTypeConstraint,
            typeParameter.HasNotNullConstraint,
            typeParameter.HasConstructorConstraint,
            FormatTypeParameterDisplay(
                typeParameter.Name,
                FormatVariance(typeParameter.Variance),
                constraints,
                typeParameter.HasReferenceTypeConstraint,
                typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated,
                typeParameter.HasValueTypeConstraint,
                typeParameter.HasUnmanagedTypeConstraint,
                typeParameter.HasNotNullConstraint,
                typeParameter.HasConstructorConstraint));
    }

    public static TypeParameterInfo FromSyntax(
        TypeParameterSyntax typeParameter,
        TypeParameterConstraintClauseSyntax? constraintClause,
        SemanticModel? semanticModel)
    {
        var name = typeParameter.Identifier.Text;
        var variance = typeParameter.VarianceKeyword.IsKind(SyntaxKind.None)
            ? null
            : typeParameter.VarianceKeyword.Text;

        var constraints = new List<TypeRef>();
        var hasReferenceTypeConstraint = false;
        var referenceNullable = false;
        var hasValueTypeConstraint = false;
        var hasUnmanagedTypeConstraint = false;
        var hasNotNullConstraint = false;
        var hasConstructorConstraint = false;

        if (constraintClause is not null)
        {
            foreach (var constraint in constraintClause.Constraints)
            {
                switch (constraint)
                {
                    case ClassOrStructConstraintSyntax classOrStruct
                        when classOrStruct.ClassOrStructKeyword.IsKind(SyntaxKind.ClassKeyword):
                        hasReferenceTypeConstraint = true;
                        referenceNullable = classOrStruct.QuestionToken.IsKind(SyntaxKind.QuestionToken);
                        break;
                    case ClassOrStructConstraintSyntax classOrStruct
                        when classOrStruct.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword):
                        hasValueTypeConstraint = true;
                        break;
                    case ClassOrStructConstraintSyntax classOrStruct
                        when classOrStruct.ClassOrStructKeyword.IsKind(SyntaxKind.UnmanagedKeyword):
                        hasUnmanagedTypeConstraint = true;
                        break;
                    case TypeConstraintSyntax typeConstraint
                        when typeConstraint.Type.ToString() == "unmanaged":
                        hasUnmanagedTypeConstraint = true;
                        break;
                    case TypeConstraintSyntax typeConstraint
                        when typeConstraint.Type.ToString() == "notnull":
                        hasNotNullConstraint = true;
                        break;
                    case TypeConstraintSyntax typeConstraint:
                        constraints.Add(FromSyntax(typeConstraint.Type, semanticModel));
                        break;
                    case ConstructorConstraintSyntax:
                        hasConstructorConstraint = true;
                        break;
                }
            }
        }

        return new TypeParameterInfo(
            name,
            variance,
            constraints,
            hasReferenceTypeConstraint,
            hasValueTypeConstraint,
            hasUnmanagedTypeConstraint,
            hasNotNullConstraint,
            hasConstructorConstraint,
            FormatTypeParameterDisplay(
                name,
                variance,
                constraints,
                hasReferenceTypeConstraint,
                referenceNullable,
                hasValueTypeConstraint,
                hasUnmanagedTypeConstraint,
                hasNotNullConstraint,
                hasConstructorConstraint));
    }

    private static TypeRef FromNamedType(INamedTypeSymbol named, bool isNullableAnnotation)
    {
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments.Length == 1)
        {
            var inner = FromSymbol(named.TypeArguments[0]);
            return new TypeRef(
                TypeRefKind.NullableValue,
                inner.Name,
                inner.Namespace,
                [],
                inner,
                ArrayRank: 0,
                IsNullableAnnotation: false,
                inner.Display + "?");
        }

        if (named.IsTupleType)
        {
            var elements = named.TupleElements.Select(static field => FromSymbol(field.Type)).ToList();
            var tupleDisplay = AppendNullable(
                "(" + string.Join(", ", elements.Select(static element => element.Display)) + ")",
                isNullableAnnotation);
            return new TypeRef(
                TypeRefKind.Tuple,
                named.Name,
                GetNamespace(named.ContainingNamespace),
                elements,
                ElementType: null,
                ArrayRank: 0,
                isNullableAnnotation,
                tupleDisplay);
        }

        if (TryGetKeyword(named, out var keyword))
            return TypeRef.Keyword(keyword, isNullableAnnotation);

        var typeArguments = named.TypeArguments.Select(FromSymbol).ToList();
        var ns = named.ContainingType is null ? GetNamespace(named.ContainingNamespace) : null;
        var name = GetNestedName(named);
        var namedDisplay = BuildNamedDisplay(named, typeArguments, isNullableAnnotation);

        return new TypeRef(
            TypeRefKind.Named,
            name,
            ns ?? GetNamespace(GetTopLevelContainingType(named).ContainingNamespace),
            typeArguments,
            ElementType: null,
            ArrayRank: 0,
            isNullableAnnotation,
            namedDisplay);
    }

    private static string BuildNamedDisplay(
        INamedTypeSymbol named,
        IReadOnlyList<TypeRef> typeArguments,
        bool isNullableAnnotation)
    {
        var builder = new StringBuilder();

        if (named.ContainingType is { } containing)
        {
            var containingDisplay = FromSymbol(containing.WithNullableAnnotation(NullableAnnotation.NotAnnotated))
                .Display
                .TrimEnd('?');
            builder.Append(containingDisplay);
            builder.Append('.');
        }
        else
        {
            var ns = GetNamespace(named.ContainingNamespace);
            if (!string.IsNullOrEmpty(ns))
            {
                builder.Append(ns);
                builder.Append('.');
            }
        }

        builder.Append(named.Name);

        if (named.Arity > 0 && typeArguments.Count > 0)
        {
            builder.Append('<');
            builder.Append(string.Join(", ", typeArguments.Select(static argument => argument.Display)));
            builder.Append('>');
        }

        return AppendNullable(builder.ToString(), isNullableAnnotation);
    }

    private static TypeRef ResolveAttributeType(AttributeSyntax attribute, SemanticModel? semanticModel)
    {
        var syntaxName = attribute.Name.ToString().Trim();
        if (semanticModel is null)
            return TypeRef.Unresolved(syntaxName);

        var symbolInfo = semanticModel.GetSymbolInfo(attribute);
        var constructor = symbolInfo.Symbol as IMethodSymbol
            ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (constructor?.ContainingType is { TypeKind: not TypeKind.Error } attributeType)
            return FromSymbol(attributeType);

        if (attribute.Name is TypeSyntax typeSyntax)
        {
            var type = semanticModel.GetTypeInfo(typeSyntax).Type;
            if (type is not null && type.TypeKind != TypeKind.Error)
                return FromSymbol(type);
        }

        return TypeRef.Unresolved(syntaxName);
    }

    private static TypeRef CreateTypeParameter(string name, bool isNullableAnnotation) =>
        new(
            TypeRefKind.TypeParameter,
            name,
            Namespace: null,
            TypeArguments: [],
            ElementType: null,
            ArrayRank: 0,
            isNullableAnnotation,
            AppendNullable(name, isNullableAnnotation));

    private static TypeRef CreateDynamic(bool isNullableAnnotation) =>
        new(
            TypeRefKind.Dynamic,
            "dynamic",
            Namespace: null,
            TypeArguments: [],
            ElementType: null,
            ArrayRank: 0,
            isNullableAnnotation,
            AppendNullable("dynamic", isNullableAnnotation));

    private static bool TryGetKeyword(INamedTypeSymbol named, out string keyword)
    {
        if (named.IsNativeIntegerType)
        {
            keyword = named.SpecialType == SpecialType.System_UIntPtr ? "nuint" : "nint";
            return true;
        }

        keyword = named.SpecialType switch
        {
            SpecialType.System_Void => "void",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Char => "char",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_IntPtr => "nint",
            SpecialType.System_UIntPtr => "nuint",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_String => "string",
            SpecialType.System_Object => "object",
            _ => string.Empty
        };

        return keyword.Length > 0;
    }

    private static string GetNestedName(INamedTypeSymbol named)
    {
        if (named.ContainingType is null)
            return named.Name;

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

    private static string? FormatVariance(VarianceKind variance) =>
        variance switch
        {
            VarianceKind.In => "in",
            VarianceKind.Out => "out",
            _ => null
        };

    internal static string FormatTypeParameterDisplay(
        string name,
        string? variance,
        IReadOnlyList<TypeRef> constraints,
        bool hasReferenceTypeConstraint,
        bool referenceTypeConstraintNullable,
        bool hasValueTypeConstraint,
        bool hasUnmanagedTypeConstraint,
        bool hasNotNullConstraint,
        bool hasConstructorConstraint)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(variance))
        {
            builder.Append(variance);
            builder.Append(' ');
        }

        builder.Append(name);

        var parts = new List<string>();
        if (hasUnmanagedTypeConstraint)
            parts.Add("unmanaged");
        else if (hasValueTypeConstraint)
            parts.Add("struct");
        else if (hasReferenceTypeConstraint)
            parts.Add(referenceTypeConstraintNullable ? "class?" : "class");

        if (hasNotNullConstraint)
            parts.Add("notnull");

        foreach (var constraint in constraints)
            parts.Add(constraint.Display);

        if (hasConstructorConstraint)
            parts.Add("new()");

        if (parts.Count == 0)
            return builder.ToString();

        builder.Append(" : ");
        builder.Append(string.Join(", ", parts));
        return builder.ToString();
    }

    private static string AppendNullable(string display, bool isNullableAnnotation) =>
        isNullableAnnotation && !display.EndsWith('?') ? display + "?" : display;

    private static readonly SymbolDisplayFormat FallbackFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}
