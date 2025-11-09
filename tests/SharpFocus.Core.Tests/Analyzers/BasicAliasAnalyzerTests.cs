using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using SharpFocus.Core.Analyzers;
using SharpFocus.Core.Models;
using SharpFocus.Core.Utilities;
using SharpFocus.Core.Tests.TestHelpers;

namespace SharpFocus.Core.Tests.Analyzers;

public class BasicAliasAnalyzerTests
{
    private readonly RoslynPlaceExtractor _placeExtractor = new();

    [Fact]
    public void Analyze_WithSimpleAssignment_DoesNotCreateAliases()
    {
        // Value type assignment shouldn't create aliases
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 5;
                    int y = x;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        var aliases = analyzer.GetAliases(xPlace);
        aliases.Should().ContainSingle(); // Only itself
        aliases.Should().Contain(xPlace);
        aliases.Should().NotContain(yPlace);
    }

    [Fact]
    public void Analyze_WithReferenceTypeAssignment_CreatesAliases()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    object x = new object();
                    object y = x;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        var xAliases = analyzer.GetAliases(xPlace);
        xAliases.Should().Contain(xPlace);
        xAliases.Should().Contain(yPlace);

        var yAliases = analyzer.GetAliases(yPlace);
        yAliases.Should().Contain(xPlace);
        yAliases.Should().Contain(yPlace);
    }

    [Fact]
    public void Analyze_WithRefParameter_CreatesAliases()
    {
        var code = @"
            class TestClass
            {
                void Method(ref int x, ref int y) {
                    Helper(ref x, ref y);
                }
                void Helper(ref int a, ref int b) { }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        // ref parameters passed to another method are conservatively aliased
        var xAliases = analyzer.GetAliases(xPlace);
        xAliases.Should().Contain(yPlace, "ref parameters may alias");
    }

    [Fact]
    public void Analyze_WithOutParameter_CreatesAliases()
    {
        var code = @"
            class TestClass
            {
                void Method(out int x, out int y) {
                    x = 0;
                    y = 0;
                    Helper(out x, out y);
                }
                void Helper(out int a, out int b) {
                    a = 1;
                    b = 2;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        // out parameters are also conservatively aliased
        var xAliases = analyzer.GetAliases(xPlace);
        xAliases.Should().Contain(yPlace, "out parameters may alias");
    }

    [Fact]
    public void AreAliased_WithSamePlace_ReturnsTrue()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 5;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var xPlace = new Place(xSymbol);

        analyzer.AreAliased(xPlace, xPlace).Should().BeTrue();
    }

    [Fact]
    public void AreAliased_WithDifferentValueTypes_ReturnsFalse()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 5;
                    int y = 10;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        // Different value type variables don't alias
        analyzer.AreAliased(xPlace, yPlace).Should().BeFalse();
    }

    [Fact]
    public void AreAliased_WithAliasedReferenceTypes_ReturnsTrue()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    string x = ""hello"";
                    string y = x;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        analyzer.AreAliased(xPlace, yPlace).Should().BeTrue();
    }

    [Fact]
    public void GetAliases_WithNullPlace_ThrowsArgumentNullException()
    {
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        var act = () => analyzer.GetAliases(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("place");
    }

    [Fact]
    public void Analyze_WithNullCfg_ThrowsArgumentNullException()
    {
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        var act = () => analyzer.Analyze(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cfg");
    }

    [Fact]
    public void GetAliases_WithNoAnalysis_ReturnsOnlySelf()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    int x = 5;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        // Don't call Analyze - should still return the place itself
        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var xPlace = new Place(xSymbol);

        var aliases = analyzer.GetAliases(xPlace);
        aliases.Should().ContainSingle();
        aliases.Should().Contain(xPlace);
    }

    [Fact]
    public void Analyze_WithMultipleReferenceAssignments_TracksAllAliases()
    {
        var code = @"
            class TestClass
            {
                void Method() {
                    object a = new object();
                    object b = a;
                    object c = b;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var analyzer = new BasicAliasAnalyzer(_placeExtractor);

        analyzer.Analyze(cfg);

        var aSymbol = CompilationHelper.GetSymbolByName(compilation, "a")!;
        var bSymbol = CompilationHelper.GetSymbolByName(compilation, "b")!;
        var cSymbol = CompilationHelper.GetSymbolByName(compilation, "c")!;
        var aPlace = new Place(aSymbol);
        var bPlace = new Place(bSymbol);
        var cPlace = new Place(cSymbol);

        // All three should be aliased
        var aAliases = analyzer.GetAliases(aPlace);
        aAliases.Should().Contain(new[] { aPlace, bPlace });

        var bAliases = analyzer.GetAliases(bPlace);
        bAliases.Should().Contain(new[] { aPlace, bPlace, cPlace });

        var cAliases = analyzer.GetAliases(cPlace);
        cAliases.Should().Contain(new[] { bPlace, cPlace });
    }
}
