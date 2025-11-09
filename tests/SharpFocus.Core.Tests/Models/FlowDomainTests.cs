using System;
using FluentAssertions;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Models;

/// <summary>
/// Tests for the FlowDomain class, which tracks data dependencies in the program.
/// Following TDD: Tests written first to define behavior.
/// </summary>
public class FlowDomainTests
{
    [Fact]
    public void Constructor_CreatesEmptyDomain()
    {
        // Act
        var domain = new FlowDomain();

        // Assert
        domain.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AddDependency_AddsLocationForPlace()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        // Act
        domain.AddDependency(place, location);

        // Assert
        domain.HasDependencies(place).Should().BeTrue();
        domain.GetDependencies(place).Should().Contain(location);
    }

    [Fact]
    public void AddDependency_WithMultipleLocations_TracksAll()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location1 = CreateTestLocation(0, 0);
        var location2 = CreateTestLocation(0, 1);

        // Act
        domain.AddDependency(place, location1);
        domain.AddDependency(place, location2);

        // Assert
        var deps = domain.GetDependencies(place);
        deps.Should().HaveCount(2);
        deps.Should().Contain(location1);
        deps.Should().Contain(location2);
    }

    [Fact]
    public void AddDependency_WithDuplicateLocation_DoesNotDuplicate()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();

        // Act
        domain.AddDependency(place, location);
        domain.AddDependency(place, location); // Add same location again

        // Assert
        domain.GetDependencies(place).Should().HaveCount(1);
    }

    [Fact]
    public void GetDependencies_ForUnknownPlace_ReturnsEmptySet()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);

        // Act
        var deps = domain.GetDependencies(place);

        // Assert
        deps.Should().BeEmpty();
    }

    [Fact]
    public void HasDependencies_ForKnownPlace_ReturnsTrue()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        domain.AddDependency(place, location);

        // Act & Assert
        domain.HasDependencies(place).Should().BeTrue();
    }

    [Fact]
    public void HasDependencies_ForUnknownPlace_ReturnsFalse()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);

        // Act & Assert
        domain.HasDependencies(place).Should().BeFalse();
    }

    [Fact]
    public void Join_CombinesTwoDomains()
    {
        // Arrange
        var domain1 = new FlowDomain();
        var domain2 = new FlowDomain();

        var symbolX = CompilationHelper.CreateTestSymbol("x");
        var symbolY = CompilationHelper.CreateTestSymbol("y");
        var placeX = new Place(symbolX);
        var placeY = new Place(symbolY);

        var loc1 = CreateTestLocation(0, 0);
        var loc2 = CreateTestLocation(0, 1);

        domain1.AddDependency(placeX, loc1);
        domain2.AddDependency(placeY, loc2);

        // Act
        var joined = domain1.Join(domain2);

        // Assert
        joined.HasDependencies(placeX).Should().BeTrue();
        joined.HasDependencies(placeY).Should().BeTrue();
        joined.GetDependencies(placeX).Should().Contain(loc1);
        joined.GetDependencies(placeY).Should().Contain(loc2);
    }

    [Fact]
    public void Join_WithOverlappingPlaces_UnionsDependencies()
    {
        // Arrange
        var domain1 = new FlowDomain();
        var domain2 = new FlowDomain();

        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);

        var loc1 = CreateTestLocation(0, 0);
        var loc2 = CreateTestLocation(0, 1);

        domain1.AddDependency(place, loc1);
        domain2.AddDependency(place, loc2);

        // Act
        var joined = domain1.Join(domain2);

        // Assert
        var deps = joined.GetDependencies(place);
        deps.Should().HaveCount(2);
        deps.Should().Contain(loc1);
        deps.Should().Contain(loc2);
    }

    [Fact]
    public void Join_DoesNotMutateOriginalDomains()
    {
        // Arrange
        var domain1 = new FlowDomain();
        var domain2 = new FlowDomain();

        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var loc = CreateTestLocation();

        domain1.AddDependency(place, loc);

        // Act
        var joined = domain1.Join(domain2);
        joined.AddDependency(place, CreateTestLocation(0, 5));

        // Assert
        domain1.GetDependencies(place).Should().HaveCount(1);
        domain2.GetDependencies(place).Should().BeEmpty();
    }

    [Fact]
    public void IsEmpty_WithNoDependencies_ReturnsTrue()
    {
        // Arrange
        var domain = new FlowDomain();

        // Act & Assert
        domain.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WithDependencies_ReturnsFalse()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        domain.AddDependency(place, location);

        // Act & Assert
        domain.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Places_ReturnsAllTrackedPlaces()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbolX = CompilationHelper.CreateTestSymbol("x");
        var symbolY = CompilationHelper.CreateTestSymbol("y");
        var placeX = new Place(symbolX);
        var placeY = new Place(symbolY);
        var location = CreateTestLocation();

        domain.AddDependency(placeX, location);
        domain.AddDependency(placeY, location);

        // Act
        var places = domain.Places;

        // Assert
        places.Should().HaveCount(2);
        places.Should().Contain(placeX);
        places.Should().Contain(placeY);
    }

    [Fact]
    public void Clear_RemovesAllDependencies()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        domain.AddDependency(place, location);

        // Act
        domain.Clear();

        // Assert
        domain.IsEmpty.Should().BeTrue();
        domain.HasDependencies(place).Should().BeFalse();
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation();
        domain.AddDependency(place, location);

        // Act
        var clone = domain.Clone();
        clone.AddDependency(place, CreateTestLocation(0, 5));

        // Assert
        clone.Should().NotBeSameAs(domain);
        clone.GetDependencies(place).Should().HaveCount(2);
        domain.GetDependencies(place).Should().HaveCount(1);
    }

    [Fact]
    public void SetDependencies_ReplacesExistingOnes()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        domain.AddDependency(place, CreateTestLocation(0, 0));

        var newDeps = new[]
        {
            CreateTestLocation(0, 2),
            CreateTestLocation(0, 3)
        };

        // Act
        domain.SetDependencies(place, newDeps);

        // Assert
        var deps = domain.GetDependencies(place);
        deps.Should().HaveCount(2);
        deps.Should().BeEquivalentTo(newDeps);
    }

    [Fact]
    public void SetDependencies_WithEmptyEnumerable_RemovesPlace()
    {
        // Arrange
        var domain = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        domain.AddDependency(place, CreateTestLocation(0, 0));

        // Act
        domain.SetDependencies(place, Array.Empty<ProgramLocation>());

        // Assert
        domain.HasDependencies(place).Should().BeFalse();
    }

    [Fact]
    public void EquivalentTo_WithIdenticalDependencies_ReturnsTrue()
    {
        // Arrange
        var domain1 = new FlowDomain();
        var domain2 = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        var location = CreateTestLocation(0, 0);
        domain1.AddDependency(place, location);
        domain2.AddDependency(place, location);

        // Act & Assert
        domain1.EquivalentTo(domain2).Should().BeTrue();
        domain2.EquivalentTo(domain1).Should().BeTrue();
    }

    [Fact]
    public void EquivalentTo_WithDifferentDependencies_ReturnsFalse()
    {
        // Arrange
        var domain1 = new FlowDomain();
        var domain2 = new FlowDomain();
        var symbol = CompilationHelper.CreateTestSymbol("x");
        var place = new Place(symbol);
        domain1.AddDependency(place, CreateTestLocation(0, 0));
        domain2.AddDependency(place, CreateTestLocation(0, 1));

        // Act
        var result = domain1.EquivalentTo(domain2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EquivalentTo_WithDifferentPlaces_ReturnsFalse()
    {
        // Arrange
        var domain1 = new FlowDomain();
        var domain2 = new FlowDomain();
        var symbolX = CompilationHelper.CreateTestSymbol("x");
        var symbolY = CompilationHelper.CreateTestSymbol("y");
        domain1.AddDependency(new Place(symbolX), CreateTestLocation(0, 0));
        domain2.AddDependency(new Place(symbolY), CreateTestLocation(0, 0));

        // Act
        var result = domain1.EquivalentTo(domain2);

        // Assert
        result.Should().BeFalse();
    }

    private static ProgramLocation CreateTestLocation(int blockOrdinal = 0, int opIndex = 0)
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    int y = 1;
                    int z = 2;
                }
            }");

        var block = cfg.Blocks.FirstOrDefault(b => b.Ordinal == blockOrdinal)
            ?? cfg.Blocks[0];

        return new ProgramLocation(block, opIndex);
    }
}
