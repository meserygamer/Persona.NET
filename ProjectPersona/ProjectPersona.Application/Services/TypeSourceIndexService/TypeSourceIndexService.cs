using Microsoft.Extensions.Logging;
using ProjectPersona.Application.Services.TypeSourceIndexService.Responses;
using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.Application.Services.TypeSourceIndexService;

/// <summary>
/// Точка вызова поиска по индексу исходников для MCP tool.
/// </summary>
public class TypeSourceIndexService
{
    private readonly TypeSourceIndexHolder _indexHolder;
    private readonly ILogger<TypeSourceIndexService> _logger;

    /// <summary>
    /// Создаёт сервис поиска по индексу для MCP tool.
    /// </summary>
    /// <param name="indexHolder">Holder с ленивой загрузкой индекса с диска.</param>
    /// <param name="logger">Логгер ошибок поиска.</param>
    public TypeSourceIndexService(TypeSourceIndexHolder indexHolder, ILogger<TypeSourceIndexService> logger)
    {
        _indexHolder = indexHolder
            ?? throw new ArgumentNullException(nameof(indexHolder));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ищет фрагменты исходного кода по FQN через загруженный индекс.
    /// </summary>
    /// <param name="fqn">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
    /// <param name="assemblyName">Имя сборки для сужения или <see langword="null"/> — все сборки.</param>
    /// <param name="memberNames">Имена членов в формате каталога или <see langword="null"/> — весь тип.</param>
    /// <param name="cancellationToken">Токен отмены ожидания индекса или поиска.</param>
    /// <returns>Успешный ответ с совпадениями или сообщение об ошибке для агента.</returns>
    public async ValueTask<FindResponse> FindAsync(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fqn))
        {
            _logger.LogWarning(
                "find failed: empty fqn. Operation=find, Fqn={Fqn}, AssemblyName={AssemblyName}, MemberNames={MemberNames}",
                fqn, assemblyName, memberNames);
            return FindResponse.Fail(
                "The fqn parameter is mandatory: specify the fully qualified type name without global:: and type parameters.");
        }

        try
        {
            var index = await _indexHolder.GetAsync(cancellationToken);
            return FindResponse.Ok(index.Find(fqn, assemblyName, memberNames));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "find cancelled. Operation=find, Fqn={Fqn}, AssemblyName={AssemblyName}, MemberNames={MemberNames}",
                fqn, assemblyName, memberNames);
            return FindResponse.Fail(
                "The request was cancelled or timed out. Retry the `find` operation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "find failed. Operation=find, Fqn={Fqn}, AssemblyName={AssemblyName}, MemberNames={MemberNames}",
                fqn, assemblyName, memberNames);
            return FindResponse.Fail(FormatSearchError(ex));
        }
    }

    private static string FormatSearchError(Exception exception)
    {
        var message = exception is AggregateException aggregate
            ? aggregate.GetBaseException().Message
            : exception.Message;

        if (exception is InvalidOperationException)
        {
            if (message.Contains("--generate-type-source-index", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("type source index", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("typeSourceIndex", StringComparison.OrdinalIgnoreCase))
            {
                return $"Failed to load the source index: {message}";
            }
        }

        return $"Search error: {message}";
    }

    /// <summary>
    /// Возвращает потребителей целевого FQN через загруженный индекс.
    /// </summary>
    /// <param name="fqn">Полное имя типа без <c>global::</c> и без списка type parameters.</param>
    /// <param name="assemblyName">Имя сборки объявления цели или <see langword="null"/> — все сборки.</param>
    /// <param name="memberNames">Имена членов цели в формате каталога или <see langword="null"/> — тип целиком.</param>
    /// <param name="limit">Потолок при <c>&gt; 0</c>; при <c>&lt;= 0</c> — без обрезки.</param>
    /// <param name="cancellationToken">Токен отмены ожидания индекса или поиска.</param>
    /// <returns>Успешный ответ с потребителями или сообщение об ошибке для агента.</returns>
    public async ValueTask<UsedByResponse> FindUsedByAsync(
        string fqn,
        string? assemblyName = null,
        IReadOnlyList<string>? memberNames = null,
        int limit = TypeSourceUsedByLimit.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fqn))
        {
            _logger.LogWarning(
                "usedBy failed: empty fqn. Operation=usedBy, Fqn={Fqn}, AssemblyName={AssemblyName}, MemberNames={MemberNames}, Limit={Limit}",
                fqn, assemblyName, memberNames, limit);
            return UsedByResponse.Fail(
                "The fqn parameter is mandatory: specify the fully qualified type name without global:: and type parameters.");
        }

        try
        {
            var index = await _indexHolder.GetAsync(cancellationToken);
            return UsedByResponse.Ok(index.FindUsedBy(fqn, assemblyName, memberNames, limit));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "usedBy cancelled. Operation=usedBy, Fqn={Fqn}, AssemblyName={AssemblyName}, MemberNames={MemberNames}, Limit={Limit}",
                fqn, assemblyName, memberNames, limit);
            return UsedByResponse.Fail(
                "The request was cancelled or timed out. Retry the `usedBy` operation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "usedBy failed. Operation=usedBy, Fqn={Fqn}, AssemblyName={AssemblyName}, MemberNames={MemberNames}, Limit={Limit}",
                fqn, assemblyName, memberNames, limit);
            return UsedByResponse.Fail(FormatSearchError(ex));
        }
    }
}
