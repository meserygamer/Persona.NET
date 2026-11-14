using ProjectPersona.SourceIndex.Models;

namespace ProjectPersona.SourceIndex.Contracts;

/// <summary>
/// Полнотекстовый поиск точной подстроки по исходникам объявлений и их членов.
/// </summary>
public interface ITypeSourceTextSearch
{
    /// <summary>
    /// Ищет подстроку в тексте объявлений типов и срезов членов.
    /// </summary>
    /// <param name="query">Искомая подстрока; не короче трёх символов (длина n-граммы хранилища).</param>
    /// <param name="assemblyName">Имя сборки для сужения или <see langword="null"/> — все сборки.</param>
    /// <param name="ignoreCase"><see langword="true"/> — сравнение без учёта регистра.</param>
    /// <param name="cancellationToken">Токен отмены поиска.</param>
    /// <returns>Документы (тип целиком или член), содержащие подстроку; пустой список, если совпадений нет.</returns>
    IReadOnlyList<TypeSourceTextMatch> Search(
        string query,
        string? assemblyName = null,
        bool ignoreCase = false,
        CancellationToken cancellationToken = default);
}
