using System.ComponentModel.DataAnnotations;

namespace ProjectPersona.WebMcpAPI;

/// <summary>
/// Настройки HTTPS из секции <c>httpsSettings</c>. Используются, только если <see cref="WebMcpAppSettings.HttpsEnable"/> включён.
/// </summary>
public class HttpsSettings : IValidatableObject
{
    /// <summary>
    /// Порт HTTPS-сервера. Значение по умолчанию — <c>7198</c>.
    /// </summary>
    [Range(1, 65535, ErrorMessage = "httpsSettings.port должен быть в диапазоне 1–65535.")]
    public int Port { get; init; } = 7198;

    /// <summary>
    /// Путь к сертификату: <c>.pfx</c>/<c>.p12</c> (PKCS#12) либо <c>.crt</c>/<c>.pem</c>/<c>.cer</c> (PEM).
    /// </summary>
    public string? CertificatePath { get; init; }

    /// <summary>
    /// Путь к файлу приватного ключа (<c>.key</c>/<c>.pem</c>) для PEM-сертификата.
    /// Не нужен, если ключ лежит в том же файле, что и сертификат, и для PFX.
    /// </summary>
    public string? KeyPath { get; init; }

    /// <summary>
    /// Пароль к PFX либо к зашифрованному PEM-ключу. Для незащищённых файлов — <c>null</c> или пустая строка.
    /// </summary>
    public string? CertificatePassword { get; init; }

    /// <summary> Валидировать объект. </summary>
    /// <param name="validationContext"> Контекст валидации. </param>
    /// <returns> Перечисление результатов валидации. </returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(CertificatePath))
            yield return new ValidationResult("httpsSettings.certificatePath обязателен.", [nameof(CertificatePath)]);
        else if (!File.Exists(CertificatePath))
            yield return new ValidationResult($"Файл сертификата не найден: {CertificatePath}", [nameof(CertificatePath)]);

        if (!string.IsNullOrWhiteSpace(KeyPath) && !File.Exists(KeyPath))
            yield return new ValidationResult($"Файл ключа не найден: {KeyPath}", [nameof(KeyPath)]);
    }
}
