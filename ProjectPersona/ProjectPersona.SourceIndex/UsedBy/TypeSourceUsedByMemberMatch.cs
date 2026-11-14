using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.SourceIndex.UsedBy;

/// <summary>
/// Потребители одного члена целевого типа (одна перегрузка при непустой сигнатуре).
/// </summary>
/// <param name="Name">Имя члена цели в формате каталога.</param>
/// <param name="UsedBy">Потребители после применения <c>limit</c>.</param>
/// <param name="UsedByTotal">Полное число потребителей до обрезки.</param>
/// <param name="Signature">Сигнатура перегрузки как в <see cref="TypeSourceReference"/>; пустая — единственная группа без разреза.</param>
public sealed record TypeSourceUsedByMemberMatch(
    string Name,
    IReadOnlyList<TypeSourceUsedBy> UsedBy,
    int UsedByTotal,
    string Signature = "");
