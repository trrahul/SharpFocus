using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Composes cross-method slices for field symbols by aggregating field access information
/// from ClassDataflowSummary. Enables field tracking across multiple methods in the same class.
/// </summary>
public interface ICrossMethodSliceComposer
{
    /// <summary>
    /// Builds a backward slice for a field symbol, showing all methods that write to or modify the field.
    /// Filters field accesses for Write and ReadWrite access types.
    /// </summary>
    /// <param name="fieldSymbol">The field symbol to analyze.</param>
    /// <param name="summary">The class dataflow summary containing field access information.</param>
    /// <param name="sourceText">The source text for computing LSP ranges.</param>
    /// <param name="focusedPlace">Place information for the focused field.</param>
    /// <returns>A slice response containing all write/modify locations, or null if no accesses found.</returns>
    public SliceResponse? BuildBackwardSlice(
        IFieldSymbol fieldSymbol,
        ClassDataflowSummary summary,
        PlaceInfo focusedPlace);

    /// <summary>
    /// Builds a forward slice for a field symbol, showing all methods that read or use the field.
    /// Filters field accesses for Read and ReadWrite access types.
    /// </summary>
    /// <param name="fieldSymbol">The field symbol to analyze.</param>
    /// <param name="summary">The class dataflow summary containing field access information.</param>
    /// <param name="sourceText">The source text for computing LSP ranges.</param>
    /// <param name="focusedPlace">Place information for the focused field.</param>
    /// <returns>A slice response containing all read/use locations, or null if no accesses found.</returns>
    public SliceResponse? BuildForwardSlice(
        IFieldSymbol fieldSymbol,
        ClassDataflowSummary summary,
        PlaceInfo focusedPlace);
}
