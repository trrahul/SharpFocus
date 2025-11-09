using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Provides slice computations for the language server.
/// </summary>
public interface IDataflowSliceService
{
    /// <summary>
    /// Computes a slice for the requested location.
    /// </summary>
    /// <param name="direction">The slice direction to compute.</param>
    /// <param name="document">The document containing the target location.</param>
    /// <param name="position">The position to analyze.</param>
    /// <param name="cancellationToken">Token used to cancel the computation.</param>
    /// <returns>A populated <see cref="SliceResponse"/> when analysis succeeded, or <c>null</c> otherwise.</returns>
    public Task<SliceResponse?> ComputeSliceAsync(
        SliceDirection direction,
        TextDocumentIdentifier document,
        Position position,
        CancellationToken cancellationToken);

    /// <summary>
    /// Computes a slice using a pre-built analysis context.
    /// </summary>
    /// <param name="direction">The slice direction to compute.</param>
    /// <param name="context">Pre-built Roslyn analysis context.</param>
    /// <returns>A populated <see cref="SliceResponse"/> when analysis succeeded, or <c>null</c> otherwise.</returns>
    public SliceResponse? ComputeSliceFromContext(
        SliceDirection direction,
        AnalysisContext context);
}
