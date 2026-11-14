namespace ProjectPersona.SourceIndex.Models;

/// <summary>
/// Объявление типа для обхода индекса без ссылок <see cref="TypeSourceReference"/>.
/// </summary>
/// <param name="AssemblyName">Имя сборки объявления.</param>
/// <param name="TargetFramework">Короткий TFM компиляции или пустая строка.</param>
/// <param name="Type">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
/// <param name="SourceCode">Текст объявления типа целиком.</param>
/// <param name="Members">Срезы членов внутри <paramref name="SourceCode"/>.</param>
public sealed record TypeSourceDeclaration(
    string AssemblyName,
    string TargetFramework,
    string Type,
    string SourceCode,
    IReadOnlyList<TypeSourceMemberSpan> Members);
