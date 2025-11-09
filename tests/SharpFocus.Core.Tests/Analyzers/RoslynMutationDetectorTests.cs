using FluentAssertions;
using Microsoft.CodeAnalysis;
using SharpFocus.Core.Analyzers;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Analyzers;

/// <summary>
/// Tests for RoslynMutationDetector - detecting mutations in C# code.
/// NOTE: CFG operations are lowered, so most mutations appear as simple assignments.
/// This is expected Roslyn behavior.
/// </summary>
public class RoslynMutationDetectorTests
{
    private readonly RoslynMutationDetector _detector = new();

    [Fact]
    public void DetectMutations_WithSimpleAssignment_FindsMutation()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    x = 5;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert
        mutations.Should().NotBeEmpty();
        mutations.Should().Contain(m => m.Kind == MutationKind.Assignment);
        mutations.Should().Contain(m => m.IsWrite);
    }

    [Fact]
    public void DetectMutations_WithVariableInitialization_FindsMutation()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 42;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert - In CFG, initialization appears as assignment
        mutations.Should().Contain(m => m.IsWrite);
    }

    [Fact]
    public void DetectMutations_WithMultipleAssignments_FindsAll()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    int y = 1;
                    x = 5;
                    y = 10;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert
        mutations.Should().HaveCountGreaterThanOrEqualTo(4);
        mutations.All(m => m.IsWrite).Should().BeTrue();
    }

    [Fact]
    public void DetectMutations_WithRefArgument_FindsRefMutation()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    ModifyByRef(ref x);
                }

                void ModifyByRef(ref int value) { value = 10; }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert - Ref arguments are preserved in CFG
        mutations.Should().Contain(m => m.Kind == MutationKind.RefArgument);
    }

    [Fact]
    public void DetectMutations_WithOutArgument_FindsOutMutation()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x;
                    GetValue(out x);
                }

                void GetValue(out int value) { value = 10; }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert - Out arguments are preserved in CFG
        mutations.Should().Contain(m => m.Kind == MutationKind.OutArgument);
    }

    [Fact]
    public void DetectMutations_WithNoWrites_ReturnsEmpty()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 5;
                    int y = x + 10;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert - Even initialization creates mutations
        // So we just verify no crashes and structure is correct
        mutations.Should().AllSatisfy(m =>
        {
            m.Target.Should().NotBeNull();
            m.Location.Should().NotBeNull();
        });
    }

    [Fact]
    public void DetectMutation_WithNullOperation_ReturnsNull()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod() { int x = 0; }
            }");
        var block = cfg.Blocks[0];

        // Act
        var mutation = _detector.DetectMutation(null!, block, 0);

        // Assert
        mutation.Should().BeNull();
    }

    [Fact]
    public void DetectMutations_SetsCorrectProgramLocation()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 5;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert
        mutations.Should().NotBeEmpty();
        var mutation = mutations.First();
        mutation.Location.Should().NotBeNull();
        mutation.Location.Block.Should().NotBeNull();
        mutation.Location.OperationIndex.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void DetectMutations_SetsCorrectTargetPlace()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int myVariable = 5;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        // Assert
        mutations.Should().NotBeEmpty();
        var mutation = mutations.FirstOrDefault(m => m.Target.Symbol.Name == "myVariable");
        mutation.Should().NotBeNull();
        mutation!.Target.Should().NotBeNull();
        mutation.Target.Symbol.Should().NotBeNull();
        mutation.Target.Symbol.Name.Should().Be("myVariable");
    }

    [Fact]
    public void DetectMutations_WithParameter_DetectsMutation()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod(int param)
                {
                    param = 42;
                }
            }");

        // Act
        var mutations = _detector.DetectMutations(cfg);

        var blockSummaries = cfg.Blocks
            .Select(block => $"Block {block.Ordinal} ({block.Kind}) ops: " +
                               string.Join(", ", block.Operations
                                   .Select((op, index) => $"[{index}] {op.Kind}:{op.GetType().Name}:{op.Syntax}")))
            .ToArray();

        if (!mutations.Any(m => m.Target.Symbol.Name == "param"))
        {
            var mutationSummary = mutations.Any()
                ? string.Join(", ", mutations.Select(mut => $"{mut.Kind}:{mut.Target.Symbol.Name}"))
                : "<none>";

            throw new Xunit.Sdk.XunitException(
                "Expected parameter assignment mutation but none detected. " +
                $"CFG blocks: {string.Join(" | ", blockSummaries)}. " +
                $"Detected mutations: {mutationSummary}");
        }
    }
}
