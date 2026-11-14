using ProjectPersona.SourceIndex;
using ProjectPersona.SourceIndex.Models;
using ProjectPersona.SourceIndex.Naming;
using ProjectPersona.SourceIndex.Storage;
using ProjectPersona.SourceIndex.UsedBy;

namespace ProjectPersona.Core.SourceLookup;

/// <summary>
/// Строит обратный индекс потребителей из исходящих <see cref="TypeSourceReference"/> при <see cref="TypeSourceIndexBuilder.Build"/>.
/// </summary>
internal static class TypeSourceUsedByInverter
{
    /// <summary>
    /// Инвертирует ссылки всех объявлений в индекс «кто использует» по FQN цели.
    /// </summary>
    /// <param name="buckets">Накопленные совпадения по FQN объявлений.</param>
    /// <returns>Словарь целевой FQN → потребители типа и членов.</returns>
    public static Dictionary<string, UsedByFqnEntry> Build(Dictionary<string, FqnBucket> buckets)
    {
        var indexedFqns = new HashSet<string>(buckets.Keys, StringComparer.Ordinal);
        var typeAccumulators = new Dictionary<string, TypeUsedByAccumulator>(StringComparer.Ordinal);
        var memberAccumulators = new Dictionary<string, MemberUsedByAccumulator>(StringComparer.Ordinal);

        foreach (var bucket in buckets.Values)
        {
            foreach (var match in bucket.EnumerateMatches())
                ProcessMatch(match, indexedFqns, typeAccumulators, memberAccumulators);
        }

        var result = new Dictionary<string, UsedByFqnEntry>(StringComparer.Ordinal);
        foreach (var fqn in indexedFqns)
        {
            typeAccumulators.TryGetValue(fqn, out var typeAccumulator);
            memberAccumulators.TryGetValue(fqn, out var memberAccumulator);

            result[fqn] = new UsedByFqnEntry(
                typeAccumulator?.Build() ?? [],
                memberAccumulator?.Build() ??
                new Dictionary<string, IReadOnlyList<MemberUsedBySignatureGroup>>(StringComparer.Ordinal));
        }

        return result;
    }

    private static void ProcessMatch(
        TypeSourceIndexedMatch match,
        HashSet<string> indexedFqns,
        Dictionary<string, TypeUsedByAccumulator> typeAccumulators,
        Dictionary<string, MemberUsedByAccumulator> memberAccumulators)
    {
        var memberReferenceKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slice in match.Members)
        {
            var consumer = CreateConsumer(match, slice.Name, slice.Signature);
            foreach (var reference in slice.References)
            {
                memberReferenceKeys.Add(CreateReferenceKey(reference));
                AddEdge(reference, consumer, indexedFqns, typeAccumulators, memberAccumulators);
            }
        }

        var declarationConsumer = CreateConsumer(match, string.Empty, string.Empty);
        foreach (var reference in match.Full.References ?? [])
        {
            if (memberReferenceKeys.Contains(CreateReferenceKey(reference)))
                continue;

            AddEdge(reference, declarationConsumer, indexedFqns, typeAccumulators, memberAccumulators);
        }
    }

    private static void AddEdge(
        TypeSourceReference reference,
        TypeSourceUsedBy consumer,
        HashSet<string> indexedFqns,
        Dictionary<string, TypeUsedByAccumulator> typeAccumulators,
        Dictionary<string, MemberUsedByAccumulator> memberAccumulators)
    {
        if (reference.Kind == TypeSourceReferenceKind.Unresolved ||
            string.IsNullOrEmpty(reference.Fqn) ||
            !indexedFqns.Contains(reference.Fqn))
        {
            return;
        }

        if (reference.Kind == TypeSourceReferenceKind.Type)
        {
            GetTypeAccumulator(typeAccumulators, reference.Fqn).Add(consumer);
            return;
        }

        var memberKey = TypeSourceMemberCatalogName.FromReferencedMember(reference);
        GetMemberAccumulator(memberAccumulators, reference.Fqn)
            .Add(memberKey, reference.Signature, consumer);
    }

    private static TypeSourceUsedBy CreateConsumer(
        TypeSourceIndexedMatch match,
        string memberName,
        string signature) =>
        new(
            match.Full.AssemblyName,
            match.Full.TargetFramework,
            match.Full.Type,
            memberName,
            signature);

    private static string CreateReferenceKey(TypeSourceReference reference) =>
        $"{reference.Kind}|{reference.Fqn}|{reference.Name}|{reference.Signature}";

    private static TypeUsedByAccumulator GetTypeAccumulator(
        Dictionary<string, TypeUsedByAccumulator> accumulators,
        string targetFqn)
    {
        if (!accumulators.TryGetValue(targetFqn, out var accumulator))
        {
            accumulator = new TypeUsedByAccumulator();
            accumulators[targetFqn] = accumulator;
        }

        return accumulator;
    }

    private static MemberUsedByAccumulator GetMemberAccumulator(
        Dictionary<string, MemberUsedByAccumulator> accumulators,
        string targetFqn)
    {
        if (!accumulators.TryGetValue(targetFqn, out var accumulator))
        {
            accumulator = new MemberUsedByAccumulator();
            accumulators[targetFqn] = accumulator;
        }

        return accumulator;
    }

    private sealed class TypeUsedByAccumulator
    {
        private readonly Dictionary<string, TypeSourceUsedBy> _entries = new(StringComparer.Ordinal);

        public void Add(TypeSourceUsedBy consumer) =>
            _entries[CreateConsumerKey(consumer)] = consumer;

        public IReadOnlyList<TypeSourceUsedBy> Build() =>
            _entries.Values
                .OrderBy(static consumer => consumer.AssemblyName, StringComparer.Ordinal)
                .ThenBy(static consumer => consumer.TargetFramework, StringComparer.Ordinal)
                .ThenBy(static consumer => consumer.Type, StringComparer.Ordinal)
                .ThenBy(static consumer => consumer.MemberName, StringComparer.Ordinal)
                .ThenBy(static consumer => consumer.Signature, StringComparer.Ordinal)
                .ToList();
    }

    private sealed class MemberUsedByAccumulator
    {
        private readonly Dictionary<string, Dictionary<string, Dictionary<string, TypeSourceUsedBy>>> _byMember =
            new(StringComparer.Ordinal);

        public void Add(string memberName, string signature, TypeSourceUsedBy consumer)
        {
            if (!_byMember.TryGetValue(memberName, out var bySignature))
            {
                bySignature = new Dictionary<string, Dictionary<string, TypeSourceUsedBy>>(StringComparer.Ordinal);
                _byMember[memberName] = bySignature;
            }

            if (!bySignature.TryGetValue(signature, out var consumers))
            {
                consumers = new Dictionary<string, TypeSourceUsedBy>(StringComparer.Ordinal);
                bySignature[signature] = consumers;
            }

            consumers[CreateConsumerKey(consumer)] = consumer;
        }

        public IReadOnlyDictionary<string, IReadOnlyList<MemberUsedBySignatureGroup>> Build() =>
            _byMember.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<MemberUsedBySignatureGroup>)pair.Value
                    .OrderBy(static signaturePair => signaturePair.Key, StringComparer.Ordinal)
                    .Select(static signaturePair => new MemberUsedBySignatureGroup(
                        signaturePair.Key,
                        signaturePair.Value.Values
                            .OrderBy(static consumer => consumer.AssemblyName, StringComparer.Ordinal)
                            .ThenBy(static consumer => consumer.TargetFramework, StringComparer.Ordinal)
                            .ThenBy(static consumer => consumer.Type, StringComparer.Ordinal)
                            .ThenBy(static consumer => consumer.MemberName, StringComparer.Ordinal)
                            .ThenBy(static consumer => consumer.Signature, StringComparer.Ordinal)
                            .ToList()))
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static string CreateConsumerKey(TypeSourceUsedBy consumer) =>
        $"{consumer.AssemblyName}|{consumer.TargetFramework}|{consumer.Type}|{consumer.MemberName}|{consumer.Signature}";
}
