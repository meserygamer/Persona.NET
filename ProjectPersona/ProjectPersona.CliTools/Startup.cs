using System.CommandLine;
using ProjectPersona.CliTools.CommandBuilders;
using ProjectPersona.CliTools.CommandHandlers;
using ProjectPersona.CliTools.Validation;

namespace ProjectPersona.CliTools;

public static class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CLI tools for project persona")
        {
            Subcommands =
            {
                new GenerateTypeCatalogCommandBuilder()
                    .SetValidator(new DataAnnotationsInputValidator<GenerateTypeCatalogOptions>())
                    .SetHandler(GenerationCli.GenerateTypeCatalogAsync).Build(),
                new GenerateTypeSourceIndexCommandBuilder()
                    .SetValidator(new DataAnnotationsInputValidator<GenerateTypeSourceIndexOptions>())
                    .SetHandler(GenerationCli.GenerateTypeSourceIndexAsync).Build(),
                new GenerateTypeArtifactsCommandBuilder()
                    .SetValidator(new DataAnnotationsInputValidator<GenerateTypeArtifactsOptions>())
                    .SetHandler(GenerationCli.GenerateTypeArtifactsAsync).Build(),
                new VectorizeTypeSourceIndexCommandBuilder()
                    .SetValidator(new DataAnnotationsInputValidator<VectorizeTypeSourceIndexOptions>())
                    .SetHandler(VectorizeTypeSourceIndexCli.RunAsync)
                    .Build(),
                new RevectorizeAssembliesCommandBuilder()
                    .SetValidator(new DataAnnotationsInputValidator<RevectorizeAssembliesOptions>())
                    .SetHandler(RevectorizeAssembliesCli.RunAsync)
                    .Build()
            }
        };
        
        var command = rootCommand.Parse(args);
        return await command.InvokeAsync();
    }
}