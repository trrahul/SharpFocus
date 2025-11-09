using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Aggregated Focus Mode analysis result containing relevant highlights.
/// </summary>
public sealed record FocusModeResponse
{
    /// <summary>
    /// Information about the place that was analyzed.
    /// </summary>
    public required PlaceInfo FocusedPlace { get; init; }

    /// <summary>
    /// Combined ranges from all slices representing relevant code.
    /// </summary>
    public required IReadOnlyList<LspRange> RelevantRanges { get; init; }

    /// <summary>
    /// Structural container ranges used for fading irrelevant code.
    /// </summary>
    public required IReadOnlyList<LspRange> ContainerRanges { get; init; }

    /// <summary>
    /// Backward slice contributing to the analysis.
    /// </summary>
    public SliceResponse? BackwardSlice { get; init; }

    /// <summary>
    /// Forward slice contributing to the analysis.
    /// </summary>
    public SliceResponse? ForwardSlice { get; init; }
}
