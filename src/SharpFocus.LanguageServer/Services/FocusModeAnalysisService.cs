using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.Core.Utilities;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Aggregates slice information for Focus Mode rendering.
/// Supports both single-method analysis (via dataflow) and cross-method field tracking.
/// </summary>
public sealed class FocusModeAnalysisService
{
    private readonly IDataflowSliceService _sliceService;
    private readonly IClassSummaryCache _classSummaryCache;
    private readonly ICrossMethodSliceComposer _crossMethodComposer;
    private readonly IWorkspaceManager _workspaceManager;
    private readonly ILogger<FocusModeAnalysisService> _logger;

    public FocusModeAnalysisService(
        IDataflowSliceService sliceService,
        IClassSummaryCache classSummaryCache,
        ICrossMethodSliceComposer crossMethodComposer,
        IWorkspaceManager workspaceManager,
        ILogger<FocusModeAnalysisService> logger)
    {
        _sliceService = sliceService ?? throw new ArgumentNullException(nameof(sliceService));
        _classSummaryCache = classSummaryCache ?? throw new ArgumentNullException(nameof(classSummaryCache));
        _crossMethodComposer = crossMethodComposer ?? throw new ArgumentNullException(nameof(crossMethodComposer));
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(nameof(workspaceManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FocusModeResponse?> AnalyzeAsync(AnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Check if the focused place is a field symbol - if so, use cross-method analysis
        if (context.FocusedPlace is { Symbol: Microsoft.CodeAnalysis.IFieldSymbol fieldSymbol })
        {
            _logger.LogDebug("Detected field symbol {FieldName}, using cross-method analysis", fieldSymbol.Name);
            return await AnalyzeFieldCrossMethodAsync(fieldSymbol, context, cancellationToken).ConfigureAwait(false);
        }

        var backward = _sliceService.ComputeSliceFromContext(SliceDirection.Backward, context);
        var forward = _sliceService.ComputeSliceFromContext(SliceDirection.Forward, context);

        if (backward is null && forward is null)
        {
            _logger.LogInformation("Focus Mode analysis produced no slices for {FilePath}", context.FilePath);
            return null;
        }

        var relevantRanges = MergeRanges(backward?.SliceRanges, forward?.SliceRanges);
        if (relevantRanges.Count == 0)
        {
            _logger.LogInformation("Focus Mode analysis found no relevant ranges for {FilePath}", context.FilePath);
            return null;
        }

        var containerRanges = MergeRanges(backward?.ContainerRanges, forward?.ContainerRanges);
        var focusedPlace = backward?.FocusedPlace ?? forward?.FocusedPlace;
        if (focusedPlace is null)
        {
            _logger.LogWarning("Focus Mode analysis could not determine the focused place for {FilePath}",
                context.FilePath);
            return null;
        }

        var backwardCounts = CountRelations(backward);
        var forwardCounts = CountRelations(forward);

        _logger.LogInformation(
            "Focus Mode summary for {FilePath} ({Symbol}) produced {RelevantCount} relevant ranges, {ContainerCount} containers, backward sources={BackwardSources}, forward transforms={ForwardTransforms}, forward sinks={ForwardSinks}",
            context.FilePath,
            focusedPlace.Name,
            relevantRanges.Count,
            containerRanges.Count,
            backwardCounts.Sources,
            forwardCounts.Transforms,
            forwardCounts.Sinks);

        return new FocusModeResponse
        {
            FocusedPlace = focusedPlace,
            RelevantRanges = relevantRanges,
            ContainerRanges = containerRanges,
            BackwardSlice = backward,
            ForwardSlice = forward
        };
    }

    private static List<LspRange> MergeRanges(params IReadOnlyList<LspRange>?[] sources)
    {
        var result = new List<LspRange>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            if (source is null) continue;

            foreach (var range in source)
            {
                var key = $"{range.Start.Line}:{range.Start.Character}-{range.End.Line}:{range.End.Character}";
                if (seen.Add(key)) result.Add(range);
            }
        }

        return result;
    }

    private static (int Sources, int Transforms, int Sinks) CountRelations(SliceResponse? slice)
    {
        return slice?.SliceRangeDetails is { } details
            ? CountRelations(details)
            : (0, 0, 0);
    }

    private static (int Sources, int Transforms, int Sinks) CountRelations(IReadOnlyList<SliceRangeInfo> details)
    {
        var counts = (Sources: 0, Transforms: 0, Sinks: 0);

        foreach (var detail in details)
            switch (detail.Relation)
            {
                case SliceRelation.Source:
                    counts.Sources++;
                    break;
                case SliceRelation.Transform:
                    counts.Transforms++;
                    break;
                case SliceRelation.Sink:
                    counts.Sinks++;
                    break;
            }

        return counts;
    }

    private async Task<FocusModeResponse?> AnalyzeFieldCrossMethodAsync(
        Microsoft.CodeAnalysis.IFieldSymbol fieldSymbol,
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        var classSymbol = fieldSymbol.ContainingType;
        if (classSymbol == null)
        {
            _logger.LogWarning("Field {FieldName} has no containing type", fieldSymbol.Name);
            return null;
        }

        var primarySyntaxRef = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (primarySyntaxRef == null)
        {
            _logger.LogWarning("Class {ClassName} has no declaring syntax references", classSymbol.Name);
            return null;
        }

        var compilation = await _workspaceManager.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null)
        {
            _logger.LogWarning("Unable to get compilation for cross-method analysis");
            return null;
        }

        var documentUri = primarySyntaxRef.SyntaxTree.FilePath ?? context.FilePath;
        var documentVersion = await GetDocumentVersionAsync(documentUri, primarySyntaxRef.SyntaxTree, cancellationToken)
            .ConfigureAwait(false);

        var summary = await _classSummaryCache
            .GetOrBuildAsync(classSymbol, compilation, documentVersion, cancellationToken)
            .ConfigureAwait(false);

        if (summary.FieldAccesses.Count == 0)
        {
            _logger.LogInformation(
                "Class {ClassName} has no tracked field accesses",
                classSymbol.Name);
            return null;
        }

        var backward = _crossMethodComposer.BuildBackwardSlice(
            fieldSymbol,
            summary,
            context.FocusInfo);

        var forward = _crossMethodComposer.BuildForwardSlice(
            fieldSymbol,
            summary,
            context.FocusInfo);

        if (backward is null && forward is null)
        {
            _logger.LogInformation(
                "No cross-method slices found for field {FieldName}",
                fieldSymbol.Name);
            return null;
        }

        var relevantRanges = MergeRanges(backward?.SliceRanges, forward?.SliceRanges);
        if (relevantRanges.Count == 0)
        {
            _logger.LogInformation(
                "Cross-method analysis found no relevant ranges for field {FieldName}",
                fieldSymbol.Name);
            return null;
        }

        var containerRanges = MergeRanges(backward?.ContainerRanges, forward?.ContainerRanges);
        var backwardCounts = CountRelations(backward);
        var forwardCounts = CountRelations(forward);

        _logger.LogInformation(
            "Cross-method focus mode for field {FieldName} in class {ClassName}: {RelevantCount} relevant ranges across {ContainerCount} methods, backward sources={BackwardSources}, forward sinks={ForwardSinks}",
            fieldSymbol.Name,
            classSymbol.Name,
            relevantRanges.Count,
            containerRanges.Count,
            backwardCounts.Sources,
            forwardCounts.Sinks);

        return new FocusModeResponse
        {
            FocusedPlace = context.FocusInfo,
            RelevantRanges = relevantRanges,
            ContainerRanges = containerRanges,
            BackwardSlice = backward,
            ForwardSlice = forward
        };
    }

    private async Task<int> GetDocumentVersionAsync(string? filePath, Microsoft.CodeAnalysis.SyntaxTree syntaxTree,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var cachedVersion = await _workspaceManager.GetDocumentVersionAsync(filePath!, cancellationToken)
                .ConfigureAwait(false);
            if (cachedVersion.HasValue) return cachedVersion.Value;
        }

        var text = await syntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return DocumentVersionCalculator.Compute(text);
    }
}
