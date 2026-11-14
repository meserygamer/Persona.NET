using ProjectPersona.Application.Pipes;

namespace ProjectPersona.Tests.Pipes;

public class ChannelPipeBaseDisposeTests
{
    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var pipe = new ForwardPipe(capacity: 1);

        await pipe.DisposeAsync();
        await pipe.DisposeAsync();
    }

    /// <summary> Передаёт элементы дальше без задержки. </summary>
    private sealed class ForwardPipe : ChannelPipeBase<int, int>
    {
        public ForwardPipe(int capacity) : base(capacity)
        {
        }

        protected override ValueTask ProcessAsync(int item, Func<int, ValueTask> emit, CancellationToken ct) =>
            emit(item);
    }
}
