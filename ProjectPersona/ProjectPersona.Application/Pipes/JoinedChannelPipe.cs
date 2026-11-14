using System.Threading.Channels;

namespace ProjectPersona.Application.Pipes;

public class JoinedChannelPipe<TInput, TIntermediate, TOutput>: IChannelPipe<TInput, TOutput>
{
    /// <summary> Первая пайпа. </summary>
    private readonly IChannelPipe<TInput, TIntermediate> _firstPipe;
    /// <summary> Вторая пайпа. </summary>
    private readonly IChannelPipe<TIntermediate, TOutput> _secondPipe;

    public JoinedChannelPipe(IChannelPipe<TInput, TIntermediate> firstPipe, 
        IChannelPipe<TIntermediate, TOutput> secondPipe)
    {
        _firstPipe = firstPipe 
                     ?? throw new ArgumentNullException(nameof(firstPipe));
        _secondPipe = secondPipe 
                      ?? throw new ArgumentNullException(nameof(secondPipe));
        
        CompleteTask = Task.WhenAll(
            _firstPipe.CompleteTask,
            _secondPipe.CompleteTask);
    }
    
    /// <summary> Уничтожить. </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _firstPipe.DisposeAsync();
        }
        finally
        {
            await _secondPipe.DisposeAsync();
        }
    }

    /// <summary> Присоединить пайп. </summary>
    /// <param name="pipe"> Присоединяемый пайп. </param>
    /// <typeparam name="TNewOutput"> Выходной тип связки. </typeparam>
    /// <returns> Связка пайпов. </returns>
    public IChannelPipe<TInput, TNewOutput> Join<TNewOutput>(IChannelPipe<TOutput, TNewOutput> pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        return new JoinedChannelPipe<TInput, TIntermediate, TNewOutput>(
            _firstPipe,
            _secondPipe.Join(pipe));
    }

    /// <summary> Стартовать пайплайн. </summary>
    /// <param name="item"> Элемент старта. </param>
    /// <returns> Пайплайн. </returns>
    public async ValueTask<IChannelPipe<TInput, TOutput>> RunOnceAsync(TInput item)
    {
        await _firstPipe.RunOnceAsync(item);
        return this;
    }

    /// <summary> Задать обработчик завершитель. </summary>
    /// <param name="batchHandler"> Обработчик батча. </param>
    public IChannelPipe<TInput, TOutput> Finalize(Func<TOutput, ValueTask>? batchHandler)
    {
        _secondPipe.Finalize(batchHandler);
        return this;
    }

    /// <summary> Отменить обе части связки. </summary>
    public void Cancel()
    {
        _firstPipe.Cancel();
        _secondPipe.Cancel();
    }

    /// <summary> Писатель в объединённый пайп. </summary>
    public ChannelWriter<TInput> Writer => _firstPipe.Writer;

    /// <summary> Общая задача завершения. </summary>
    public Task CompleteTask { get; }
}