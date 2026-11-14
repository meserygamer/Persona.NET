using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Извлекает и нормализует XML-комментарии документации из символов Roslyn и синтаксических узлов.
/// </summary>
internal static class XmlDocumentationReader
{
    /// <summary>
    /// Читает XML-документацию символа или ведущего комментария узла и возвращает свёрнутую строку.
    /// </summary>
    /// <param name="node">Синтаксический узел, ведущие тривиальные элементы которого могут содержать комментарий документации.</param>
    /// <param name="symbol">Объявленный символ Roslyn или <see langword="null"/>.</param>
    /// <param name="context">Контекст XML-документации для компиляции или <see langword="null"/>.</param>
    /// <returns>Многострочная строка с нормализованным текстом документации или <see langword="null"/>.</returns>
    public static string? GetXmlDocumentation(
        SyntaxNode node,
        ISymbol? symbol = null,
        XmlDocumentationContext? context = null)
    {
        var resolver = context?.Resolver;

        if (symbol is not null && resolver is not null)
        {
            var resolved = resolver.ResolveFormatted(symbol);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        var trivia = GetDocumentationCommentTrivia(node);
        if (trivia is null)
            return null;

        var memberElement = ConvertTriviaToMemberElement(trivia);
        if (memberElement is null)
            return null;

        if (symbol is not null && resolver is not null)
            memberElement = resolver.ExpandInheritdoc(memberElement, symbol, [], depth: 0);

        return XmlDocumentationFormatter.FormatMemberElement(memberElement, context?.Compilation);
    }

    private static DocumentationCommentTriviaSyntax? GetDocumentationCommentTrivia(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax documentationComment)
                return documentationComment;
        }

        return null;
    }

    private static XElement? ConvertTriviaToMemberElement(DocumentationCommentTriviaSyntax comment)
    {
        var memberElement = new XElement("member");
        foreach (var xmlNode in comment.Content)
            AppendTriviaNode(memberElement, xmlNode);

        return memberElement.HasElements ? memberElement : null;
    }

    private static void AppendTriviaNode(XElement parent, XmlNodeSyntax xmlNode)
    {
        switch (xmlNode)
        {
            case XmlElementSyntax element:
            {
                var elementName = element.StartTag.Name.ToString();
                var child = new XElement(elementName);
                CopyAttributes(child, element.StartTag.Attributes);
                foreach (var contentNode in element.Content)
                    AppendTriviaContent(child, contentNode);
                parent.Add(child);
                break;
            }

            case XmlEmptyElementSyntax empty:
            {
                var elementName = empty.Name.ToString();
                var child = new XElement(elementName);
                CopyAttributes(child, empty.Attributes);
                parent.Add(child);
                break;
            }
        }
    }

    private static void AppendTriviaContent(XElement parent, XmlNodeSyntax xmlNode)
    {
        switch (xmlNode)
        {
            case XmlTextSyntax text:
                parent.Add(new XText(GetXmlTextTokenValue(text)));
                break;

            case XmlElementSyntax element:
            {
                var elementName = element.StartTag.Name.ToString();
                var child = new XElement(elementName);
                CopyAttributes(child, element.StartTag.Attributes);
                foreach (var contentNode in element.Content)
                    AppendTriviaContent(child, contentNode);
                parent.Add(child);
                break;
            }

            case XmlEmptyElementSyntax empty:
            {
                var elementName = empty.Name.ToString();
                var child = new XElement(elementName);
                CopyAttributes(child, empty.Attributes);
                parent.Add(child);
                break;
            }

            case XmlCDataSectionSyntax cdata:
                parent.Add(new XText(string.Concat(cdata.TextTokens.Select(static token => token.ValueText))));
                break;
        }
    }

    private static void CopyAttributes(XElement element, SyntaxList<XmlAttributeSyntax> attributes)
    {
        foreach (var attribute in attributes)
        {
            var attributeName = attribute.Name.ToString();
            var attributeValue = GetRawAttributeValue(attribute);
            if (string.IsNullOrEmpty(attributeValue))
                continue;

            element.SetAttributeValue(attributeName, attributeValue);
        }
    }

    private static string GetRawAttributeValue(XmlAttributeSyntax attribute)
    {
        if (attribute is XmlNameAttributeSyntax nameAttribute)
            return nameAttribute.Identifier.ToString();

        if (attribute is XmlTextAttributeSyntax textAttribute)
            return string.Concat(textAttribute.TextTokens.Select(static token => token.ValueText));

        return ExtractQuotedValue(attribute.ToFullString());
    }

    private static string ExtractQuotedValue(string attributeText)
    {
        var equalsIndex = attributeText.IndexOf('=');
        if (equalsIndex < 0)
            return string.Empty;

        var firstQuote = attributeText.IndexOf('"', equalsIndex);
        var lastQuote = attributeText.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote)
            return string.Empty;

        return attributeText[(firstQuote + 1)..lastQuote];
    }

    private static string GetXmlTextTokenValue(XmlTextSyntax text)
    {
        var builder = new StringBuilder();
        foreach (var token in text.TextTokens)
        {
            if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(token.ValueText);
        }

        return builder.ToString();
    }
}
