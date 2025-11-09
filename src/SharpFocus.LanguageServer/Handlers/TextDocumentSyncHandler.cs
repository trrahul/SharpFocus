using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Handlers;

/// <summary>
/// Handles text document synchronization between the client and server.
/// </summary>
public sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly IWorkspaceManager _workspaceManager;
    private readonly IFlowAnalysisCache _analysisCache;
    private readonly ILogger<TextDocumentSyncHandler> _logger;

    public TextDocumentSyncHandler(
        IWorkspaceManager workspaceManager,
        IFlowAnalysisCache analysisCache,
        ILogger<TextDocumentSyncHandler> logger)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(nameof(workspaceManager));
        _analysisCache = analysisCache ?? throw new ArgumentNullException(nameof(analysisCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "csharp");
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.cs"),
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = true }
        };
    }

    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document opened: {Uri}", request.TextDocument.Uri);

        await _workspaceManager.UpdateDocumentAsync(
            request.TextDocument.Uri.GetFileSystemPath(),
            request.TextDocument.Text,
            cancellationToken);

        _analysisCache.InvalidateDocument(request.TextDocument.Uri.GetFileSystemPath());

        return Unit.Value;
    }

    public override async Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        if (!request.ContentChanges.Any())
            return Unit.Value;

        _logger.LogDebug("Document changed: {Uri}", request.TextDocument.Uri);

        var content = request.ContentChanges.Last().Text;

        await _workspaceManager.UpdateDocumentAsync(
            request.TextDocument.Uri.GetFileSystemPath(),
            content,
            cancellationToken);

        _analysisCache.InvalidateDocument(request.TextDocument.Uri.GetFileSystemPath());

        return Unit.Value;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document saved: {Uri}", request.TextDocument.Uri);
        return Task.FromResult(Unit.Value);
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document closed: {Uri}", request.TextDocument.Uri);
        _analysisCache.InvalidateDocument(request.TextDocument.Uri.GetFileSystemPath());
        return Task.FromResult(Unit.Value);
    }
}
