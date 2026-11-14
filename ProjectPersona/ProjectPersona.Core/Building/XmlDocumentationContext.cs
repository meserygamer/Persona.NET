using Microsoft.CodeAnalysis;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Контекст разбора XML-документации для одной компиляции Roslyn.
/// </summary>
internal sealed class XmlDocumentationContext(Compilation compilation)
{
    /// <summary>
    /// Компиляция, в которой разрешаются cref и inheritdoc.
    /// </summary>
    public Compilation Compilation { get; } = compilation;

    private XmlDocumentationInheritdocResolver? _resolver;

    /// <summary>
    /// Кэширующий резолвер inheritdoc для текущей компиляции, включая XML referenced-сборок.
    /// </summary>
    public XmlDocumentationInheritdocResolver Resolver =>
        _resolver ??= new XmlDocumentationInheritdocResolver(Compilation);
}
