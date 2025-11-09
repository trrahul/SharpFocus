using FluentAssertions;
using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Models;

/// <summary>
/// Tests for the Place class, which represents memory locations in C# programs.
/// Following TDD: Tests written first to define behavior.
/// </summary>
public class PlaceTests
{
    private static ISymbol CreateTestSymbol(string name)
        => CompilationHelper.CreateTestSymbol(name);    [Fact]
    public void Constructor_WithValidSymbol_CreatesPlace()
    {
        // Arrange
        var symbol = CreateTestSymbol("testVar");

        // Act
        var place = new Place(symbol);

        // Assert
        place.Symbol.Should().Be(symbol);
        place.AccessPath.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullSymbol_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new Place(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("symbol");
    }

    [Fact]
    public void WithProjection_AddsToAccessPath()
    {
        // Arrange
        var baseSymbol = CreateTestSymbol("obj");
        var fieldSymbol = CreateTestSymbol("field");
        var place = new Place(baseSymbol);

        // Act
        var projected = place.WithProjection(fieldSymbol);

        // Assert
        projected.Symbol.Should().Be(baseSymbol);
        projected.AccessPath.Should().HaveCount(1);
        projected.AccessPath.Should().Contain(fieldSymbol);
    }

    [Fact]
    public void WithProjection_PreservesExistingPath()
    {
        // Arrange
        var baseSymbol = CreateTestSymbol("obj");
        var field1 = CreateTestSymbol("field1");
        var field2 = CreateTestSymbol("field2");
        var place = new Place(baseSymbol).WithProjection(field1);

        // Act
        var projected = place.WithProjection(field2);

        // Assert
        projected.AccessPath.Should().HaveCount(2);
        projected.AccessPath[0].Should().Be(field1);
        projected.AccessPath[1].Should().Be(field2);
    }

    [Fact]
    public void Equals_WithSameSymbolAndPath_ReturnsTrue()
    {
        // Arrange
        var symbol = CreateTestSymbol("x");
        var field = CreateTestSymbol("field");
        var place1 = new Place(symbol).WithProjection(field);
        var place2 = new Place(symbol).WithProjection(field);

        // Act & Assert
        place1.Should().Be(place2);
        (place1 == place2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentSymbol_ReturnsFalse()
    {
        // Arrange
        var symbol1 = CreateTestSymbol("x");
        var symbol2 = CreateTestSymbol("y");
        var place1 = new Place(symbol1);
        var place2 = new Place(symbol2);

        // Act & Assert
        place1.Should().NotBe(place2);
        (place1 != place2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameContent_ReturnsSameHash()
    {
        // Arrange
        var symbol = CreateTestSymbol("x");
        var place1 = new Place(symbol);
        var place2 = new Place(symbol);

        // Act & Assert
        place1.GetHashCode().Should().Be(place2.GetHashCode());
    }

    [Fact]
    public void ToString_WithNoProjections_ReturnsSymbolName()
    {
        // Arrange
        var symbol = CreateTestSymbol("myVariable");
        var place = new Place(symbol);

        // Act
        var result = place.ToString();

        // Assert
        result.Should().Contain("myVariable");
    }

    [Fact]
    public void ToString_WithProjections_ShowsFullPath()
    {
        // Arrange
        var baseSymbol = CreateTestSymbol("obj");
        var field1 = CreateTestSymbol("field1");
        var field2 = CreateTestSymbol("field2");
        var place = new Place(baseSymbol)
            .WithProjection(field1)
            .WithProjection(field2);

        // Act
        var result = place.ToString();

        // Assert
        result.Should().Contain("obj");
        result.Should().Contain("field1");
        result.Should().Contain("field2");
    }

    [Fact]
    public void IsProjectionOf_WithSameBase_ReturnsTrue()
    {
        // Arrange
        var baseSymbol = CreateTestSymbol("obj");
        var field = CreateTestSymbol("field");
        var basPlace = new Place(baseSymbol);
        var projected = basPlace.WithProjection(field);

        // Act & Assert
        projected.IsProjectionOf(basPlace).Should().BeTrue();
    }

    [Fact]
    public void IsProjectionOf_WithDifferentBase_ReturnsFalse()
    {
        // Arrange
        var symbol1 = CreateTestSymbol("obj1");
        var symbol2 = CreateTestSymbol("obj2");
        var field = CreateTestSymbol("field");
        var place1 = new Place(symbol1);
        var place2 = new Place(symbol2).WithProjection(field);

        // Act & Assert
        place2.IsProjectionOf(place1).Should().BeFalse();
    }

    [Fact]
    public void IsProjectionOf_WithSelf_ReturnsFalse()
    {
        // Arrange
        var symbol = CreateTestSymbol("obj");
        var place = new Place(symbol);

        // Act & Assert
        place.IsProjectionOf(place).Should().BeFalse();
    }
}
