using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Coordinates analysis services for slicing, focus mode, and aggregated flow requests.
/// </summary>
public interface IAnalysisOrchestrator
{
    /// <summary>
    /// Computes a dataflow slice for the specified direction at the given document position.
    /// </summary>
    public Task<SliceResponse?> ComputeSliceAsync(
        SliceDirection direction,
        TextDocumentIdentifier document,
        Position position,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs focus mode analysis which combines backward and forward slices.
    /// </summary>
    public Task<FocusModeResponse?> AnalyzeFocusModeAsync(
        FocusModeRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs aggregated flow analysis returning both slice directions.
    /// </summary>
    public Task<FlowAnalysisResponse?> AnalyzeFlowAsync(
        FlowAnalysisRequest request,
        CancellationToken cancellationToken);
}
