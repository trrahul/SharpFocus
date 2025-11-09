using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Abstractions;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Handlers;

/// <summary>
/// Handles focus requests to highlight data dependencies for a selected place.
/// </summary>
public sealed class FocusHandler : IFocusHandler
{
    private readonly IAnalysisContextBuilder _contextBuilder;
    private readonly IPlaceExtractor _placeExtractor;
    private readonly ILogger<FocusHandler> _logger;

    public FocusHandler(
        IAnalysisContextBuilder contextBuilder,
        IPlaceExtractor placeExtractor,
        ILogger<FocusHandler> logger)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FocusResponse?> Handle(FocusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            _logger.LogInformation(
                "Processing focus request for {FilePath} at position {Line}:{Character}",
                request.TextDocument.Uri.GetFileSystemPath(),
                request.Position.Line,
                request.Position.Character);

            var context = await _contextBuilder
                .BuildAsync(request.TextDocument, request.Position, cancellationToken)
                .ConfigureAwait(false);

            if (context is null)
            {
                return null;
            }

            var dependencyRanges = new List<LspRange>();
            var dependencyDetails = new List<DependencyRangeInfo>();

            if (context.CacheEntry.TryGetDependencies(context.FocusCacheKey, out var dependencyLocations))
            {
                foreach (var dependency in dependencyLocations)
                {
                    if (!FlowAnalysisUtilities.TryResolveProgramLocation(context.ControlFlowGraph, dependency, out var location))
                    {
                        _logger.LogDebug(
                            "Skipping dependency {Block}:{Index} because it could not be mapped to a program location",
                            dependency.BlockOrdinal,
                            dependency.OperationIndex);
                        continue;
                    }

                    var operation = FlowAnalysisUtilities.TryGetOperation(location);
                    if (operation?.Syntax is null)
                    {
                        _logger.LogDebug(
                            "Skipping dependency {Block}:{Index} because it has no syntax node",
                            dependency.BlockOrdinal,
                            dependency.OperationIndex);
                        continue;
                    }

                    var range = FlowAnalysisUtilities.ToLspRange(context.SourceText, operation.Syntax.Span);
                    dependencyRanges.Add(range);

                    var contributingPlace = FlowAnalysisUtilities.TryCreateRepresentativePlace(_placeExtractor, operation);
                    var dependencyPlace = PlaceInfoFactory.CreatePlaceInfo(
                        operation.Syntax,
                        context.SourceText,
                        contributingPlace,
                        fallbackKind: operation.Kind.ToString());

                    dependencyDetails.Add(new DependencyRangeInfo
                    {
                        Range = range,
                        Place = dependencyPlace
                    });
                }
            }
            else
            {
                _logger.LogDebug("Cache entry did not contain dependencies for key {Key}", context.FocusCacheKey);
            }

            var dependencyDetailsView = dependencyDetails.Count == 0 ? null : dependencyDetails;

            _logger.LogInformation(
                "Found {Count} dependencies for place {Place} in member {Member} (cacheHit={CacheHit})",
                dependencyRanges.Count,
                context.FocusInfo.Name,
                context.MemberDisplayName,
                context.CacheHit);

            _logger.LogDebug("Cache statistics: {@Stats}", context.CacheStatistics);

            for (var i = 0; i < dependencyRanges.Count; i++)
            {
                var range = dependencyRanges[i];
                _logger.LogDebug(
                    "Dependency[{Index}] -> Start {StartLine}:{StartChar}, End {EndLine}:{EndChar}",
                    i,
                    range.Start.Line,
                    range.Start.Character,
                    range.End.Line,
                    range.End.Character);
            }

            return new FocusResponse
            {
                FocusedPlace = context.FocusInfo,
                DependencyRanges = dependencyRanges,
                DependencyRangeDetails = dependencyDetailsView,
                ContainerRanges = context.ContainerRanges
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing focus request");
            return null;
        }
    }
}
