using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Analyzes control flow dependencies in a control flow graph.
/// </summary>
/// <remarks>
/// Control dependencies track how control flow (branches, loops) affects which statements execute.
/// A statement S2 is control-dependent on statement S1 if S1 determines whether S2 executes.
///
/// Example:
/// <code>
/// if (x > 0)      // S1
///     y = 1;      // S2
/// </code>
/// S2 is control-dependent on S1 because the condition at S1 determines if S2 executes.
///
/// This analysis uses dominator analysis to compute control dependencies:
/// - Dominators: A block X dominates Y if all paths to Y must go through X
/// - Control dependence: Y is control-dependent on X if X has multiple successors
///   and Y is reachable from X but not dominated by X
/// </remarks>
public interface IControlFlowDependencyAnalyzer
{
    /// <summary>
    /// Analyzes the control flow graph to compute control dependencies.
    /// </summary>
    /// <param name="cfg">The control flow graph to analyze.</param>
    /// <remarks>
    /// This method should be called once before using GetControlDependencies.
    /// It computes dominators and control dependencies for all blocks in the CFG.
    /// </remarks>
    public void Analyze(ControlFlowGraph cfg);

    /// <summary>
    /// Gets the control dependencies for a specific program location.
    /// </summary>
    /// <param name="location">The program location to get dependencies for.</param>
    /// <returns>
    /// A set of program locations that control whether the given location executes.
    /// Returns an empty set if the location has no control dependencies.
    /// </returns>
    /// <remarks>
    /// Control dependencies are typically branch conditions (if, switch, loops)
    /// that determine whether a statement executes.
    /// </remarks>
    public IReadOnlySet<ProgramLocation> GetControlDependencies(ProgramLocation location);

    /// <summary>
    /// Gets the basic blocks that control whether a given block executes.
    /// </summary>
    /// <param name="block">The basic block to get controlling blocks for.</param>
    /// <returns>
    /// A set of basic blocks containing branch conditions that control the given block.
    /// Returns an empty set if the block has no control dependencies.
    /// </returns>
    public IReadOnlySet<BasicBlock> GetControllingBlocks(BasicBlock block);
}
