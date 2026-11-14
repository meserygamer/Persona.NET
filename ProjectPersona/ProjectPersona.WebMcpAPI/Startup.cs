using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using ProjectPersona.Application;
using ProjectPersona.Application.Services.RagSearchService;
using ProjectPersona.Application.Services.TextSearchService;
using ProjectPersona.Application.Services.TypeSourceIndexService;
using ProjectPersona.RagTextSearch;
using ProjectPersona.RagTextSearch.LanceDB;
using ProjectPersona.RagTextSearch.Llama;
using ProjectPersona.SourceIndex.Sqlite;
using ProjectPersona.WebMcpAPI;
using ProjectPersona.WebMcpAPI.McpTool;
using Serilog;

AppLogging.Configure();
AppLogging.RegisterUnhandledExceptionHandlers();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json");

    builder.Host.UseSerilog();

    var settings = builder.Configuration.Get<WebMcpAppSettings>() ?? new WebMcpAppSettings();

    var validationResults = new List<ValidationResult>();
    if (!Validator.TryValidateObject(settings, new ValidationContext(settings), validationResults, validateAllProperties: true))
        throw new InvalidOperationException(
            "Некорректные настройки: " + string.Join("; ", validationResults.Select(r => r.ErrorMessage)));

    var ip = IPAddress.Parse(settings.Host);

    // IPv6 в URL заключается в квадратные скобки: http://[::]:5095
    var hostInUrl = ip.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ip}]" : ip.ToString();
    var urls = new List<string> { $"http://{hostInUrl}:{settings.Port}" };

    if (settings is { HttpsEnable: true, HttpsSettings: { } https })
    {
        urls.Add($"https://{hostInUrl}:{https.Port}");

        var certificate = HttpsCertificateLoader.Load(https);
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.ConfigureHttpsDefaults(options => options.ServerCertificate = certificate));
    }

    builder.WebHost.UseUrls(urls.ToArray());

    Log.Information("RAG search is {State} by configuration (ragSearchEnabled={Value})",
        settings.RagSearchEnabled ? "enabled" : "disabled", settings.RagSearchEnabled);

    var mcpServer = builder.Services
        .Configure<AppSettings>(builder.Configuration)
        .Configure<WebMcpAppSettings>(builder.Configuration)
        .AddSqliteTypeSourceIndex(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value.TypeSourceIndexPath)
        .AddSingleton<TypeSourceIndexHolder>()
        .AddSingleton<TypeSourceIndexService>()
        .AddSingleton<TextSearchService>()
        .AddHostedService<TypeSourceIndexStartupValidator>()
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<TypeSourceIndexTool>()
        .WithTools<TextSearchTool>();

    if (settings.RagSearchEnabled)
    {
        builder.Services
            .Configure<LlamaSettings>(builder.Configuration.GetSection("llama"))
            .Configure<LanceDbSettings>(builder.Configuration.GetSection("lanceDb"))
            .AddSingleton(sp => sp.GetRequiredService<IOptions<LlamaSettings>>().Value)
            .AddSingleton(sp => sp.GetRequiredService<IOptions<LanceDbSettings>>().Value)
            .AddRagSearch();
        mcpServer.WithTools<RagSearchTool>();
    }

    var app = builder.Build();

    app.MapMcp("/mcp");

    await app.RunAsync();
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
