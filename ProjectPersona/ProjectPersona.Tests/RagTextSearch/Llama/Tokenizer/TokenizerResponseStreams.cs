namespace ProjectPersona.Tests.RagTextSearch.Llama.Tokenizer;

/// <summary>Отдаёт тело по одному байту за чтение.</summary>
internal sealed class OneByteStream : ChunkedStream
{
    /// <summary>Создаёт поток, каждое чтение которого возвращает не больше одного байта.</summary>
    public OneByteStream(byte[] data)
        : base(data, chunkSize: 1)
    {
    }
}

/// <summary>Отдаёт тело фиксированными кусками, как сокет с коротким <c>Read</c>.</summary>
internal class ChunkedStream : Stream
{
    private readonly byte[] _data;
    private readonly int _chunkSize;
    private int _offset;

    /// <summary>Сколько байт вернул самый длинный успешный <c>Read</c>.</summary>
    public int MaxBytesReturned { get; private set; }

    /// <summary>Создаёт поток поверх <paramref name="data"/> с чтением не больше <paramref name="chunkSize"/> байт за раз.</summary>
    public ChunkedStream(byte[] data, int chunkSize)
    {
        _data = data;
        _chunkSize = chunkSize;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => ReadChunk(buffer);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(ReadChunk(buffer.Span));

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Копирует очередной кусок в <paramref name="destination"/>.</summary>
    private int ReadChunk(Span<byte> destination)
    {
        if (_offset >= _data.Length)
            return 0;

        var count = Math.Min(_chunkSize, Math.Min(destination.Length, _data.Length - _offset));
        _data.AsSpan(_offset, count).CopyTo(destination);
        _offset += count;
        if (count > MaxBytesReturned)
            MaxBytesReturned = count;

        return count;
    }
}

/// <summary>Отдаёт весь буфер одним чтением и помечает повторный вызов.</summary>
internal sealed class OneShotStream : Stream
{
    private readonly byte[] _data;
    private bool _read;

    /// <summary>Создаёт поток, тело которого умещается в один вызов чтения.</summary>
    public OneShotStream(byte[] data) => _data = data;

    /// <summary><see langword="true"/>, если чтение вызвали после того, как тело уже отдано.</summary>
    public bool ReadPastFirstBuffer { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => ReadOnce(buffer);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(ReadOnce(buffer.Span));

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Первый вызов копирует всё тело, следующие сообщают о лишнем чтении.</summary>
    private int ReadOnce(Span<byte> destination)
    {
        if (_read)
        {
            ReadPastFirstBuffer = true;
            return 0;
        }

        _read = true;
        _data.CopyTo(destination);
        return _data.Length;
    }
}
