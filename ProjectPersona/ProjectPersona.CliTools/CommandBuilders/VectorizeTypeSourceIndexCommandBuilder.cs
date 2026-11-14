using System.CommandLine;
using ProjectPersona.CliTools.Validation;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Фабрика команды векторизации type-source-index в LanceDB.
/// </summary>
public class VectorizeTypeSourceIndexCommandBuilder
{
    /// <summary>
    /// Текущий обработчик.
    /// </summary>
    private Func<VectorizeTypeSourceIndexOptions, Task>? _handler;

    /// <summary> Валидатор сформированного объекта опций. </summary>
    private ICommandInputValidator<VectorizeTypeSourceIndexOptions>? _validator;

    /// <summary>
    /// Устанавливает обработчик команды.
    /// </summary>
    /// <param name="handler">Обработчик команды.</param>
    /// <returns>Фабрика.</returns>
    public VectorizeTypeSourceIndexCommandBuilder SetHandler(Func<VectorizeTypeSourceIndexOptions, Task>? handler)
    {
        _handler = handler;
        return this;
    }

    /// <summary>
    /// Устанавливает валидатор объекта опций, который запускается перед обработчиком.
    /// </summary>
    /// <param name="validator">Валидатор опций.</param>
    /// <returns>Фабрика.</returns>
    public VectorizeTypeSourceIndexCommandBuilder SetValidator(ICommandInputValidator<VectorizeTypeSourceIndexOptions>? validator)
    {
        _validator = validator;
        return this;
    }

    /// <summary>
    /// Создаёт команду векторизации индекса исходников.
    /// </summary>
    /// <returns>Команда.</returns>
    public Command Build()
    {
        var indexPathArgument = new Argument<string>("IndexPath")
        {
            Description = "The path to the type source index directory containing type-source-index.db"
        };

        var lanceDbPathArgument = new Argument<string>("LanceDbPath")
        {
            Description = "The path where the LanceDB vector store directory will be created or updated"
        };

        var vectorDimensionOption = new Option<int>("--vector-dimension")
        {
            Description = "Vector dimension stored in LanceDB",
            Required = true
        };

        var tableNameOption = new Option<string?>("--table-name")
        {
            Description = "LanceDB table name (default: type_source_index)"
        };

        var llamaOptionsOption = new Option<string>("--llama-options")
        {
            Description = "Path to the JSON file with llama options (model-path, batchSize, nCtx, documentQueryTemplate, ...)",
            Required = true
        };

        var command = new Command(
            "vectorize-type-source-index",
            "Build LanceDB vectors from an existing type source index")
        {
            Arguments = { indexPathArgument, lanceDbPathArgument },
            Options =
            {
                vectorDimensionOption,
                tableNameOption,
                llamaOptionsOption
            }
        };

        if (_handler is not null)
        {
            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var options = new VectorizeTypeSourceIndexOptions(
                    parseResult.GetRequiredValue<string>(indexPathArgument),
                    parseResult.GetRequiredValue<string>(lanceDbPathArgument),
                    parseResult.GetRequiredValue<int>(vectorDimensionOption),
                    parseResult.GetValue(tableNameOption),
                    parseResult.GetRequiredValue<string>(llamaOptionsOption));

                await RunAsync(options, cancellationToken);
            });
        }

        return command;
    }

    /// <summary> Проверить опции валидатором и передать их обработчику. </summary>
    /// <param name="options"> Сформированные опции. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    private async Task RunAsync(VectorizeTypeSourceIndexOptions options, CancellationToken cancellationToken)
    {
        if (_validator is not null)
            await _validator.ValidateAsync(options, cancellationToken);

        await _handler!(options);
    }
}
