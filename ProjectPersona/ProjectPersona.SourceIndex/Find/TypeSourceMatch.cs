using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.SourceIndex.Find;

/// <summary>
/// Совпадение FQN с исходным кодом типа целиком или с выбранными членами.
/// </summary>
/// <param name="AssemblyName">Имя сборки, которой принадлежит объявление.</param>
/// <param name="Type">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
/// <param name="TargetFramework">Короткий TFM компиляции или пустая строка, если таргет неизвестен.</param>
/// <param name="SourceCode">Текст объявления типа целиком; <see langword="null"/>, если запрошены отдельные члены.</param>
/// <param name="References">Уникальные ссылки внутри объявления типа; <see langword="null"/>, если запрошены отдельные члены.</param>
/// <param name="Members">Исходники запрошенных членов; <see langword="null"/>, если возвращён тип целиком.</param>
public sealed record TypeSourceMatch(
    string AssemblyName,
    string Type,
    string TargetFramework,
    string? SourceCode,
    IReadOnlyList<TypeSourceReference>? References,
    IReadOnlyList<TypeSourceMemberMatch>? Members);
