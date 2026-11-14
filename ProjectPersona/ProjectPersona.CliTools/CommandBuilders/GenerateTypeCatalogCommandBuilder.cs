using System.CommandLine;
using ProjectPersona.CliTools.Validation;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary> Фабрика команды генерации каталога документации для типов в сборке. </summary>
public class GenerateTypeCatalogCommandBuilder
{
    /// <summary> Текущий обработчик. </summary>
    private Func<GenerateTypeCatalogOptions, Task>? _handler;

    /// <summary> Валидатор сформированного объекта опций. </summary>
    private ICommandInputValidator<GenerateTypeCatalogOptions>? _validator;

    /// <summary> Установить обработчик команды. </summary>
    /// <param name="handler"> Обработчик команды. </param>
    /// <returns> Фабрика. </returns>
    public GenerateTypeCatalogCommandBuilder SetHandler(Func<GenerateTypeCatalogOptions, Task>? handler)
    {
        _handler = handler;
        return this;
    }
    
    /// <summary> Установить валидатор объекта опций, который запускается перед обработчиком. </summary>
    /// <param name="validator"> Валидатор опций. </param>
    /// <returns> Фабрика. </returns>
    public GenerateTypeCatalogCommandBuilder SetValidator(ICommandInputValidator<GenerateTypeCatalogOptions>? validator)
    {
        _validator = validator;
        return this;
    }

    /// <summary> Создать команду генерации каталога документации для типов в сборке. </summary>
    /// <returns> Команда. </returns>
    public Command Build()
    {
        var solutionPathArgument = new Argument<string>("SolutionPath")
        {
            Description = "The path to the solution file"
        };
        
        var outputPathArgument = new Argument<string>("OutputPath")
        {
            Description = "The path where the directory with .md will be created"
        };
        
        var command = new Command("generate-type-catalog", "Generate type catalog with .md documentation for each .dll library")
        {
            Arguments = { solutionPathArgument, outputPathArgument },
        };

        if (_handler is not null)
        {
            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var options = new GenerateTypeCatalogOptions(
                    parseResult.GetRequiredValue<string>(solutionPathArgument),
                    parseResult.GetRequiredValue<string>(outputPathArgument));

                await RunAsync(options, cancellationToken);
            });
        }

        return command;
    }

    /// <summary> Проверить опции валидатором и передать их обработчику. </summary>
    /// <param name="options"> Сформированные опции. </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    private async Task RunAsync(GenerateTypeCatalogOptions options, CancellationToken cancellationToken)
    {
        if (_validator is not null)
            await _validator.ValidateAsync(options, cancellationToken);

        await _handler!(options);
    }
}
