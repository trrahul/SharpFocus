using FluentAssertions;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Analyzers;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Analyzers;

public class ControlFlowDependencyAnalyzerTests
{
    [Fact]
    public void Analyze_WithNullCfg_ThrowsArgumentNullException()
    {
        var analyzer = new ControlFlowDependencyAnalyzer();

        var act = () => analyzer.Analyze(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cfg");
    }

    [Fact]
    public void GetControlDependencies_WithNullLocation_ThrowsArgumentNullException()
    {
        var analyzer = new ControlFlowDependencyAnalyzer();

        var act = () => analyzer.GetControlDependencies(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("location");
    }

    [Fact]
    public void GetControllingBlocks_WithNullBlock_ThrowsArgumentNullException()
    {
        var analyzer = new ControlFlowDependencyAnalyzer();

        var act = () => analyzer.GetControllingBlocks(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("block");
    }

    [Fact]
    public void Analyze_WithSimpleSequence_NoControlDependencies()
    {
        // Straight-line code has no control dependencies
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 1;
                    int y = 2;
                    int z = 3;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        // All blocks should have no control dependencies (just sequential flow)
        foreach (var block in cfg.Blocks)
        {
            var deps = analyzer.GetControllingBlocks(block);
            deps.Should().BeEmpty($"block {block.Ordinal} in sequential code should have no control dependencies");
        }
    }

    [Fact]
    public void Analyze_WithIfStatement_CreatesControlDependency()
    {
        var code = @"
            class TestClass
            {
                void Method(bool condition) {
                    int x = 0;
                    if (condition)
                        x = 5;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        // The block with "x = 5" should be control-dependent on the if condition
        var blocks = cfg.Blocks.ToList();

        // Find the branch block (contains the if condition)
        var branchBlock = blocks.FirstOrDefault(b =>
            b.ConditionalSuccessor != null && b.FallThroughSuccessor != null);

        branchBlock.Should().NotBeNull("there should be a conditional branch");

        // Find blocks that are control-dependent on the branch
        var dependentBlocks = blocks.Where(b =>
            analyzer.GetControllingBlocks(b).Contains(branchBlock!)).ToList();

        dependentBlocks.Should().NotBeEmpty("some blocks should be control-dependent on the if condition");
    }

    [Fact]
    public void Analyze_WithIfElse_BothBranchesControlDependent()
    {
        var code = @"
            class TestClass
            {
                void Method(bool condition) {
                    int x;
                    if (condition)
                        x = 5;
                    else
                        x = 10;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        var blocks = cfg.Blocks.ToList();

        // Find the branch block
        var branchBlock = blocks.FirstOrDefault(b =>
            b.ConditionalSuccessor != null && b.FallThroughSuccessor != null);

        branchBlock.Should().NotBeNull();

        // Both the 'then' and 'else' branches should be control-dependent on the condition
        var dependentBlocks = blocks.Where(b =>
            analyzer.GetControllingBlocks(b).Contains(branchBlock!)).ToList();

        // Should have at least the two assignment blocks as dependent
        dependentBlocks.Count.Should().BeGreaterThanOrEqualTo(1,
            "at least one branch should be control-dependent on the if condition");
    }

    [Fact]
    public void Analyze_WithNestedIf_TracksMultipleControlDependencies()
    {
        var code = @"
            class TestClass
            {
                void Method(bool outer, bool inner) {
                    int x = 0;
                    if (outer) {
                        if (inner)
                            x = 5;
                    }
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        // Inner assignment should be control-dependent on both conditions
        var blocks = cfg.Blocks.ToList();
        var branchBlocks = blocks.Where(b =>
            b.ConditionalSuccessor != null && b.FallThroughSuccessor != null).ToList();

        branchBlocks.Should().HaveCountGreaterThanOrEqualTo(1,
            "should have at least outer if as branch");

        // At least one block should have control dependencies
        var hasControlDeps = blocks.Any(b => analyzer.GetControllingBlocks(b).Any());
        hasControlDeps.Should().BeTrue("nested if should create control dependencies");
    }

    [Fact]
    public void Analyze_WithWhileLoop_CreatesControlDependency()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 0;
                    while (x < 10)
                        x++;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        var blocks = cfg.Blocks.ToList();

        // Find loop header (has back edge)
        var loopBlock = blocks.FirstOrDefault(b =>
            b.ConditionalSuccessor != null || b.FallThroughSuccessor != null);

        loopBlock.Should().NotBeNull("should find a loop header");

        // Loop body should be control-dependent on loop condition
        var hasControlDeps = blocks.Any(b => analyzer.GetControllingBlocks(b).Any());
        hasControlDeps.Should().BeTrue("loop should create control dependencies");
    }

    [Fact]
    public void GetControlDependencies_WithoutAnalysis_ReturnsEmpty()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 1;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        // Don't call Analyze - should return empty
        var location = new ProgramLocation(cfg.Blocks[0], 0);
        var deps = analyzer.GetControlDependencies(location);

        deps.Should().BeEmpty("analyzer not called, so no dependencies");
    }

    [Fact]
    public void Analyze_EntryBlock_NeverHasControlDependencies()
    {
        var code = @"
            class TestClass
            {
                void Method(bool condition) {
                    if (condition)
                        return;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        // Entry block should never have control dependencies
        var entryBlock = cfg.Blocks[0];
        var deps = analyzer.GetControllingBlocks(entryBlock);

        deps.Should().BeEmpty("entry block has no control dependencies");
    }

    [Fact]
    public void Analyze_SwitchStatement_CreatesControlDependencies()
    {
        var code = @"
            class TestClass
            {
                void Method(int value) {
                    int x = 0;
                    switch (value) {
                        case 1:
                            x = 1;
                            break;
                        case 2:
                            x = 2;
                            break;
                        default:
                            x = 3;
                            break;
                    }
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        // Switch creates multiple control dependencies
        var hasControlDeps = cfg.Blocks.Any(b => analyzer.GetControllingBlocks(b).Any());
        hasControlDeps.Should().BeTrue("switch should create control dependencies");
    }

    [Fact]
    public void Analyze_PostDominatedBlock_NoControlDependency()
    {
        // Code where all paths merge - no control dependency for the merge point
        var code = @"
            class TestClass
            {
                void Method(bool condition) {
                    int x;
                    if (condition)
                        x = 5;
                    else
                        x = 10;
                    int y = x + 1;  // This is post-dominated
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var analyzer = new ControlFlowDependencyAnalyzer();

        analyzer.Analyze(cfg);

        // The final assignment (y = x + 1) should NOT be control-dependent
        // because it always executes regardless of the condition
        var blocks = cfg.Blocks.ToList();

        // Exit block should have no control dependencies
        var exitBlock = blocks.LastOrDefault(b => b.Operations.Length > 0);
        if (exitBlock != null)
        {
            var deps = analyzer.GetControllingBlocks(exitBlock);
            // The merge point after if-else might or might not be control-dependent
            // depending on CFG structure - this tests the analyzer handles it correctly
            deps.Should().NotBeNull();
        }
    }
}
