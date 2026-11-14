namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary>
/// Снимок прогресса векторизации объявлений type-source-index.
/// </summary>
/// <param name="Processed">Число объявлений, чьи фрагменты уже записаны в хранилище.</param>
/// <param name="Total">Общее число объявлений в индексе.</param>
/// <param name="IsComplete">Все батчи записаны и хранилище оптимизировано.</param>
public readonly record struct VectorizationProgress(long Processed, long Total, bool IsComplete = false)
{
    /// <summary>
    /// Целый процент для отображения. 100 только при <see cref="IsComplete"/>;
    /// пока есть незаписанные фрагменты или не закончена оптимизация, значение не превышает 99.
    /// </summary>
    public int Percent
    {
        get
        {
            if (Total == 0)
                return 100;

            if (IsComplete)
                return 100;

            var raw = (int)(Processed * 100L / Total);
            return Processed >= Total ? Math.Min(raw, 99) : raw;
        }
    }
}
