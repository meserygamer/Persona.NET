using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.SourceIndex.Sqlite.Writing;

/// <summary>
/// Ключ сборки в таблице <c>assembly</c>: имя и короткий TFM; отсутствующий TFM хранится как пустая строка.
/// </summary>
/// <param name="Name">Имя сборки.</param>
/// <param name="TargetFramework">Короткий TFM или пустая строка.</param>
internal readonly record struct SqliteAssemblyKey(string Name, string TargetFramework)
{
    /// <summary>
    /// Возвращает ключ сборки, которой принадлежит объявление.
    /// </summary>
    /// <param name="match">Объявление с описанием сборки.</param>
    /// <returns>Ключ сборки и TFM объявления.</returns>
    public static SqliteAssemblyKey Of(TypeSourceIndexedMatch match) =>
        new(match.Full.AssemblyName, match.Full.TargetFramework ?? string.Empty);
}
