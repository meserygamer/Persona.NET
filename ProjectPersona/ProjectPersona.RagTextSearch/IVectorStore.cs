using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.RagTextSearch;

/// <summary> Хранилище векторов. </summary>
public interface IVectorStore : IDisposable
{
    /// <summary> Добавить векторы в хранилище. </summary>
    /// <param name="vectors"> Векторы. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Задача на добавление. </returns>
    ValueTask AddVectorsAsync(IEnumerable<VectorData>? vectors, CancellationToken ct = default);
    
    /// <summary> Проверить, существование хранилища. </summary>
    /// <param name="ct"> Токен отмены операции. </param>
    /// <returns> Существование хранилища. </returns>
    /// <remarks>
    /// Сбой доступа к хранилищу пробрасывается; <see langword="false"/> не используется как признак недоступности.
    /// </remarks>
    ValueTask<bool> StoreExistsAsync(CancellationToken ct = default);
    
    /// <summary> Опустошить хранилище: хранилище остаётся, но данные полностью очищаются. </summary>
    /// <param name="ct"> Токен отмены операции. </param>
    /// <returns> Задача очистки. Если хранилища нет, метод завершается без изменений. </returns>
    /// <remarks>
    /// После успешной очистки <see cref="StoreExistsAsync"/> возвращает <see langword="true"/>: это не удаление хранилища.
    /// Сбой доступа к хранилищу пробрасывается.
    /// </remarks>
    ValueTask ClearStoreAsync(CancellationToken ct = default);

    /// <summary> Очистить векторы конкретных сборок. Остальные сборки и структура хранилища не затрагиваются. </summary>
    /// <param name="assemblies">
    /// Пары «код сборки – короткий TFM компиляции», векторы которых нужно удалить.
    /// Если TFM равен <see langword="null"/> или пустой, удаляются векторы сборки для всех TFM. Пустая коллекция ничего не удаляет.
    /// </param>
    /// <param name="ct"> Токен отмены операции. </param>
    /// <returns> Задача очистки. Если хранилища нет, метод завершается без изменений. </returns>
    /// <exception cref="ArgumentNullException"> Коллекция равна null. </exception>
    /// <exception cref="ArgumentException"> Код сборки пустой. </exception>
    ValueTask ClearAssembliesAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies, CancellationToken ct = default);

    /// <summary> Найти ближайшие вектора. </summary>
    /// <param name="vector"> Поисковый вектор. </param>
    /// <param name="maximumVectors"> Максимальное количество векторов. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Массив метаданных ближайших векторов. </returns>
    ValueTask<VectorData[]> SearchNearestVectorsAsync(float[] vector, int maximumVectors, CancellationToken ct = default);

    /// <summary> Оптимизировать хранение. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Задача на оптимизацию. </returns>
    ValueTask OptimizeAsync(CancellationToken ct = default);

    /// <summary> Прочитать манифест хранилища. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <returns> Манифест; <see langword="null"/>, если его нет. </returns>
    /// <exception cref="InvalidOperationException"> Манифест повреждён. </exception>
    ValueTask<VectorStoreManifest?> ReadManifestAsync(CancellationToken ct = default);

    /// <summary> Записать манифест хранилища, заменив прежний. </summary>
    /// <param name="manifest"> Манифест. </param>
    /// <param name="ct"> Токен отмены. </param>
    ValueTask WriteManifestAsync(VectorStoreManifest manifest, CancellationToken ct = default);

    /// <summary> Удалить манифест хранилища. Если его нет — ничего не делает. </summary>
    /// <param name="ct"> Токен отмены. </param>
    /// <remarks> Хранилище без манифеста считается невалидным. </remarks>
    ValueTask DeleteManifestAsync(CancellationToken ct = default);

    /// <summary>
    /// Отметить начало обновления сборок: <see cref="VectorStoreState.Updating"/> и объединение сборок
    /// с уже незавершёнными (<see cref="VectorStoreManifest.WithUpdating"/>).
    /// </summary>
    /// <param name="assemblies"> Пары «код сборки – TFM»; пустой TFM — все TFM сборки. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <exception cref="InvalidOperationException"> Манифеста нет или он повреждён. </exception>
    ValueTask MarkUpdatingAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies, CancellationToken ct = default);

    /// <summary>
    /// Отметить завершение обновления сборок: из незавершённых убираются только они,
    /// <see cref="VectorStoreState.Ready"/> — если незавершённых не осталось (<see cref="VectorStoreManifest.WithCompleted"/>).
    /// </summary>
    /// <param name="assemblies"> Пары «код сборки – TFM»; пустой TFM — все TFM сборки. </param>
    /// <param name="ct"> Токен отмены. </param>
    /// <exception cref="InvalidOperationException"> Манифеста нет или он повреждён. </exception>
    ValueTask MarkReadyAsync(IEnumerable<(string Assembly, string? TargetFramework)> assemblies, CancellationToken ct = default);
}