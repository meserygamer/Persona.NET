using ProjectPersona.SourceIndex.Storage;

namespace ProjectPersona.SourceIndex.Contracts;

/// <summary>
/// Сохраняет индекс исходников в постоянное хранилище. Расположение задаёт реализация.
/// </summary>
public interface ITypeSourceIndexWriter
{
    /// <summary>
    /// Полностью заменяет содержимое хранилища переданным снимком.
    /// </summary>
    /// <param name="snapshot">Индекс, построенный из исходников.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Число записанных элементов.</returns>
    Task<TypeSourceIndexWriteResult> WriteAsync(
        TypeSourceIndexSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
