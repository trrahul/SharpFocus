using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SharpFocus.LanguageServer.Services.Slicing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Abstractions;
using SharpFocus.LanguageServer.Protocol;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services.Slicing;

/// <summary>
/// Computes backward slices highlighting value sources for the focused place.
/// </summary>
public sealed class BackwardSliceStrategy : ISliceComputationStrategy
{
    private readonly IPlaceExtractor _placeExtractor;
    private readonly ILogger<BackwardSliceStrategy> _logger;

    public BackwardSliceStrategy(IPlaceExtractor placeExtractor, ILogger<BackwardSliceStrategy> logger)
    {
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SliceDirection Direction { get; } = SliceDirection.Backward;

    public SliceComputationResult Compute(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.CacheEntry.TryGetDependencies(context.FocusCacheKey, out var dependencyLocations) || dependencyLocations.IsDefaultOrEmpty)
        {
            BackwardSliceLog.NoDependencies(_logger, context.FocusCacheKey);
            return SliceComputationResult.Empty;
        }

        var ranges = new List<LspRange>(dependencyLocations.Length);
        var details = new List<SliceRangeInfo>(dependencyLocations.Length);

        foreach (var dependency in dependencyLocations)
        {
            if (!FlowAnalysisUtilities.TryResolveProgramLocation(context.ControlFlowGraph, dependency, out var location))
            {
                BackwardSliceLog.UnmappedDependency(_logger, dependency.BlockOrdinal, dependency.OperationIndex);
                continue;
            }

            var operation = FlowAnalysisUtilities.TryGetOperation(location);
            if (operation?.Syntax is null)
            {
                BackwardSliceLog.MissingSyntax(_logger, dependency.BlockOrdinal, dependency.OperationIndex);
                continue;
            }

            var preciseSpan = FlowAnalysisUtilities.GetPreciseSyntaxSpan(operation);
            var range = FlowAnalysisUtilities.ToLspRange(context.SourceText, preciseSpan);
            ranges.Add(range);

            var contributingPlace = FlowAnalysisUtilities.TryCreateRepresentativePlace(_placeExtractor, operation);
            var placeInfo = PlaceInfoFactory.CreatePlaceInfo(
                operation.Syntax,
                context.SourceText,
                contributingPlace,
                fallbackKind: operation.Kind.ToString());

            var summary = SliceSummaryFormatter.FormatBackward(context.FocusInfo, placeInfo);

            details.Add(new SliceRangeInfo
            {
                Range = range,
                Place = placeInfo,
                Relation = SliceRelation.Source,
                OperationKind = operation.Kind.ToString(),
                Summary = summary
            });
        }

        IReadOnlyList<LspRange> rangeResult = ranges.Count == 0
            ? Array.Empty<LspRange>()
            : ranges;
        IReadOnlyList<SliceRangeInfo>? detailResult = details.Count == 0 ? null : details;

        return new SliceComputationResult(rangeResult, detailResult);
    }
}

internal static partial class BackwardSliceLog
{
    [LoggerMessage(EventId = 40, Level = LogLevel.Debug, Message = "No backward dependencies found for cache key {CacheKey}")]
    public static partial void NoDependencies(ILogger logger, string cacheKey);

    [LoggerMessage(EventId = 41, Level = LogLevel.Debug, Message = "Skipping dependency {Block}:{Index} because it could not be mapped to a program location")]
    public static partial void UnmappedDependency(ILogger logger, int block, int index);

    [LoggerMessage(EventId = 42, Level = LogLevel.Debug, Message = "Skipping dependency {Block}:{Index} because it has no syntax node")]
    public static partial void MissingSyntax(ILogger logger, int block, int index);
}
