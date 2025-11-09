using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Interface for detecting mutations (writes) to memory locations in code.
/// Mutations include assignments, ref/out arguments, increments, decrements, etc.
/// </summary>
public interface IMutationDetector
{
    /// <summary>
    /// Analyzes a control flow graph to find all mutations.
    /// </summary>
    /// <param name="cfg">The control flow graph to analyze.</param>
    /// <returns>Collection of all mutations found in the CFG.</returns>
    public IReadOnlyList<Mutation> DetectMutations(ControlFlowGraph cfg);

    /// <summary>
    /// Analyzes a single operation to determine if it represents a mutation.
    /// </summary>
    /// <param name="operation">The operation to analyze.</param>
    /// <param name="containingBlock">The basic block containing this operation.</param>
    /// <param name="operationIndex">The index of this operation within the block.</param>
    /// <returns>Mutation if the operation is a write, null otherwise.</returns>
    public Mutation? DetectMutation(IOperation operation, BasicBlock containingBlock, int operationIndex);
}
