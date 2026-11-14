namespace ProjectPersona.SourceIndex.Models;

/// <summary>
/// Вид ссылки на тип или член, найденной в объявлении типа.
/// </summary>
public enum TypeSourceReferenceKind
{
    /// <summary>Использование типа.</summary>
    Type,

    /// <summary>Метод или оператор.</summary>
    Method,

    /// <summary>Конструктор.</summary>
    Constructor,

    /// <summary>Свойство.</summary>
    Property,

    /// <summary>Индексатор.</summary>
    Indexer,

    /// <summary>Поле.</summary>
    Field,

    /// <summary>Событие.</summary>
    Event,

    /// <summary>Не удалось семантически разрешить символ.</summary>
    Unresolved,

    /// <summary>Константа перечисления. В метаданных Roslyn это поле, в каталоге — отдельный член.</summary>
    EnumMember
}
