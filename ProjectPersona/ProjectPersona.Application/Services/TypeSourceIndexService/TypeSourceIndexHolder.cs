using Microsoft.Extensions.Logging;
using ProjectPersona.SourceIndex.Contracts;

namespace ProjectPersona.Application.Services.TypeSourceIndexService;

/// <summary>
/// Ленивая обёртка над индексом исходников; открывает хранилище один раз при первом обращении.
/// </summary>
public sealed class TypeSourceIndexHolder
{
    private readonly Lazy<Task<ITypeSourceLookup>> _lazy;

    /// <summary>
    /// Создаёт holder с отложенным открытием индекса через <paramref name="factory"/>.
    /// </summary>
    /// <param name="factory">Фабрика, которая знает, где и в каком формате лежит индекс.</param>
    /// <param name="logger">Логгер прогресса и ошибок загрузки.</param>
    public TypeSourceIndexHolder(ITypeSourceLookupFactory factory, ILogger<TypeSourceIndexHolder> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _lazy = new Lazy<Task<ITypeSourceLookup>>(() => LoadIndexAsync(factory, logger));
    }

    /// <summary>
    /// Возвращает загруженный индекс, дожидаясь завершения фоновой или первой загрузки.
    /// </summary>
    /// <param name="ct">Токен отмены ожидания или загрузки индекса.</param>
    /// <returns>Поиск по сериализованному индексу исходников.</returns>
    public async Task<ITypeSourceLookup> GetAsync(CancellationToken ct) =>
        await _lazy.Value.WaitAsync(ct);

    private static async Task<ITypeSourceLookup> LoadIndexAsync(ITypeSourceLookupFactory factory, ILogger logger)
    {
        logger.LogInformation("Loading type source index: {Location}", factory.Location);

        try
        {
            var index = await factory.OpenAsync().ConfigureAwait(false);
            logger.LogInformation("Type source index is ready");
            return index;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load type source index from {Location}", factory.Location);
            throw;
        }
    }
}
