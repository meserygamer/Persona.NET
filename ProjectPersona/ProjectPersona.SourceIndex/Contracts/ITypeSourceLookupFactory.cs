namespace ProjectPersona.SourceIndex.Contracts;

/// <summary>
/// Открывает ранее сохранённый индекс исходников для поиска. Расположение задаёт реализация.
/// </summary>
public interface ITypeSourceLookupFactory
{
    /// <summary>
    /// Человекочитаемое расположение индекса для логов.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// Открывает индекс из хранилища.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Поиск по сохранённому индексу.</returns>
    /// <exception cref="InvalidOperationException">Индекс отсутствует, повреждён или имеет неподдерживаемую версию формата.</exception>
    Task<ITypeSourceLookup> OpenAsync(CancellationToken cancellationToken = default);
}
