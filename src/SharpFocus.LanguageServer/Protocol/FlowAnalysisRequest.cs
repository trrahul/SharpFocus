using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Request to compute Flow Analysis results for both slice directions.
/// </summary>
public sealed record FlowAnalysisRequest : IRequest<FlowAnalysisResponse?>
{
    /// <summary>
    /// Document to analyze.
    /// </summary>
    public required TextDocumentIdentifier TextDocument { get; init; }

    /// <summary>
    /// Cursor position within the document.
    /// </summary>
    public required Position Position { get; init; }
}
