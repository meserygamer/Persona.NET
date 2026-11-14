using ProjectPersona.SourceIndex.Naming;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.SourceIndex.Projection;

/// <summary>
/// Собирает <see cref="TypeSourceUsedByMatch"/> по целевым объявлениям и индексу потребителей.
/// </summary>
internal static class TypeSourceUsedByProjector
{
    /// <summary>
    /// Формирует ответ <see cref="FindUsedBy"/> для найденных объявлений цели.
    /// </summary>
    /// <param name="targets">Объявления целевого FQN (partial, TFM).</param>
    /// <param name="usedByEntry">Потребители цели или <see langword="null"/>, если рёбра не индексировались.</param>
    /// <param name="memberNames">Имена членов цели или <see langword="null"/> — тип целиком.</param>
    /// <param name="limit">Потолок элементов в каждом списке <c>UsedBy</c>.</param>
    public static IReadOnlyList<TypeSourceUsedByMatch> Project(
        IReadOnlyList<TypeSourceIndexedMatch> targets,
        UsedByFqnEntry? usedByEntry,
        IReadOnlyList<string>? memberNames,
        int limit)
    {
        if (targets.Count == 0)
            return [];

        var filter = TypeSourceMatchFilter.NormalizeMemberNames(memberNames);
        var result = new List<TypeSourceUsedByMatch>(targets.Count);

        foreach (var target in targets)
        {
            var match = filter is null
                ? ProjectType(target, usedByEntry, limit)
                : ProjectMembers(target, usedByEntry, filter, limit);

            result.Add(match);
        }

        return result;
    }

    private static TypeSourceUsedByMatch ProjectType(
        TypeSourceIndexedMatch target,
        UsedByFqnEntry? usedByEntry,
        int limit)
    {
        var consumers = FilterByTargetFramework(
            usedByEntry?.TypeUsedBy ?? [],
            target.Full.TargetFramework);

        var (items, total) = TypeSourceUsedByLimit.Apply(consumers, limit);
        return new TypeSourceUsedByMatch(
            target.Full.AssemblyName,
            target.Full.Type,
            target.Full.TargetFramework,
            items,
            total,
            Members: null);
    }

    private static TypeSourceUsedByMatch ProjectMembers(
        TypeSourceIndexedMatch target,
        UsedByFqnEntry? usedByEntry,
        HashSet<string> memberNames,
        int limit)
    {
        var members = new List<TypeSourceUsedByMemberMatch>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var memberName in memberNames.OrderBy(static name => name, StringComparer.Ordinal))
        {
            var matched = new List<TypeSourceMemberSlice>();
            foreach (var slice in target.Members)
            {
                if (TypeSourceMemberCatalogName.MatchesDeclaredMemberQuery(memberName, slice, target.Full.Type))
                    matched.Add(slice);
            }

            if (matched.Count == 0)
                continue;

            var byName = matched
                .GroupBy(static slice => slice.Name, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var nameGroup in byName)
            {
                var responseName = nameGroup.Key;
                var consumerGroups = TypeSourceMemberCatalogName.GetMemberConsumerGroups(
                    usedByEntry?.MemberUsedBy,
                    target.Full.Type,
                    responseName);

                if (byName.Count == 1 &&
                    !string.Equals(responseName, memberName, StringComparison.Ordinal))
                {
                    consumerGroups = MergeConsumerGroups(
                        consumerGroups,
                        TypeSourceMemberCatalogName.GetMemberConsumerGroups(
                            usedByEntry?.MemberUsedBy,
                            target.Full.Type,
                            memberName));
                }

                var allowedSignatures = new HashSet<string>(StringComparer.Ordinal);
                foreach (var slice in nameGroup)
                    allowedSignatures.Add(slice.Signature);

                consumerGroups = FilterBySignatures(consumerGroups, allowedSignatures);

                var sawConsumers = false;
                foreach (var group in consumerGroups)
                {
                    var key = TypeSourceMemberCatalogName.CreateLookupKey(responseName, group.Signature);
                    if (!emitted.Add(key))
                        continue;

                    sawConsumers = true;
                    var filtered = FilterByTargetFramework(group.UsedBy, target.Full.TargetFramework);
                    var (items, total) = TypeSourceUsedByLimit.Apply(filtered, limit);
                    members.Add(new TypeSourceUsedByMemberMatch(responseName, items, total, group.Signature));
                }

                var declaredSignatures = nameGroup
                    .Select(static slice => slice.Signature)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (declaredSignatures.Count > 1)
                    declaredSignatures.RemoveAll(static signature => signature.Length == 0);

                declaredSignatures.Sort(StringComparer.Ordinal);
                var hasParameterSignature = sawConsumers
                    ? consumerGroups.Any(static group => group.Signature.Length > 0)
                    : declaredSignatures.Any(static signature => signature.Length > 0);

                foreach (var signature in declaredSignatures)
                {
                    if (signature.Length == 0 && hasParameterSignature)
                        continue;

                    var key = TypeSourceMemberCatalogName.CreateLookupKey(responseName, signature);
                    if (!emitted.Add(key))
                        continue;

                    members.Add(new TypeSourceUsedByMemberMatch(responseName, [], 0, signature));
                }
            }
        }

        return new TypeSourceUsedByMatch(
            target.Full.AssemblyName,
            target.Full.Type,
            target.Full.TargetFramework,
            UsedBy: null,
            UsedByTotal: 0,
            members);
    }

    /// <summary>
    /// Оставляет группы потребителей, чья сигнатура есть у отобранных срезов.
    /// Каталожное имя <c>operator -</c> хранит обе арности; запрос <c>op_UnaryNegation</c> или <c>op_Subtraction</c> оставляет одну.
    /// </summary>
    private static IReadOnlyList<MemberUsedBySignatureGroup> FilterBySignatures(
        IReadOnlyList<MemberUsedBySignatureGroup> groups,
        HashSet<string> allowedSignatures)
    {
        if (groups.Count == 0 || allowedSignatures.Count == 0)
            return groups;

        var filtered = new List<MemberUsedBySignatureGroup>(groups.Count);
        foreach (var group in groups)
        {
            if (allowedSignatures.Contains(group.Signature))
                filtered.Add(group);
        }

        return filtered;
    }

    private static IReadOnlyList<MemberUsedBySignatureGroup> MergeConsumerGroups(
        IReadOnlyList<MemberUsedBySignatureGroup> left,
        IReadOnlyList<MemberUsedBySignatureGroup> right)
    {
        if (right.Count == 0)
            return left;

        if (left.Count == 0)
            return right;

        var merged = new Dictionary<string, MemberUsedBySignatureGroup>(StringComparer.Ordinal);
        foreach (var group in left)
            merged[group.Signature] = group;

        foreach (var group in right)
        {
            if (merged.TryGetValue(group.Signature, out var existing))
            {
                merged[group.Signature] = new MemberUsedBySignatureGroup(
                    group.Signature,
                    MergeUsedBy(existing.UsedBy, group.UsedBy));
            }
            else
            {
                merged[group.Signature] = group;
            }
        }

        return merged.Values
            .OrderBy(static group => group.Signature, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<TypeSourceUsedBy> MergeUsedBy(
        IReadOnlyList<TypeSourceUsedBy> left,
        IReadOnlyList<TypeSourceUsedBy> right)
    {
        var merged = new Dictionary<string, TypeSourceUsedBy>(StringComparer.Ordinal);
        foreach (var consumer in left)
            merged[ConsumerKey(consumer)] = consumer;

        foreach (var consumer in right)
            merged[ConsumerKey(consumer)] = consumer;

        return merged.Values
            .OrderBy(static consumer => consumer.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.TargetFramework, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.Type, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.MemberName, StringComparer.Ordinal)
            .ThenBy(static consumer => consumer.Signature, StringComparer.Ordinal)
            .ToList();
    }

    private static string ConsumerKey(TypeSourceUsedBy consumer) =>
        $"{consumer.AssemblyName}|{consumer.TargetFramework}|{consumer.Type}|{consumer.MemberName}|{consumer.Signature}";

    private static IReadOnlyList<TypeSourceUsedBy> FilterByTargetFramework(
        IReadOnlyList<TypeSourceUsedBy> consumers,
        string targetFramework)
    {
        if (string.IsNullOrEmpty(targetFramework))
            return consumers;

        return consumers
            .Where(consumer => string.Equals(consumer.TargetFramework, targetFramework, StringComparison.Ordinal))
            .ToList();
    }
}
