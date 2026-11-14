using Serilog;
using Serilog.Events;

namespace ProjectPersona.WebMcpAPI;

/// <summary>
/// Общая конфигурация Serilog для web MCP-сервера.
/// </summary>
internal static class AppLogging
{
    /// <summary>
    /// Создаёт глобальный логгер: файл в <c>BaseDirectory/logs</c> и stderr только для <see cref="LogEventLevel.Error"/> и выше.
    /// </summary>
    public static void Configure()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day)
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Error,
                standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();
    }

    /// <summary>
    /// Подписывает глобальные обработчики неперехваченных исключений домена и задач.
    /// </summary>
    public static void RegisterUnhandledExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Необработанное исключение домена приложения");
            else
                Log.Fatal("Необработанное исключение домена приложения: {ExceptionObject}", e.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Ненаблюдаемое исключение задачи");
            e.SetObserved();
        };
    }
}
