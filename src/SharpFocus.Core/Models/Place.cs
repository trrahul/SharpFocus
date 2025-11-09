using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SharpFocus.Core.Models;

/// <summary>
/// Represents a memory location in a C# program that can be read or written.
/// A Place consists of a base symbol (variable, parameter, field) and an optional
/// access path representing projections (e.g., obj.field.subfield).
/// </summary>
public sealed record Place
{
    /// <summary>
    /// Gets the base symbol of this place (variable, parameter, field, etc.).
    /// </summary>
    public ISymbol Symbol { get; init; }

    /// <summary>
    /// Gets the projection path from the base symbol.
    /// </summary>
    /// <example>
    /// For <c>obj.field.subfield</c>, the access path contains [field, subfield].
    /// </example>
    public ImmutableList<ISymbol> AccessPath { get; init; }

    /// <summary>
    /// Creates a new Place with the specified base symbol.
    /// </summary>
    /// <param name="symbol">The base symbol (variable, parameter, field, etc.).</param>
    /// <exception cref="ArgumentNullException">Thrown when symbol is null.</exception>
    public Place(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        Symbol = symbol;
        AccessPath = ImmutableList<ISymbol>.Empty;
    }

    /// <summary>
    /// Private constructor for creating places with access paths.
    /// </summary>
    private Place(ISymbol symbol, ImmutableList<ISymbol> accessPath)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(accessPath);
        Symbol = symbol;
        AccessPath = accessPath;
    }

    /// <summary>
    /// Creates a new Place that extends this place with a field projection.
    /// </summary>
    /// <param name="field">The field symbol to project.</param>
    /// <returns>A new Place with the extended access path.</returns>
    /// <exception cref="ArgumentNullException">Thrown when field is null.</exception>
    /// <example>
    /// <code>
    /// var objPlace = new Place(objSymbol);
    /// var fieldPlace = objPlace.WithProjection(fieldSymbol); // obj.field
    /// </code>
    /// </example>
    public Place WithProjection(ISymbol field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return new Place(Symbol, AccessPath.Add(field));
    }

    /// <summary>
    /// Determines if this place is a projection of another place.
    /// </summary>
    /// <param name="other">The potential base place.</param>
    /// <returns>True if this place extends the other place; otherwise, false.</returns>
    public bool IsProjectionOf(Place other)
    {
        if (other == null)
            return false;

        // Must have same base symbol
        if (!SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol))
            return false;

        // Must have longer path
        if (AccessPath.Count <= other.AccessPath.Count)
            return false;

        // All elements of other's path must match
        for (int i = 0; i < other.AccessPath.Count; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(AccessPath[i], other.AccessPath[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of this place.
    /// </summary>
    public override string ToString()
    {
        if (AccessPath.IsEmpty)
            return Symbol.Name;

        var parts = new List<string> { Symbol.Name };
        parts.AddRange(AccessPath.Select(s => s.Name));
        return string.Join(".", parts);
    }

    #region Equality (using SymbolEqualityComparer)

    public bool Equals(Place? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (!SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol))
            return false;

        if (AccessPath.Count != other.AccessPath.Count)
            return false;

        for (int i = 0; i < AccessPath.Count; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(AccessPath[i], other.AccessPath[i]))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Symbol, SymbolEqualityComparer.Default);

        foreach (var symbol in AccessPath)
        {
            hash.Add(symbol, SymbolEqualityComparer.Default);
        }

        return hash.ToHashCode();
    }

    #endregion
}
