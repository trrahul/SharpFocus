using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services.Slicing;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Default implementation of <see cref="IDataflowSliceService"/> that orchestrates Roslyn-based flow analysis.
/// </summary>
public sealed class DataflowSliceService : IDataflowSliceService
{
    private readonly IAnalysisContextBuilder _contextBuilder;
    private readonly Dictionary<SliceDirection, ISliceComputationStrategy> _strategyMap;
    private readonly ILogger<DataflowSliceService> _logger;

    public DataflowSliceService(
        IAnalysisContextBuilder contextBuilder,
        IEnumerable<ISliceComputationStrategy> strategies,
        ILogger<DataflowSliceService> logger)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        ArgumentNullException.ThrowIfNull(strategies);

        _strategyMap = strategies.ToDictionary(strategy => strategy.Direction);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SliceResponse?> ComputeSliceAsync(
        SliceDirection direction,
        TextDocumentIdentifier document,
        Position position,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var context = await _contextBuilder.BuildAsync(document, position, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        return ComputeSliceFromContext(direction, context);
    }

    public SliceResponse? ComputeSliceFromContext(
        SliceDirection direction,
        AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_strategyMap.TryGetValue(direction, out var strategy))
        {
            _logger.LogWarning("Unsupported slice direction {Direction}", direction);
            return null;
        }

        var computation = strategy.Compute(context);
        LogSliceSummary(direction, context, computation);

        return new SliceResponse
        {
            Direction = direction,
            FocusedPlace = context.FocusInfo,
            SliceRanges = computation.Ranges,
            SliceRangeDetails = computation.Details,
            ContainerRanges = context.ContainerRanges
        };
    }

    private void LogSliceSummary(
        SliceDirection direction,
        AnalysisContext context,
        SliceComputationResult result)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var (sources, transforms, sinks) = result.CountRelations();

        _logger.LogDebug(
            "Slice {Direction} for {Place} produced {RangeCount} ranges (sources={SourceCount}, transforms={TransformCount}, sinks={SinkCount})",
            direction,
            context.FocusInfo.Name,
            result.Ranges.Count,
            sources,
            transforms,
            sinks);
    }
}
