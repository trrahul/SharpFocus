using System.Collections.Generic;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Defines the transfer function applied during dataflow analysis to propagate dependencies
/// across individual program locations.
/// </summary>
public interface IDataflowTransferFunction
{
    /// <summary>
    /// Prepares the transfer function for a specific control flow graph.
    /// </summary>
    /// <param name="cfg">The control flow graph to analyze.</param>
    /// <remarks>
    /// Implementations may pre-compute auxiliary data such as mutation tables, alias maps, or
    /// control dependencies. This method must be called before invoking <see cref="Apply"/>.
    /// </remarks>
    public void Initialize(ControlFlowGraph cfg);

    /// <summary>
    /// Applies the transfer function to a specific program location.
    /// </summary>
    /// <param name="inputState">The incoming dataflow state.</param>
    /// <param name="location">The program location to analyze.</param>
    /// <returns>The resulting dataflow state after applying the transfer function.</returns>
    public FlowDomain Apply(FlowDomain inputState, ProgramLocation location);

    /// <summary>
    /// Gets the read locations tracked during initialization.
    /// </summary>
    public IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Place>> ReadsByLocation { get; }

    /// <summary>
    /// Gets the mutation locations tracked during initialization.
    /// </summary>
    public IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Mutation>> MutationsByLocation { get; }
}
