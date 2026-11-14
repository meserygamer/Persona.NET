namespace ProjectPersona.Application.Services.RagSearchService;

/// <summary> Версия конвейера векторизации, записываемая в манифест хранилища. </summary>
public static class VectorizationPipeline
{
    /// <summary>
    /// Текущая версия. Поднимать при любом изменении, влияющем на вектор или на формат записи, которое задаётся кодом,
    /// а не настройками: сборка заголовка и текста декларации (<see cref="Pipes.SplitDeclarationToEmbedPipe"/>),
    /// L2-нормализация эмбеддинга, формат метаданных (<see cref="TypeSourceVectorMetadata"/>).
    /// Базы с другой версией считаются невалидными.
    /// </summary>
    public const int Version = 1;
}
