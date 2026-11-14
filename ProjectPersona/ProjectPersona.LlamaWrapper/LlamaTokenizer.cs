using System.Buffers;
using System.Text;
using ProjectPersona.LlamaWrapper.Native;

namespace ProjectPersona.LlamaWrapper;

/// <summary>
/// Локальный токенизатор Llama: словарь GGUF через <c>llama.dll</c>, без HTTP и без весов модели.
/// </summary>
public sealed class LlamaTokenizer : IDisposable
{
    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] EmptyUtf8 = [0];

    /// <summary>
    /// Читатели — токенизация (словарь неизменяем после загрузки), писатель — освобождение указателей.
    /// </summary>
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private IntPtr _model;
    private IntPtr _vocab;
    private readonly bool _ownsModel;

    private LlamaTokenizer(IntPtr model, IntPtr vocab, bool ownsModel)
    {
        _model = model;
        _vocab = vocab;
        _ownsModel = ownsModel;
    }

    /// <summary>
    /// Открывает словарь GGUF. Повторные вызовы делят один процессный экземпляр <c>llama.dll</c>.
    /// </summary>
    /// <param name="modelPath">Путь к файлу GGUF.</param>
    /// <param name="nativeLibraryDirectory">
    /// Каталог с <c>llama.dll</c> и зависимостями. Обязателен, автопоиска нет.
    /// </param>
    /// <returns>Токенизатор, который нужно освободить через <see cref="Dispose"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="modelPath"/> пустой.</exception>
    /// <exception cref="FileNotFoundException">Нет GGUF или <c>llama.dll</c>.</exception>
    /// <exception cref="InvalidOperationException">Нативная библиотека не совпала по ABI или не открыла файл.</exception>
    public static LlamaTokenizer Load(string modelPath, string? nativeLibraryDirectory = null)
    {
        var (model, vocab) = LlamaNative.LoadModel(modelPath, nativeLibraryDirectory, vocabOnly: true);
        return new LlamaTokenizer(model, vocab, ownsModel: true);
    }

    /// <summary>
    /// Оборачивает уже загруженную модель (владелец модели — вызывающий код, например <see cref="LlamaModelSession"/>).
    /// </summary>
    /// <param name="model">Указатель модели.</param>
    /// <param name="vocab">Указатель словаря.</param>
    internal static LlamaTokenizer Attach(IntPtr model, IntPtr vocab) =>
        new(model, vocab, ownsModel: false);

    /// <summary>Сбрасывает указатели без освобождения модели (после dispose сессии).</summary>
    internal void MarkDetached()
    {
        _lock.EnterWriteLock();
        try
        {
            _model = IntPtr.Zero;
            _vocab = IntPtr.Zero;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Считает токены в тексте.
    /// </summary>
    /// <param name="text">Текст. Пустая строка даёт ноль токенов, если специальные токены не добавляются.</param>
    /// <param name="addSpecial">Добавлять BOS и EOS, если так настроен словарь.</param>
    /// <param name="parseSpecial">Разбирать специальные токены в тексте, а не оставлять их обычными символами.</param>
    /// <returns>Число токенов.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Токенизация не поместилась в <c>int</c>.</exception>
    /// <exception cref="ObjectDisposedException">Словарь уже освобождён.</exception>
    public int CountTokens(string text, bool addSpecial, bool parseSpecial = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ids = TokenizeIds(text, addSpecial, parseSpecial);
        return ids.Length;
    }

    /// <summary>
    /// Токенизирует текст и возвращает идентификаторы вместе с кусками текста.
    /// Если склейка <c>llama_token_to_piece</c> совпадает с UTF-8 <paramref name="text"/>,
    /// куски — срезы этой строки, и их конкатенация равна <paramref name="text"/>.
    /// </summary>
    /// <param name="text">Текст.</param>
    /// <param name="addSpecial">Добавлять BOS и EOS, если так настроен словарь.</param>
    /// <param name="parseSpecial">Разбирать специальные токены в тексте, а не оставлять их обычными символами.</param>
    /// <returns>Токены в порядке <c>llama_tokenize</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> равен <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Токенизация не поместилась в <c>int</c> или piece не прочитан.</exception>
    /// <exception cref="ObjectDisposedException">Словарь уже освобождён.</exception>
    public LlamaTokenPiece[] Tokenize(string text, bool addSpecial, bool parseSpecial = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        _lock.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            var ids = TokenizeIdsUnlocked(text, addSpecial, parseSpecial);
            if (ids.Length == 0)
                return [];

            var rawPieces = new byte[ids.Length][];
            var totalBytes = 0;
            for (var i = 0; i < ids.Length; i++)
            {
                rawPieces[i] = ReadPieceBytes(ids[i]);
                totalBytes += rawPieces[i].Length;
            }

            var blob = new byte[totalBytes];
            var offset = 0;
            foreach (var raw in rawPieces)
            {
                raw.CopyTo(blob, offset);
                offset += raw.Length;
            }

            if (TrySliceOriginalText(text, ids, rawPieces, blob, out var sliced))
                return sliced;

            var pieces = new LlamaTokenPiece[ids.Length];
            for (var i = 0; i < ids.Length; i++)
                pieces[i] = new LlamaTokenPiece(ids[i], DecodePiece(rawPieces[i]));

            return pieces;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Возвращает идентификаторы токенов без декодирования piece.
    /// </summary>
    /// <param name="text">Текст.</param>
    /// <param name="addSpecial">Добавлять BOS/EOS.</param>
    /// <param name="parseSpecial">Разбирать специальные токены.</param>
    public int[] GetTokenIds(string text, bool addSpecial, bool parseSpecial = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TokenizeIds(text, addSpecial, parseSpecial);
    }

    /// <summary>Освобождает словарь. Повторный вызов ничего не делает.</summary>
    public void Dispose()
    {
        if (!_ownsModel)
            return;

        IntPtr model;
        _lock.EnterWriteLock();
        try
        {
            model = _model;
            _model = IntPtr.Zero;
            _vocab = IntPtr.Zero;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        LlamaNative.FreeModel(model);
    }

    private int[] TokenizeIds(string text, bool addSpecial, bool parseSpecial)
    {
        _lock.EnterReadLock();
        try
        {
            ThrowIfDisposed();
            return TokenizeIdsUnlocked(text, addSpecial, parseSpecial);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private int[] TokenizeIdsUnlocked(string text, bool addSpecial, bool parseSpecial)
    {
        var utf8 = text.Length == 0 ? EmptyUtf8 : Encoding.UTF8.GetBytes(text);
        var textLength = text.Length == 0 ? 0 : utf8.Length;
        var capacity = Math.Max(8, textLength + 2);

        while (true)
        {
            var buffer = ArrayPool<int>.Shared.Rent(capacity);
            try
            {
                var count = LlamaNative.Tokenize(_vocab, utf8, textLength, buffer, addSpecial, parseSpecial);
                if (count == int.MinValue)
                    throw new InvalidOperationException("Токенизация переполнила int32.");

                if (count < 0)
                {
                    capacity = -count;
                    continue;
                }

                var ids = new int[count];
                if (count > 0)
                    Array.Copy(buffer, ids, count);

                return ids;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(buffer);
            }
        }
    }

    private byte[] ReadPieceBytes(int tokenId)
    {
        var capacity = 32;
        while (true)
        {
            var scratch = ArrayPool<byte>.Shared.Rent(capacity);
            try
            {
                var written = LlamaNative.TokenToPiece(_vocab, tokenId, scratch);
                if (written == int.MinValue)
                    throw new InvalidOperationException($"Не удалось прочитать piece токена {tokenId}.");

                if (written < 0)
                {
                    capacity = -written;
                    continue;
                }

                var copy = new byte[written];
                scratch.AsSpan(0, written).CopyTo(copy);
                return copy;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(scratch);
            }
        }
    }

    /// <summary>
    /// Если склейка piece совпадает с UTF-8 <paramref name="text"/>, режет исходную строку по символам.
    /// Байт, оборванный на границе токена, не получает символ: его забирает токен, который символ заканчивает.
    /// </summary>
    private static bool TrySliceOriginalText(
        string text,
        int[] ids,
        byte[][] rawPieces,
        byte[] blob,
        out LlamaTokenPiece[] pieces)
    {
        pieces = [];
        if (!blob.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(text)))
            return false;

        var runeCharEnds = new int[text.Length];
        var runeByteEnds = new int[text.Length];
        var runeCount = 0;
        var charEnd = 0;
        var byteEnd = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            charEnd += rune.Utf16SequenceLength;
            byteEnd += rune.Utf8SequenceLength;
            runeCharEnds[runeCount] = charEnd;
            runeByteEnds[runeCount] = byteEnd;
            runeCount++;
        }

        pieces = new LlamaTokenPiece[ids.Length];
        var runeIndex = 0;
        var tokenByteEnd = 0;
        var charPos = 0;
        for (var i = 0; i < ids.Length; i++)
        {
            tokenByteEnd += rawPieces[i].Length;
            var nextChar = charPos;
            while (runeIndex < runeCount && runeByteEnds[runeIndex] <= tokenByteEnd)
            {
                nextChar = runeCharEnds[runeIndex];
                runeIndex++;
            }

            pieces[i] = new LlamaTokenPiece(ids[i], text[charPos..nextChar]);
            charPos = nextChar;
        }

        return charPos == text.Length;
    }

    private void ThrowIfDisposed()
    {
        if (_model == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(LlamaTokenizer));
    }

    /// <summary>
    /// Декодирует piece. Валидный UTF-8 остаётся строкой; один байт вне UTF-8 — литерал <c>&lt;0xHH&gt;</c>,
    /// который умеет разворачивать выравнивание токенов в исходный текст.
    /// </summary>
    private static string DecodePiece(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        try
        {
            return Utf8Strict.GetString(bytes);
        }
        catch (DecoderFallbackException) when (bytes.Length == 1)
        {
            return $"<0x{bytes[0]:X2}>";
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
