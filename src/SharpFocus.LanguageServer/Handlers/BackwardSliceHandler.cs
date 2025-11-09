using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Handlers;

/// <summary>
/// Handles backward slice requests by computing dependencies that reach a focused place.
/// </summary>
public sealed class BackwardSliceHandler : IBackwardSliceHandler
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly ILogger<BackwardSliceHandler> _logger;

    public BackwardSliceHandler(
        IAnalysisOrchestrator orchestrator,
        ILogger<BackwardSliceHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SliceResponse?> Handle(BackwardSliceRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing backward slice for {Document} at position {Line}:{Character}",
            request.TextDocument.Uri,
            request.Position.Line,
            request.Position.Character);

        try
        {
            return await _orchestrator.ComputeSliceAsync(
                SliceDirection.Backward,
                request.TextDocument,
                request.Position,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing backward slice request");
            return null;
        }
    }
}
