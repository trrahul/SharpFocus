using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Default coordinator for language-server analysis workflows.
/// Builds a single AnalysisContext and reuses it across multiple analysis types.
/// </summary>
public sealed class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly IAnalysisContextBuilder _contextBuilder;
    private readonly IDataflowSliceService _sliceService;
    private readonly FocusModeAnalysisService _focusModeService;
    private readonly AggregatedFlowAnalysisService _flowAnalysisService;
    private readonly ILogger<AnalysisOrchestrator> _logger;

    public AnalysisOrchestrator(
        IAnalysisContextBuilder contextBuilder,
        IDataflowSliceService sliceService,
        FocusModeAnalysisService focusModeService,
        AggregatedFlowAnalysisService flowAnalysisService,
        ILogger<AnalysisOrchestrator> logger)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _sliceService = sliceService ?? throw new ArgumentNullException(nameof(sliceService));
        _focusModeService = focusModeService ?? throw new ArgumentNullException(nameof(focusModeService));
        _flowAnalysisService = flowAnalysisService ?? throw new ArgumentNullException(nameof(flowAnalysisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SliceResponse?> ComputeSliceAsync(
        SliceDirection direction,
        TextDocumentIdentifier document,
        Position position,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var context = await BuildContextAsync(
            document,
            position,
            $"{direction} slice",
            cancellationToken).ConfigureAwait(false);

        if (context is null) return null;

        return _sliceService.ComputeSliceFromContext(direction, context);
    }

    public async Task<FocusModeResponse?> AnalyzeFocusModeAsync(
        FocusModeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await BuildContextAsync(
            request.TextDocument,
            request.Position,
            "focus mode analysis",
            cancellationToken).ConfigureAwait(false);

        if (context is null) return null;

        return await _focusModeService.AnalyzeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FlowAnalysisResponse?> AnalyzeFlowAsync(
        FlowAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await BuildContextAsync(
            request.TextDocument,
            request.Position,
            "flow analysis",
            cancellationToken).ConfigureAwait(false);

        if (context is null) return null;

        return _flowAnalysisService.Analyze(context);
    }

    private async Task<AnalysisContext?> BuildContextAsync(
        TextDocumentIdentifier document,
        Position position,
        string scenario,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Building context for {Scenario} at {Document}:{Line}:{Character}",
            scenario,
            document.Uri,
            position.Line,
            position.Character);

        var context = await _contextBuilder
            .BuildAsync(document, position, cancellationToken)
            .ConfigureAwait(false);

        if (context is null)
            _logger.LogWarning(
                "{Scenario} aborted: unable to build analysis context for {Document}",
                scenario,
                document.Uri);

        return context;
    }
}
