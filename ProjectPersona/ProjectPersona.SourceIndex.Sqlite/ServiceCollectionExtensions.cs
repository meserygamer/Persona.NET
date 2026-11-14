using Microsoft.Extensions.DependencyInjection;
using ProjectPersona.SourceIndex.Contracts;
using ProjectPersona.SourceIndex.Sqlite.Configuration;
using ProjectPersona.SourceIndex.Sqlite.Reading;
using ProjectPersona.SourceIndex.Sqlite.Searching;
using ProjectPersona.SourceIndex.Sqlite.Writing;

namespace ProjectPersona.SourceIndex.Sqlite;

/// <summary>
/// Регистрация SQLite-хранилища индекса исходников в DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует <see cref="ITypeSourceLookupFactory"/>, <see cref="ITypeSourceIndexWriter"/> и <see cref="ITypeSourceTextSearch"/> поверх файла SQLite.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="databasePathFactory">Путь к файлу индекса; <see langword="null"/> или пустая строка — <c>BaseDirectory/type-source-index/type-source-index.db</c>.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    public static IServiceCollection AddSqliteTypeSourceIndex(
        this IServiceCollection services,
        Func<IServiceProvider, string?> databasePathFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databasePathFactory);

        services.AddSingleton(sp => new SqliteTypeSourceIndexOptions { DatabasePath = databasePathFactory(sp) });
        services.AddSingleton<ITypeSourceLookupFactory, SqliteTypeSourceLookupFactory>();
        services.AddSingleton<ITypeSourceIndexWriter, SqliteTypeSourceIndexWriter>();
        services.AddSingleton<ITypeSourceTextSearch, SqliteTypeSourceTextSearch>();
        return services;
    }
}
