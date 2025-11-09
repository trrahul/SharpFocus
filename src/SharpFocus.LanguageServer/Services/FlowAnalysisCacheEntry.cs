using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

public readonly record struct CachedProgramLocation(int BlockOrdinal, int OperationIndex);

/// <summary>
/// Represents cached flow-analysis results keyed by symbol projections.
/// </summary>
public sealed class FlowAnalysisCacheEntry
{
    private FlowAnalysisCacheEntry(
        ImmutableDictionary<string, ImmutableArray<CachedProgramLocation>> dependencies,
        ImmutableDictionary<string, ImmutableArray<CachedProgramLocation>> reads,
        ImmutableDictionary<string, ImmutableArray<Place>> aliasSets,
        ImmutableDictionary<CachedProgramLocation, ImmutableArray<Place>> mutationTargets)
    {
        Dependencies = dependencies;
        Reads = reads;
        AliasSets = aliasSets;
        MutationTargets = mutationTargets;
    }

    public ImmutableDictionary<string, ImmutableArray<CachedProgramLocation>> Dependencies { get; }

    public ImmutableDictionary<string, ImmutableArray<CachedProgramLocation>> Reads { get; }

    public ImmutableDictionary<string, ImmutableArray<Place>> AliasSets { get; }

    public ImmutableDictionary<CachedProgramLocation, ImmutableArray<Place>> MutationTargets { get; }

    public static FlowAnalysisCacheEntry Create(
        FlowAnalysisResults results,
        IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Place>> readsByLocation,
        IReadOnlyDictionary<Place, IReadOnlyCollection<Place>> aliasMap,
        IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Mutation>> mutationsByLocation)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(readsByLocation);
        ArgumentNullException.ThrowIfNull(aliasMap);
        ArgumentNullException.ThrowIfNull(mutationsByLocation);

        var dependencyBuilder = new Dictionary<string, HashSet<CachedProgramLocation>>(StringComparer.Ordinal);
        var readBuilder = new Dictionary<string, HashSet<CachedProgramLocation>>(StringComparer.Ordinal);
        var aliasBuilder = new Dictionary<string, HashSet<Place>>(StringComparer.Ordinal);
        var mutationBuilder = new Dictionary<CachedProgramLocation, HashSet<Place>>();

        void EnsureAliasEntry(Place place)
        {
            var key = CreateCacheKey(place);
            if (!aliasBuilder.TryGetValue(key, out var set))
            {
                set = new HashSet<Place>();
                aliasBuilder[key] = set;
            }

            set.Add(place);
        }

        foreach (var location in results.Locations)
        {
            var state = results.GetState(location);
            foreach (var trackedPlace in state.Places)
            {
                EnsureAliasEntry(trackedPlace);

                var placeDependencies = state.GetDependencies(trackedPlace);
                if (placeDependencies.Count == 0)
                {
                    continue;
                }

                var key = CreateCacheKey(trackedPlace);
                if (!dependencyBuilder.TryGetValue(key, out var set))
                {
                    set = new HashSet<CachedProgramLocation>();
                    dependencyBuilder[key] = set;
                }

                foreach (var dependency in placeDependencies)
                {
                    set.Add(new CachedProgramLocation(dependency.Block.Ordinal, dependency.OperationIndex));
                }
            }
        }

        foreach (var (location, readPlaces) in readsByLocation)
        {
            var cachedLocation = new CachedProgramLocation(location.Block.Ordinal, location.OperationIndex);

            foreach (var place in readPlaces)
            {
                EnsureAliasEntry(place);
                var key = CreateCacheKey(place);

                if (!readBuilder.TryGetValue(key, out var set))
                {
                    set = new HashSet<CachedProgramLocation>();
                    readBuilder[key] = set;
                }

                set.Add(cachedLocation);
            }
        }

        foreach (var (place, aliases) in aliasMap)
        {
            EnsureAliasEntry(place);
            var key = CreateCacheKey(place);

            if (!aliasBuilder.TryGetValue(key, out var set))
            {
                set = new HashSet<Place>();
                aliasBuilder[key] = set;
            }

            foreach (var alias in aliases)
            {
                EnsureAliasEntry(alias);
                set.Add(alias);
            }
        }

        foreach (var (location, mutations) in mutationsByLocation)
        {
            if (mutations.Count == 0)
            {
                continue;
            }

            var cachedLocation = new CachedProgramLocation(location.Block.Ordinal, location.OperationIndex);
            if (!mutationBuilder.TryGetValue(cachedLocation, out var targets))
            {
                targets = new HashSet<Place>();
                mutationBuilder[cachedLocation] = targets;
            }

            foreach (var mutation in mutations)
            {
                EnsureAliasEntry(mutation.Target);
                targets.Add(mutation.Target);
            }
        }

        var dependencies = dependencyBuilder.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(loc => loc.BlockOrdinal)
                .ThenBy(loc => loc.OperationIndex)
                .ToImmutableArray());

        var reads = readBuilder.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(loc => loc.BlockOrdinal)
                .ThenBy(loc => loc.OperationIndex)
                .ToImmutableArray());

        var aliasesImmutable = aliasBuilder.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(place => place.ToString(), StringComparer.Ordinal)
                .ToImmutableArray());

        var mutationTargets = mutationBuilder.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(place => place.ToString(), StringComparer.Ordinal)
                .ToImmutableArray());

        return new FlowAnalysisCacheEntry(dependencies, reads, aliasesImmutable, mutationTargets);
    }

    public bool TryGetDependencies(string symbolKey, out ImmutableArray<CachedProgramLocation> locations)
    {
        if (Dependencies.TryGetValue(symbolKey, out var stored))
        {
            locations = stored;
            return true;
        }

        locations = ImmutableArray<CachedProgramLocation>.Empty;
        return false;
    }

    public bool TryGetReads(string symbolKey, out ImmutableArray<CachedProgramLocation> locations)
    {
        if (Reads.TryGetValue(symbolKey, out var stored))
        {
            locations = stored;
            return true;
        }

        locations = ImmutableArray<CachedProgramLocation>.Empty;
        return false;
    }

    public bool TryGetAliases(string symbolKey, out ImmutableArray<Place> aliases)
    {
        if (AliasSets.TryGetValue(symbolKey, out var stored))
        {
            aliases = stored;
            return true;
        }

        aliases = ImmutableArray<Place>.Empty;
        return false;
    }

    public bool TryGetMutationTargets(CachedProgramLocation location, out ImmutableArray<Place> targets)
    {
        if (MutationTargets.TryGetValue(location, out var stored))
        {
            targets = stored;
            return true;
        }

        targets = ImmutableArray<Place>.Empty;
        return false;
    }

    public static string CreateCacheKey(Place place)
    {
        var builder = new StringBuilder();
        builder.Append(BuildSymbolIdentity(place.Symbol));

        foreach (var projection in place.AccessPath)
        {
            builder.Append('|');
            builder.Append(BuildSymbolIdentity(projection));
        }

        return builder.ToString();
    }

    private static string BuildSymbolIdentity(ISymbol symbol)
    {
        var docId = symbol.GetDocumentationCommentId();
        if (!string.IsNullOrEmpty(docId))
        {
            return docId!;
        }

        var sourceLocation = symbol.Locations.FirstOrDefault(static loc => loc.IsInSource && loc.SourceTree is not null);
        if (sourceLocation != null && sourceLocation.SourceTree != null)
        {
            var span = sourceLocation.SourceSpan;
            return $"{sourceLocation.SourceTree.FilePath}:{span.Start}:{span.Length}:{symbol.Name}:{symbol.Kind}";
        }

        return $"[metadata]:{symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}";
    }
}
