using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Handlers;

/// <summary>
/// Handles Flow Analysis requests returning both slice directions in a single call.
/// </summary>
public sealed class FlowAnalysisHandler : IFlowAnalysisHandler
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly ILogger<FlowAnalysisHandler> _logger;

    public FlowAnalysisHandler(
        IAnalysisOrchestrator orchestrator,
        ILogger<FlowAnalysisHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<FlowAnalysisResponse?> Handle(FlowAnalysisRequest request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling Flow Analysis request for {Document} at {Line}:{Character}",
            request.TextDocument.Uri,
            request.Position.Line,
            request.Position.Character);

        return _orchestrator.AnalyzeFlowAsync(request, cancellationToken);
    }
}
