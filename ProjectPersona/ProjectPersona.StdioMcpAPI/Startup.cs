using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectPersona.Application;
using ProjectPersona.Application.Services.RagSearchService;
using ProjectPersona.Application.Services.TextSearchService;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.RagTextSearch.LanceDB;
using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.SourceIndex.Sqlite;
using ProjectPersona.StdioMcpAPI;
using ProjectPersona.StdioMcpAPI.McpTool;
using Serilog;

AppLogging.Configure();
AppLogging.RegisterUnhandledExceptionHandlers();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json");

    var ragSearchEnabled = (builder.Configuration.Get<AppSettings>() ?? new AppSettings()).RagSearchEnabled;
    Log.Information("RAG search is {State} by configuration (ragSearchEnabled={Value})",
        ragSearchEnabled ? "enabled" : "disabled", ragSearchEnabled);

    var mcpServer = builder.Services
        .AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(dispose: true);
        })
        .Configure<AppSettings>(builder.Configuration)
        .AddSqliteTypeSourceIndex(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value.TypeSourceIndexPath)
        .AddSingleton<TypeSourceIndexHolder>()
        .AddSingleton<TypeSourceIndexService>()
        .AddSingleton<TextSearchService>()
        .AddHostedService<TypeSourceIndexStartupValidator>()
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<TypeSourceIndexTool>()
        .WithTools<TextSearchTool>();

    if (ragSearchEnabled)
    {
        builder.Services
            .Configure<LlamaSettings>(builder.Configuration.GetSection("llama"))
            .Configure<LanceDbSettings>(builder.Configuration.GetSection("lanceDb"))
            .AddSingleton(sp => sp.GetRequiredService<IOptions<LlamaSettings>>().Value)
            .AddSingleton(sp => sp.GetRequiredService<IOptions<LanceDbSettings>>().Value)
            .AddRagSearch();
        mcpServer.WithTools<RagSearchTool>();
    }

    await builder
        .Build()
        .RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось из-за неперехваченной ошибки");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
