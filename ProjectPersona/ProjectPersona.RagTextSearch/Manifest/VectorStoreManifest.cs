namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary> Состояние векторного хранилища. </summary>
public enum VectorStoreState
{
    /// <summary> Все векторы записаны. </summary>
    Ready,
    /// <summary> Частичная ревекторизация не завершена: сборки из <see cref="VectorStoreManifest.PendingAssemblies"/> неполны. </summary>
    Updating
}

/// <summary> Сборка, обновление векторов которой не завершено. </summary>
/// <param name="Assembly">Код сборки.</param>
/// <param name="TargetFramework">Короткий TFM; <see langword="null"/> — все TFM сборки.</param>
public sealed record PendingAssembly(string Assembly, string? TargetFramework)
{
    /// <inheritdoc />
    public override string ToString() =>
        TargetFramework is null ? Assembly : $"{Assembly}:{TargetFramework}";
}

/// <summary>
/// Манифест векторного хранилища: чем и как построена основная таблица и завершено ли её обновление.
/// Хранится в служебной таблице LanceDB рядом с основной.
/// </summary>
public sealed record VectorStoreManifest
{
    /// <summary> Версия формата манифеста и таблиц, которую понимает текущий код. </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary> Версия формата манифеста и таблиц. </summary>
    public required int FormatVersion { get; init; }

    /// <summary> Версия конвейера векторизации в коде, которым построена база. </summary>
    public required int PipelineVersion { get; init; }

    /// <summary> Размерность векторов. </summary>
    public required int VectorDimension { get; init; }

    /// <summary> Жёсткая часть отпечатка эмбеддера. </summary>
    public required EmbeddingFingerprint EmbeddingFingerprint { get; init; }

    /// <summary> Мягкие параметры построения. </summary>
    public required EmbeddingBuildParameters BuildParameters { get; init; }

    /// <summary> Шаблон промпта документа, с которым строилась база; справочно. </summary>
    public required string DocumentQueryTemplate { get; init; }

    /// <summary> Шаблон поискового запроса, с которым строилась база; справочно. </summary>
    public required string SearchQueryTemplate { get; init; }

    /// <summary> Состояние хранилища. </summary>
    public VectorStoreState State { get; init; } = VectorStoreState.Ready;

    /// <summary> Сборки, обновление которых не завершено. </summary>
    public IReadOnlyList<PendingAssembly> PendingAssemblies { get; init; } = [];

    /// <summary>
    /// Отмечает начало обновления сборок: объединяет их с уже незавершёнными и переводит в <see cref="VectorStoreState.Updating"/>.
    /// </summary>
    /// <param name="assemblies">Пары «код сборки – TFM»; пустой TFM — все TFM сборки.</param>
    /// <returns>Новый манифест.</returns>
    public VectorStoreManifest WithUpdating(IEnumerable<(string Assembly, string? TargetFramework)> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var pending = PendingAssemblies.ToList();
        foreach (var key in ToPending(assemblies))
        {
            if (!pending.Contains(key))
                pending.Add(key);
        }

        return this with { State = VectorStoreState.Updating, PendingAssemblies = pending };
    }

    /// <summary>
    /// Отмечает завершение обновления сборок: убирает из незавершённых только их.
    /// <see cref="VectorStoreState.Ready"/> выставляется, только если незавершённых не осталось.
    /// </summary>
    /// <param name="assemblies">Пары «код сборки – TFM»; пустой TFM — все TFM сборки.</param>
    /// <returns>Новый манифест.</returns>
    /// <remarks>
    /// Сборка без TFM перезаписала векторы всех своих TFM, поэтому закрывает и записи этой сборки с конкретным TFM.
    /// Сборка с TFM закрывает только запись с тем же TFM.
    /// </remarks>
    public VectorStoreManifest WithCompleted(IEnumerable<(string Assembly, string? TargetFramework)> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var completed = ToPending(assemblies).ToList();
        var pending = PendingAssemblies
            .Where(item => !completed.Any(done =>
                string.Equals(done.Assembly, item.Assembly, StringComparison.Ordinal) &&
                (done.TargetFramework is null ||
                 string.Equals(done.TargetFramework, item.TargetFramework, StringComparison.Ordinal))))
            .ToList();

        return this with
        {
            State = pending.Count == 0 ? VectorStoreState.Ready : VectorStoreState.Updating,
            PendingAssemblies = pending
        };
    }

    /// <summary> Преобразовать в сборки на обновлении. </summary>
    /// <param name="assemblies"> Сборки. </param>
    /// <returns> Перечисление сборок на обновлении. </returns>
    /// <exception cref="ArgumentException"> Возникает, если имя сборки пустое. </exception>
    private static IEnumerable<PendingAssembly> ToPending(IEnumerable<(string Assembly, string? TargetFramework)> assemblies) =>
        assemblies.Select(key =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Assembly, nameof(assemblies));
            return new PendingAssembly(key.Assembly, string.IsNullOrEmpty(key.TargetFramework) ? null : key.TargetFramework);
        });
}
