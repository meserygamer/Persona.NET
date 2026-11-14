using System.Threading.Channels;

namespace ProjectPersona.Application.Pipes;

/// <summary> Потоковый обработчик данных. </summary>
/// <typeparam name="TInput"> Тип входящих данных. </typeparam>
/// <typeparam name="TOutput"> Тип исходящих данных. </typeparam>
public interface IChannelPipe<TInput, TOutput>: IAsyncDisposable
{
    /// <summary> Присоединить пайпу. </summary>
    /// <param name="pipe"> Присоединяемая пайпа. </param>
    /// <typeparam name="TNewOutput"> Выходной тип итоговой связки. </typeparam>
    /// <returns> Связка двух Pipe. </returns>
    IChannelPipe<TInput, TNewOutput> Join<TNewOutput>(IChannelPipe<TOutput, TNewOutput> pipe);

    /// <summary> Стартовать пайплайн. </summary>
    /// <param name="item"> Элемент старта. </param>
    /// <returns> Пайплайн. </returns>
    ValueTask<IChannelPipe<TInput, TOutput>> RunOnceAsync(TInput item);

    /// <summary> Задать обработчик завершитель. </summary>
    /// <param name="batchHandler"> Обработчик батча. </param>
    IChannelPipe<TInput, TOutput> Finalize(Func<TOutput, ValueTask>? batchHandler);

    /// <summary> Отменить обработку: все фоновые циклы связки получают сигнал отмены. </summary>
    void Cancel();

    /// <summary> Писатель в текущий канал. </summary>
    ChannelWriter<TInput> Writer { get; }

    /// <summary> Задача завершения pipe. </summary>
    Task CompleteTask { get; }
}