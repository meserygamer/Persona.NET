using System.ComponentModel.DataAnnotations;
using ProjectPersona.CliTools.Validation.Attributes;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Параметры подкоманды <c>vectorize-type-source-index</c>.
/// </summary>
/// <param name="IndexPath">Каталог с SQLite-файлом type-source-index.</param>
/// <param name="LanceDbPath">Каталог LanceDB.</param>
/// <param name="VectorDimension">Размерность вектора.</param>
/// <param name="TableName">Имя таблицы LanceDB или <see langword="null"/> для значения по умолчанию.</param>
/// <param name="LlamaOptionsPath">Путь к JSON-файлу с настройками llama.</param>
public sealed record VectorizeTypeSourceIndexOptions(
    [property: Required, ExistingDirectory] string IndexPath,
    [property: Required] string LanceDbPath,
    [property: Range(1, int.MaxValue, ErrorMessage = "--vector-dimension must be at least 1.")] int VectorDimension,
    string? TableName,
    [property: Required, ExistingFile] string LlamaOptionsPath);
