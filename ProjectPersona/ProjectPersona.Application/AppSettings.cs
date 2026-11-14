namespace ProjectPersona.Application;

/// <summary>
/// Настройки stdio MCP-сервера и CLI-генерации из <c>appsettings.json</c>.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Путь к SQLite-файлу type-source-index (например, <c>C:\indexes\type-source-index.db</c>). Если пусто — <c>BaseDirectory/type-source-index/type-source-index.db</c>.
    /// </summary>
    public string? TypeSourceIndexPath { get; init; }

    /// <summary>
    /// Включает семантический RAG-поиск (tool <c>ragSearch</c>). При <see langword="false"/> модель, векторная база и прогрев не подключаются, tool не публикуется.
    /// </summary>
    public bool RagSearchEnabled { get; init; } = true;
}
