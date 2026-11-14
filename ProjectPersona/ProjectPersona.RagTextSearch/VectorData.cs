namespace ProjectPersona.RagTextSearch;

/// <summary> Векторные данные исходников типов. </summary>
public readonly record struct VectorData
{
    /// <summary> Вектор. </summary>
    public float[] Vector { get; init; }

    /// <summary> Код сборки, для которой выполнена векторизация. </summary>
    public string Assembly { get; init; }

    /// <summary> Короткий TFM компиляции, для которой выполнена векторизация. </summary>
    public string TargetFramework { get; init; }

    /// <summary> Метаданные. </summary>
    public string Metadata { get; init; }
};