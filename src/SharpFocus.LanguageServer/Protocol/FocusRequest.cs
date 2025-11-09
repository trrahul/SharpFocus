using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Request to focus on a specific place in the code, highlighting its dependencies.
/// </summary>
public sealed record FocusRequest : IRequest<FocusResponse?>
{
    /// <summary>
    /// The document containing the code to analyze.
    /// </summary>
    public required TextDocumentIdentifier TextDocument { get; init; }

    /// <summary>
    /// The position in the document to focus on.
    /// </summary>
    public required Position Position { get; init; }
}
