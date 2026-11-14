using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Storage;

/// <summary>
/// Потребители одного целевого FQN: ссылки на тип и по именам членов.
/// </summary>
/// <param name="TypeUsedBy">Потребители со ссылкой на тип (<see cref="TypeSourceReferenceKind.Type"/>).</param>
/// <param name="MemberUsedBy">Потребители по ключу хранения (имя символа или каталожное имя) и сигнатуре перегрузки.</param>
public sealed record UsedByFqnEntry(
    IReadOnlyList<TypeSourceUsedBy> TypeUsedBy,
    IReadOnlyDictionary<string, IReadOnlyList<MemberUsedBySignatureGroup>> MemberUsedBy);
