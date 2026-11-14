namespace ProjectPersona.SourceIndex.Models;

/// <summary>
/// Уникальная ссылка на тип или член, используемый в объявлении типа.
/// </summary>
/// <param name="Kind">Вид ссылки.</param>
/// <param name="Display">Текст из исходника.</param>
/// <param name="Fqn">FQN типа в формате каталога; для члена — FQN содержащего типа; для <see cref="TypeSourceReferenceKind.Unresolved"/> — пустая строка.</param>
/// <param name="Name">Имя члена; для типа — короткое имя типа.</param>
/// <param name="AssemblyName">Имя сборки, которой принадлежит символ, или пустая строка.</param>
/// <param name="Signature">Компактная сигнатура метода, конструктора или индексатора; иначе пустая строка.</param>
public sealed record TypeSourceReference(
    TypeSourceReferenceKind Kind,
    string Display,
    string Fqn,
    string Name,
    string AssemblyName,
    string Signature);
