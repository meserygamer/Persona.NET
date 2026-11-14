using System.ComponentModel.DataAnnotations;
using System.Net;

namespace ProjectPersona.WebMcpAPI;

/// <summary>
/// Проверяет, что строка является корректным IPv4- или IPv6-адресом. <c>null</c> считается валидным — его отсекает <see cref="RequiredAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IpAddressAttribute : ValidationAttribute
{
    public IpAddressAttribute(): base("Значение должно быть корректным IP-адресом."){}

    public override bool IsValid(object? value) =>
        value is null || (value is string text && IPAddress.TryParse(text, out _));
}
