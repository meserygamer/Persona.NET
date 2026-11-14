using System.ComponentModel.DataAnnotations;
using ProjectPersona.RagTextSearch.LanceDB.Data;

namespace ProjectPersona.RagTextSearch.LanceDB;

/// <summary>
/// Настройки LanceDB. Пустые и состоящие из пробелов строки заменяются значениями по умолчанию
/// при любом способе создания (биндинг конфигурации, <c>new</c>, <c>with</c>).
/// </summary>
public record LanceDbSettings : IValidatableObject
{
    private static readonly string DefaultPathToLanceDb = Path.Combine(AppContext.BaseDirectory, "lance-db");
    private const string DefaultTableName = "type_source_index";
    private const string DefaultIndexVectorName = "vector_index";

    [Required]
    public string PathToLanceDb
    {
        get;
        set => field = OrDefault(value, DefaultPathToLanceDb);
    } = DefaultPathToLanceDb;

    [Required]
    public string TableName
    {
        get;
        init => field = OrDefault(value, DefaultTableName);
    } = DefaultTableName;

    [Required]
    public string IndexVectorName
    {
        get;
        init => field = OrDefault(value, DefaultIndexVectorName);
    } = DefaultIndexVectorName;

    [Range(1, int.MaxValue)]
    public int VectorDimension { get; init; } = 1;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(TableName, LanceDbManifestRepository.MetaTableName, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                $"{nameof(TableName)} '{TableName}' is reserved for the vector store manifest.",
                [nameof(TableName)]);
        }
    }

    private static string OrDefault(string? value, string defaultValue) =>
        string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
}
