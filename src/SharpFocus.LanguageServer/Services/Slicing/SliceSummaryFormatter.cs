using System;
using System.Collections.Generic;
using System.Linq;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services.Slicing;

/// <summary>
/// Provides helper methods for creating human readable slice summaries.
/// </summary>
internal static class SliceSummaryFormatter
{
    public static string? FormatBackward(PlaceInfo focusedPlace, PlaceInfo contributingPlace)
    {
        if (string.IsNullOrWhiteSpace(contributingPlace.Name) || string.IsNullOrWhiteSpace(focusedPlace.Name))
        {
            return null;
        }

        if (string.Equals(contributingPlace.Name, focusedPlace.Name, StringComparison.Ordinal))
        {
            return $"{contributingPlace.Name} updates its value";
        }

        return $"{contributingPlace.Name} flows into {focusedPlace.Name}";
    }

    public static string? FormatForward(
        PlaceInfo focusedPlace,
        PlaceInfo operationPlace,
        SliceRelation relation,
        IReadOnlyCollection<SharpFocus.Core.Models.Place> mutationTargets)
    {
        if (string.IsNullOrWhiteSpace(operationPlace.Name) || string.IsNullOrWhiteSpace(focusedPlace.Name))
        {
            return null;
        }

        return relation switch
        {
            SliceRelation.Transform when mutationTargets.Count > 0
                => $"{operationPlace.Name} propagates {focusedPlace.Name} into {FormatPlaceList(mutationTargets)}",
            SliceRelation.Sink => $"{operationPlace.Name} consumes {focusedPlace.Name}",
            _ => $"{operationPlace.Name} observes {focusedPlace.Name}"
        };
    }

    private static string FormatPlaceList(IEnumerable<SharpFocus.Core.Models.Place> places)
    {
        var names = places
            .Select(place => place.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
        {
            return "another location";
        }

        if (names.Count <= 3)
        {
            return string.Join(", ", names);
        }

        var limited = names.Take(3).ToList();
        limited.Add("...");
        return string.Join(", ", limited);
    }
}
