using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Нормализует XML-документацию в плоскую строку для снимка каталога.
/// </summary>
internal static class XmlDocumentationFormatter
{
    public static string? FormatMemberElement(XElement memberElement, Compilation? compilation)
    {
        var parts = new List<string>();

        foreach (var child in memberElement.Elements())
        {
            var elementName = child.Name.LocalName;
            var text = CollapseWhitespace(FormatInnerText(child, compilation));
            if (string.IsNullOrEmpty(text) && elementName != "inheritdoc")
                continue;

            parts.Add(elementName switch
            {
                "summary" or "remarks" => text,
                "param" or "typeparam" => AppendNamedDocumentation(child.Attribute("name")?.Value, text),
                "returns" => $"returns: {text}",
                "exception" => AppendNamedDocumentation(
                    ResolveCrefDisplay(child.Attribute("cref")?.Value, compilation),
                    text),
                "inheritdoc" => FormatUnresolvedInheritdoc(child),
                _ => text
            });
        }

        return parts.Count == 0 ? null : string.Join('\n', parts);
    }

    public static string FormatInnerText(XElement element, Compilation? compilation)
    {
        var builder = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child:
                    FormatInlineElement(builder, child, compilation);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void FormatInlineElement(StringBuilder builder, XElement element, Compilation? compilation)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
            {
                var innerText = CollapseWhitespace(element.Value);
                if (!string.IsNullOrEmpty(innerText))
                {
                    builder.Append(innerText);
                    break;
                }

                var replacement = ResolveCrefDisplay(element.Attribute("cref")?.Value, compilation)
                    ?? element.Attribute("href")?.Value
                    ?? element.Attribute("langword")?.Value;
                if (!string.IsNullOrEmpty(replacement))
                    builder.Append(replacement);
                break;
            }

            case "paramref":
            case "typeparamref":
                builder.Append(element.Attribute("name")?.Value);
                break;

            case "para":
                builder.Append(' ');
                builder.Append(CollapseWhitespace(FormatInnerText(element, compilation)));
                builder.Append(' ');
                break;

            default:
                builder.Append(CollapseWhitespace(FormatInnerText(element, compilation)));
                break;
        }
    }

    public static string? ResolveCrefDisplay(string? cref, Compilation? compilation)
    {
        if (string.IsNullOrWhiteSpace(cref))
            return null;

        var normalized = NormalizeDocumentationId(cref);
        if (compilation is null)
            return StripDocumentationIdPrefix(normalized);

        var symbol = DocumentationCommentId.GetFirstSymbolForReferenceId(normalized, compilation)
            ?? DocumentationCommentId.GetFirstSymbolForDeclarationId(normalized, compilation);

        return symbol is null
            ? StripDocumentationIdPrefix(normalized)
            : FormatSymbolDisplay(symbol);
    }

    public static string FormatSymbolDisplay(ISymbol symbol) =>
        symbol switch
        {
            ITypeSymbol typeSymbol => TypeRefResolver.FromSymbol(typeSymbol).Display,
            INamespaceSymbol namespaceSymbol => namespaceSymbol.ToDisplayString(),
            { ContainingType: not null } contained =>
                $"{FormatSymbolDisplay(contained.ContainingType)}.{contained.Name}",
            _ => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        };

    public static string NormalizeDocumentationId(string documentationId)
    {
        var trimmed = documentationId.Trim();
        return trimmed.StartsWith("!:", StringComparison.Ordinal) ? trimmed[2..] : trimmed;
    }

    public static string StripDocumentationIdPrefix(string documentationId)
    {
        var trimmed = NormalizeDocumentationId(documentationId);
        var separator = trimmed.IndexOf(':');
        return separator >= 0 && separator < trimmed.Length - 1
            ? trimmed[(separator + 1)..]
            : trimmed;
    }

    private static string FormatUnresolvedInheritdoc(XElement inheritdoc)
    {
        var target = inheritdoc.Attribute("target")?.Value;
        var cref = inheritdoc.Attribute("cref")?.Value;
        if (!string.IsNullOrWhiteSpace(target))
            return $"inheritdoc from {target}";

        if (!string.IsNullOrWhiteSpace(cref))
            return $"inheritdoc from {StripDocumentationIdPrefix(cref)}";

        return "inheritdoc";
    }

    private static string AppendNamedDocumentation(string? name, string text) =>
        string.IsNullOrEmpty(name) ? text : $"{name}: {text}";

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousWhitespace = false;

        foreach (var ch in text.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWhitespace)
                {
                    builder.Append(' ');
                    previousWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWhitespace = false;
        }

        return builder.ToString();
    }
}
