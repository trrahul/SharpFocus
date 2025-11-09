using System.Reflection;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Analyzers;

/// <summary>
/// Computes control flow dependencies using dominator and post-dominator analysis.
/// </summary>
/// <remarks>
/// This analyzer implements control dependence analysis through:
/// 1. Computing dominators for each block (which blocks must execute before this one)
/// 2. Identifying branch points (blocks with multiple successors)
/// 3. Determining which blocks are control-dependent on each branch
///
/// A block Y is control-dependent on block X if:
/// - X has multiple successors (it's a branch)
/// - Y is reachable from one of X's successors
/// - Y is not dominated by X (X doesn't always execute before Y)
/// </remarks>
public class ControlFlowDependencyAnalyzer : IControlFlowDependencyAnalyzer
{
    private readonly Dictionary<BasicBlock, HashSet<BasicBlock>> _dominators = new();
    private readonly Dictionary<BasicBlock, HashSet<BasicBlock>> _controlDependencies = new();
    private readonly Dictionary<BasicBlock, HashSet<BasicBlock>> _postDominators = new();
    private readonly Dictionary<BasicBlock, BasicBlock?> _immediatePostDominators = new();
    private ControlFlowGraph? _cfg;

    private static readonly PropertyInfo? _switchCaseSuccessorsProperty = typeof(BasicBlock).GetProperty("SwitchCaseSuccessors");
    private static readonly PropertyInfo? _successorsProperty = typeof(BasicBlock).GetProperty("Successors");

    /// <inheritdoc/>
    public void Analyze(ControlFlowGraph cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        _cfg = cfg;
        _dominators.Clear();
        _controlDependencies.Clear();
        _postDominators.Clear();
        _immediatePostDominators.Clear();

        ComputeDominators();
        ComputePostDominators();
        ComputeControlDependencies();
    }

    /// <inheritdoc/>
    public IReadOnlySet<ProgramLocation> GetControlDependencies(ProgramLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (_cfg == null)
            return new HashSet<ProgramLocation>();

        var controllingBlocks = GetControllingBlocks(location.Block);
        var result = new HashSet<ProgramLocation>();

        foreach (var block in controllingBlocks)
        {
            // The control dependency is the branch condition, which is typically
            // in the BranchValue or the last operation of the block
            if (block.BranchValue != null)
            {
                // For conditional branches, use the branch value's location
                // This is typically the condition expression
                var branchLocation = new ProgramLocation(block, block.Operations.Length);
                result.Add(branchLocation);
            }
            else if (block.Operations.Length > 0)
            {
                // For blocks without explicit branch value, use last operation
                var branchLocation = new ProgramLocation(block, block.Operations.Length - 1);
                result.Add(branchLocation);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlySet<BasicBlock> GetControllingBlocks(BasicBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return _controlDependencies.TryGetValue(block, out var deps)
            ? deps
            : new HashSet<BasicBlock>();
    }

    /// <summary>
    /// Computes dominators using iterative dataflow analysis.
    /// </summary>
    /// <remarks>
    /// Dominator algorithm:
    /// 1. Initialize: Entry block dominates only itself, all others dominated by all blocks
    /// 2. Iterate: For each block B, Dom(B) = {B} ∪ (∩ Dom(pred) for all predecessors)
    /// 3. Repeat until fixpoint (no changes)
    ///
    /// Time complexity: O(N²) where N is the number of blocks
    /// </remarks>
    private void ComputeDominators()
    {
        if (_cfg == null) return;

        var blocks = _cfg.Blocks;
        if (blocks.Length == 0) return;

        // Initialize dominators
        var allBlocks = new HashSet<BasicBlock>(blocks);

        foreach (var block in blocks)
        {
            if (block.Ordinal == 0) // Entry block
            {
                _dominators[block] = new HashSet<BasicBlock> { block };
            }
            else
            {
                // All other blocks start dominated by all blocks
                _dominators[block] = new HashSet<BasicBlock>(allBlocks);
            }
        }

        // Iterate to fixpoint
        bool changed = true;
        while (changed)
        {
            changed = false;

            // Skip entry block (ordinal 0)
            for (int i = 1; i < blocks.Length; i++)
            {
                var block = blocks[i];
                var newDominators = new HashSet<BasicBlock> { block };

                var predecessors = block.Predecessors
                    .Select(p => p.Source)
                    .Where(p => _dominators.ContainsKey(p))
                    .ToList();

                if (predecessors.Count > 0)
                {
                    // Start with first predecessor's dominators
                    var intersection = new HashSet<BasicBlock>(_dominators[predecessors[0]]);

                    // Intersect with remaining predecessors
                    foreach (var pred in predecessors.Skip(1))
                    {
                        intersection.IntersectWith(_dominators[pred]);
                    }

                    newDominators.UnionWith(intersection);
                }

                // Check if changed
                if (!newDominators.SetEquals(_dominators[block]))
                {
                    _dominators[block] = newDominators;
                    changed = true;
                }
            }
        }
    }

    /// <summary>
    /// Computes control dependencies based on post-dominator relationships.
    /// </summary>
    /// <remarks>
    /// Control dependency algorithm:
    /// For each branch block X (multiple successors):
    ///   For each successor S of X:
    ///     Walk from S until we reach a block dominated by X
    ///     All blocks in this walk are control-dependent on X
    ///
    /// Intuition: If X can decide whether to reach Y, then Y is control-dependent on X
    /// </remarks>
    private void ComputeControlDependencies()
    {
        if (_cfg == null) return;

        foreach (var block in _cfg.Blocks)
        {
            _controlDependencies[block] = new HashSet<BasicBlock>();
        }

        // For each block with multiple successors (branch point)
        foreach (var block in _cfg.Blocks)
        {
            var successors = GetSuccessorBlocks(block);

            // Only process if this is actually a branch (multiple successors or conditional)
            if (successors.Count == 0 || (successors.Count == 1 && block.ConditionalSuccessor == null))
                continue;

            var stopBlock = _immediatePostDominators.TryGetValue(block, out var immediatePostDom)
                ? immediatePostDom
                : null;

            foreach (var successor in successors)
            {
                var worklist = new Stack<BasicBlock>();
                var visited = new HashSet<BasicBlock>();
                worklist.Push(successor);

                while (worklist.Count > 0)
                {
                    var current = worklist.Pop();

                    if (!visited.Add(current))
                        continue;

                    if (current == block)
                        continue;

                    if (stopBlock != null && current == stopBlock)
                        continue;

                    _controlDependencies[current].Add(block);

                    foreach (var child in GetSuccessorBlocks(current))
                    {
                        // Stop exploration when reaching the stop block from below
                        if (stopBlock != null && child == stopBlock)
                            continue;

                        worklist.Push(child);
                    }
                }
            }
        }
    }

    private void ComputePostDominators()
    {
        if (_cfg == null)
            return;

        var blocks = _cfg.Blocks;
        var exitBlock = blocks[blocks.Length - 1];

        // Initialize post-dominator sets
        foreach (var block in blocks)
        {
            if (block == exitBlock)
            {
                _postDominators[block] = new HashSet<BasicBlock> { block };
            }
            else
            {
                _postDominators[block] = new HashSet<BasicBlock>(blocks);
            }
        }

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var block in blocks)
            {
                if (block == exitBlock)
                    continue;

                var successors = GetSuccessorBlocks(block);

                HashSet<BasicBlock> intersection;

                if (successors.Count > 0)
                {
                    intersection = new HashSet<BasicBlock>(_postDominators[successors[0]]);

                    for (int i = 1; i < successors.Count; i++)
                    {
                        intersection.IntersectWith(_postDominators[successors[i]]);
                    }
                }
                else
                {
                    // No successors: treat as only post-dominated by itself
                    intersection = new HashSet<BasicBlock>();
                }

                intersection.Add(block);

                if (!_postDominators[block].SetEquals(intersection))
                {
                    _postDominators[block] = intersection;
                    changed = true;
                }
            }
        }

        // Compute immediate post-dominators from the post-dominator sets
        foreach (var block in blocks)
        {
            if (!_postDominators.TryGetValue(block, out var postDomSet))
            {
                _immediatePostDominators[block] = null;
                continue;
            }

            var candidates = postDomSet
                .Where(candidate => candidate != block)
                .OrderBy(setMember => _postDominators.TryGetValue(setMember, out var memberSet) ? memberSet.Count : int.MaxValue)
                .ToList();

            _immediatePostDominators[block] = candidates.FirstOrDefault();
        }
    }

    private static List<BasicBlock> GetSuccessorBlocks(BasicBlock block)
    {
        var results = new List<BasicBlock>();
        var seen = new HashSet<BasicBlock>();

        // Roslyn exposes different successor shapes (conditional, fall-through, switch cases).
        // We collect all available successors, falling back to reflection for switch branches to
        // remain compatible across Roslyn versions.
        static void AddBranch(ControlFlowBranch? branch, HashSet<BasicBlock> seenSet, List<BasicBlock> list)
        {
            if (branch?.Destination != null && seenSet.Add(branch.Destination))
            {
                list.Add(branch.Destination);
            }
        }

        AddBranch(block.ConditionalSuccessor, seen, results);
        AddBranch(block.FallThroughSuccessor, seen, results);

        if (_switchCaseSuccessorsProperty?.GetValue(block) is System.Collections.IEnumerable switchBranches)
        {
            foreach (var item in switchBranches)
            {
                if (item is ControlFlowBranch branch)
                {
                    AddBranch(branch, seen, results);
                }
            }
        }

        if (_successorsProperty?.GetValue(block) is System.Collections.IEnumerable additionalBranches)
        {
            foreach (var item in additionalBranches)
            {
                if (item is ControlFlowBranch branch)
                {
                    AddBranch(branch, seen, results);
                }
            }
        }

        return results;
    }

}
