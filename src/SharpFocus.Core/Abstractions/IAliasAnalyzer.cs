using System.Collections.Generic;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Provides aliasing information between <see cref="Place"/> instances within a control flow graph.
/// </summary>
public interface IAliasAnalyzer
{
    /// <summary>
    /// Analyses the supplied control flow graph and records alias relationships.
    /// </summary>
    /// <param name="cfg">The control flow graph to inspect.</param>
    public void Analyze(ControlFlowGraph cfg);

    /// <summary>
    /// Gets the set of places that may alias the supplied <paramref name="place"/>.
    /// </summary>
    /// <param name="place">The place whose aliases are requested.</param>
    /// <returns>A read-only set of aliasing places. Returns an empty set when none are known.</returns>
    public IReadOnlySet<Place> GetAliases(Place place);

    /// <summary>
    /// Checks whether two places may alias.
    /// </summary>
    /// <param name="left">First place.</param>
    /// <param name="right">Second place.</param>
    /// <returns><c>true</c> when both places may refer to the same storage location.</returns>
    public bool AreAliased(Place left, Place right);

    /// <summary>
    /// Produces a snapshot of the tracked alias relationships for caching or diagnostics.
    /// </summary>
    public IReadOnlyDictionary<Place, IReadOnlyCollection<Place>> ExportAliases();
}
