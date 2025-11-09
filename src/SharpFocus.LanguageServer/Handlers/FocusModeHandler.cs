using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Handlers;

/// <summary>
/// Handles aggregated Focus Mode requests.
/// </summary>
public sealed class FocusModeHandler : IFocusModeHandler
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly ILogger<FocusModeHandler> _logger;

    public FocusModeHandler(
        IAnalysisOrchestrator orchestrator,
        ILogger<FocusModeHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<FocusModeResponse?> Handle(FocusModeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling Focus Mode request for {Document} at {Line}:{Character}",
            request.TextDocument.Uri,
            request.Position.Line,
            request.Position.Character);

        return _orchestrator.AnalyzeFocusModeAsync(request, cancellationToken);
    }
}
