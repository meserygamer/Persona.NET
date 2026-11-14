namespace ProjectPersona.Core.Entities;

public sealed record NamespaceGroup(
    string Name,
    IReadOnlyList<DeclaredType> Types);
