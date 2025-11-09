using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services.Slicing;

/// <summary>
/// Resolves the alias closure for a place using cached flow-analysis results.
/// </summary>
public interface ISliceAliasResolver
{
    public HashSet<Place> ResolveAliasSet(Place place, FlowAnalysisCacheEntry cacheEntry);
}

public sealed class SliceAliasResolver : ISliceAliasResolver
{
    public HashSet<Place> ResolveAliasSet(Place place, FlowAnalysisCacheEntry cacheEntry)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(cacheEntry);

        var aliases = new HashSet<Place> { place };
        var key = FlowAnalysisCacheEntry.CreateCacheKey(place);

        AppendAliases(cacheEntry, key, aliases);

        if (place.AccessPath.Count > 0)
        {
            foreach (var basePlace in EnumerateBasePlaces(place))
            {
                var baseKey = FlowAnalysisCacheEntry.CreateCacheKey(basePlace);
                if (!cacheEntry.TryGetAliases(baseKey, out var baseAliases) || baseAliases.IsDefaultOrEmpty)
                {
                    continue;
                }

                foreach (var baseAlias in baseAliases)
                {
                    var projected = ProjectPlace(baseAlias, place.AccessPath);
                    if (projected != null)
                    {
                        aliases.Add(projected);
                    }
                }
            }
        }

        return aliases;
    }

    private static void AppendAliases(
        FlowAnalysisCacheEntry cacheEntry,
        string key,
        HashSet<Place> accumulator)
    {
        if (!cacheEntry.TryGetAliases(key, out var stored) || stored.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var alias in stored)
        {
            accumulator.Add(alias);
        }
    }

    private static IEnumerable<Place> EnumerateBasePlaces(Place place)
    {
        var current = new Place(place.Symbol);
        yield return current;

        for (var index = 0; index < place.AccessPath.Count; index++)
        {
            current = current.WithProjection(place.AccessPath[index]);
            yield return current;
        }
    }

    private static Place? ProjectPlace(Place basePlace, ImmutableList<ISymbol> projection)
    {
        if (projection.Count == 0)
        {
            return basePlace;
        }

        var result = basePlace;
        foreach (var component in projection)
        {
            result = result.WithProjection(component);
        }

        return result;
    }
}
