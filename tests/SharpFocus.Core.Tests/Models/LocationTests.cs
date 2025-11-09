using FluentAssertions;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Models;

/// <summary>
/// Tests for the ProgramLocation class, which represents specific points in the control flow graph.
/// Following TDD: Tests written first to define behavior.
/// </summary>
public class LocationTests
{
    private static ControlFlowGraph CreateTestCFG()
    {
        var code = @"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    int y = x + 1;
                }
            }";

        return CompilationHelper.CreateControlFlowGraph(code);
    }

    [Fact]
    public void Constructor_WithValidBlockAndIndex_CreatesLocation()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[0];
        var index = 0;

        // Act
        var location = new ProgramLocation(block, index);

        // Assert
        location.Block.Should().Be(block);
        location.OperationIndex.Should().Be(index);
    }

    [Fact]
    public void Constructor_WithNullBlock_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ProgramLocation(null!, 0);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("block");
    }

    [Fact]
    public void Constructor_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[0];

        // Act
        var action = () => new ProgramLocation(block, -1);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("operationIndex");
    }

    [Fact]
    public void Equals_WithSameBlockAndIndex_ReturnsTrue()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[0];
        var location1 = new ProgramLocation(block, 0);
        var location2 = new ProgramLocation(block, 0);

        // Act & Assert
        location1.Should().Be(location2);
        (location1 == location2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentIndex_ReturnsFalse()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[0];
        var location1 = new ProgramLocation(block, 0);
        var location2 = new ProgramLocation(block, 1);

        // Act & Assert
        location1.Should().NotBe(location2);
        (location1 != location2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentBlock_ReturnsFalse()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var location1 = new ProgramLocation(cfg.Blocks[0], 0);
        var location2 = new ProgramLocation(cfg.Blocks[1], 0);

        // Act & Assert
        location1.Should().NotBe(location2);
    }

    [Fact]
    public void GetHashCode_WithSameContent_ReturnsSameHash()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[0];
        var location1 = new ProgramLocation(block, 0);
        var location2 = new ProgramLocation(block, 0);

        // Act & Assert
        location1.GetHashCode().Should().Be(location2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsBlockAndIndexInfo()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[1]; // Block 1
        var location = new ProgramLocation(block, 2);

        // Act
        var result = location.ToString();

        // Assert
        result.Should().Contain("1"); // Block ordinal
        result.Should().Contain("2"); // Operation index
    }

    [Fact]
    public void CompareTo_WithSameBlock_ComparesIndex()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var block = cfg.Blocks[0];
        var location1 = new ProgramLocation(block, 0);
        var location2 = new ProgramLocation(block, 1);

        // Act & Assert
        location1.CompareTo(location2).Should().BeLessThan(0);
        location2.CompareTo(location1).Should().BeGreaterThan(0);
        location1.CompareTo(location1).Should().Be(0);
    }

    [Fact]
    public void CompareTo_WithDifferentBlocks_ComparesOrdinal()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var location1 = new ProgramLocation(cfg.Blocks[0], 5);
        var location2 = new ProgramLocation(cfg.Blocks[1], 2);

        // Act & Assert
        // Block 0 comes before Block 1 regardless of operation index
        location1.CompareTo(location2).Should().BeLessThan(0);
        location2.CompareTo(location1).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Operators_LessThan_WorksCorrectly()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var location1 = new ProgramLocation(cfg.Blocks[0], 0);
        var location2 = new ProgramLocation(cfg.Blocks[0], 1);

        // Act & Assert
        (location1 < location2).Should().BeTrue();
        (location2 < location1).Should().BeFalse();
    }

    [Fact]
    public void Operators_GreaterThan_WorksCorrectly()
    {
        // Arrange
        var cfg = CreateTestCFG();
        var location1 = new ProgramLocation(cfg.Blocks[0], 1);
        var location2 = new ProgramLocation(cfg.Blocks[0], 0);

        // Act & Assert
        (location1 > location2).Should().BeTrue();
        (location2 > location1).Should().BeFalse();
    }
}
