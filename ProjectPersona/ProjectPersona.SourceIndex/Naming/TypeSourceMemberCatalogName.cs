using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Naming;

/// <summary>
/// Имя члена цели в формате каталога для ключей <c>member_used_by</c> и запроса <c>memberNames</c>.
/// </summary>
internal static class TypeSourceMemberCatalogName
{
    private const char LookupKeySeparator = '\u001e';

    /// <summary>
    /// Возвращает имя целевого члена по ссылке из <see cref="TypeSourceReferenceCollector"/>.
    /// </summary>
    /// <param name="reference">Семантическая ссылка на член цели.</param>
    public static string FromReferencedMember(TypeSourceReference reference) =>
        reference.Kind switch
        {
            TypeSourceReferenceKind.Indexer => "this",
            TypeSourceReferenceKind.Constructor => FromConstructor(reference),
            _ => reference.Name
        };

    /// <summary>
    /// Ключ группы потребителей в индексе: имя члена и опциональная сигнатура перегрузки.
    /// </summary>
    public static string CreateLookupKey(string memberName, string signature) =>
        string.IsNullOrEmpty(signature)
            ? memberName
            : $"{memberName}{LookupKeySeparator}{signature}";

    /// <summary>
    /// Разбирает ключ группы на имя члена и сигнатуру.
    /// </summary>
    public static (string MemberName, string Signature) ParseLookupKey(string lookupKey)
    {
        var separatorIndex = lookupKey.IndexOf(LookupKeySeparator);
        if (separatorIndex < 0)
            return (lookupKey, string.Empty);

        return (lookupKey[..separatorIndex], lookupKey[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Каталожное имя члена для ответа <c>usedBy</c>. Символьное <c>op_*</c> сводится к <c>operator …</c>.
    /// </summary>
    /// <param name="memberName">Имя из запроса или ключ хранения.</param>
    public static string ToCatalogName(string memberName) =>
        TryMapMetadataNameToOperatorCatalogName(memberName, out var catalogName)
            ? catalogName
            : memberName;

    /// <summary>
    /// Имя члена для ответа <c>find</c>/<c>usedBy</c>: каталожное имя объявленного среза, если запрос к нему относится.
    /// </summary>
    /// <param name="target">Объявление целевого типа.</param>
    /// <param name="memberName">Имя из запроса.</param>
    public static string ToResponseCatalogName(TypeSourceIndexedMatch target, string memberName)
    {
        string? explicitName = null;
        foreach (var slice in target.Members)
        {
            if (!MatchesDeclaredMemberQuery(memberName, slice, target.Full.Type))
                continue;

            if (string.Equals(memberName, slice.Name, StringComparison.Ordinal))
                return slice.Name;

            explicitName ??= slice.Name;
        }

        return explicitName ?? ToCatalogName(memberName);
    }

    /// <summary>
    /// Сигнатуры объявленных срезов, которые покрывает <paramref name="memberName"/>.
    /// </summary>
    /// <param name="target">Объявление целевого типа.</param>
    /// <param name="memberName">Имя из запроса <c>memberNames</c>.</param>
    public static IReadOnlyList<string> GetDeclaredSignatures(TypeSourceIndexedMatch target, string memberName)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slice in target.Members)
        {
            if (MatchesDeclaredMemberQuery(memberName, slice, target.Full.Type))
                signatures.Add(slice.Signature);
        }

        if (signatures.Count > 1)
            signatures.Remove(string.Empty);

        return signatures
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Проверяет, есть ли у объявления срез члена с каталожным именем <paramref name="memberName"/>.
    /// </summary>
    public static bool IsDeclaredMember(TypeSourceIndexedMatch target, string memberName)
    {
        foreach (var slice in target.Members)
        {
            if (MatchesDeclaredMemberQuery(memberName, slice, target.Full.Type))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Возвращает группы потребителей по имени из запроса <c>memberNames</c>.
    /// </summary>
    public static IReadOnlyList<MemberUsedBySignatureGroup> GetMemberConsumerGroups(
        IReadOnlyDictionary<string, IReadOnlyList<MemberUsedBySignatureGroup>>? memberUsedBy,
        string targetTypeFqn,
        string memberName)
    {
        if (memberUsedBy is null || memberUsedBy.Count == 0)
            return [];

        var merged = new Dictionary<string, MemberUsedBySignatureGroup>(StringComparer.Ordinal);
        foreach (var storageName in EnumerateUsedByLookupNames(memberName, targetTypeFqn))
        {
            if (!memberUsedBy.TryGetValue(storageName, out var groups))
                continue;

            foreach (var group in groups)
            {
                var key = CreateLookupKey(storageName, group.Signature);
                if (merged.TryGetValue(key, out var existing))
                {
                    merged[key] = new MemberUsedBySignatureGroup(
                        group.Signature,
                        MergeConsumers(existing.UsedBy, group.UsedBy));
                }
                else
                {
                    merged[key] = group;
                }
            }
        }

        return merged.Values
            .OrderBy(static group => group.Signature, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Ищет список потребителей члена по имени из запроса или устаревшему ключу <c>.ctor</c>.
    /// </summary>
    /// <param name="memberUsedBy">Индекс потребителей по ключу группы.</param>
    /// <param name="targetTypeFqn">FQN целевого типа.</param>
    /// <param name="memberName">Имя члена из запроса <c>memberNames</c>.</param>
    /// <param name="consumers">Найденный список потребителей.</param>
    /// <returns><see langword="true"/>, если список найден.</returns>
    public static bool TryGetMemberConsumers(
        IReadOnlyDictionary<string, IReadOnlyList<MemberUsedBySignatureGroup>>? memberUsedBy,
        string targetTypeFqn,
        string memberName,
        out IReadOnlyList<TypeSourceUsedBy> consumers)
    {
        var groups = GetMemberConsumerGroups(memberUsedBy, targetTypeFqn, memberName);
        if (groups.Count == 0)
        {
            consumers = [];
            return false;
        }

        consumers = MergeConsumers([], groups.SelectMany(static group => group.UsedBy));
        return true;
    }

    private static IEnumerable<string> EnumerateUsedByLookupNames(string memberName, string targetTypeFqn)
    {
        yield return memberName;

        foreach (var alias in GetUsedByStorageNames(memberName))
            yield return alias;

        var shortName = GetShortTypeName(targetTypeFqn);
        if (string.Equals(memberName, ".ctor", StringComparison.Ordinal))
            yield return shortName;

        if (string.Equals(memberName, shortName, StringComparison.Ordinal))
            yield return ".ctor";
    }

    /// <summary>
    /// Проверяет, что срез объявления соответствует имени из запроса <c>memberNames</c> (включая алиасы операторов).
    /// </summary>
    internal static bool MatchesDeclaredMemberQuery(
        string query,
        TypeSourceMemberSlice slice,
        string targetTypeFqn)
    {
        if (string.Equals(query, slice.Name, StringComparison.Ordinal))
            return true;

        if (!query.Contains('.') &&
            IsExplicitInterfaceMemberName(slice.Name) &&
            string.Equals(query, GetMemberNameTail(slice.Name), StringComparison.Ordinal))
        {
            return true;
        }

        var shortName = GetShortTypeName(targetTypeFqn);
        if (string.Equals(query, ".ctor", StringComparison.Ordinal) &&
            string.Equals(slice.Name, shortName, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(query, shortName, StringComparison.Ordinal) &&
            string.Equals(slice.Name, ".ctor", StringComparison.Ordinal))
        {
            return true;
        }

        if (query.StartsWith("op_Checked", StringComparison.Ordinal))
            return false;

        if (TryGetAmbiguousOperatorArity(query, out var arity))
        {
            if (!string.Equals(slice.Name, ToCatalogName(query), StringComparison.Ordinal))
                return false;

            if (string.IsNullOrEmpty(slice.Signature))
                return arity == 2;

            return CountParameters(slice.Signature) == arity;
        }

        foreach (var alias in GetUsedByStorageNames(slice.Name))
        {
            if (IsAmbiguousMetadataName(alias))
                continue;

            if (string.Equals(alias, query, StringComparison.Ordinal))
                return true;
        }

        foreach (var alias in GetUsedByStorageNames(query))
        {
            if (IsAmbiguousMetadataName(alias))
                continue;

            if (string.Equals(alias, slice.Name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> GetUsedByStorageNames(string catalogMemberName)
    {
        if (catalogMemberName.StartsWith("operator ", StringComparison.Ordinal))
        {
            var token = catalogMemberName["operator ".Length..].Trim();
            foreach (var metadataName in EnumerateMetadataNames(token))
                yield return metadataName;
        }
        else if (string.Equals(catalogMemberName, "op_Implicit", StringComparison.Ordinal))
        {
            yield return "implicit operator";
        }
        else if (string.Equals(catalogMemberName, "op_Explicit", StringComparison.Ordinal))
        {
            yield return "explicit operator";
        }
        else if (catalogMemberName.StartsWith("implicit operator ", StringComparison.Ordinal) ||
                 catalogMemberName.StartsWith("explicit operator ", StringComparison.Ordinal))
        {
            if (catalogMemberName.StartsWith("implicit operator ", StringComparison.Ordinal))
                yield return "op_Implicit";
            else
                yield return "op_Explicit";
        }
        else if (catalogMemberName.StartsWith("op_", StringComparison.Ordinal))
        {
            if (TryMapMetadataNameToOperatorCatalogName(catalogMemberName, out var catalogName))
                yield return catalogName;
        }
    }

    private static IEnumerable<string> EnumerateMetadataNames(string token)
    {
        switch (token)
        {
            case "+":
                yield return "op_Addition";
                yield return "op_UnaryPlus";
                yield break;
            case "-":
                yield return "op_Subtraction";
                yield return "op_UnaryNegation";
                yield break;
        }

        if (TryMapOperatorTokenToMetadataName(token, out var metadataName))
            yield return metadataName;
    }

    private static bool TryGetAmbiguousOperatorArity(string metadataName, out int arity)
    {
        switch (metadataName)
        {
            case "op_UnaryPlus":
            case "op_UnaryNegation":
                arity = 1;
                return true;
            case "op_Addition":
            case "op_Subtraction":
                arity = 2;
                return true;
            default:
                arity = 0;
                return false;
        }
    }

    private static bool IsAmbiguousMetadataName(string metadataName) =>
        TryGetAmbiguousOperatorArity(metadataName, out _);

    private static int CountParameters(string signature)
    {
        if (signature.Length < 2 || signature[0] != '(' || signature[^1] != ')')
            return -1;

        var inner = signature[1..^1].Trim();
        if (inner.Length == 0)
            return 0;

        var depth = 0;
        var count = 1;
        foreach (var character in inner)
        {
            switch (character)
            {
                case '<':
                case '(':
                case '[':
                    depth++;
                    break;
                case '>':
                case ')':
                case ']':
                    if (depth > 0)
                        depth--;
                    break;
                case ',' when depth == 0:
                    count++;
                    break;
            }
        }

        return count;
    }

    private static bool TryMapOperatorTokenToMetadataName(string token, out string metadataName)
    {
        metadataName = token switch
        {
            "+" => "op_Addition",
            "-" => "op_Subtraction",
            "*" => "op_Multiply",
            "/" => "op_Division",
            "%" => "op_Modulus",
            "==" => "op_Equality",
            "!=" => "op_Inequality",
            "<" => "op_LessThan",
            ">" => "op_GreaterThan",
            "<=" => "op_LessThanOrEqual",
            ">=" => "op_GreaterThanOrEqual",
            "!" => "op_LogicalNot",
            "~" => "op_OnesComplement",
            "++" => "op_Increment",
            "--" => "op_Decrement",
            "true" => "op_True",
            "false" => "op_False",
            "&" => "op_BitwiseAnd",
            "|" => "op_BitwiseOr",
            "^" => "op_ExclusiveOr",
            "<<" => "op_LeftShift",
            ">>" => "op_RightShift",
            ">>>" => "op_UnsignedRightShift",
            _ => string.Empty
        };

        return metadataName.Length > 0;
    }

    private static bool TryMapMetadataNameToOperatorCatalogName(string metadataName, out string catalogName)
    {
        catalogName = metadataName switch
        {
            "op_Addition" => "operator +",
            "op_UnaryPlus" => "operator +",
            "op_Subtraction" => "operator -",
            "op_UnaryNegation" => "operator -",
            "op_Multiply" => "operator *",
            "op_Division" => "operator /",
            "op_Modulus" => "operator %",
            "op_Equality" => "operator ==",
            "op_Inequality" => "operator !=",
            "op_LessThan" => "operator <",
            "op_GreaterThan" => "operator >",
            "op_LessThanOrEqual" => "operator <=",
            "op_GreaterThanOrEqual" => "operator >=",
            "op_LogicalNot" => "operator !",
            "op_OnesComplement" => "operator ~",
            "op_Increment" => "operator ++",
            "op_Decrement" => "operator --",
            "op_True" => "operator true",
            "op_False" => "operator false",
            "op_BitwiseAnd" => "operator &",
            "op_BitwiseOr" => "operator |",
            "op_ExclusiveOr" => "operator ^",
            "op_LeftShift" => "operator <<",
            "op_RightShift" => "operator >>",
            "op_UnsignedRightShift" => "operator >>>",
            "op_CheckedAddition" => "operator +",
            "op_CheckedSubtraction" => "operator -",
            "op_CheckedMultiply" => "operator *",
            "op_CheckedDivision" => "operator /",
            "op_CheckedUnaryNegation" => "operator -",
            "op_CheckedIncrement" => "operator ++",
            "op_CheckedDecrement" => "operator --",
            _ => string.Empty
        };

        return catalogName.Length > 0;
    }

    private static IReadOnlyList<TypeSourceUsedBy> MergeConsumers(
        IReadOnlyList<TypeSourceUsedBy> left,
        IEnumerable<TypeSourceUsedBy> right)
    {
        var merged = new Dictionary<string, TypeSourceUsedBy>(StringComparer.Ordinal);
        foreach (var consumer in left)
            merged[CreateConsumerKey(consumer)] = consumer;

        foreach (var consumer in right)
            merged[CreateConsumerKey(consumer)] = consumer;

        return merged.Values
            .OrderBy(static consumer => consumer.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.TargetFramework, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.Type, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.MemberName, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.Signature, StringComparer.Ordinal)
            .ToList();
    }

    private static string FromConstructor(TypeSourceReference reference)
    {
        if (!string.Equals(reference.Name, ".ctor", StringComparison.Ordinal))
            return reference.Name;

        return GetShortTypeName(reference.Fqn);
    }

    private static bool IsExplicitInterfaceMemberName(string memberName)
    {
        if (memberName.StartsWith("operator ", StringComparison.Ordinal) ||
            memberName.StartsWith("implicit operator ", StringComparison.Ordinal) ||
            memberName.StartsWith("explicit operator ", StringComparison.Ordinal))
        {
            return false;
        }

        var dot = memberName.LastIndexOf('.');
        return dot > 0 && dot < memberName.Length - 1;
    }

    private static string GetMemberNameTail(string memberName)
    {
        var dot = memberName.LastIndexOf('.');
        return dot < 0 ? memberName : memberName[(dot + 1)..];
    }

    private static string GetShortTypeName(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot < 0 ? fqn : fqn[(lastDot + 1)..];
    }

    private static string CreateConsumerKey(TypeSourceUsedBy consumer) =>
        $"{consumer.AssemblyName}|{consumer.TargetFramework}|{consumer.Type}|{consumer.MemberName}|{consumer.Signature}";
}
