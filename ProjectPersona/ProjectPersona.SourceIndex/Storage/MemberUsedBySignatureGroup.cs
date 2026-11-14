using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Storage;

/// <summary>
/// Потребители одной перегрузки члена в индексе <c>member_used_by</c>.
/// </summary>
/// <param name="Signature">Сигнатура перегрузки; пустая — группа без разреза по параметрам.</param>
/// <param name="UsedBy">Уникальные потребители.</param>
public sealed record MemberUsedBySignatureGroup(
    string Signature,
    IReadOnlyList<TypeSourceUsedBy> UsedBy);
