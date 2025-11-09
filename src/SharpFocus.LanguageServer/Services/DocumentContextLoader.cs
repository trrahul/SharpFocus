using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Loads Roslyn state for a text document position and resolves the focused place.
/// </summary>
public sealed class DocumentContextLoader
{
    private readonly IWorkspaceManager _workspaceManager;
    private readonly IPlaceResolver _placeResolver;
    private readonly ILogger<DocumentContextLoader> _logger;

    public DocumentContextLoader(
        IWorkspaceManager workspaceManager,
        IPlaceResolver placeResolver,
        ILogger<DocumentContextLoader> logger)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(nameof(workspaceManager));
        _placeResolver = placeResolver ?? throw new ArgumentNullException(nameof(placeResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentContextResult?> LoadAsync(
        TextDocumentIdentifier document,
        Position position,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var filePath = document.Uri.GetFileSystemPath();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Log.DocumentPathMissing(_logger, document.Uri.ToString());
            return null;
        }

        var semanticModel = await _workspaceManager
            .GetSemanticModelAsync(filePath!, cancellationToken)
            .ConfigureAwait(false);

        var syntaxTree = await _workspaceManager
            .GetSyntaxTreeAsync(filePath!, cancellationToken)
            .ConfigureAwait(false);

        if (semanticModel is null || syntaxTree is null)
        {
            Log.RoslynArtifactsUnavailable(_logger, filePath);
            return null;
        }

        var sourceText = await syntaxTree
            .GetTextAsync(cancellationToken)
            .ConfigureAwait(false);

        var linePosition = new LinePosition((int)position.Line, (int)position.Character);
        var absolutePosition = sourceText.Lines.GetPosition(linePosition);

        var root = await syntaxTree
            .GetRootAsync(cancellationToken)
            .ConfigureAwait(false);

        var token = root.FindToken(absolutePosition, findInsideTrivia: true);
        var node = token.Parent;
        if (node is null)
        {
            Log.SyntaxNodeNotFound(_logger, filePath, absolutePosition);
            return null;
        }

        var place = _placeResolver.Resolve(semanticModel, node, token, cancellationToken);
        if (place is null)
        {
            Log.PlaceNotResolved(_logger, filePath, absolutePosition);
            return null;
        }

        FlowAnalysisUtilities.TryFindBodyOwner(node, out var bodyOwner);

        return new DocumentContextResult(
            DocumentUri: document.Uri.ToString(),
            FilePath: filePath!,
            SemanticModel: semanticModel,
            SyntaxTree: syntaxTree,
            SourceText: sourceText,
            FocusNode: node,
            FocusToken: token,
            FocusedPlace: place,
            BodyOwner: bodyOwner,
            AbsolutePosition: absolutePosition);
    }
}

/// <summary>
/// Container for resolved Roslyn context at a specific document position.
/// </summary>
public sealed record DocumentContextResult(
    string DocumentUri,
    string FilePath,
    SemanticModel SemanticModel,
    SyntaxTree SyntaxTree,
    SourceText SourceText,
    SyntaxNode FocusNode,
    SyntaxToken FocusToken,
    Place FocusedPlace,
    SyntaxNode? BodyOwner,
    int AbsolutePosition)
{
    public IFieldSymbol? FieldSymbol => FocusedPlace.Symbol as IFieldSymbol;
}

internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Document URI {DocumentUri} has no local file path and cannot be analysed")]
    public static partial void DocumentPathMissing(ILogger logger, string documentUri);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unable to obtain semantic model or syntax tree for {FilePath}")]
    public static partial void RoslynArtifactsUnavailable(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Could not locate syntax node at position {Position} in {FilePath}")]
    public static partial void SyntaxNodeNotFound(ILogger logger, string filePath, int position);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "No place resolved at position {Position} in {FilePath}")]
    public static partial void PlaceNotResolved(ILogger logger, string filePath, int position);
}
