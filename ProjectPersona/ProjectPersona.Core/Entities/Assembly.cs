namespace ProjectPersona.Core.Entities;

/// <summary> Сборка. </summary>
public record Assembly
{
    /// <summary> Создать сборку. </summary>
    /// <param name="name"> Имя сборки. </param>
    /// <param name="targetFramework"> Целевой фреймворк или null, при неизвестности. </param>
    public Assembly(string name, string? targetFramework)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TargetFramework = targetFramework;
    }
    
    /// <summary> Имя сборки. </summary>
    public string Name { get; init; }
    
    /// <summary> Целевой фреймворк. </summary>
    public string? TargetFramework { get; init; }
};