namespace ProjectPersona.RagTextSearch.Utils;

/// <summary>
/// Асинхронная блокировка «много читателей / один писатель».
/// </summary>
/// <remarks>
/// В отличие от <see cref="ReaderWriterLockSlim"/> не привязана к потоку, поэтому её можно держать через <c>await</c>.
/// Не реентерабельна: повторный захват из-под уже взятой блокировки приведёт к взаимной блокировке.
/// Писатель, ожидающий очереди, не даёт новым читателям проскочить вперёд (читатели не морят писателя).
/// </remarks>
public sealed class AsyncReaderWriterLock : IDisposable
{
    /// <summary> Очередь входа: сериализует захват ресурса первым читателем и писателем. </summary>
    private readonly SemaphoreSlim _entry = new(1, 1);
    /// <summary> Ресурс: занят писателем либо группой читателей. </summary>
    private readonly SemaphoreSlim _resource = new(1, 1);
    /// <summary> Число активных читателей. </summary>
    private int _readers;

    /// <summary> Захватить блокировку для чтения (общий доступ). </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Токен, освобождающий блокировку при <see cref="IDisposable.Dispose"/>. </returns>
    public async ValueTask<Releaser> ReadAsync(CancellationToken ct = default)
    {
        await _entry.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Interlocked.Increment(ref _readers) == 1)
            {
                try
                {
                    await _resource.WaitAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Decrement(ref _readers);
                    throw;
                }
            }
        }
        finally
        {
            _entry.Release();
        }

        return new Releaser(this, isWriter: false);
    }

    /// <summary> Захватить блокировку для записи (эксклюзивный доступ). </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Токен, освобождающий блокировку при <see cref="IDisposable.Dispose"/>. </returns>
    public async ValueTask<Releaser> WriteAsync(CancellationToken ct = default)
    {
        await _entry.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _resource.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _entry.Release();
        }

        return new Releaser(this, isWriter: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _entry.Dispose();
        _resource.Dispose();
    }

    private void ReleaseRead()
    {
        if (Interlocked.Decrement(ref _readers) == 0)
            _resource.Release();
    }

    private void ReleaseWrite() => _resource.Release();

    /// <summary> Токен захваченной блокировки. </summary>
    public readonly struct Releaser : IDisposable
    {
        private readonly AsyncReaderWriterLock _owner;
        private readonly bool _isWriter;

        internal Releaser(AsyncReaderWriterLock owner, bool isWriter)
        {
            _owner = owner;
            _isWriter = isWriter;
        }

        /// <summary> Освободить блокировку. </summary>
        public void Dispose()
        {
            if (_owner is null)
                return;

            if (_isWriter)
                _owner.ReleaseWrite();
            else
                _owner.ReleaseRead();
        }
    }
}
