using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.SourceIndex.Find;

/// <summary>
/// Исходный код одного члена типа и ссылки внутри его объявления.
/// </summary>
/// <param name="Name">Имя члена в формате каталога.</param>
/// <param name="SourceCode">Текст объявления члена из исходника, включая leading trivia.</param>
/// <param name="References">Уникальные ссылки на типы и члены внутри этого объявления.</param>
public sealed record TypeSourceMemberMatch(
    string Name,
    string SourceCode,
    IReadOnlyList<TypeSourceReference> References);
