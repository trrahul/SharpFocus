using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Request to compute Focus Mode analysis for the current cursor location.
/// </summary>
public sealed record FocusModeRequest : IRequest<FocusModeResponse?>
{
    /// <summary>
    /// Document to analyze.
    /// </summary>
    public required TextDocumentIdentifier TextDocument { get; init; }

    /// <summary>
    /// Position inside the document to analyze.
    /// </summary>
    public required Position Position { get; init; }
}
