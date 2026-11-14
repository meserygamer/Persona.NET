using System.Buffers;
using System.Text.Json;

namespace ProjectPersona.RagTextSearch.Llama.Tokenizer;

/// <summary> Парсер токенов с кусками текста в ответе <c>/tokenize</c>. </summary>
public static class ModelTokenParser
{
    /// <summary>
    /// Читает объекты массива <c>tokens</c> (<c>id</c> и строка <c>piece</c>) из UTF-8 JSON.
    /// Поток разбирается <see cref="Utf8JsonReader"/> в один арендованный буфер, без DOM.
    /// </summary>
    /// <param name="utf8Json">Тело ответа <c>/tokenize</c> при <c>with_pieces</c>.</param>
    /// <param name="initialCapacity">Начальная ёмкость результата. Ноль и больше.</param>
    /// <returns>Токены в порядке массива <c>tokens</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> меньше нуля.</exception>
    /// <exception cref="InvalidOperationException">
    /// Поток пуст, JSON некорректен, поле <c>tokens</c> отсутствует или обрезано,
    /// либо элемент не является объектом с целым <c>id</c> и строкой <c>piece</c>.
    /// </exception>
    public static async Task<LlamaModelToken[]> ReadTokensAsync(Stream utf8Json, int initialCapacity = 16)
    {
        if (initialCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Начальная ёмкость не может быть отрицательной.");

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var state = new JsonReaderState();
            var phase = TokenPhase.SeekTokens;
            var draft = new TokenDraft();
            var skipDepth = 0;
            var tokens = new List<LlamaModelToken>(initialCapacity);
            var filled = 0;
            var hasData = false;

            while (true)
            {
                var read = await utf8Json.ReadAsync(buffer.AsMemory(filled, buffer.Length - filled));
                if (read == 0)
                {
                    if (!hasData)
                        throw new InvalidOperationException("Пустой ответ токенизатора.");

                    if (filled == 0)
                        FinishTokens(ReadOnlySpan<byte>.Empty, ref state, ref phase, ref draft, ref skipDepth, tokens);
                    else
                        FinishTokens(buffer.AsSpan(0, filled), ref state, ref phase, ref draft, ref skipDepth, tokens);

                    return tokens.ToArray();
                }

                hasData = true;
                filled += read;
                var consumed = ConsumeTokens(
                    buffer.AsSpan(0, filled),
                    isFinalBlock: false,
                    ref state,
                    ref phase,
                    ref draft,
                    ref skipDepth,
                    tokens);

                if (phase == TokenPhase.Done)
                    return tokens.ToArray();

                if (consumed == 0 && filled == buffer.Length)
                    throw new InvalidOperationException("Слишком длинный токен JSON в ответе токенизатора.");

                var remaining = filled - consumed;
                if (consumed > 0 && remaining > 0)
                    Buffer.BlockCopy(buffer, consumed, buffer, 0, remaining);

                filled = remaining;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Некорректный JSON токенизатора: {ex.Message}", ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Дочитывает хвост как последний блок и требует законченный массив объектов <c>tokens</c>.
    /// </summary>
    private static void FinishTokens(
        ReadOnlySpan<byte> tail,
        ref JsonReaderState state,
        ref TokenPhase phase,
        ref TokenDraft draft,
        ref int skipDepth,
        List<LlamaModelToken> tokens)
    {
        try
        {
            ConsumeTokens(tail, isFinalBlock: true, ref state, ref phase, ref draft, ref skipDepth, tokens);
        }
        catch (JsonException ex) when (phase != TokenPhase.SeekTokens && phase != TokenPhase.Done)
        {
            throw new InvalidOperationException("Ответ токенизатора обрезан.", ex);
        }

        if (phase != TokenPhase.Done)
        {
            throw new InvalidOperationException(phase == TokenPhase.SeekTokens
                ? "Поле 'tokens' отсутствует."
                : "Ответ токенизатора обрезан.");
        }
    }

    /// <summary>
    /// Продвигает разбор одного куска UTF-8 JSON и дописывает готовые токены в <paramref name="tokens"/>.
    /// </summary>
    /// <returns>Сколько байт из <paramref name="data"/> уже разобрано.</returns>
    private static int ConsumeTokens(
        ReadOnlySpan<byte> data,
        bool isFinalBlock,
        ref JsonReaderState state,
        ref TokenPhase phase,
        ref TokenDraft draft,
        ref int skipDepth,
        List<LlamaModelToken> tokens)
    {
        var reader = new Utf8JsonReader(data, isFinalBlock, state);
        while (phase != TokenPhase.Done && reader.Read())
        {
            switch (phase)
            {
                case TokenPhase.SeekTokens:
                    if (reader.TokenType == JsonTokenType.PropertyName
                        && reader.CurrentDepth == 1
                        && reader.ValueTextEquals("tokens"u8))
                    {
                        phase = TokenPhase.ExpectArray;
                    }

                    break;

                case TokenPhase.ExpectArray:
                    if (reader.TokenType != JsonTokenType.StartArray)
                        throw new InvalidOperationException("Поле 'tokens' не является массивом.");

                    phase = TokenPhase.InArray;
                    break;

                case TokenPhase.InArray:
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        draft = default;
                        phase = TokenPhase.InObject;
                    }
                    else if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        phase = TokenPhase.Done;
                    }
                    else
                    {
                        throw new InvalidOperationException("Массив 'tokens' должен содержать только объекты.");
                    }

                    break;

                case TokenPhase.InObject:
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("id"u8))
                            phase = TokenPhase.ReadId;
                        else if (reader.ValueTextEquals("piece"u8))
                            phase = TokenPhase.ReadPiece;
                        else
                            phase = TokenPhase.SkipValue;
                    }
                    else if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        if (!draft.HasId || draft.Piece is null)
                            throw new InvalidOperationException("Элемент массива 'tokens' должен содержать 'id' и 'piece'.");

                        tokens.Add(new LlamaModelToken { Id = draft.Id, Piece = draft.Piece });
                        draft = default;
                        phase = TokenPhase.InArray;
                    }
                    else
                    {
                        throw new InvalidOperationException("Некорректный объект в массиве 'tokens'.");
                    }

                    break;

                case TokenPhase.ReadId:
                    if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out draft.Id))
                        throw new InvalidOperationException("Поле 'id' должно быть целым числом.");

                    draft.HasId = true;
                    phase = TokenPhase.InObject;
                    break;

                case TokenPhase.ReadPiece:
                    if (reader.TokenType != JsonTokenType.String)
                        throw new InvalidOperationException("Поле 'piece' должно быть строкой.");

                    draft.Piece = reader.GetString() ?? string.Empty;
                    phase = TokenPhase.InObject;
                    break;

                case TokenPhase.SkipValue:
                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    {
                        skipDepth = reader.CurrentDepth;
                        phase = TokenPhase.SkipContainer;
                    }
                    else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray or JsonTokenType.PropertyName)
                    {
                        throw new InvalidOperationException("Некорректный объект в массиве 'tokens'.");
                    }
                    else
                    {
                        phase = TokenPhase.InObject;
                    }

                    break;

                case TokenPhase.SkipContainer:
                    if ((reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                        && reader.CurrentDepth == skipDepth)
                    {
                        phase = TokenPhase.InObject;
                    }

                    break;
            }
        }

        state = reader.CurrentState;
        return (int)reader.BytesConsumed;
    }

    /// <summary> Незавершённый объект токена. <c>id</c> и <c>piece</c> читаются в любом порядке. </summary>
    private struct TokenDraft
    {
        /// <summary> Идентификатор, если <see cref="HasId"/> равен <see langword="true"/>. </summary>
        public long Id;

        /// <summary> Кусок текста. <see langword="null"/>, пока строка <c>piece</c> не прочитана. </summary>
        public string? Piece;

        /// <summary> Поле <c>id</c> уже встретилось. Ноль — допустимый идентификатор, поэтому флаг отдельный. </summary>
        public bool HasId;
    }

    /// <summary> Фаза чтения объектов <c>tokens</c>. </summary>
    private enum TokenPhase : byte
    {
        /// <summary> Ищем свойство <c>tokens</c> на корне объекта. </summary>
        SeekTokens,

        /// <summary> Следующий токен должен быть началом массива. </summary>
        ExpectArray,

        /// <summary> Ждём объект токена или конец массива. </summary>
        InArray,

        /// <summary> Читаем свойства одного токена. </summary>
        InObject,

        /// <summary> Следующее значение — целое <c>id</c>. </summary>
        ReadId,

        /// <summary> Следующее значение — строка <c>piece</c>. </summary>
        ReadPiece,

        /// <summary> Следующее значение неизвестного свойства нужно пропустить. </summary>
        SkipValue,

        /// <summary> Пропускаем вложенный объект или массив до парной скобки. </summary>
        SkipContainer,

        /// <summary> Массив прочитан. </summary>
        Done
    }
}
