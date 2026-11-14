namespace ProjectPersona.Core.Dto;

public sealed record FunctionSignature(
    TypeRef? ReturnType,
    IReadOnlyList<ParameterInfo> Parameters);
