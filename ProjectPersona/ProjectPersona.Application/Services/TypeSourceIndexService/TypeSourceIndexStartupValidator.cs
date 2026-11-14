using Microsoft.Extensions.Hosting;

namespace ProjectPersona.Application.Services.TypeSourceIndexService;

/// <summary>
/// Проверяет индекс исходников при старте хоста; при ошибке запуск сервера прерывается.
/// </summary>
public sealed class TypeSourceIndexStartupValidator : IHostedService
{
    private readonly TypeSourceIndexHolder _indexHolder;

    /// <summary>
    /// Создаёт валидатор индекса исходников.
    /// </summary>
    /// <param name="indexHolder">Holder, открывающий и проверяющий файл индекса.</param>
    public TypeSourceIndexStartupValidator(TypeSourceIndexHolder indexHolder)
    {
        _indexHolder = indexHolder
            ?? throw new ArgumentNullException(nameof(indexHolder));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Файл индекса отсутствует, повреждён, имеет другую версию формата или в нём нет обязательных таблиц.</exception>
    public async Task StartAsync(CancellationToken cancellationToken) =>
        await _indexHolder.GetAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
