namespace ProjectPersona.Core.Dto;

public sealed record TypeParameterInfo(
    string Name,
    string? Variance,
    IReadOnlyList<TypeRef> Constraints,
    bool HasReferenceTypeConstraint,
    bool HasValueTypeConstraint,
    bool HasUnmanagedTypeConstraint,
    bool HasNotNullConstraint,
    bool HasConstructorConstraint,
    string Display);
