using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Engine;

/// <summary>
/// Executes the SharpFocus dataflow analysis by iterating the transfer function
/// to a fixpoint across the control-flow graph.
/// </summary>
public sealed class DataflowEngine : IDataflowEngine
{
    private static readonly PropertyInfo? _switchCaseSuccessorsProperty = typeof(BasicBlock).GetProperty("SwitchCaseSuccessors");
    private static readonly PropertyInfo? _successorsProperty = typeof(BasicBlock).GetProperty("Successors");

    private readonly IDataflowTransferFunction _transferFunction;

    public DataflowEngine(IDataflowTransferFunction transferFunction)
    {
        _transferFunction = transferFunction ?? throw new ArgumentNullException(nameof(transferFunction));
    }

    /// <inheritdoc />
    public FlowAnalysisResults Analyze(ControlFlowGraph cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        _transferFunction.Initialize(cfg);

        var exitStates = new Dictionary<BasicBlock, FlowDomain>();
        foreach (var block in cfg.Blocks)
        {
            exitStates[block] = new FlowDomain();
        }

        var locationStates = new Dictionary<ProgramLocation, FlowDomain>();
        var worklist = new Queue<BasicBlock>();
        var pending = new HashSet<BasicBlock>();

        foreach (var block in cfg.Blocks)
        {
            worklist.Enqueue(block);
            pending.Add(block);
        }

        while (worklist.Count > 0)
        {
            var block = worklist.Dequeue();
            pending.Remove(block);
            var inputState = MergePredecessorStates(block, exitStates);
            var previousState = exitStates[block];
            var outputState = ProcessBlock(block, inputState, locationStates);

            if (!outputState.EquivalentTo(previousState))
            {
                exitStates[block] = outputState;
                EnqueueSuccessors(block, worklist, pending);
            }
        }

        return new FlowAnalysisResults(locationStates);
    }

    private FlowDomain ProcessBlock(
        BasicBlock block,
        FlowDomain inputState,
        Dictionary<ProgramLocation, FlowDomain> locationStates)
    {
        var state = inputState;

        for (var index = 0; index < block.Operations.Length; index++)
        {
            var location = new ProgramLocation(block, index);
            state = _transferFunction.Apply(state, location);
            locationStates[location] = state;
        }

        if (block.BranchValue != null)
        {
            var branchLocation = new ProgramLocation(block, block.Operations.Length);
            state = _transferFunction.Apply(state, branchLocation);
            locationStates[branchLocation] = state;
        }

        return state;
    }

    private static FlowDomain MergePredecessorStates(
        BasicBlock block,
        Dictionary<BasicBlock, FlowDomain> exitStates)
    {
        FlowDomain? merged = null;

        foreach (var predecessor in block.Predecessors)
        {
            if (!exitStates.TryGetValue(predecessor.Source, out var predecessorState))
            {
                continue;
            }

            merged = merged == null
                ? predecessorState.Clone()
                : merged.Join(predecessorState);
        }

        return merged ?? new FlowDomain();
    }

    private static void EnqueueSuccessors(
        BasicBlock block,
        Queue<BasicBlock> worklist,
        HashSet<BasicBlock> pending)
    {
        foreach (var successor in GetSuccessorBlocks(block))
        {
            if (pending.Add(successor))
            {
                worklist.Enqueue(successor);
            }
        }
    }

    private static IEnumerable<BasicBlock> GetSuccessorBlocks(BasicBlock block)
    {
        var seen = new HashSet<BasicBlock>();

        if (block.ConditionalSuccessor?.Destination is BasicBlock conditional && seen.Add(conditional))
            yield return conditional;

        if (block.FallThroughSuccessor?.Destination is BasicBlock fallthrough && seen.Add(fallthrough))
            yield return fallthrough;

        if (_switchCaseSuccessorsProperty?.GetValue(block) is System.Collections.IEnumerable switchBranches)
        {
            foreach (var item in switchBranches)
            {
                if (item is ControlFlowBranch branch && branch.Destination is BasicBlock destination && seen.Add(destination))
                    yield return destination;
            }
        }

        if (_successorsProperty?.GetValue(block) is System.Collections.IEnumerable additionalBranches)
        {
            foreach (var item in additionalBranches)
            {
                if (item is ControlFlowBranch branch && branch.Destination is BasicBlock destination && seen.Add(destination))
                    yield return destination;
            }
        }
    }
}
