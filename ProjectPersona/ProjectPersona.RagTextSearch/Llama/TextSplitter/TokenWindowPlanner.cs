namespace ProjectPersona.RagTextSearch.Llama.TextSplitter;

/// <summary> Планирует окна токенов со скользящим шагом и перекрытием. </summary>
internal static class TokenWindowPlanner
{
    /// <summary> Строит скользящие окна <c>[start, end)</c> без пропусков токенов. </summary>
    /// <param name="tokenCount">Число токенов <c>N</c>.</param>
    /// <param name="window">Размер окна <c>W</c> (должен быть ≥ 1).</param>
    /// <param name="overlap">Перекрытие <c>O</c> (0 ≤ O &lt; W).</param>
    /// <returns>Список окон; при <c>N == 0</c> — пустой.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="window"/> &lt; 1, <paramref name="overlap"/> &lt; 0 или ≥ <paramref name="window"/>.
    /// </exception>
    public static List<(int Start, int End)> PlanWindows(int tokenCount, int window, int overlap)
    {
        if (window < 1)
            throw new ArgumentOutOfRangeException(nameof(window), "Размер окна должен быть не меньше 1.");

        if (overlap < 0)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Перекрытие не может быть отрицательным.");

        if (overlap >= window)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Перекрытие должно быть меньше размера окна.");

        if (tokenCount == 0)
            return [];

        if (tokenCount <= window)
            return [(0, tokenCount)];

        var hop = window - overlap;
        var windows = new List<(int Start, int End)>();
        var start = 0;

        while (start + window < tokenCount)
        {
            windows.Add((start, start + window));
            start += hop;
        }

        var lastStart = tokenCount - window;
        if (windows.Count == 0)
        {
            windows.Add((lastStart, tokenCount));
            return windows;
        }

        if (windows[^1].Start == lastStart)
            windows[^1] = (lastStart, tokenCount);
        else
            windows.Add((lastStart, tokenCount));

        return windows;
    }
}
