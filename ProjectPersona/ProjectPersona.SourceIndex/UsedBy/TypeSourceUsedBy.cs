using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.SourceIndex.UsedBy;

/// <summary>
/// Потребитель типа или члена: объявление, в котором найдена ссылка на цель.
/// </summary>
/// <param name="AssemblyName">Имя сборки потребителя.</param>
/// <param name="TargetFramework">Короткий TFM компиляции потребителя или пустая строка.</param>
/// <param name="Type">FQN типа потребителя.</param>
/// <param name="MemberName">Имя члена потребителя в формате каталога; пустая строка, если ссылка только на уровне объявления типа.</param>
/// <param name="Signature">Сигнатура вызывающего члена в формате <see cref="TypeSourceReference.Signature"/>; пустая, если у вызывающего нет списка параметров.</param>
public sealed record TypeSourceUsedBy(
    string AssemblyName,
    string TargetFramework,
    string Type,
    string MemberName,
    string Signature = "");
