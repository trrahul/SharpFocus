using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Protocol;

/// <summary>
/// Represents a single dependency range with its associated place information.
/// </summary>
public sealed record DependencyRangeInfo
{
    /// <summary>
    /// The code range.
    /// </summary>
    public required LspRange Range { get; init; }

    /// <summary>
    /// Information about the place at this range (symbol name, kind).
    /// </summary>
    public required PlaceInfo Place { get; init; }
}

/// <summary>
/// Response containing the ranges to highlight for a focus request.
/// </summary>
public sealed record FocusResponse
{
    /// <summary>
    /// Information about the place that was focused on.
    /// </summary>
    public required PlaceInfo FocusedPlace { get; init; }

    /// <summary>
    /// Ranges that should be highlighted as dependencies.
    /// </summary>
    public required IReadOnlyList<LspRange> DependencyRanges { get; init; }

    /// <summary>
    /// Detailed information about each dependency range, including place/symbol data.
    /// </summary>
    public IReadOnlyList<DependencyRangeInfo>? DependencyRangeDetails { get; init; }

    /// <summary>
    /// Ranges representing control flow containers (if statements, loops, etc.)
    /// </summary>
    public required IReadOnlyList<LspRange> ContainerRanges { get; init; }
}

/// <summary>
/// Information about a place in the code.
/// </summary>
public sealed record PlaceInfo
{
    /// <summary>
    /// The display name of the place (e.g., variable name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The range where the place is located in the document.
    /// </summary>
    public required LspRange Range { get; init; }

    /// <summary>
    /// The kind of place (local, parameter, field, etc.)
    /// </summary>
    public required string Kind { get; init; }
}
