using System.ComponentModel.DataAnnotations;
using ProjectPersona.CliTools.Validation.Attributes;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Параметры подкоманды <c>generate-type-artifacts</c>.
/// </summary>
/// <param name="SolutionPath">Путь к файлу <c>.sln</c>.</param>
/// <param name="CatalogOutputPath">Каталог вывода markdown-файлов.</param>
/// <param name="IndexOutputPath">Каталог вывода файла <c>type-source-index.db</c>.</param>
public sealed record GenerateTypeArtifactsOptions(
    [property: Required, ExistingFile] string SolutionPath,
    [property: Required] string CatalogOutputPath,
    [property: Required] string IndexOutputPath);
