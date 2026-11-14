namespace ProjectPersona.Core.Dto;

/// <summary>
/// Снимок объявленного члена типа: вид, доступ, сигнатура, XML-документация и явная реализация интерфейса.
/// </summary>
/// <param name="Name">Идентификатор члена в исходнике (<c>GetEnumerator</c>, <c>this</c>, <c>operator +</c>).</param>
/// <param name="Kind">Вид члена.</param>
/// <param name="Accessibility">
/// Эффективный уровень доступа. У явной реализации интерфейса — <see cref="AccessibilityLevel.Private"/>,
/// в markdown модификатор доступа не пишется.
/// </param>
/// <param name="XmlDocumentation">Нормализованная XML-документация или <see langword="null"/>.</param>
/// <param name="Signature">Сигнатура метода, свойства или поля; <see langword="null"/> у члена перечисления.</param>
/// <param name="Attributes">Атрибуты члена.</param>
/// <param name="Modifiers">Модификаторы без accessibility.</param>
/// <param name="Accessors">Компактные аксессоры свойства или индексера.</param>
/// <param name="ExplicitInterface">Интерфейс явной реализации или <see langword="null"/>.</param>
public sealed record DeclaredMember(
    string Name,
    MemberKind Kind,
    AccessibilityLevel Accessibility,
    string? XmlDocumentation,
    FunctionSignature? Signature,
    IReadOnlyList<AttributeInfo> Attributes,
    IReadOnlyList<string> Modifiers,
    IReadOnlyList<string> Accessors,
    TypeRef? ExplicitInterface);
