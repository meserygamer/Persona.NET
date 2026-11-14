using System.Security.Cryptography.X509Certificates;

namespace ProjectPersona.WebMcpAPI;

/// <summary>
/// Загружает серверный сертификат с приватным ключом из PFX или PEM (<c>.crt</c>/<c>.pem</c>).
/// </summary>
public static class HttpsCertificateLoader
{
    private static readonly string[] Pkcs12Extensions = [".pfx", ".p12"];

    public static X509Certificate2 Load(HttpsSettings settings)
    {
        var path = settings.CertificatePath!;
        var password = string.IsNullOrEmpty(settings.CertificatePassword) ? null : settings.CertificatePassword;

        if (Pkcs12Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            return X509CertificateLoader.LoadPkcs12FromFile(path, password);

        var keyPath = string.IsNullOrWhiteSpace(settings.KeyPath) ? null : settings.KeyPath;
        using var pem = password is null
            ? X509Certificate2.CreateFromPemFile(path, keyPath)
            : X509Certificate2.CreateFromEncryptedPemFile(path, password, keyPath);

        // Ключ из PEM эфемерный: на Windows SChannel не может использовать такой ключ для TLS,
        // поэтому пересобираем сертификат через PKCS#12.
        return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
    }
}
