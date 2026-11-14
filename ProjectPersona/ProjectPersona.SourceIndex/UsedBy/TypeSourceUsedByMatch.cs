namespace ProjectPersona.SourceIndex.UsedBy;

/// <summary>
/// Совпадение целевого FQN с потребителями типа целиком или выбранных членов.
/// </summary>
/// <param name="AssemblyName">Имя сборки объявления цели.</param>
/// <param name="Type">FQN целевого типа.</param>
/// <param name="TargetFramework">Короткий TFM компиляции цели или пустая строка.</param>
/// <param name="UsedBy">Потребители типа после <c>limit</c>; <see langword="null"/>, если запрошены отдельные члены.</param>
/// <param name="UsedByTotal">Полное число потребителей типа до обрезки; <c>0</c>, если запрошены отдельные члены.</param>
/// <param name="Members">Потребители запрошенных членов; <see langword="null"/>, если возвращён тип целиком.</param>
public sealed record TypeSourceUsedByMatch(
    string AssemblyName,
    string Type,
    string TargetFramework,
    IReadOnlyList<TypeSourceUsedBy>? UsedBy,
    int UsedByTotal,
    IReadOnlyList<TypeSourceUsedByMemberMatch>? Members);
