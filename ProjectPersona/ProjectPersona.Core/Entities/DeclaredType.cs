using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Entities;
 
public sealed record DeclaredType(
    string Name,
    CodeTypeKind Kind,
    AccessibilityLevel Accessibility,
    string? XmlDocumentation,
    FunctionSignature? Signature,
    IReadOnlyList<AttributeInfo> Attributes,
    IReadOnlyList<BaseTypeRef> BaseTypes,
    IReadOnlyList<TypeParameterInfo> TypeParameters,
    IReadOnlyList<string> Modifiers,
    IReadOnlyList<DeclaredMember> Members,
    IReadOnlyList<DeclaredType> NestedTypes);
