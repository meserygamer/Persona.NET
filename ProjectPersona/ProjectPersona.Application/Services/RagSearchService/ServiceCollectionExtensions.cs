using Microsoft.Extensions.DependencyInjection;
using ProjectPersona.RagTextSearch;
using ProjectPersona.Application.Services.RagSearchService.Pipes;

namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Регистрация векторизации type-source-index в DI. </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует всё для MCP-tool <c>ragSearch</c>: эмбеддер и векторную базу, <see cref="RagIndexStateHolder"/>,
    /// <see cref="RagSearchService"/> и прогрев при старте. Требует <c>LlamaSettings</c> и <c>LanceDbSettings</c> в контейнере.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    public static IServiceCollection AddRagSearch(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRagTextSearch();
        services.AddSingleton<RagIndexStateHolder>();
        services.AddSingleton<RagSearchService>();
        services.AddHostedService<RagIndexWarmupService>();
        return services;
    }

    /// <summary>
    /// Регистрирует <see cref="TypeSourceIndexVectorizerV2"/> и её пайпы.
    /// Фабрики создают новые пайпы на каждый запуск векторизации.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    public static IServiceCollection AddTypeSourceIndexVectorizer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<TypeSourceProducerPipe>();
        services.AddTransient<SplitDeclarationToEmbedPipe>();
        services.AddTransient<EmbeddingPipe>();
        services.AddTransient<InsertVectorDataInDbPipe>();

        services.AddSingleton<Func<TypeSourceProducerPipe>>(sp =>
            () => sp.GetRequiredService<TypeSourceProducerPipe>());
        // Пайп с параметрами (набор сборок) создаётся вручную, остальные зависимости берутся из контейнера.
        services.AddSingleton<Func<IEnumerable<(string Assembly, string? TargetFramework)>, AssemblyTypeSourceProducerPipe>>(sp =>
            assemblies => ActivatorUtilities.CreateInstance<AssemblyTypeSourceProducerPipe>(sp, assemblies));
        services.AddSingleton<Func<SplitDeclarationToEmbedPipe>>(sp =>
            () => sp.GetRequiredService<SplitDeclarationToEmbedPipe>());
        services.AddSingleton<Func<EmbeddingPipe>>(sp =>
            () => sp.GetRequiredService<EmbeddingPipe>());
        services.AddSingleton<Func<InsertVectorDataInDbPipe>>(sp =>
            () => sp.GetRequiredService<InsertVectorDataInDbPipe>());

        services.AddSingleton<TypeSourceIndexVectorizerV2>();
        return services;
    }
}
