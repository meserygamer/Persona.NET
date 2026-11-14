using System.ComponentModel.DataAnnotations;

namespace ProjectPersona.CliTools.Validation.Attributes;

/// <summary>
/// Требует непустой список селекторов сборок вида <c>Name</c> или <c>Name:tfm</c> с непустым именем.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class AssemblySelectorsAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberNames = new[] { validationContext.MemberName! };

        if (value is not IReadOnlyCollection<string> { Count: > 0 } selectors)
            return new ValidationResult("At least one --assembly is required.", memberNames);

        foreach (var selector in selectors)
        {
            var separator = selector.LastIndexOf(':');
            var name = (separator < 0 ? selector : selector[..separator]).Trim();

            if (name.Length == 0)
            {
                return new ValidationResult(
                    $"Invalid --assembly value '{selector}': assembly name is empty.",
                    memberNames);
            }
        }

        return ValidationResult.Success;
    }
}
