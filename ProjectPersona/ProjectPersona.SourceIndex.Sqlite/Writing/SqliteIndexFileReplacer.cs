namespace ProjectPersona.SourceIndex.Sqlite.Writing;

/// <summary>
/// Заменяет файл индекса готовым временным файлом; повторяет попытку, если целевой файл занят другим процессом.
/// </summary>
internal static class SqliteIndexFileReplacer
{
    /// <summary>Число попыток замены файла.</summary>
    private const int ReplaceAttempts = 5;

    /// <summary>Пауза между попытками замены файла.</summary>
    private static readonly TimeSpan ReplaceRetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Перемещает временный файл поверх целевого.
    /// </summary>
    /// <param name="tempPath">Готовый временный файл.</param>
    /// <param name="databasePath">Целевой файл индекса.</param>
    /// <exception cref="InvalidOperationException">Целевой файл занят после всех попыток.</exception>
    public static void Replace(string tempPath, string databasePath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(tempPath, databasePath, overwrite: true);
                return;
            }
            catch (IOException ex)
            {
                if (attempt >= ReplaceAttempts)
                {
                    throw new InvalidOperationException(
                        $"Cannot replace type source index '{databasePath}': the file is in use. Stop the MCP server and retry.",
                        ex);
                }

                Thread.Sleep(ReplaceRetryDelay);
            }
        }
    }
}
