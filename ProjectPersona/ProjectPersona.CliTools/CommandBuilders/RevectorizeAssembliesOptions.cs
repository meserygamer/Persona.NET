using System.ComponentModel.DataAnnotations;
using ProjectPersona.CliTools.Validation.Attributes;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Параметры подкоманды <c>revectorize-assemblies</c>.
/// </summary>
/// <param name="IndexPath">Каталог type-source-index.</param>
/// <param name="LanceDbPath">Каталог существующей LanceDB.</param>
/// <param name="VectorDimension">Ожидаемая размерность; <see langword="null"/> — взять из манифеста.</param>
/// <param name="TableName">Имя таблицы LanceDB; <see langword="null"/> — по умолчанию.</param>
/// <param name="LlamaOptionsPath">Путь к JSON-файлу настроек llama.</param>
/// <param name="Assemblies">Значения <c>--assembly</c> вида <c>Name</c> или <c>Name:tfm</c>.</param>
public sealed record RevectorizeAssembliesOptions(
    [property: Required, ExistingDirectory] string IndexPath,
    [property: Required] string LanceDbPath,
    [property: Range(1, int.MaxValue, ErrorMessage = "--vector-dimension must be at least 1.")] int? VectorDimension,
    string? TableName,
    [property: Required, ExistingFile] string LlamaOptionsPath,
    [property: AssemblySelectors] IReadOnlyCollection<string> Assemblies);
