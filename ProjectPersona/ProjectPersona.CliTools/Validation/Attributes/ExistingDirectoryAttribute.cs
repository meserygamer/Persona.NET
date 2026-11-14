using System.ComponentModel.DataAnnotations;

namespace ProjectPersona.CliTools.Validation.Attributes;

/// <summary>
/// Требует, чтобы строковое значение было путём к существующему каталогу. Пустое значение не проверяется: для него есть <see cref="RequiredAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ExistingDirectoryAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string { Length: > 0 } path || Directory.Exists(path))
            return ValidationResult.Success;

        return new ValidationResult(
            ErrorMessage ?? $"{validationContext.DisplayName}: directory not found: {path}",
            [validationContext.MemberName!]);
    }
}
