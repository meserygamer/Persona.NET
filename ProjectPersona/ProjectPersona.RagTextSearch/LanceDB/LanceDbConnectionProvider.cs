using System.ComponentModel.DataAnnotations;
using lancedb;
using Table = lancedb.Table;

namespace ProjectPersona.RagTextSearch.LanceDB;

/// <summary> Владелец подключения к LanceDB: лениво открывает его и отдаёт остальным слоям. </summary>
/// <remarks> Не берёт <see cref="Utils.AsyncReaderWriterLock"/>: защита от гонок при подключении своя. </remarks>
public sealed class LanceDbConnectionProvider : IDisposable
{
    /// <summary> Настройки LanceDB. </summary>
    private readonly LanceDbSettings _settings;
    /// <summary> Подключение к БД. </summary>
    private readonly Connection _connection = new();
    /// <summary> Защита от параллельного открытия подключения. </summary>
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    /// <summary> Создать владельца подключения к LanceDB. </summary>
    /// <param name="settings"> Настройки LanceDB. </param>
    /// <exception cref="ArgumentNullException"> settings равен null. </exception>
    public LanceDbConnectionProvider(LanceDbSettings settings)
    {
        _settings = settings
                    ?? throw new ArgumentNullException(nameof(settings));

        Validator.ValidateObject(settings, new ValidationContext(settings), true);
    }

    /// <summary> Получить открытое подключение, открыв его при необходимости. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Подключение. </returns>
    public async ValueTask<Connection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection.IsOpen())
            return _connection;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_connection.IsOpen())
                await _connection.Connect(_settings.PathToLanceDb).ConfigureAwait(false);
        }
        finally
        {
            _connectLock.Release();
        }

        return _connection;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connectLock.Dispose();
        _connection.Dispose();
    }
}
