using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services.Slicing;

/// <summary>
/// Computes forward slices highlighting propagation of the focused place.
/// </summary>
public sealed class ForwardSliceStrategy : ISliceComputationStrategy
{
    private readonly IPlaceExtractor _placeExtractor;
    private readonly ISliceAliasResolver _aliasResolver;
    private readonly ILogger<ForwardSliceStrategy> _logger;

    public ForwardSliceStrategy(
        IPlaceExtractor placeExtractor,
        ISliceAliasResolver aliasResolver,
        ILogger<ForwardSliceStrategy> logger)
    {
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
        _aliasResolver = aliasResolver ?? throw new ArgumentNullException(nameof(aliasResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SliceDirection Direction { get; } = SliceDirection.Forward;

    public SliceComputationResult Compute(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var processedKeys = new HashSet<string>(StringComparer.Ordinal);
        var queuedKeys = new HashSet<string>(StringComparer.Ordinal);
        var detailMap = new Dictionary<CachedProgramLocation, ForwardSliceDetail>();
        var worklist = new Queue<Place>();

        worklist.Enqueue(context.FocusedPlace);
        queuedKeys.Add(context.FocusCacheKey);

        while (worklist.Count > 0)
        {
            var current = worklist.Dequeue();
            var currentKey = FlowAnalysisCacheEntry.CreateCacheKey(current);
            if (!processedKeys.Add(currentKey))
            {
                continue;
            }

            foreach (var alias in _aliasResolver.ResolveAliasSet(current, context.CacheEntry))
            {
                var aliasKey = FlowAnalysisCacheEntry.CreateCacheKey(alias);
                if (!context.CacheEntry.TryGetReads(aliasKey, out var readLocations) || readLocations.IsDefaultOrEmpty)
                {
                    continue;
                }

                foreach (var cachedLocation in readLocations)
                {
                    if (!FlowAnalysisUtilities.TryResolveProgramLocation(context.ControlFlowGraph, cachedLocation, out var programLocation))
                    {
                        continue;
                    }

                    var operation = FlowAnalysisUtilities.TryGetOperation(programLocation);
                    if (operation?.Syntax is null)
                    {
                        continue;
                    }

                    if (!detailMap.TryGetValue(cachedLocation, out var detail))
                    {
                        var operationPlace = FlowAnalysisUtilities.TryCreateRepresentativePlace(_placeExtractor, operation);
                        detail = new ForwardSliceDetail(programLocation, operation, operationPlace);
                        detailMap[cachedLocation] = detail;
                    }

                    if (!context.CacheEntry.TryGetMutationTargets(cachedLocation, out var targets) || targets.IsDefaultOrEmpty)
                    {
                        continue;
                    }

                    detail.AddTargets(targets);

                    foreach (var target in targets)
                    {
                        var targetKey = FlowAnalysisCacheEntry.CreateCacheKey(target);
                        if (!processedKeys.Contains(targetKey) && queuedKeys.Add(targetKey))
                        {
                            worklist.Enqueue(target);
                        }
                    }
                }
            }
        }

        if (detailMap.Count == 0)
        {
            return SliceComputationResult.Empty;
        }

        var ordered = detailMap.Values
            .OrderBy(detail => detail.Location.Block.Ordinal)
            .ThenBy(detail => detail.Location.OperationIndex)
            .ToList();

        var ranges = new List<LspRange>(ordered.Count);
        var details = new List<SliceRangeInfo>(ordered.Count);

        foreach (var detail in ordered)
        {
            var syntax = detail.Operation.Syntax;
            var preciseSpan = FlowAnalysisUtilities.GetPreciseSyntaxSpan(detail.Operation);
            var range = FlowAnalysisUtilities.ToLspRange(context.SourceText, preciseSpan);
            ranges.Add(range);

            var placeInfo = PlaceInfoFactory.CreatePlaceInfo(
                syntax,
                context.SourceText,
                detail.OperationPlace,
                fallbackKind: detail.Operation.Kind.ToString());

            var relation = detail.HasMutationTargets
                ? SliceRelation.Transform
                : SliceRelation.Sink;

            var summary = SliceSummaryFormatter.FormatForward(
                context.FocusInfo,
                placeInfo,
                relation,
                detail.MutationTargets);

            details.Add(new SliceRangeInfo
            {
                Range = range,
                Place = placeInfo,
                Relation = relation,
                OperationKind = detail.Operation.Kind.ToString(),
                Summary = summary
            });
        }

        IReadOnlyList<LspRange> rangeResult = ranges.Count == 0
            ? Array.Empty<LspRange>()
            : ranges;
        IReadOnlyList<SliceRangeInfo>? detailResult = details.Count == 0 ? null : details;

        return new SliceComputationResult(rangeResult, detailResult);
    }

    private sealed class ForwardSliceDetail
    {
        private readonly HashSet<Place> _mutationTargets = new();

        public ForwardSliceDetail(ProgramLocation location, IOperation operation, Place? operationPlace)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            OperationPlace = operationPlace;
        }

        public ProgramLocation Location { get; }

        public IOperation Operation { get; }

        public Place? OperationPlace { get; }

        public IReadOnlyCollection<Place> MutationTargets => _mutationTargets;

        public bool HasMutationTargets => _mutationTargets.Count > 0;

        public void AddTargets(ImmutableArray<Place> targets)
        {
            foreach (var target in targets)
            {
                _mutationTargets.Add(target);
            }
        }
    }
}
