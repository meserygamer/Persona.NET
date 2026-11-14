namespace ProjectPersona.Core.Dto;

public sealed record ParameterInfo(
    string Name,
    TypeRef Type,
    IReadOnlyList<string> Modifiers,
    string? DefaultValue);
