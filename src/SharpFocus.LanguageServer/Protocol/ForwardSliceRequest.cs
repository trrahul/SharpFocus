using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Request to compute a forward slice for the place at the specified position.
/// </summary>
public sealed record ForwardSliceRequest : IRequest<SliceResponse?>
{
    /// <summary>
    /// The document containing the code to analyze.
    /// </summary>
    public required TextDocumentIdentifier TextDocument { get; init; }

    /// <summary>
    /// The position in the document to analyze.
    /// </summary>
    public required Position Position { get; init; }
}
