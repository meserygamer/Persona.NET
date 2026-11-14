namespace ProjectPersona.CliTools.Validation;

/// <summary>
/// Валидатор входных данных CLI-команды.
/// </summary>
/// <typeparam name="TInput">Тип проверяемых входных данных.</typeparam>
public interface ICommandInputValidator<in TInput>
{
    /// <summary>
    /// Проверяет входные данные до начала выполнения команды.
    /// </summary>
    /// <param name="input">Проверяемые данные.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся, если данные корректны.</returns>
    /// <exception cref="ArgumentException">Данные некорректны.</exception>
    /// <exception cref="DirectoryNotFoundException">Требуемый каталог не существует.</exception>
    Task ValidateAsync(TInput input, CancellationToken cancellationToken = default);
}
