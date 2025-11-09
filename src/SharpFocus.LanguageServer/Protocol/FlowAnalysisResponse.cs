namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Combined Flow Analysis result containing both slice directions.
/// </summary>
public sealed record FlowAnalysisResponse
{
    /// <summary>
    /// Backward slice result, if available.
    /// </summary>
    public SliceResponse? BackwardSlice { get; init; }

    /// <summary>
    /// Forward slice result, if available.
    /// </summary>
    public SliceResponse? ForwardSlice { get; init; }
}
