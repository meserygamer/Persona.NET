namespace ProjectPersona.Core.Dto;

public sealed record TypeRef(
    TypeRefKind Kind,
    string Name,
    string? Namespace,
    IReadOnlyList<TypeRef> TypeArguments,
    TypeRef? ElementType,
    int ArrayRank,
    bool IsNullableAnnotation,
    string Display)
{
    public static TypeRef Unresolved(string syntaxText)
    {
        var display = string.IsNullOrWhiteSpace(syntaxText) ? "var" : syntaxText.Trim();
        return new(
            TypeRefKind.Unresolved,
            display,
            Namespace: null,
            TypeArguments: [],
            ElementType: null,
            ArrayRank: 0,
            IsNullableAnnotation: false,
            display);
    }

    public static TypeRef Keyword(string keyword, bool isNullableAnnotation = false)
    {
        var display = isNullableAnnotation ? keyword + "?" : keyword;
        return new(
            TypeRefKind.Keyword,
            keyword,
            Namespace: null,
            TypeArguments: [],
            ElementType: null,
            ArrayRank: 0,
            isNullableAnnotation,
            display);
    }
}
