using System.CommandLine;
using ProjectPersona.CliTools.Validation;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Фабрика команды одновременной генерации type-catalog и type-source-index.
/// </summary>
public class GenerateTypeArtifactsCommandBuilder
{
    /// <summary>
    /// Текущий обработчик.
    /// </summary>
    private Func<GenerateTypeArtifactsOptions, Task>? _handler;

    /// <summary> Валидатор сформированного объекта опций. </summary>
    private ICommandInputValidator<GenerateTypeArtifactsOptions>? _validator;

    /// <summary>
    /// Устанавливает обработчик команды.
    /// </summary>
    /// <param name="handler">Обработчик команды.</param>
    /// <returns>Фабрика.</returns>
    public GenerateTypeArtifactsCommandBuilder SetHandler(Func<GenerateTypeArtifactsOptions, Task>? handler)
    {
        _handler = handler;
        return this;
    }

    /// <summary>
    /// Устанавливает валидатор объекта опций, который запускается перед обработчиком.
    /// </summary>
    /// <param name="validator">Валидатор опций.</param>
    /// <returns>Фабрика.</returns>
    public GenerateTypeArtifactsCommandBuilder SetValidator(ICommandInputValidator<GenerateTypeArtifactsOptions>? validator)
    {
        _validator = validator;
        return this;
    }

    /// <summary>
    /// Создаёт команду генерации type-catalog и type-source-index за одно открытие решения.
    /// </summary>
    /// <returns>Команда.</returns>
    public Command Build()
    {
        var solutionPathArgument = new Argument<string>("SolutionPath")
        {
            Description = "The path to the solution file"
        };

        var catalogOutputPathArgument = new Argument<string>("CatalogOutputPath")
        {
            Description = "The path where the directory with .md will be created"
        };

        var indexOutputPathArgument = new Argument<string>("IndexOutputPath")
        {
            Description = "The path to the directory where type-source-index.db will be created"
        };

        var command = new Command(
            "generate-type-artifacts",
            "Generate type catalog and type source index in a single solution load")
        {
            Arguments = { solutionPathArgument, catalogOutputPathArgument, indexOutputPathArgument },
        };

        if (_handler is not null)
        {
            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var options = new GenerateTypeArtifactsOptions(
                    parseResult.GetRequiredValue<string>(solutionPathArgument),
                    parseResult.GetRequiredValue<string>(catalogOutputPathArgument),
                    parseResult.GetRequiredValue<string>(indexOutputPathArgument));

                await RunAsync(options, cancellationToken);
            });
        }

        return command;
    }

    /// <summary> Проверить опции валидатором и передать их обработчику. </summary>
    /// <param name="options"> Сформированные опции. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    private async Task RunAsync(GenerateTypeArtifactsOptions options, CancellationToken cancellationToken)
    {
        if (_validator is not null)
            await _validator.ValidateAsync(options, cancellationToken);

        await _handler!(options);
    }
}
