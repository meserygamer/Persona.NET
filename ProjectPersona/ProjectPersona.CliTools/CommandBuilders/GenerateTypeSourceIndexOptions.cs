using System.ComponentModel.DataAnnotations;
using ProjectPersona.CliTools.Validation.Attributes;

namespace ProjectPersona.CliTools.CommandBuilders;

/// <summary>
/// Параметры подкоманды <c>generate-type-source-index</c>.
/// </summary>
/// <param name="SolutionPath">Путь к файлу <c>.sln</c>.</param>
/// <param name="OutputPath">Каталог вывода файла <c>type-source-index.db</c>.</param>
public sealed record GenerateTypeSourceIndexOptions(
    [property: Required, ExistingFile] string SolutionPath,
    [property: Required] string OutputPath);
