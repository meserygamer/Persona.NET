using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.CodeAnalysis;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Разворачивает <c>&lt;inheritdoc/&gt;</c> в XML-документации символа через цепочку override/interface,
/// documentation ID и XML referenced-сборок.
/// </summary>
internal sealed class XmlDocumentationInheritdocResolver(Compilation compilation)
{
    private const int MaxInheritdocDepth = 32;
    private readonly Dictionary<ISymbol, string?> _formattedCache = new(SymbolEqualityComparer.Default);
    private readonly XmlDocumentationAssemblyLookup _assemblyLookup = new(compilation);

    /// <summary>
    /// Возвращает развёрнутую и отформатированную XML-документацию символа или <see langword="null"/>.
    /// </summary>
    /// <param name="symbol">Символ, документацию которого нужно развернуть.</param>
    public string? ResolveFormatted(ISymbol symbol)
    {
        if (_formattedCache.TryGetValue(symbol, out var cached))
            return cached;

        try
        {
            var xml = GetCommentXml(symbol);
            if (!HasUsableDocumentation(xml))
            {
                _formattedCache[symbol] = null;
                return null;
            }

            var element = TryParseMemberElement(xml!);
            if (element is null)
            {
                _formattedCache[symbol] = null;
                return null;
            }

            var expanded = ExpandInheritdoc(element, symbol, [], depth: 0);
            var formatted = XmlDocumentationFormatter.FormatMemberElement(expanded, compilation);
            _formattedCache[symbol] = formatted;
            return formatted;
        }
        catch (Exception)
        {
            _formattedCache[symbol] = null;
            return null;
        }
    }

    /// <summary>
    /// Подставляет содержимое <c>inheritdoc</c> из цели cref, override или реализации интерфейса.
    /// </summary>
    /// <param name="memberElement">Разобранный XML-элемент <c>member</c>.</param>
    /// <param name="symbol">Символ, которому принадлежит комментарий.</param>
    /// <param name="expandingSymbols">Символы, inheritdoc которых сейчас разворачивается (защита от циклов).</param>
    /// <param name="depth">Текущая глубина рекурсии.</param>
    public XElement ExpandInheritdoc(
        XElement memberElement,
        ISymbol symbol,
        HashSet<ISymbol> expandingSymbols,
        int depth)
    {
        if (depth >= MaxInheritdocDepth)
            return memberElement;

        if (!expandingSymbols.Add(symbol))
            return memberElement;

        var inheritdocElements = memberElement.Elements()
            .Where(static element => element.Name.LocalName == "inheritdoc")
            .ToList();

        if (inheritdocElements.Count == 0)
            return memberElement;

        var localElements = memberElement.Elements()
            .Where(static element => element.Name.LocalName != "inheritdoc")
            .Select(static element => new XElement(element))
            .ToList();

        var localTagNames = localElements
            .Select(static element => element.Name.LocalName)
            .ToHashSet(StringComparer.Ordinal);

        var inheritedElements = new List<XElement>();

        foreach (var inheritdoc in inheritdocElements)
        {
            var cref = inheritdoc.Attribute("cref")?.Value;
            var path = inheritdoc.Attribute("path")?.Value;
            var targetSymbol = ResolveInheritdocTarget(symbol, cref, expandingSymbols);
            if (targetSymbol is null)
            {
                inheritedElements.Add(CreateUnresolvedInheritdocElement(cref, targetSymbol: null));
                continue;
            }

            var documentedSymbol = ResolveDocumentedSymbol(targetSymbol, expandingSymbols);
            var targetXml = GetCommentXml(documentedSymbol);
            if (!HasUsableDocumentation(targetXml))
            {
                inheritedElements.Add(CreateUnresolvedInheritdocElement(cref, documentedSymbol));
                continue;
            }

            var targetElement = TryParseMemberElement(targetXml!);
            if (targetElement is null)
            {
                inheritedElements.Add(CreateUnresolvedInheritdocElement(cref, documentedSymbol));
                continue;
            }

            if (expandingSymbols.Contains(documentedSymbol))
            {
                inheritedElements.Add(CreateUnresolvedInheritdocElement(cref, documentedSymbol));
                continue;
            }

            var expandedTarget = ExpandInheritdoc(targetElement, documentedSymbol, expandingSymbols, depth + 1);
            inheritedElements.AddRange(SelectInheritedElements(expandedTarget, path));
        }

        var merged = new XElement(memberElement.Name);
        merged.Add(localElements);

        foreach (var inherited in inheritedElements)
        {
            if (inherited.Name.LocalName == "inheritdoc")
            {
                merged.Add(inherited);
                continue;
            }

            if (!localTagNames.Contains(inherited.Name.LocalName))
                merged.Add(inherited);
        }

        return merged;
    }

    private string? GetCommentXml(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: false);
        if (HasUsableDocumentation(xml))
            return xml;

        var original = symbol.OriginalDefinition;
        if (!SymbolEqualityComparer.Default.Equals(symbol, original))
        {
            xml = original.GetDocumentationCommentXml(expandIncludes: false);
            if (HasUsableDocumentation(xml))
                return xml;
        }

        return _assemblyLookup.FindMemberXml(symbol);
    }

    private static bool HasUsableDocumentation(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return false;

        var element = TryParseMemberElement(xml);
        return element is { HasElements: true };
    }

    private static IEnumerable<XElement> SelectInheritedElements(XElement expandedTarget, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var normalizedPath = path.Trim();
                if (normalizedPath.StartsWith('/'))
                    normalizedPath = normalizedPath[1..];

                var selected = expandedTarget.XPathSelectElements(normalizedPath).ToList();
                if (selected.Count > 0)
                    return selected.Select(static element => new XElement(element));
            }
            catch (System.Xml.XPath.XPathException)
            {
            }
        }

        return expandedTarget.Elements()
            .Where(static element => element.Name.LocalName != "inheritdoc")
            .Select(static element => new XElement(element));
    }

    private ISymbol ResolveDocumentedSymbol(ISymbol symbol, HashSet<ISymbol> expandingSymbols)
    {
        var current = symbol;
        var chainVisited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        while (true)
        {
            if (!chainVisited.Add(current))
                return current;

            if (HasUsableDocumentation(GetCommentXml(current)))
                return current;

            var next = FindImmediateInheritedSymbol(current);
            if (next is null
                || expandingSymbols.Contains(next)
                || SymbolEqualityComparer.Default.Equals(next, current))
                return current;

            current = next;
        }
    }

    /// <summary>
    /// Выбирает цель <c>inheritdoc</c>: <c>cref</c> (в том числе тип целиком) либо ближайший override/interface-член.
    /// </summary>
    private ISymbol? ResolveInheritdocTarget(ISymbol symbol, string? cref, HashSet<ISymbol> expandingSymbols)
    {
        if (!string.IsNullOrWhiteSpace(cref))
        {
            var resolved = ResolveDocumentationId(cref);
            if (resolved is not null)
                return resolved;
        }

        var inherited = FindImmediateInheritedSymbol(symbol);
        if (inherited is null || expandingSymbols.Contains(inherited))
            return null;

        return inherited;
    }

    private static XElement? TryParseMemberElement(string xml)
    {
        try
        {
            return XElement.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private ISymbol? FindImmediateInheritedSymbol(ISymbol symbol)
    {
        switch (symbol)
        {
            case IPropertySymbol property:
                if (property.OverriddenProperty is not null)
                    return property.OverriddenProperty;
                if (property.ExplicitInterfaceImplementations.Length > 0)
                    return property.ExplicitInterfaceImplementations[0];
                return FindImplicitInterfaceImplementation(property);

            case IMethodSymbol method:
                if (method.OverriddenMethod is not null)
                    return method.OverriddenMethod;
                if (method.ExplicitInterfaceImplementations.Length > 0)
                    return method.ExplicitInterfaceImplementations[0];
                return FindImplicitInterfaceImplementation(method);

            case IEventSymbol eventSymbol:
                if (eventSymbol.OverriddenEvent is not null)
                    return eventSymbol.OverriddenEvent;
                if (eventSymbol.ExplicitInterfaceImplementations.Length > 0)
                    return eventSymbol.ExplicitInterfaceImplementations[0];
                return FindImplicitInterfaceImplementation(eventSymbol);

            case INamedTypeSymbol namedType:
                if (namedType.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
                    return baseType;
                return namedType.Interfaces.FirstOrDefault();
        }

        return null;
    }

    private ISymbol? FindImplicitInterfaceImplementation(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        if (containingType is null)
            return null;

        ISymbol? fallbackCandidate = null;

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var candidate in iface.GetMembers(symbol.Name))
            {
                if (candidate.Kind != symbol.Kind)
                    continue;

                var implementation = containingType.FindImplementationForInterfaceMember(candidate);
                if (SymbolEqualityComparer.Default.Equals(implementation, symbol))
                    return candidate;

                if (implementation is null && IsCompatibleInterfaceMember(symbol, candidate))
                    fallbackCandidate ??= candidate;
            }
        }

        if (symbol is IPropertySymbol property)
        {
            foreach (var iface in containingType.AllInterfaces)
            {
                foreach (var candidate in iface.GetMembers())
                {
                    if (candidate is not IPropertySymbol ifaceProperty)
                        continue;

                    var implementation = containingType.FindImplementationForInterfaceMember(ifaceProperty);
                    if (!SymbolEqualityComparer.Default.Equals(implementation, symbol))
                    {
                        if (implementation is null && IsCompatibleInterfaceMember(symbol, ifaceProperty))
                            fallbackCandidate ??= ifaceProperty;
                        continue;
                    }

                    return ifaceProperty;
                }
            }
        }

        return fallbackCandidate;
    }

    private static bool IsCompatibleInterfaceMember(ISymbol symbol, ISymbol candidate) =>
        symbol switch
        {
            IPropertySymbol property when candidate is IPropertySymbol ifaceProperty =>
                property.IsIndexer == ifaceProperty.IsIndexer &&
                property.Name == ifaceProperty.Name,
            IMethodSymbol method when candidate is IMethodSymbol ifaceMethod =>
                method.Name == ifaceMethod.Name &&
                method.MethodKind == ifaceMethod.MethodKind &&
                method.Parameters.Length == ifaceMethod.Parameters.Length,
            IEventSymbol eventSymbol when candidate is IEventSymbol ifaceEvent =>
                eventSymbol.Name == ifaceEvent.Name,
            _ => symbol.Name == candidate.Name
        };

    private ISymbol? ResolveDocumentationId(string documentationId)
    {
        var normalized = XmlDocumentationFormatter.NormalizeDocumentationId(documentationId);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var symbol = DocumentationCommentId.GetFirstSymbolForReferenceId(normalized, compilation)
            ?? DocumentationCommentId.GetFirstSymbolForDeclarationId(normalized, compilation);

        if (symbol is not null)
            return symbol;

        if (!normalized.Contains(':', StringComparison.Ordinal))
        {
            symbol = DocumentationCommentId.GetFirstSymbolForReferenceId($"T:{normalized}", compilation)
                ?? DocumentationCommentId.GetFirstSymbolForDeclarationId($"T:{normalized}", compilation);
        }

        return symbol;
    }

    private static XElement CreateUnresolvedInheritdocElement(string? cref, ISymbol? targetSymbol)
    {
        var element = new XElement("inheritdoc");
        if (!string.IsNullOrWhiteSpace(cref))
            element.SetAttributeValue("cref", cref);

        if (targetSymbol is not null)
            element.SetAttributeValue("target", XmlDocumentationFormatter.FormatSymbolDisplay(targetSymbol));

        return element;
    }
}
