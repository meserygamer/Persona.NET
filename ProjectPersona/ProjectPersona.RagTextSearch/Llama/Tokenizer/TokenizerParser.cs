using System.Buffers;
using System.Text.Json;

namespace ProjectPersona.RagTextSearch.Llama.Tokenizer;

/// <summary> Парсер числа токенов в ответе <c>/tokenize</c>. </summary>
public static class TokenizerParser
{
    /// <summary>
    /// Считает элементы массива <c>tokens</c> в UTF-8 JSON.
    /// Идентификаторы не сохраняются: поток читается в один арендованный буфер.
    /// </summary>
    /// <param name="utf8Json">Тело ответа <c>/tokenize</c>.</param>
    /// <returns>Число токенов.</returns>
    /// <exception cref="InvalidOperationException">
    /// Поток пуст, JSON некорректен, поле <c>tokens</c> отсутствует или обрезано,
    /// либо массив содержит не только числа.
    /// </exception>
    public static async Task<long> CountTokensAsync(Stream utf8Json)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var state = new JsonReaderState();
            var phase = Phase.SeekTokens;
            long count = 0;
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
                        Finish(ReadOnlySpan<byte>.Empty, ref state, ref phase, ref count);
                    else
                        Finish(buffer.AsSpan(0, filled), ref state, ref phase, ref count);

                    return count;
                }

                hasData = true;
                filled += read;
                var consumed = Consume(buffer.AsSpan(0, filled), isFinalBlock: false, ref state, ref phase, ref count);
                if (phase == Phase.Done)
                    return count;

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
    /// Дочитывает хвост как последний блок и требует законченный массив <c>tokens</c>.
    /// Обрыв после начала <c>tokens</c> — «обрезан», готовый JSON без этого поля — «отсутствует».
    /// </summary>
    private static void Finish(
        ReadOnlySpan<byte> tail,
        ref JsonReaderState state,
        ref Phase phase,
        ref long count)
    {
        try
        {
            Consume(tail, isFinalBlock: true, ref state, ref phase, ref count);
        }
        catch (JsonException ex) when (phase is Phase.ExpectArray or Phase.InArray)
        {
            throw new InvalidOperationException("Ответ токенизатора обрезан.", ex);
        }

        if (phase != Phase.Done)
        {
            throw new InvalidOperationException(phase == Phase.SeekTokens
                ? "Поле 'tokens' отсутствует."
                : "Ответ токенизатора обрезан.");
        }
    }

    /// <summary>
    /// Продвигает разбор одного куска UTF-8 JSON.
    /// </summary>
    /// <returns>Сколько байт из <paramref name="data"/> уже разобрано.</returns>
    private static int Consume(
        ReadOnlySpan<byte> data,
        bool isFinalBlock,
        ref JsonReaderState state,
        ref Phase phase,
        ref long count)
    {
        var reader = new Utf8JsonReader(data, isFinalBlock, state);
        while (phase != Phase.Done && reader.Read())
        {
            switch (phase)
            {
                case Phase.SeekTokens:
                    if (reader.TokenType == JsonTokenType.PropertyName
                        && reader.CurrentDepth == 1
                        && reader.ValueTextEquals("tokens"u8))
                    {
                        phase = Phase.ExpectArray;
                    }

                    break;

                case Phase.ExpectArray:
                    if (reader.TokenType != JsonTokenType.StartArray)
                        throw new InvalidOperationException("Поле 'tokens' не является массивом.");

                    phase = Phase.InArray;
                    break;

                case Phase.InArray:
                    if (reader.TokenType == JsonTokenType.Number)
                        count++;
                    else if (reader.TokenType == JsonTokenType.EndArray)
                        phase = Phase.Done;
                    else
                        throw new InvalidOperationException("Массив 'tokens' должен содержать только числа.");

                    break;
            }
        }

        state = reader.CurrentState;
        return (int)reader.BytesConsumed;
    }

    /// <summary> Фаза поиска массива <c>tokens</c>. </summary>
    private enum Phase : byte
    {
        /// <summary> Ищем свойство <c>tokens</c> на корне объекта. </summary>
        SeekTokens,

        /// <summary> Следующий токен должен быть началом массива. </summary>
        ExpectArray,

        /// <summary> Считаем числа до конца массива. </summary>
        InArray,

        /// <summary> Массив прочитан. </summary>
        Done
    }
}
