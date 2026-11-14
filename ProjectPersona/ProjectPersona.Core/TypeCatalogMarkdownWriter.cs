using System.Text;
using ProjectPersona.Core.Building;
using ProjectPersona.Core.Dto;
using ProjectPersona.Core.Entities;

namespace ProjectPersona.Core;

/// <summary>
/// Записывает <see cref="TypeCatalog"/> в набор markdown-файлов: один файл на компиляцию сборки.
/// </summary>
public static class TypeCatalogMarkdownWriter
{
    /// <summary>
    /// Записывает каталог в <paramref name="outputDirectory"/>. При нескольких TFM одной сборки создаются отдельные файлы <c>{AssemblyName}.{tfm}.md</c>.
    /// </summary>
    /// <param name="catalog">Каталог типов для сериализации.</param>
    /// <param name="outputDirectory">Каталог вывода markdown-файлов.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public static async Task WriteAsync(
        TypeCatalog catalog,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var multiTargetAssemblies = AssemblyArtifactFileName.GetMultiTargetAssemblyNames(
            catalog.AssemblyGroups.Select(static assemblyGroup => assemblyGroup.Assembly.Name));
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyGroup in catalog.AssemblyGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var includeTargetFramework = multiTargetAssemblies.Contains(assemblyGroup.Assembly.Name);
            var fileName = AssemblyArtifactFileName.Allocate(
                assemblyGroup.Assembly.Name,
                assemblyGroup.Assembly.TargetFramework,
                includeTargetFramework,
                ".md",
                usedFileNames);
            var path = Path.Combine(outputDirectory, fileName);
            var content = RenderAssembly(assemblyGroup, includeTargetFramework);
            await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string RenderAssembly(AssemblyGroup assemblyGroup, bool includeTargetFrameworkInHeading)
    {
        var builder = new StringBuilder();
        var namespaceCount = assemblyGroup.Namespaces.Count;
        var typeCount = assemblyGroup.Namespaces.Sum(static ns => CountTypes(ns.Types));
        var assembly =  assemblyGroup.Assembly;

        builder.AppendLine("---");
        builder.Append("assembly: ").AppendLine(ToYamlScalar(assembly.Name));
        if (!string.IsNullOrEmpty(assembly.TargetFramework))
            builder.Append("targetFramework: ").AppendLine(ToYamlScalar(assembly.TargetFramework));
        builder.Append("namespaces: ").AppendLine(namespaceCount.ToString());
        builder.Append("types: ").AppendLine(typeCount.ToString());
        builder.AppendLine("---");
        builder.AppendLine();
        builder.Append("# ").Append(assembly.Name);
        if (includeTargetFrameworkInHeading && !string.IsNullOrEmpty(assembly.TargetFramework))
            builder.Append(" (").Append(assembly.TargetFramework).Append(')');
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Namespaces");
        builder.AppendLine();

        foreach (var ns in assemblyGroup.Namespaces)
        {
            var displayName = NamespaceDisplayName(ns.Name);
            builder.Append("- [")
                .Append(displayName)
                .Append("](#")
                .Append(NamespaceAnchor(ns.Name))
                .AppendLine(")");
        }

        foreach (var ns in assemblyGroup.Namespaces)
            RenderNamespace(builder, ns);

        return builder.ToString();
    }

    private static void RenderNamespace(StringBuilder builder, NamespaceGroup ns)
    {
        var displayName = NamespaceDisplayName(ns.Name);
        var anchor = NamespaceAnchor(ns.Name);

        builder.AppendLine();
        builder.Append("## ").Append(displayName)
            .Append(" {#").Append(anchor).AppendLine("}");

        foreach (var type in ns.Types)
            RenderType(builder, ns.Name, type, type.Name);
    }

    private static void RenderType(
        StringBuilder builder,
        string namespaceName,
        DeclaredType type,
        string typePath)
    {
        var fqn = BuildFqn(namespaceName, typePath);
        var heading = FormatTypeHeading(type, typePath);

        builder.AppendLine();
        builder.Append("### `").Append(heading).Append("` {#")
            .Append(TypeAnchor(fqn)).AppendLine("}");
        builder.AppendLine();
        builder.Append("FQN: `").Append(fqn).AppendLine("`");

        if (type.TypeParameters.Count > 0)
        {
            builder.Append("Type params: ");
            AppendBacktickList(builder, type.TypeParameters.Select(static parameter => parameter.Display));
            builder.AppendLine();
        }

        var baseTypes = type.BaseTypes
            .Where(static item => item.Kind != BaseTypeKind.Interface)
            .Select(static item => item.Type.Display)
            .ToList();
        if (baseTypes.Count > 0)
        {
            builder.Append("Base: ");
            AppendBacktickList(builder, baseTypes);
            builder.AppendLine();
        }

        var interfaces = type.BaseTypes
            .Where(static item => item.Kind == BaseTypeKind.Interface)
            .Select(static item => item.Type.Display)
            .ToList();
        if (interfaces.Count > 0)
        {
            builder.Append("Implements: ");
            AppendBacktickList(builder, interfaces);
            builder.AppendLine();
        }

        if (type.Attributes.Count > 0)
        {
            builder.Append("Attributes: ");
            AppendBacktickList(builder, type.Attributes.Select(static attribute => attribute.Display));
            builder.AppendLine();
        }

        AppendXmlBlockquote(builder, type.XmlDocumentation, indent: 0);

        if (type.Members.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("#### Members");
            builder.AppendLine();

            foreach (var member in type.Members)
                RenderMember(builder, member);
        }

        foreach (var nested in type.NestedTypes)
            RenderType(builder, namespaceName, nested, $"{typePath}.{nested.Name}");
    }

    private static void RenderMember(StringBuilder builder, DeclaredMember member)
    {
        builder.Append("- `").Append(FormatMemberSignature(member)).AppendLine("`");

        foreach (var attribute in member.Attributes)
            builder.Append("  - attr `").Append(attribute.Display).AppendLine("`");

        if (member.Signature is { } signature)
        {
            foreach (var parameter in signature.Parameters)
            {
                builder.Append("  - param `").Append(parameter.Name).Append("`: `")
                    .Append(FormatParameterType(parameter)).Append('`');
                if (parameter.DefaultValue is not null)
                    builder.Append(" = `").Append(parameter.DefaultValue).Append('`');
                builder.AppendLine();
            }

            if (member.Kind is MemberKind.Method or MemberKind.Operator &&
                signature.ReturnType is not null)
                builder.Append("  - returns: `").Append(signature.ReturnType.Display).AppendLine("`");
        }

        AppendXmlBlockquote(builder, member.XmlDocumentation, indent: 2);
    }

    private static string FormatTypeHeading(DeclaredType type, string typePath)
    {
        var access = FormatAccessibility(type.Accessibility);
        var modifiers = FormatTypeModifiers(type.Modifiers);
        var kind = FormatTypeKind(type.Kind);
        var generics = FormatTypeParameterNames(type.TypeParameters);

        if (type.Kind == CodeTypeKind.Delegate && type.Signature is { } signature)
        {
            var returnType = signature.ReturnType?.Display ?? "void";
            return $"{access}{modifiers} delegate {returnType} {typePath}{generics}({FormatParametersInline(signature)})";
        }

        return $"{access}{modifiers} {kind} {typePath}{generics}".Trim();
    }

    private static string FormatTypeModifiers(IReadOnlyList<string> modifiers) =>
        modifiers.Count == 0 ? string.Empty : " " + string.Join(' ', modifiers);

    private static string FormatTypeParameterNames(IReadOnlyList<TypeParameterInfo> typeParameters)
    {
        if (typeParameters.Count == 0)
            return string.Empty;

        return "<" + string.Join(", ", typeParameters.Select(static parameter =>
            string.IsNullOrEmpty(parameter.Variance)
                ? parameter.Name
                : $"{parameter.Variance} {parameter.Name}")) + ">";
    }

    private static string FormatMemberSignature(DeclaredMember member)
    {
        var access = member.ExplicitInterface is null
            ? FormatAccessibility(member.Accessibility)
            : string.Empty;
        var modifiers = FormatMemberModifiers(member.Modifiers);
        var name = FormatMemberName(member);

        return member.Kind switch
        {
            MemberKind.Method or MemberKind.Constructor or MemberKind.Operator =>
                FormatCallable(access, modifiers, name, member.Signature),
            MemberKind.Indexer => FormatIndexer(access, modifiers, name, member.Signature, member.Accessors),
            MemberKind.Property => FormatProperty(access, modifiers, name, member.Signature, member.Accessors),
            MemberKind.Field => FormatField(access, modifiers, member.Name, member.Signature),
            MemberKind.Event => FormatEvent(access, modifiers, name, member.Signature),
            MemberKind.EnumMember => member.Name,
            _ => $"{access}{modifiers} {name}".Trim()
        };
    }

    private static string FormatMemberName(DeclaredMember member) =>
        member.ExplicitInterface is { } explicitInterface
            ? $"{explicitInterface.Display}.{member.Name}"
            : member.Name;

    private static string FormatMemberModifiers(IReadOnlyList<string> modifiers) =>
        modifiers.Count == 0 ? string.Empty : " " + string.Join(' ', modifiers);

    private static string FormatProperty(
        string access,
        string modifiers,
        string name,
        FunctionSignature? signature,
        IReadOnlyList<string> accessors)
    {
        var type = signature?.ReturnType?.Display;
        if (type is null)
            return $"{access}{modifiers} {name}{FormatAccessors(accessors)}".Trim();

        if (signature is { Parameters.Count: > 0 })
        {
            var parameters = FormatParametersInline(signature);
            return $"{access}{modifiers} {type} {name}[{parameters}]{FormatAccessors(accessors)}".Trim();
        }

        return $"{access}{modifiers} {type} {name}{FormatAccessors(accessors)}".Trim();
    }

    private static string FormatField(
        string access,
        string modifiers,
        string name,
        FunctionSignature? signature)
    {
        var type = signature?.ReturnType?.Display;
        return type is null
            ? $"{access}{modifiers} field {name}".Trim()
            : $"{access}{modifiers} field {type} {name}".Trim();
    }

    private static string FormatEvent(
        string access,
        string modifiers,
        string name,
        FunctionSignature? signature)
    {
        var type = signature?.ReturnType?.Display;
        return type is null
            ? $"{access}{modifiers} event {name}".Trim()
            : $"{access}{modifiers} event {type} {name}".Trim();
    }

    private static string FormatAccessors(IReadOnlyList<string> accessors) =>
        accessors.Count == 0 ? string.Empty : " { " + string.Join("; ", accessors) + "; }";

    private static string FormatCallable(string access, string modifiers, string name, FunctionSignature? signature)
    {
        var parameters = FormatParametersInline(signature);
        if (signature?.ReturnType is { } returnType)
            return $"{access}{modifiers} {returnType.Display} {name}({parameters})".Trim();

        return $"{access}{modifiers} {name}({parameters})".Trim();
    }

    private static string FormatIndexer(
        string access,
        string modifiers,
        string name,
        FunctionSignature? signature,
        IReadOnlyList<string> accessors)
    {
        var parameters = FormatParametersInline(signature);
        if (signature?.ReturnType is { } returnType)
            return $"{access}{modifiers} {returnType.Display} {name}[{parameters}]{FormatAccessors(accessors)}".Trim();

        return $"{access}{modifiers} {name}[{parameters}]{FormatAccessors(accessors)}".Trim();
    }

    private static string FormatParametersInline(FunctionSignature? signature)
    {
        if (signature is null || signature.Parameters.Count == 0)
            return string.Empty;

        return string.Join(", ", signature.Parameters.Select(static parameter =>
        {
            var text = $"{FormatParameterType(parameter)} {parameter.Name}";
            return parameter.DefaultValue is null ? text : $"{text} = {parameter.DefaultValue}";
        }));
    }

    private static string FormatParameterType(ParameterInfo parameter)
    {
        var type = parameter.Type.Display;
        if (parameter.Modifiers.Count == 0)
            return type;

        return string.Join(' ', parameter.Modifiers) + " " + type;
    }

    private static void AppendBacktickList(StringBuilder builder, IEnumerable<string> items)
    {
        var first = true;
        foreach (var item in items)
        {
            if (!first)
                builder.Append(", ");

            builder.Append('`').Append(item).Append('`');
            first = false;
        }
    }

    private static void AppendXmlBlockquote(StringBuilder builder, string? xmlDocumentation, int indent)
    {
        if (string.IsNullOrWhiteSpace(xmlDocumentation))
            return;

        var prefix = indent > 0 ? new string(' ', indent) : string.Empty;
        if (indent == 0)
            builder.AppendLine();

        foreach (var line in xmlDocumentation.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            builder.Append(prefix).Append("> ").AppendLine(trimmed);
        }
    }

    private static int CountTypes(IReadOnlyList<DeclaredType> types)
    {
        var count = 0;
        foreach (var type in types)
            count += 1 + CountTypes(type.NestedTypes);

        return count;
    }

    private static string BuildFqn(string namespaceName, string typePath) =>
        string.IsNullOrEmpty(namespaceName) ? typePath : $"{namespaceName}.{typePath}";

    private static string NamespaceDisplayName(string namespaceName) =>
        string.IsNullOrEmpty(namespaceName) ? "(global)" : namespaceName;

    private static string NamespaceAnchor(string namespaceName) =>
        string.IsNullOrEmpty(namespaceName) ? "ns-global" : $"ns-{ToAnchor(namespaceName)}";

    private static string TypeAnchor(string fqn) => $"type-{ToAnchor(fqn)}";

    private static string ToAnchor(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousHyphen = false;

        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousHyphen = false;
                continue;
            }

            if (previousHyphen)
                continue;

            builder.Append('-');
            previousHyphen = true;
        }

        return builder.ToString().Trim('-');
    }

    private static string FormatAccessibility(AccessibilityLevel accessibility) =>
        accessibility switch
        {
            AccessibilityLevel.Public => "public",
            AccessibilityLevel.Private => "private",
            AccessibilityLevel.Protected => "protected",
            AccessibilityLevel.Internal => "internal",
            AccessibilityLevel.ProtectedInternal => "protected internal",
            AccessibilityLevel.PrivateProtected => "private protected",
            AccessibilityLevel.File => "file",
            _ => accessibility.ToString().ToLowerInvariant()
        };

    private static string FormatTypeKind(CodeTypeKind kind) =>
        kind switch
        {
            CodeTypeKind.Class => "class",
            CodeTypeKind.Struct => "struct",
            CodeTypeKind.Interface => "interface",
            CodeTypeKind.Record => "record",
            CodeTypeKind.RecordStruct => "record struct",
            CodeTypeKind.Enum => "enum",
            CodeTypeKind.Delegate => "delegate",
            _ => kind.ToString().ToLowerInvariant()
        };

    private static string ToYamlScalar(string value)
    {
        if (value.Length == 0)
            return "''";

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch) || ":#{}[]&*?|-<>=!%@`'\",".Contains(ch))
                return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }

        return value;
    }
}
