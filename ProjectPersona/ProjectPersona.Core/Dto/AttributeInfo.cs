namespace ProjectPersona.Core.Dto;

public sealed record AttributeInfo(
    TypeRef Type,
    string? Target,
    string ArgumentsDisplay)
{
    public string Display =>
        string.IsNullOrEmpty(Target)
            ? Type.Display + ArgumentsDisplay
            : $"{Target}: {Type.Display}{ArgumentsDisplay}";
}
