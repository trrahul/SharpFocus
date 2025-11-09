using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Tests.TestHelpers;
using SharpFocus.Core.Utilities;

namespace SharpFocus.Core.Tests.Utilities;

public class RoslynPlaceExtractorTests
{
    private readonly RoslynPlaceExtractor _extractor = new();

    [Fact]
    public void TryCreatePlace_LocalReference_ReturnsLocalPlace()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    x = 5;
                }
            }");

        var assignment = GetAssignments(cfg)
            .First(a => a.Target is ILocalReferenceOperation local && local.Local.Name == "x");

        var place = _extractor.TryCreatePlace(assignment.Target);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("x");
        place.AccessPath.Should().BeEmpty();
    }

    [Fact]
    public void TryCreatePlace_ParameterReference_ReturnsParameterPlace()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod(int value)
                {
                    value = 42;
                }
            }");

        var assignment = GetAssignments(cfg)
            .First(a => a.Target is IParameterReferenceOperation parameter && parameter.Parameter.Name == "value");

        var place = _extractor.TryCreatePlace(assignment.Target);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("value");
        place.AccessPath.Should().BeEmpty();
    }

    [Fact]
    public void TryCreatePlace_FieldReference_ReturnsProjectedPlace()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class Inner { public int Count; }
            class Wrapper { public Inner Inner; }
            class TestClass
            {
                void TestMethod()
                {
                    var wrapper = new Wrapper();
                    wrapper.Inner = new Inner();
                    wrapper.Inner.Count = 5;
                }
            }");

        var assignment = GetAssignments(cfg)
            .First(a => a.Target is IFieldReferenceOperation field && field.Field.Name == "Count");

        var place = _extractor.TryCreatePlace(assignment.Target);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("wrapper");
        place.AccessPath.Should().HaveCount(2);
        place.AccessPath[0].Name.Should().Be("Inner");
        place.AccessPath[1].Name.Should().Be("Count");
    }

    [Fact]
    public void TryCreatePlace_PropertyReference_ReturnsProjectedPlace()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class Address { public string Street { get; set; } }
            class Person { public Address Address { get; set; } }
            class TestClass
            {
                void TestMethod(Person person)
                {
                    person.Address = new Address();
                    person.Address.Street = ""Main"";
                }
            }");

        var assignment = GetAssignments(cfg)
            .First(a => a.Target is IPropertyReferenceOperation property && property.Property.Name == "Street");

        var place = _extractor.TryCreatePlace(assignment.Target);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("person");
        place.AccessPath.Should().HaveCount(2);
        place.AccessPath[0].Name.Should().Be("Address");
        place.AccessPath[1].Name.Should().Be("Street");
    }

    [Fact]
    public void TryCreatePlace_StaticField_ReturnsFieldPlace()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class Store { public static int Value; }
            class TestClass
            {
                void TestMethod()
                {
                    Store.Value = 10;
                }
            }");

        var assignment = GetAssignments(cfg)
            .First(a => a.Target is IFieldReferenceOperation field && field.Field.Name == "Value");

        var place = _extractor.TryCreatePlace(assignment.Target);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("Value");
        place.AccessPath.Should().BeEmpty();
    }

    [Fact]
    public void TryCreatePlace_ArrayElement_ReturnsArrayBasePlace()
    {
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int[] numbers = new int[3];
                    numbers[0] = 5;
                }
            }");

        var assignment = GetAssignments(cfg)
            .First(a => a.Target is IArrayElementReferenceOperation);

        var place = _extractor.TryCreatePlace(assignment.Target);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("numbers");
        place.AccessPath.Should().BeEmpty();
    }

    private static IEnumerable<ISimpleAssignmentOperation> GetAssignments(ControlFlowGraph cfg)
    {
        foreach (var block in cfg.Blocks)
        {
            foreach (var operation in block.Operations)
            {
                if (operation is IExpressionStatementOperation { Operation: ISimpleAssignmentOperation assignment })
                {
                    yield return assignment;
                }
            }
        }
    }
}
