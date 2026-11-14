using ProjectPersona.SourceIndex.Contracts;

namespace ProjectPersona.SourceIndex.Storage;

/// <summary>
/// Сколько элементов хранилище записало при <see cref="ITypeSourceIndexWriter.WriteAsync"/>.
/// </summary>
/// <param name="Assemblies">Компиляции сборок (имя и TFM).</param>
/// <param name="Declarations">Объявления типов.</param>
/// <param name="MemberSlices">Срезы членов.</param>
/// <param name="References">Ссылки типов и членов.</param>
/// <param name="UsedBy">Рёбра «потребитель → цель».</param>
public sealed record TypeSourceIndexWriteResult(
    int Assemblies,
    int Declarations,
    int MemberSlices,
    int References,
    int UsedBy);
