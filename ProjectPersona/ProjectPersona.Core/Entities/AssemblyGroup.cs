namespace ProjectPersona.Core.Entities;

/// <summary> Снимок объявленных типов одной компиляции сборки, сгруппированных по пространствам имён. </summary>
/// <param name="Assembly"> Сборка. </param>
/// <param name="Namespaces"> Пространства имён со снимками типов. </param>
public sealed record AssemblyGroup(
    Assembly Assembly,
    IReadOnlyList<NamespaceGroup> Namespaces);
