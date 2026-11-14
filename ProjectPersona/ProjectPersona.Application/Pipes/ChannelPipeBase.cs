using System.Threading.Channels;

namespace ProjectPersona.Application.Pipes;

/// <summary>
/// Общий каркас пайпа: входной канал, фоновый цикл обработки и распространение сбоя по цепочке.
/// Потомок реализует только <see cref="ProcessAsync"/>.
/// </summary>
/// <typeparam name="TInput"> Тип входящих данных. </typeparam>
/// <typeparam name="TOutput"> Тип исходящих данных. </typeparam>
public abstract class ChannelPipeBase<TInput, TOutput> : IChannelPipe<TInput, TOutput>
{
    /// <summary> Входной канал пайпа. </summary>
    private readonly Channel<TInput> _input;
    /// <summary> Источник задачи на завершение обработки. </summary>
    private readonly TaskCompletionSource _tcs = new();
    /// <summary> Источник токенов отмены. </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    /// <summary> Текущая таска на обработку. </summary>
    private Task? _joinTask;
    /// <summary> Пайпа уже освобождена; повторный <see cref="DisposeAsync"/> ничего не делает. </summary>
    private bool _disposed;

    /// <summary> Создать пайп с входным каналом заданной ёмкости. </summary>
    /// <param name="capacity"> Ёмкость входного канала. </param>
    protected ChannelPipeBase(int capacity)
    {
        _input = Channel.CreateBounded<TInput>(capacity);
    }

    /// <summary> Токен отмены, который получает обработка элементов. </summary>
    protected CancellationToken StopToken => _cancellationTokenSource.Token;

    /// <inheritdoc />
    public ChannelWriter<TInput> Writer => _input.Writer;

    /// <inheritdoc />
    public Task CompleteTask => _tcs.Task;

    /// <summary> Обработать один элемент входного канала. </summary>
    /// <param name="item"> Элемент. </param>
    /// <param name="emit"> Отправка результата дальше: в следующий пайп или в обработчик. </param>
    /// <param name="ct"> Токен отмены. </param>
    protected abstract ValueTask ProcessAsync(TInput item, Func<TOutput, ValueTask> emit, CancellationToken ct);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Пайп уже подключён или завершён обработчиком.</exception>
    public IChannelPipe<TInput, TNewOutput> Join<TNewOutput>(IChannelPipe<TOutput, TNewOutput> pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        Start(item => pipe.Writer.WriteAsync(item, StopToken), pipe.Writer);

        return new JoinedChannelPipe<TInput, TOutput, TNewOutput>(this, pipe);
    }

    /// <inheritdoc />
    public async ValueTask<IChannelPipe<TInput, TOutput>> RunOnceAsync(TInput item)
    {
        await Writer.WriteAsync(item, StopToken).ConfigureAwait(false);
        Writer.TryComplete();

        return this;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Пайп уже подключён или завершён обработчиком.</exception>
    public IChannelPipe<TInput, TOutput> Finalize(Func<TOutput, ValueTask>? batchHandler)
    {
        Start(batchHandler ?? (_ => ValueTask.CompletedTask), downstream: null);

        return this;
    }

    /// <inheritdoc />
    public void Cancel() => _cancellationTokenSource.Cancel();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        Writer.TryComplete();

        try
        {
            await _cancellationTokenSource.CancelAsync();

            if (_joinTask != null)
                await _joinTask;
            else
                _tcs.TrySetCanceled();
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }
    }

    /// <summary> Запустить фоновый цикл, который читает вход и отправляет результаты в <paramref name="emit"/>. </summary>
    /// <param name="emit"> Отправка результата дальше. </param>
    /// <param name="downstream">
    /// Входной канал следующего пайпа. При завершении цикла он закрывается, и ошибка доходит до следующего пайпа.
    /// </param>
    private void Start(Func<TOutput, ValueTask> emit, ChannelWriter<TOutput>? downstream)
    {
        if (_joinTask != null)
            throw new InvalidOperationException("Already joined!");

        var ct = StopToken;
        _joinTask = Task.Run(async () =>
        {
            Exception? exception = null;

            try
            {
                await foreach (var item in _input.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                    await ProcessAsync(item, emit, ct).ConfigureAwait(false);

                _tcs.TrySetResult();
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                exception = ex;
                _tcs.TrySetCanceled(ct);
            }
            catch (Exception ex)
            {
                exception = ex;
                _tcs.TrySetException(ex);
            }
            finally
            {
                // Закрываем оба канала: писатель выше по цепочке получит исключение, а не зависнет на полном канале,
                // а следующий пайп увидит сбой при чтении.
                _input.Writer.TryComplete(exception);
                downstream?.TryComplete(exception);
            }
        });
    }
}
