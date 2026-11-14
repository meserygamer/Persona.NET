using ProjectPersona.LlamaWrapper;
using ProjectPersona.RagTextSearch.Llama;

namespace ProjectPersona.CliTools.Validation;

/// <summary>
/// Проверяет настройки llama из файла до очистки LanceDB: атрибуты и возможность загрузить модель.
/// </summary>
internal sealed class LlamaSettingsInputValidator : ICommandInputValidator<LlamaSettings>
{
    /// <inheritdoc />
    public async Task ValidateAsync(LlamaSettings input, CancellationToken cancellationToken = default)
    {
        await new DataAnnotationsInputValidator<LlamaSettings>().ValidateAsync(input, cancellationToken)
            .ConfigureAwait(false);

        LlamaModelSession.Validate(
            input.ModelPath,
            new LlamaLoadOptions
            {
                NativeLibraryDirectory = string.IsNullOrWhiteSpace(input.NativeLibraryDirectory)
                    ? null
                    : input.NativeLibraryDirectory,
                Devices = input.Devices,
                FlashAttn = input.FlashAttn
            });
    }
}
