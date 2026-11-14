using ProjectPersona.SourceIndex.Find;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Contracts;

/// <summary>
/// Поиск исходного кода объявлений типов по FQN.
/// </summary>
public interface ITypeSourceLookup
{
    /// <summary>
    /// Возвращает фрагменты исходного кода для указанного FQN.
    /// </summary>
    /// <param name="fqn">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
    /// <param name="assemblyName">Имя сборки для сужения результата или <see langword="null"/>, чтобы вернуть совпадения из всех сборок.</param>
    /// <param name="memberNames">Имена членов в формате каталога; <see langword="null"/> или пустой список — весь тип.</param>
    /// <returns>Список совпадений; пустой, если FQN не найден, тип отсутствует в указанной сборке или ни один член не совпал.</returns>
    IReadOnlyList<TypeSourceMatch> Find(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null);

    /// <summary>
    /// Возвращает потребителей, ссылающихся на указанный FQN.
    /// </summary>
    /// <param name="fqn">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
    /// <param name="assemblyName">Имя сборки объявления цели или <see langword="null"/> — все сборки.</param>
    /// <param name="memberNames">Имена членов цели в формате каталога; <see langword="null"/> или пустой список — потребители типа.</param>
    /// <param name="limit">Строгий потолок при <c>&gt; 0</c>; при <c>&lt;= 0</c> — без обрезки; по умолчанию 100.</param>
    /// <returns>Список совпадений; пустой, если FQN не найден или ни один запрошенный член не имеет потребителей.</returns>
    IReadOnlyList<TypeSourceUsedByMatch> FindUsedBy(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null,
        int limit = TypeSourceUsedByLimit.DefaultLimit);

    /// <summary> Получить число деклараций. </summary>
    /// <param name="assemblies"> Сборки. </param>
    /// <returns> Получить число деклараций по сборкам. Если assemblies == null, то считаем общее число деклараций. </returns>
    long GetDeclarationCount(IEnumerable<(string AssemblyName, string? TargetFramework)>? assemblies);

    /// <summary>
    /// Перечисляет все объявления типов в индексе без материализации ссылок.
    /// </summary>
    /// <returns>Объявления по всем FQN, сборкам и TFM.</returns>
    IEnumerable<TypeSourceDeclaration> EnumerateDeclarations();

    /// <summary>
    /// Перечисляет объявления типов указанных сборок без материализации ссылок.
    /// </summary>
    /// <param name="keys">Пары «имя сборки – короткий TFM» (например <c>net472</c>). Если TFM <see langword="null"/> или пустой, возвращаются объявления сборки для всех TFM.</param>
    /// <returns>Объявления указанных сборок по всем FQN; пустая последовательность, если ни одна сборка не найдена или пары не переданы.</returns>
    /// <exception cref="ArgumentNullException">Коллекция равна null.</exception>
    /// <exception cref="ArgumentException">Имя сборки пустое либо состоит из пробелов.</exception>
    IEnumerable<TypeSourceDeclaration> EnumerateDeclarations(IEnumerable<(string AssemblyName, string? TargetFramework)> keys);
}
