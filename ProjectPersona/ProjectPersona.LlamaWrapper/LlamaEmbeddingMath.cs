namespace ProjectPersona.LlamaWrapper;

/// <summary>
/// Вспомогательные операции над векторами эмбеддингов.
/// </summary>
internal static class LlamaEmbeddingMath
{
    /// <summary>
    /// Нормализует вектор по L2 (евклидова норма = 1), как OpenAI-совместимый <c>/v1/embeddings</c>.
    /// </summary>
    /// <param name="vector">Вектор in-place.</param>
    public static void L2Normalize(Span<float> vector)
    {
        if (vector.IsEmpty)
            return;

        double sumSquares = 0;
        foreach (var value in vector)
            sumSquares += value * value;

        if (sumSquares <= 0)
            return;

        var invNorm = (float)(1.0 / Math.Sqrt(sumSquares));
        for (var i = 0; i < vector.Length; i++)
            vector[i] *= invNorm;
    }
}
