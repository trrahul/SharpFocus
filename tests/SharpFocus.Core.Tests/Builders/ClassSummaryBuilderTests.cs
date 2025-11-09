using FluentAssertions;
using Microsoft.CodeAnalysis;
using SharpFocus.Analysis.Builders;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;
using Xunit;

namespace SharpFocus.Core.Tests.Builders;

/// <summary>
/// Tests for ClassSummaryBuilder which analyzes classes and builds comprehensive dataflow summaries.
/// </summary>
public class ClassSummaryBuilderTests
{
    private readonly ClassSummaryBuilder _builder = new();

    [Fact]
    public async Task AnalyzeClassAsync_WithSimpleFieldRead_DetectsRead()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _field;

                void Method()
                {
                    var x = _field;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        summary.Should().NotBeNull();
        summary.ClassSymbol.Should().Be(classSymbol);
        summary.FieldAccesses.Should().ContainKey(GetFieldSymbol(compilation, "_field"));

        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_field"));
        fieldAccesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.Read);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithSimpleFieldWrite_DetectsWrite()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _field;

                void Method()
                {
                    _field = 42;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_field"));
        fieldAccesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.Write);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithFieldInitializer_IncludesInitializerWrite()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _value = 1;

                void Update()
                {
                    _value = 2;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldSymbol = GetFieldSymbol(compilation, "_value");
        var fieldAccesses = summary.GetFieldAccesses(fieldSymbol);

        fieldAccesses.Should().HaveCount(2, "initializer and method assignments should both appear");

        fieldAccesses.Should().Contain(a => a.IsFieldInitializer);

        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.Write &&
            a.ContainingMethod.MethodKind == MethodKind.Constructor &&
            a.ContainingMethod.IsImplicitlyDeclared &&
            a.IsFieldInitializer);

        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.Write &&
            a.ContainingMethod.Name == "Update" &&
            !a.IsFieldInitializer);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithFieldIncrement_DetectsReadWrite()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _counter;

                void Increment()
                {
                    _counter++;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_counter"));
        fieldAccesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.ReadWrite);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithCompoundAssignment_DetectsReadWrite()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _value;

                void Add(int amount)
                {
                    _value += amount;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_value"));
        fieldAccesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.ReadWrite);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithMultipleMethods_DetectsAllAccesses()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _count;

                void Increment()
                {
                    _count++;
                }

                int GetValue()
                {
                    return _count;
                }

                void Reset()
                {
                    _count = 0;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_count"));
        fieldAccesses.Should().HaveCount(3);

        // Increment: ReadWrite (_count++)
        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.ReadWrite &&
            a.ContainingMethod.Name == "Increment");

        // GetValue: Read (return _count)
        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.Read &&
            a.ContainingMethod.Name == "GetValue");

        // Reset: Write (_count = 0)
        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.Write &&
            a.ContainingMethod.Name == "Reset");
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithMultipleFields_TracksEachSeparately()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _field1;
                private string _field2;

                void Method()
                {
                    _field1 = 5;
                    var x = _field2;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        summary.FieldAccesses.Should().HaveCount(2);

        var field1Accesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_field1"));
        field1Accesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.Write);

        var field2Accesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_field2"));
        field2Accesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.Read);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithConstructor_DetectsFieldAccess()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _value;

                public TestClass(int value)
                {
                    _value = value;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_value"));
        fieldAccesses.Should().ContainSingle();
        fieldAccesses.First().ContainingMethod.MethodKind.Should().Be(MethodKind.Constructor);
        fieldAccesses.First().Type.Should().Be(AccessType.Write);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithPropertyAccessor_DetectsFieldAccess()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                    set { _value = value; }
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_value"));
        fieldAccesses.Should().HaveCount(2);

        // Getter: Read
        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.Read &&
            a.ContainingMethod.MethodKind == MethodKind.PropertyGet);

        // Setter: Write
        fieldAccesses.Should().Contain(a =>
            a.Type == AccessType.Write &&
            a.ContainingMethod.MethodKind == MethodKind.PropertySet);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithStaticField_TracksCorrectly()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private static int _staticCount;
                private int _instanceCount;

                void Method()
                {
                    _staticCount++;
                    _instanceCount++;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        summary.FieldAccesses.Should().HaveCount(2);

        var staticField = GetFieldSymbol(compilation, "_staticCount");
        staticField.IsStatic.Should().BeTrue();
        var staticAccesses = summary.GetFieldAccesses(staticField);
        staticAccesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.ReadWrite);

        var instanceField = GetFieldSymbol(compilation, "_instanceCount");
        instanceField.IsStatic.Should().BeFalse();
        var instanceAccesses = summary.GetFieldAccesses(instanceField);
        instanceAccesses.Should().ContainSingle()
            .Which.Type.Should().Be(AccessType.ReadWrite);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithFieldInNestedExpression_DetectsAccess()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _value;

                bool Check()
                {
                    return _value > 0 && _value < 100;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_value"));
        fieldAccesses.Should().HaveCount(2, "field is read twice in the expression");
        fieldAccesses.Should().AllSatisfy(a => a.Type.Should().Be(AccessType.Read));
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithNoFields_ReturnsEmptySummary()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                void Method()
                {
                    var local = 42;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        summary.Should().NotBeNull();
        summary.FieldAccesses.Should().BeEmpty();
        summary.TotalAccessCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithFieldNeverAccessed_NotInSummary()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _unused;
                private int _used;

                void Method()
                {
                    _used = 42;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        summary.FieldAccesses.Should().HaveCount(1);
        summary.FieldAccesses.Should().ContainKey(GetFieldSymbol(compilation, "_used"));
        summary.FieldAccesses.Should().NotContainKey(GetFieldSymbol(compilation, "_unused"));
    }

    [Fact]
    public async Task AnalyzeClassAsync_WithRefParameter_DetectsReadWrite()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _value;

                void Method()
                {
                    Helper(ref _value);
                }

                void Helper(ref int x)
                {
                    x = 10;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_value"));
        fieldAccesses.Should().ContainSingle();
        // ref parameter context is treated as ReadWrite
        fieldAccesses.First().Type.Should().Be(AccessType.ReadWrite);
    }

    [Fact]
    public async Task AnalyzeClassAsync_SetsDocumentUriAndVersion()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _field;
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        summary.DocumentUri.Should().NotBeNull();
        summary.DocumentVersion.Should().NotBe(0);
    }

    [Fact]
    public async Task AnalyzeClassAsync_AccessLocation_ContainsCorrectPosition()
    {
        // Arrange
        var code = @"
            class TestClass
            {
                private int _field;

                void Method()
                {
                    _field = 42;
                }
            }";

        var compilation = CompilationHelper.CreateCompilation(code);
        var classSymbol = GetClassSymbol(compilation, "TestClass");

        // Act
        var summary = await AnalyzeAsync(compilation, classSymbol);

        // Assert
        var fieldAccesses = summary.GetFieldAccesses(GetFieldSymbol(compilation, "_field"));
        var access = fieldAccesses.Single();

        access.Location.Should().NotBeNull();
        access.Location.IsInSource.Should().BeTrue();
        access.Operation.Should().NotBeNull();
        access.ContainingMethod.Name.Should().Be("Method");
    }

    private Task<ClassDataflowSummary> AnalyzeAsync(Compilation compilation, INamedTypeSymbol classSymbol)
    {
        return _builder.AnalyzeClassAsync(classSymbol, compilation, TestContext.Current.CancellationToken);
    }

    // Helper methods

    private static INamedTypeSymbol GetClassSymbol(Compilation compilation, string className)
    {
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);

        var classDecl = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == className);

        return semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol
            ?? throw new InvalidOperationException($"Could not find class symbol for {className}");
    }

    private static IFieldSymbol GetFieldSymbol(Compilation compilation, string fieldName)
    {
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);

        var field = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .First(v => v.Identifier.Text == fieldName);

        return semanticModel.GetDeclaredSymbol(field) as IFieldSymbol
            ?? throw new InvalidOperationException($"Could not find field symbol for {fieldName}");
    }
}
