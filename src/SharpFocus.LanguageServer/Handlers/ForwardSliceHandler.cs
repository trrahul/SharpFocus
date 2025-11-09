using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Handlers;

/// <summary>
/// Handles forward slice requests by computing statements affected by a focused place.
/// </summary>
public sealed class ForwardSliceHandler : IForwardSliceHandler
{
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly ILogger<ForwardSliceHandler> _logger;

    public ForwardSliceHandler(
        IAnalysisOrchestrator orchestrator,
        ILogger<ForwardSliceHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SliceResponse?> Handle(ForwardSliceRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing forward slice for {Document} at position {Line}:{Character}",
            request.TextDocument.Uri,
            request.Position.Line,
            request.Position.Character);

        try
        {
            return await _orchestrator.ComputeSliceAsync(
                SliceDirection.Forward,
                request.TextDocument,
                request.Position,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing forward slice request");
            return null;
        }
    }
}
