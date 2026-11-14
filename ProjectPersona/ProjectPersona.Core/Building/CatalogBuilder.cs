using ProjectPersona.Core.Dto;
using ProjectPersona.Core.Entities;

namespace ProjectPersona.Core.Building;

internal sealed record TypeSnapshot(
    string Name,
    CodeTypeKind Kind,
    AccessibilityLevel Accessibility,
    bool HasExplicitAccessibility,
    string? XmlDocumentation,
    FunctionSignature? Signature,
    IReadOnlyList<AttributeInfo> Attributes,
    IReadOnlyList<BaseTypeRef> BaseTypes,
    IReadOnlyList<TypeParameterInfo> TypeParameters,
    IReadOnlyList<string> Modifiers,
    IReadOnlyList<DeclaredMember> Members,
    IReadOnlyList<TypeSnapshot> NestedTypes)
{
    public DeclaredType ToDeclaredType() =>
        new(
            Name,
            Kind,
            Accessibility,
            XmlDocumentation,
            Signature,
            Attributes,
            BaseTypes,
            TypeParameters,
            Modifiers,
            Members,
            NestedTypes.Select(static nested => nested.ToDeclaredType()).ToList());
}

/// <summary>
/// Накапливает снимки типов и объединяет частичные объявления в отсортированную иерархию каталога; компиляции одной сборки с разными TFM хранятся раздельно.
/// </summary>
internal sealed class CatalogBuilder
{
    private readonly Dictionary<AssemblyIdentity, AssemblyBuilder> _assemblies = [];

    /// <summary>
    /// Регистрирует снимок типа в указанной компиляции сборки и пространстве имён.
    /// </summary>
    /// <param name="assembly">Идентичность компиляции сборки, которой принадлежит тип.</param>
    /// <param name="namespaceName">Пространство имён, содержащее тип, или пустая строка для глобального пространства имён.</param>
    /// <param name="type">Разобранный снимок типа для добавления или объединения.</param>
    public void AddType(AssemblyIdentity assembly, string namespaceName, TypeSnapshot type)
    {
        if (!_assemblies.TryGetValue(assembly, out var assemblyBuilder))
        {
            assemblyBuilder = new AssemblyBuilder();
            _assemblies[assembly] = assemblyBuilder;
        }

        assemblyBuilder.AddType(namespaceName, type);
    }

    /// <summary>
    /// Формирует итоговые отсортированные группы сборок из всех зарегистрированных снимков типов.
    /// </summary>
    /// <returns>Доступный только для чтения список групп сборок, упорядоченный по имени сборки и TFM.</returns>
    public IReadOnlyList<AssemblyGroup> Build() =>
        _assemblies
            .OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.TargetFramework, StringComparer.Ordinal)
            .Select(static pair => new AssemblyGroup(
                new Assembly(pair.Key.Name, pair.Key.TargetFrameworkOrNull),
                pair.Value.Build()))
            .ToList();
}

internal sealed class AssemblyBuilder
{
    private readonly Dictionary<string, NamespaceBuilder> _namespaces = new(StringComparer.Ordinal);

    public void AddType(string namespaceName, TypeSnapshot type)
    {
        if (!_namespaces.TryGetValue(namespaceName, out var namespaceBuilder))
        {
            namespaceBuilder = new NamespaceBuilder();
            _namespaces[namespaceName] = namespaceBuilder;
        }

        namespaceBuilder.AddType(type);
    }

    public IReadOnlyList<NamespaceGroup> Build() =>
        _namespaces
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new NamespaceGroup(pair.Key, pair.Value.Build()))
            .ToList();
}

internal sealed class NamespaceBuilder
{
    private readonly Dictionary<TypeKey, TypeNodeBuilder> _types = new();

    public void AddType(TypeSnapshot type)
    {
        var key = new TypeKey(type.Name, type.Kind);
        if (!_types.TryGetValue(key, out var existing))
        {
            _types[key] = TypeNodeBuilder.FromSnapshot(type);
            return;
        }

        existing.Merge(type);
    }

    public IReadOnlyList<DeclaredType> Build() =>
        _types
            .OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.Kind)
            .Select(static pair => pair.Value.Build())
            .ToList();
}

internal readonly record struct TypeKey(string Name, CodeTypeKind Kind);

internal readonly record struct MemberKey(
    string Name,
    MemberKind Kind,
    AccessibilityLevel Accessibility,
    string? ReturnType,
    string ParametersKey,
    string? ExplicitInterface)
{
    public static MemberKey From(DeclaredMember member) =>
        new(
            member.Name,
            member.Kind,
            member.Accessibility,
            member.Signature?.ReturnType?.Display,
            FormatParametersKey(member.Signature),
            member.ExplicitInterface?.Display);

    private static string FormatParametersKey(FunctionSignature? signature)
    {
        if (signature is null || signature.Parameters.Count == 0)
            return string.Empty;

        return string.Join(
            "|",
            signature.Parameters.Select(static parameter =>
            {
                var modifiers = parameter.Modifiers.Count == 0
                    ? string.Empty
                    : string.Join(' ', parameter.Modifiers) + " ";
                return $"{modifiers}{parameter.Type.Display}:{parameter.Name}:{parameter.DefaultValue ?? string.Empty}";
            }));
    }
}

internal sealed class TypeNodeBuilder
{
    private string _name = string.Empty;
    private CodeTypeKind _kind;
    private AccessibilityLevel _accessibility;
    private bool _hasExplicitAccessibility;
    private string? _xmlDocumentation;
    private FunctionSignature? _signature;
    private readonly List<AttributeInfo> _attributes = [];
    private readonly HashSet<string> _attributeKeys = new(StringComparer.Ordinal);
    private readonly List<BaseTypeRef> _baseTypes = [];
    private readonly HashSet<string> _baseTypeKeys = new(StringComparer.Ordinal);
    private readonly List<TypeParameterInfo> _typeParameters = [];
    private readonly List<string> _modifiers = [];
    private readonly HashSet<string> _modifierKeys = new(StringComparer.Ordinal);
    private readonly List<DeclaredMember> _members = [];
    private readonly HashSet<MemberKey> _memberKeys = new();
    private readonly Dictionary<TypeKey, TypeNodeBuilder> _nestedTypes = new();

    public static TypeNodeBuilder FromSnapshot(TypeSnapshot type)
    {
        var builder = new TypeNodeBuilder();
        builder.Merge(type);
        return builder;
    }

    public void Merge(TypeSnapshot type)
    {
        if (string.IsNullOrEmpty(_name))
        {
            _name = type.Name;
            _kind = type.Kind;
            _accessibility = type.Accessibility;
            _hasExplicitAccessibility = type.HasExplicitAccessibility;
            _xmlDocumentation = type.XmlDocumentation;
            _signature = type.Signature;
        }
        else
        {
            if (_xmlDocumentation is null && type.XmlDocumentation is not null)
                _xmlDocumentation = type.XmlDocumentation;

            if (!_hasExplicitAccessibility && type.HasExplicitAccessibility)
            {
                _accessibility = type.Accessibility;
                _hasExplicitAccessibility = true;
            }

            if (_signature is null && type.Signature is not null)
                _signature = type.Signature;
        }

        if (_typeParameters.Count == 0 && type.TypeParameters.Count > 0)
            _typeParameters.AddRange(type.TypeParameters);

        AddUniqueAttributes(type.Attributes);
        AddUniqueBaseTypes(type.BaseTypes);
        AddUnique(_modifiers, _modifierKeys, type.Modifiers);
        AddMembers(type.Members);

        foreach (var nestedType in type.NestedTypes)
        {
            var key = new TypeKey(nestedType.Name, nestedType.Kind);
            if (!_nestedTypes.TryGetValue(key, out var existing))
            {
                _nestedTypes[key] = TypeNodeBuilder.FromSnapshot(nestedType);
                continue;
            }

            existing.Merge(nestedType);
        }
    }

    private void AddMembers(IEnumerable<DeclaredMember> members)
    {
        foreach (var member in members)
            AddMember(member);
    }

    private void AddMember(DeclaredMember member)
    {
        var index = FindCompatibleMember(member);
        if (index >= 0)
        {
            MergeMember(index, member);
            return;
        }

        _memberKeys.Add(MemberKey.From(member));
        _members.Add(member);
    }

    private int FindCompatibleMember(DeclaredMember member)
    {
        var key = MemberKey.From(member);
        if (_memberKeys.Contains(key))
            return _members.FindIndex(existing => MemberKey.From(existing).Equals(key));

        for (var index = 0; index < _members.Count; index++)
        {
            if (AreCompatibleMembers(_members[index], member))
                return index;
        }

        return -1;
    }

    private static bool AreCompatibleMembers(DeclaredMember left, DeclaredMember right)
    {
        if (left.Name != right.Name ||
            left.Kind != right.Kind ||
            left.Accessibility != right.Accessibility ||
            !TypeRefCompatibility.AreCompatible(left.ExplicitInterface, right.ExplicitInterface))
            return false;

        return AreCompatibleSignatures(left.Signature, right.Signature);
    }

    private static bool AreCompatibleSignatures(FunctionSignature? left, FunctionSignature? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        if (!TypeRefCompatibility.AreCompatible(left.ReturnType, right.ReturnType))
            return false;

        if (left.Parameters.Count != right.Parameters.Count)
            return false;

        for (var index = 0; index < left.Parameters.Count; index++)
        {
            var leftParameter = left.Parameters[index];
            var rightParameter = right.Parameters[index];
            if (leftParameter.Name != rightParameter.Name ||
                leftParameter.DefaultValue != rightParameter.DefaultValue ||
                !leftParameter.Modifiers.SequenceEqual(rightParameter.Modifiers) ||
                !TypeRefCompatibility.AreCompatible(leftParameter.Type, rightParameter.Type))
                return false;
        }

        return true;
    }

    private void MergeMember(int index, DeclaredMember incoming)
    {
        var existing = _members[index];
        var oldKey = MemberKey.From(existing);
        var signature = PreferSignature(existing.Signature, incoming.Signature);
        var xmlDocumentation = existing.XmlDocumentation ?? incoming.XmlDocumentation;
        var attributes = UnionAttributes(existing.Attributes, incoming.Attributes);
        var modifiers = UnionPreserveOrder(existing.Modifiers, incoming.Modifiers);
        var accessors = UnionPreserveOrder(existing.Accessors, incoming.Accessors);
        var explicitInterface = TypeRefCompatibility.PreferNullable(
            existing.ExplicitInterface,
            incoming.ExplicitInterface);

        if (signature == existing.Signature &&
            xmlDocumentation == existing.XmlDocumentation &&
            ReferenceEquals(attributes, existing.Attributes) &&
            ReferenceEquals(modifiers, existing.Modifiers) &&
            ReferenceEquals(accessors, existing.Accessors) &&
            explicitInterface == existing.ExplicitInterface)
            return;

        var merged = existing with
        {
            Signature = signature,
            XmlDocumentation = xmlDocumentation,
            Attributes = attributes,
            Modifiers = modifiers,
            Accessors = accessors,
            ExplicitInterface = explicitInterface
        };

        _members[index] = merged;

        var newKey = MemberKey.From(merged);
        if (oldKey.Equals(newKey))
            return;

        _memberKeys.Remove(oldKey);
        _memberKeys.Add(newKey);
    }

    private static FunctionSignature? PreferSignature(FunctionSignature? existing, FunctionSignature? incoming)
    {
        if (existing is null)
            return incoming;
        if (incoming is null)
            return existing;

        var returnType = TypeRefCompatibility.PreferNullable(existing.ReturnType, incoming.ReturnType);
        if (existing.Parameters.Count == 0)
            return returnType == existing.ReturnType ? existing : existing with { ReturnType = returnType };

        List<ParameterInfo>? parameters = null;
        for (var index = 0; index < existing.Parameters.Count; index++)
        {
            var current = existing.Parameters[index];
            var preferredType = TypeRefCompatibility.Prefer(current.Type, incoming.Parameters[index].Type);
            if (preferredType == current.Type)
                continue;

            parameters ??= [.. existing.Parameters];
            parameters[index] = current with { Type = preferredType };
        }

        if (parameters is null && returnType == existing.ReturnType)
            return existing;

        return new FunctionSignature(returnType, parameters ?? existing.Parameters);
    }

    private void AddUniqueAttributes(IEnumerable<AttributeInfo> incoming)
    {
        foreach (var item in incoming)
        {
            if (_attributeKeys.Add(item.Display))
                _attributes.Add(item);
        }
    }

    private void AddUniqueBaseTypes(IEnumerable<BaseTypeRef> incoming)
    {
        foreach (var item in incoming)
        {
            var existingIndex = _baseTypes.FindIndex(existing =>
                existing.Kind == item.Kind &&
                TypeRefCompatibility.AreCompatible(existing.Type, item.Type));

            if (existingIndex < 0)
            {
                var key = $"{item.Kind}:{item.Type.Display}";
                if (!_baseTypeKeys.Add(key))
                    continue;

                _baseTypes.Add(item);
                continue;
            }

            var existing = _baseTypes[existingIndex];
            var preferred = TypeRefCompatibility.Prefer(existing.Type, item.Type);
            if (preferred == existing.Type)
                continue;

            _baseTypeKeys.Remove($"{existing.Kind}:{existing.Type.Display}");
            _baseTypes[existingIndex] = existing with { Type = preferred };
            _baseTypeKeys.Add($"{existing.Kind}:{preferred.Display}");
        }
    }

    private static void AddUnique(List<string> items, HashSet<string> keys, IEnumerable<string> incoming)
    {
        foreach (var item in incoming)
        {
            if (keys.Add(item))
                items.Add(item);
        }
    }

    private static IReadOnlyList<AttributeInfo> UnionAttributes(
        IReadOnlyList<AttributeInfo> existing,
        IReadOnlyList<AttributeInfo> incoming)
    {
        if (incoming.Count == 0)
            return existing;
        if (existing.Count == 0)
            return incoming;

        List<AttributeInfo>? result = null;
        HashSet<string>? keys = null;

        foreach (var item in incoming)
        {
            keys ??= new HashSet<string>(existing.Select(static attribute => attribute.Display), StringComparer.Ordinal);
            if (!keys.Add(item.Display))
                continue;

            result ??= [.. existing];
            result.Add(item);
        }

        return result ?? existing;
    }

    private static IReadOnlyList<string> UnionPreserveOrder(
        IReadOnlyList<string> existing,
        IReadOnlyList<string> incoming)
    {
        if (incoming.Count == 0)
            return existing;
        if (existing.Count == 0)
            return incoming;

        List<string>? result = null;
        HashSet<string>? keys = null;

        foreach (var item in incoming)
        {
            keys ??= new HashSet<string>(existing, StringComparer.Ordinal);
            if (!keys.Add(item))
                continue;

            result ??= [.. existing];
            result.Add(item);
        }

        return result ?? existing;
    }

    public DeclaredType Build()
    {
        var nestedTypes = _nestedTypes
            .OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.Kind)
            .Select(static pair => pair.Value.Build())
            .ToList();

        return new DeclaredType(
            _name,
            _kind,
            _accessibility,
            _xmlDocumentation,
            _signature,
            _attributes,
            _baseTypes,
            _typeParameters,
            _modifiers,
            _members,
            nestedTypes);
    }
}
