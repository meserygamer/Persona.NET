namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Состояние векторной базы на момент старта сервера. </summary>
public enum RagIndexStatus
{
    /// <summary> Таблицы нет. </summary>
    Missing,
    /// <summary> Нет манифеста, либо он несовместим с текущим кодом или эмбеддером. </summary>
    Invalid,
    /// <summary> Частичная ревекторизация не завершена; поиск работает с предупреждением. </summary>
    Updating,
    /// <summary> База готова. </summary>
    Ready
}

/// <summary> Результат проверки векторной базы при старте. </summary>
/// <param name="Status">Состояние базы.</param>
/// <param name="Error">Причина для <see cref="RagIndexStatus.Missing"/> и <see cref="RagIndexStatus.Invalid"/>.</param>
/// <param name="Warnings">Предупреждения для ответа поиска; пустой список, если их нет.</param>
public sealed record RagIndexState(
    RagIndexStatus Status,
    string? Error,
    IReadOnlyList<string> Warnings)
{
    /// <summary> Поиск по базе возможен. </summary>
    public bool IsSearchable => Status is RagIndexStatus.Ready or RagIndexStatus.Updating;
}
