using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Models;

/// <summary>
/// Tests for the Mutation class, which represents modifications to memory locations.
/// Following TDD: Tests written first to define behavior.
/// </summary>
public class MutationTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesMutation()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        // Act
        var mutation = new Mutation(place, location, MutationKind.Assignment);

        // Assert
        mutation.Target.Should().Be(place);
        mutation.Location.Should().Be(location);
        mutation.Kind.Should().Be(MutationKind.Assignment);
    }

    [Fact]
    public void Constructor_WithNullPlace_ThrowsArgumentNullException()
    {
        // Arrange
        var location = CreateTestLocation();

        // Act
        var action = () => new Mutation(null!, location, MutationKind.Assignment);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("target");
    }

    [Fact]
    public void Constructor_WithNullLocation_ThrowsArgumentNullException()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);

        // Act
        var action = () => new Mutation(place, null!, MutationKind.Assignment);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("location");
    }

    [Fact]
    public void Equals_WithSameContent_ReturnsTrue()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        var mutation1 = new Mutation(place, location, MutationKind.Assignment);
        var mutation2 = new Mutation(place, location, MutationKind.Assignment);

        // Act & Assert
        mutation1.Should().Be(mutation2);
        (mutation1 == mutation2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentKind_ReturnsFalse()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        var mutation1 = new Mutation(place, location, MutationKind.Assignment);
        var mutation2 = new Mutation(place, location, MutationKind.RefArgument);

        // Act & Assert
        mutation1.Should().NotBe(mutation2);
        (mutation1 != mutation2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameContent_ReturnsSameHash()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        var mutation1 = new Mutation(place, location, MutationKind.Assignment);
        var mutation2 = new Mutation(place, location, MutationKind.Assignment);

        // Act & Assert
        mutation1.GetHashCode().Should().Be(mutation2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("myVar");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        var mutation = new Mutation(place, location, MutationKind.Assignment);

        // Act
        var result = mutation.ToString();

        // Assert
        result.Should().Contain("myVar");
        result.Should().Contain("Assignment");
    }

    [Theory]
    [InlineData(MutationKind.Assignment)]
    [InlineData(MutationKind.RefArgument)]
    [InlineData(MutationKind.OutArgument)]
    [InlineData(MutationKind.Initialization)]
    [InlineData(MutationKind.Increment)]
    [InlineData(MutationKind.Decrement)]
    [InlineData(MutationKind.CompoundAssignment)]
    public void MutationKind_AllValues_AreSupported(MutationKind kind)
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        // Act
        var mutation = new Mutation(place, location, kind);

        // Assert
        mutation.Kind.Should().Be(kind);
    }

    [Fact]
    public void IsWrite_ForAssignment_ReturnsTrue()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        var mutation = new Mutation(place, location, MutationKind.Assignment);

        // Act & Assert
        mutation.IsWrite.Should().BeTrue();
    }

    [Fact]
    public void IsWrite_ForRefArgument_ReturnsTrue()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        var mutation = new Mutation(place, location, MutationKind.RefArgument);

        // Act & Assert
        mutation.IsWrite.Should().BeTrue();
    }

    [Fact]
    public void IsWrite_ForOutArgument_ReturnsTrue()
    {
        // Arrange
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        var mutation = new Mutation(place, location, MutationKind.OutArgument);

        // Act & Assert
        mutation.IsWrite.Should().BeTrue();
    }

    private static ProgramLocation CreateTestLocation()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                }
            }");

        return new ProgramLocation(cfg.Blocks[0], 0);
    }
}
