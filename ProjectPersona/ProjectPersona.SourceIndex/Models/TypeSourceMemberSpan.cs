namespace ProjectPersona.SourceIndex.Models;

/// <summary>
/// Смещение среза члена внутри <see cref="TypeSourceDeclaration.SourceCode"/>.
/// </summary>
/// <param name="Name">Имя члена в формате каталога.</param>
/// <param name="Start">Смещение фрагмента в символах UTF-16.</param>
/// <param name="Length">Длина фрагмента в символах.</param>
public sealed record TypeSourceMemberSpan(
    string Name,
    int Start,
    int Length);
