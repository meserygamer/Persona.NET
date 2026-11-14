using System.Text.Json;
using ProjectPersona.RagTextSearch.Llama;

namespace ProjectPersona.CliTools;

/// <summary>
/// Чтение <see cref="LlamaSettings"/> из JSON-файла (шаблона опций llama).
/// </summary>
internal static class LlamaSettingsFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>
    /// Загружает настройки llama из JSON-файла.
    /// </summary>
    /// <param name="path">Путь к файлу шаблона.</param>
    /// <returns>Настройки llama; пути и строки нормализованы.</returns>
    /// <exception cref="FileNotFoundException">Файл не найден.</exception>
    /// <exception cref="ArgumentException">Файл не является корректным JSON-шаблоном.</exception>
    public static LlamaSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Llama options file not found: {path}", path);

        LlamaSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<LlamaSettings>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid llama options file '{path}': {ex.Message}", ex);
        }

        if (settings is null)
            throw new ArgumentException($"Llama options file '{path}' is empty.");

        return settings with
        {
            ModelPath = settings.ModelPath.Trim(),
            NativeLibraryDirectory = settings.NativeLibraryDirectory.Trim(),
            Devices = string.IsNullOrWhiteSpace(settings.Devices) ? null : settings.Devices.Trim(),
            FlashAttn = string.IsNullOrWhiteSpace(settings.FlashAttn) ? null : settings.FlashAttn.Trim()
        };
    }
}
