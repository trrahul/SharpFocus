using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Builds reusable Roslyn analysis contexts for focus and slicing operations.
/// </summary>
public interface IAnalysisContextBuilder
{
    /// <summary>
    /// Attempts to build an analysis context for the given document position.
    /// </summary>
    public Task<AnalysisContext?> BuildAsync(TextDocumentIdentifier document, Position position, CancellationToken cancellationToken);
}
