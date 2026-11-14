using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.SourceIndex.Storage;

/// <summary>
/// Полное совпадение FQN с индексированными срезами членов внутри <see cref="Full.SourceCode"/>.
/// </summary>
/// <param name="Full">Полное совпадение: объявление типа целиком.</param>
/// <param name="Members">Срезы членов: имя, смещение и длина в <see cref="TypeSourceMatch.SourceCode"/>, ссылки члена.</param>
public sealed record TypeSourceIndexedMatch(
    TypeSourceMatch Full,
    IReadOnlyList<TypeSourceMemberSlice> Members);

/// <summary>
/// Индексированный срез одного члена внутри текста объявления типа.
/// </summary>
/// <param name="Name">Имя члена в формате каталога.</param>
/// <param name="Start">Смещение фрагмента в <see cref="TypeSourceMatch.SourceCode"/> полного совпадения.</param>
/// <param name="Length">Длина фрагмента в символах.</param>
/// <param name="References">Уникальные ссылки внутри этого члена.</param>
/// <param name="Signature">Сигнатура самого члена; пустая у поля, свойства, события и константы перечисления.</param>
public sealed record TypeSourceMemberSlice(
    string Name,
    int Start,
    int Length,
    IReadOnlyList<TypeSourceReference> References,
    string Signature = "");
