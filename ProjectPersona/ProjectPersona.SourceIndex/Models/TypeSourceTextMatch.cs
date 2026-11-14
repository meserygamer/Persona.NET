namespace ProjectPersona.SourceIndex.Models;

/// <summary>
/// Документ полнотекстового поиска, в котором найдена подстрока: объявление типа целиком или срез одного члена.
/// </summary>
/// <param name="AssemblyName">Имя сборки объявления.</param>
/// <param name="TargetFramework">Короткий TFM компиляции или пустая строка.</param>
/// <param name="Type">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
/// <param name="MemberName">Имя члена в формате каталога или <see langword="null"/>, если документ — весь тип.</param>
/// <param name="Text">Текст документа, в котором искали.</param>
/// <param name="Indexes">Позиции всех неперекрывающихся вхождений подстроки в <paramref name="Text"/> по возрастанию.</param>
public sealed record TypeSourceTextMatch(
    string AssemblyName,
    string TargetFramework,
    string Type,
    string? MemberName,
    string Text,
    IReadOnlyList<int> Indexes);
