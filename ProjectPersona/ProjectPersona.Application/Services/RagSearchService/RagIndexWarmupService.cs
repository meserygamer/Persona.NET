using Microsoft.Extensions.Hosting;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Запускает проверку векторной базы при старте хоста, чтобы первый поиск не ждал загрузки модели. </summary>
/// <remarks> Результат проверки не ожидается: старт хоста не блокируется, а ошибки попадают в состояние holder. </remarks>
public sealed class RagIndexWarmupService : IHostedService
{
    /// <summary> Holder состояния векторной базы. </summary>
    private readonly RagIndexStateHolder _stateHolder;

    /// <summary> Создаёт сервис прогрева. </summary>
    /// <param name="stateHolder"> Holder состояния векторной базы. </param>
    /// <exception cref="ArgumentNullException"> <paramref name="stateHolder"/> равен <see langword="null"/>. </exception>
    public RagIndexWarmupService(RagIndexStateHolder stateHolder)
    {
        _stateHolder = stateHolder ?? throw new ArgumentNullException(nameof(stateHolder));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _stateHolder.GetAsync(CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
