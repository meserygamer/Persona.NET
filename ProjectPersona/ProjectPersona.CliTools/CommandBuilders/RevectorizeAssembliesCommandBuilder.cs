using System.CommandLine;
using ProjectPersona.CliTools.Validation;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Фабрика команды частичной перевекторизации отдельных сборок в LanceDB.
/// </summary>
public class RevectorizeAssembliesCommandBuilder
{
    /// <summary>
    /// Текущий обработчик.
    /// </summary>
    private Func<RevectorizeAssembliesOptions, Task>? _handler;

    /// <summary> Валидатор сформированного объекта опций. </summary>
    private ICommandInputValidator<RevectorizeAssembliesOptions>? _validator;

    /// <summary>
    /// Устанавливает обработчик команды.
    /// </summary>
    /// <param name="handler">Обработчик команды.</param>
    /// <returns>Фабрика.</returns>
    public RevectorizeAssembliesCommandBuilder SetHandler(Func<RevectorizeAssembliesOptions, Task>? handler)
    {
        _handler = handler;
        return this;
    }

    /// <summary>
    /// Устанавливает валидатор объекта опций, который запускается перед обработчиком.
    /// </summary>
    /// <param name="validator">Валидатор опций.</param>
    /// <returns>Фабрика.</returns>
    public RevectorizeAssembliesCommandBuilder SetValidator(ICommandInputValidator<RevectorizeAssembliesOptions>? validator)
    {
        _validator = validator;
        return this;
    }

    /// <summary>
    /// Создаёт команду перевекторизации сборок.
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
            Description = "The path to the existing LanceDB vector store directory"
        };

        var assemblyOption = new Option<string[]>("--assembly")
        {
            Description = "Assembly to re-vectorize: 'Name' (all target frameworks) or 'Name:tfm'. Can be repeated",
            Required = true,
            AllowMultipleArgumentsPerToken = true
        };

        var vectorDimensionOption = new Option<int?>("--vector-dimension")
        {
            Description = "Expected vector dimension; read from the LanceDB manifest. If given, must match the manifest"
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
            "revectorize-assemblies",
            "Replace LanceDB vectors of the given assemblies using the current type source index")
        {
            Arguments = { indexPathArgument, lanceDbPathArgument },
            Options =
            {
                assemblyOption,
                vectorDimensionOption,
                tableNameOption,
                llamaOptionsOption
            }
        };

        if (_handler is not null)
        {
            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var options = new RevectorizeAssembliesOptions(
                    parseResult.GetRequiredValue<string>(indexPathArgument),
                    parseResult.GetRequiredValue<string>(lanceDbPathArgument),
                    parseResult.GetValue(vectorDimensionOption),
                    parseResult.GetValue(tableNameOption),
                    parseResult.GetRequiredValue<string>(llamaOptionsOption),
                    parseResult.GetRequiredValue<string[]>(assemblyOption));

                await RunAsync(options, cancellationToken);
            });
        }

        return command;
    }

    /// <summary> Проверить опции валидатором и передать их обработчику. </summary>
    /// <param name="options"> Сформированные опции. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    private async Task RunAsync(RevectorizeAssembliesOptions options, CancellationToken cancellationToken)
    {
        if (_validator is not null)
            await _validator.ValidateAsync(options, cancellationToken);

        await _handler!(options);
    }
}
