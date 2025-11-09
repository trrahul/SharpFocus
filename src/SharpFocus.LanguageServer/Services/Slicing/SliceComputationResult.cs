using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services.Slicing;

/// <summary>
/// Represents the outcome of executing a slice computation strategy.
/// </summary>
public readonly record struct SliceComputationResult(
    IReadOnlyList<LspRange> Ranges,
    IReadOnlyList<SliceRangeInfo>? Details)
{
    public static SliceComputationResult Empty { get; } = new(Array.Empty<LspRange>(), null);

    public bool IsEmpty => Ranges.Count == 0;

    public (int Sources, int Transforms, int Sinks) CountRelations()
    {
        if (Details is null || Details.Count == 0)
        {
            return (0, 0, 0);
        }

        var sources = 0;
        var transforms = 0;
        var sinks = 0;

        foreach (var detail in Details)
        {
            switch (detail.Relation)
            {
                case SliceRelation.Source:
                    sources++;
                    break;
                case SliceRelation.Transform:
                    transforms++;
                    break;
                case SliceRelation.Sink:
                    sinks++;
                    break;
            }
        }

        return (sources, transforms, sinks);
    }
}
