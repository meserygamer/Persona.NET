using System.Text.Json;

namespace ProjectPersona.RagTextSearch.Llama.Embedder;

public static class EmbeddingParser
{
    private sealed class EmbeddingFormatException(string message) : Exception(message);

    /// <summary>
    /// Безопасно разбирает ответ эмбеддинг-API (OpenAI-совместимый формат).
    /// Не бросает исключений: при ошибке возвращает false и текст причины.
    /// </summary>
    public static bool TryParseEmbeddings(string? json, out float[][] embeddings, out string? error)
    {
        embeddings = Array.Empty<float[]>();
        error = null;

        try
        {
            using var document = ParseJson(json);
            var items = ReadItems(GetDataArray(document.RootElement));

            embeddings = items
                .OrderBy(x => x.Index)
                .Select(x => x.Vector)
                .ToArray();
            
            return true;
        }
        catch (EmbeddingFormatException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (JsonException ex)
        {
            error = $"Некорректный JSON: {ex.Message}";
            return false;
        }
    }

    private static JsonDocument ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new EmbeddingFormatException("Пустой ответ.");

        return JsonDocument.Parse(json);
    }

    private static JsonElement GetDataArray(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new EmbeddingFormatException("Корень JSON не является объектом.");

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new EmbeddingFormatException("Поле 'data' отсутствует или не является массивом.");

        if (data.GetArrayLength() == 0)
            throw new EmbeddingFormatException("Массив 'data' пуст.");

        return data;
    }

    private static List<(int Index, float[] Vector)> ReadItems(JsonElement data)
    {
        var items = new List<(int Index, float[] Vector)>(data.GetArrayLength());
        int position = 0;

        foreach (var element in data.EnumerateArray())
        {
            items.Add(ReadItem(element, position));
            position++;
        }

        return items;
    }

    private static (int Index, float[] Vector) ReadItem(JsonElement element, int position)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("embedding", out var embedding))
        {
            throw new EmbeddingFormatException($"Элемент data[{position}] не содержит поля 'embedding'.");
        }

        var vector = ReadVector(embedding, position);
        var index = ReadIndex(element, fallback: position);

        return (index, vector);
    }

    private static float[] ReadVector(JsonElement embedding, int position)
    {
        if (embedding.ValueKind != JsonValueKind.Array)
        {
            throw new EmbeddingFormatException(
                $"data[{position}].embedding не является массивом (возможно, запрошен encoding_format=base64).");
        }

        var vector = new float[embedding.GetArrayLength()];
        if (vector.Length == 0)
            throw new EmbeddingFormatException($"data[{position}].embedding пуст.");

        int i = 0;
        foreach (var value in embedding.EnumerateArray())
        {
            vector[i] = ReadNumber(value, position, i);
            i++;
        }

        return vector;
    }

    private static float ReadNumber(JsonElement value, int position, int i)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out var number))
            throw new EmbeddingFormatException($"data[{position}].embedding[{i}] не является числом.");

        return number;
    }

    private static int ReadIndex(JsonElement element, int fallback)
    {
        return element.TryGetProperty("index", out var idx) &&
               idx.ValueKind == JsonValueKind.Number &&
               idx.TryGetInt32(out var index)
            ? index
            : fallback;
    }
}