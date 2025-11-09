using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Extracts <see cref="Place"/> instances from Roslyn operations.
/// </summary>
public interface IPlaceExtractor
{
    /// <summary>
    /// Attempts to create a <see cref="Place"/> representing the given operation.
    /// Returns <c>null</c> when the operation does not map to a concrete memory location.
    /// </summary>
    /// <param name="operation">The Roslyn operation to analyze.</param>
    /// <returns>The corresponding <see cref="Place"/> or <c>null</c>.</returns>
    public Place? TryCreatePlace(IOperation? operation);
}
