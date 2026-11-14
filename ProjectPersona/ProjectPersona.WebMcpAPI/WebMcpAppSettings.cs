using System.ComponentModel.DataAnnotations;
using ProjectPersona.Application;

namespace ProjectPersona.WebMcpAPI;

/// <summary>
/// Настройки web MCP-сервера из <c>appsettings.json</c>: расширяют общие <see cref="AppSettings"/> полями хоста.
/// </summary>
public class WebMcpAppSettings : AppSettings, IValidatableObject
{
    /// <summary>
    /// Порт HTTP-сервера на <c>localhost</c>. Значение по умолчанию — <c>5095</c>.
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Порт должен быть в диапазоне 1–65535.")]
    public int Port { get; init; } = 5095;

    /// <summary>
    /// IP-адрес, на котором слушает HTTP-сервер. Значение по умолчанию — <c>0.0.0.0</c> (все IPv4-интерфейсы).
    /// </summary>
    [Required(ErrorMessage = "Host обязателен.")]
    [IpAddress]
    public string Host { get; init; } = "0.0.0.0";

    /// <summary>
    /// Включает HTTPS. Если <c>false</c> (по умолчанию) — сервер слушает только HTTP.
    /// </summary>
    public bool HttpsEnable { get; init; }

    /// <summary>
    /// Параметры HTTPS (порт, сертификат). Обязательны, если <see cref="HttpsEnable"/> включён.
    /// </summary>
    public HttpsSettings? HttpsSettings { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!HttpsEnable)
            yield break;

        if (HttpsSettings is null)
        {
            yield return new ValidationResult("httpsSettings обязателен, если httpsEnable включён.", [nameof(HttpsSettings)]);
            yield break;
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(HttpsSettings, new ValidationContext(HttpsSettings), results, validateAllProperties: true);
        foreach (var result in results)
            yield return result;

        if (HttpsSettings.Port == Port)
            yield return new ValidationResult("httpsSettings.port должен отличаться от port.", [nameof(HttpsSettings)]);
    }
}
