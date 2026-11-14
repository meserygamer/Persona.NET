using System.ComponentModel.DataAnnotations;

namespace ProjectPersona.CliTools.Validation;

/// <summary>
/// Валидатор, проверяющий атрибуты <see cref="ValidationAttribute"/> на свойствах входных данных.
/// </summary>
/// <typeparam name="TInput">Тип проверяемых входных данных.</typeparam>
public sealed class DataAnnotationsInputValidator<TInput> : ICommandInputValidator<TInput>
    where TInput : notnull
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException">Нарушено хотя бы одно ограничение; в сообщении перечислены все.</exception>
    public Task ValidateAsync(TInput input, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true))
        {
            throw new ArgumentException(
                $"Invalid {typeof(TInput).Name}: " + string.Join("; ", errors.Select(e => e.ErrorMessage)));
        }

        return Task.CompletedTask;
    }
}
