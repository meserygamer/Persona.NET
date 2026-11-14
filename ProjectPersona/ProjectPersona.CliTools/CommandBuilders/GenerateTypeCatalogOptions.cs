using System.ComponentModel.DataAnnotations;
using ProjectPersona.CliTools.Validation.Attributes;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Параметры подкоманды <c>generate-type-catalog</c>.
/// </summary>
/// <param name="SolutionPath">Путь к файлу <c>.sln</c>.</param>
/// <param name="OutputPath">Каталог вывода markdown-файлов.</param>
public sealed record GenerateTypeCatalogOptions(
    [property: Required, ExistingFile] string SolutionPath,
    [property: Required] string OutputPath);
