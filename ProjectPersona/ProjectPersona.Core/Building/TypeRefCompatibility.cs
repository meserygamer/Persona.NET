using System.Text;
using ProjectPersona.Core.Dto;

namespace ProjectPersona.Core.Building;

/// <summary>
/// Сравнивает типы для слияния снимков: resolved FQN и нерезолвленное короткое имя считаются одним типом.
/// </summary>
internal static class TypeRefCompatibility
{
    public static bool AreCompatible(TypeRef? left, TypeRef? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return left is null && right is null;
        if (left.Display == right.Display)
            return true;

        if (left.Kind != TypeRefKind.Unresolved && right.Kind != TypeRefKind.Unresolved)
            return false;

        return Unqualify(left.Display) == Unqualify(right.Display);
    }

    public static TypeRef Prefer(TypeRef left, TypeRef right) =>
        left.Kind == TypeRefKind.Unresolved && right.Kind != TypeRefKind.Unresolved
            ? right
            : left;

    public static TypeRef? PreferNullable(TypeRef? left, TypeRef? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;
        return Prefer(left, right);
    }

    public static string Unqualify(string typeDisplay)
    {
        var trimmed = typeDisplay.Trim();
        if (trimmed.StartsWith("global::", StringComparison.Ordinal))
            trimmed = trimmed["global::".Length..];

        return UnqualifyType(trimmed);
    }

    private static string UnqualifyType(string type)
    {
        type = type.Trim();
        if (type.Length == 0)
            return type;

        var suffixStart = FindSuffixStart(type);
        var core = suffixStart >= 0 ? type[..suffixStart] : type;
        var suffix = suffixStart >= 0 ? type[suffixStart..] : string.Empty;

        if (core.StartsWith('(') && core.EndsWith(')'))
            return UnqualifyTuple(core) + suffix;

        var genericStart = IndexOfGenericStart(core);
        if (genericStart < 0)
            return CanonicalSimpleName(LastIdentifier(core)) + suffix;

        var name = CanonicalSimpleName(LastIdentifier(core[..genericStart]));
        var args = SplitGenericArguments(core[(genericStart + 1)..^1]);
        var builder = new StringBuilder(name.Length + core.Length - genericStart);
        builder.Append(name);
        builder.Append('<');
        for (var index = 0; index < args.Count; index++)
        {
            if (index > 0)
                builder.Append(", ");
            builder.Append(UnqualifyType(args[index]));
        }

        builder.Append('>');
        builder.Append(suffix);
        return builder.ToString();
    }

    private static string UnqualifyTuple(string tuple)
    {
        var inner = tuple[1..^1];
        var elements = SplitGenericArguments(inner);
        var builder = new StringBuilder(tuple.Length);
        builder.Append('(');
        for (var index = 0; index < elements.Count; index++)
        {
            if (index > 0)
                builder.Append(", ");
            builder.Append(UnqualifyType(elements[index]));
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static int FindSuffixStart(string type)
    {
        var depth = 0;
        for (var index = 0; index < type.Length; index++)
        {
            var ch = type[index];
            switch (ch)
            {
                case '<' or '(':
                    depth++;
                    break;
                case '>' or ')':
                    depth--;
                    break;
                case '[' or '*' or '?' when depth == 0 && index > 0:
                    return index;
            }
        }

        return -1;
    }

    private static int IndexOfGenericStart(string type)
    {
        var depth = 0;
        for (var index = 0; index < type.Length; index++)
        {
            var ch = type[index];
            switch (ch)
            {
                case '<' when depth == 0:
                    return index;
                case '<' or '(':
                    depth++;
                    break;
                case '>' or ')':
                    depth--;
                    break;
            }
        }

        return -1;
    }

    private static List<string> SplitGenericArguments(string arguments)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
            return result;

        var depth = 0;
        var start = 0;
        for (var index = 0; index < arguments.Length; index++)
        {
            var ch = arguments[index];
            switch (ch)
            {
                case '<' or '(' or '[':
                    depth++;
                    break;
                case '>' or ')' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(arguments[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }

        result.Add(arguments[start..].Trim());
        return result;
    }

    private static string LastIdentifier(string qualifiedName)
    {
        var lastDot = qualifiedName.LastIndexOf('.');
        return lastDot < 0 ? qualifiedName.Trim() : qualifiedName[(lastDot + 1)..].Trim();
    }

    private static string CanonicalSimpleName(string name) =>
        name switch
        {
            "Object" => "object",
            "String" => "string",
            "Boolean" => "bool",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "Int16" => "short",
            "UInt16" => "ushort",
            "Int32" => "int",
            "UInt32" => "uint",
            "Int64" => "long",
            "UInt64" => "ulong",
            "IntPtr" => "nint",
            "UIntPtr" => "nuint",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "Char" => "char",
            "Void" => "void",
            _ => name
        };
}
