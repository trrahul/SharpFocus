using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Represents a single range in a slice with its associated place information.
/// </summary>
public sealed record SliceRangeInfo
{
    /// <summary>
    /// The code range.
    /// </summary>
    public required LspRange Range { get; init; }

    /// <summary>
    /// Information about the key place involved in this range.
    /// </summary>
    public required PlaceInfo Place { get; init; }

    /// <summary>
    /// Relationship between this range and the focused place.
    /// </summary>
    public required SliceRelation Relation { get; init; }

    /// <summary>
    /// The Roslyn operation kind driving this range.
    /// </summary>
    public required string OperationKind { get; init; }

    /// <summary>
    /// Optional human-readable summary describing the relationship.
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// Represents the result of a slicing request.
/// </summary>
public sealed record SliceResponse
{
    /// <summary>
    /// Direction of the slice that produced these ranges.
    /// </summary>
    public required SliceDirection Direction { get; init; }

    /// <summary>
    /// Information about the place that the slice targets.
    /// </summary>
    public required PlaceInfo FocusedPlace { get; init; }

    /// <summary>
    /// Ranges that compose the slice.
    /// </summary>
    public required IReadOnlyList<LspRange> SliceRanges { get; init; }

    /// <summary>
    /// Detailed information about each range in the slice, including place/symbol data.
    /// </summary>
    public IReadOnlyList<SliceRangeInfo>? SliceRangeDetails { get; init; }

    /// <summary>
    /// Optional container ranges that provide structural context.
    /// </summary>
    public required IReadOnlyList<LspRange> ContainerRanges { get; init; }
}

/// <summary>
/// Indicates whether a slice is backward (toward dependencies) or forward (toward dependents).
/// </summary>
public enum SliceDirection
{
    Backward,
    Forward
}

/// <summary>
/// Relationship classification for a range within a slice.
/// </summary>
public enum SliceRelation
{
    /// <summary>
    /// This range supplies data that flows into the focused place.
    /// </summary>
    Source,

    /// <summary>
    /// This range transforms or propagates the focused data.
    /// </summary>
    Transform,

    /// <summary>
    /// This range consumes data that originated from the focused place.
    /// </summary>
    Sink
}
