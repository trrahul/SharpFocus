using System;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Analyzers;
using SharpFocus.Core.Engine;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;
using SharpFocus.Core.Utilities;

namespace SharpFocus.Core.Tests.Engine;

public class DataflowTransferFunctionTests
{
    [Fact]
    public void Initialize_WithNullCfg_ThrowsArgumentNullException()
    {
        var transfer = CreateTransferFunction();

        Action act = () => transfer.Initialize(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cfg");
    }

    [Fact]
    public void Apply_BeforeInitialize_ThrowsInvalidOperationException()
    {
        var code = @"
            class TestClass
            {
                void Method()
                {
                    int x = 0;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var transfer = CreateTransferFunction();
        var location = new ProgramLocation(cfg.Blocks[0], 0);
        var state = new FlowDomain();

        Action act = () => transfer.Apply(state, location);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Initialize must be called*");
    }

    [Fact]
    public void Apply_WithNullLocation_ThrowsArgumentNullException()
    {
        var code = @"
            class TestClass
            {
                void Method() { int x = 0; }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var transfer = CreateTransferFunction();
        transfer.Initialize(cfg);

        Action act = () => transfer.Apply(new FlowDomain(), null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("location");
    }

    [Fact]
    public void Apply_WithSimpleAssignment_StrongUpdateReplacesDependencies()
    {
        var code = @"
            class TestClass
            {
                void Method(int input)
                {
                    int y = 0;
                    y = input;
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var placeExtractor = new RoslynPlaceExtractor();
        var aliasAnalyzer = new BasicAliasAnalyzer(placeExtractor);
        var mutationDetector = new RoslynMutationDetector(placeExtractor);
        var controlAnalyzer = new ControlFlowDependencyAnalyzer();
        var transfer = new DataflowTransferFunction(aliasAnalyzer, mutationDetector, controlAnalyzer, placeExtractor);

        transfer.Initialize(cfg);

        var mutation = mutationDetector.DetectMutations(cfg)
            .Where(m => m.Target.Symbol.Name == "y" && m.Kind == MutationKind.Assignment)
            .OrderBy(m => m.Location.Block.Ordinal)
            .ThenBy(m => m.Location.OperationIndex)
            .Last();
        var location = mutation.Location;

        var inputSymbol = CompilationHelper.GetSymbolByName(compilation, "input")!;
        var inputPlace = new Place(inputSymbol);
        var initialState = new FlowDomain();
        var previousDependency = new ProgramLocation(cfg.Blocks[0], 0);
        initialState.AddDependency(mutation.Target, previousDependency);
        var inputDependency = new ProgramLocation(cfg.Blocks[0], 1);
        initialState.AddDependency(inputPlace, inputDependency);

        var result = transfer.Apply(initialState, location);

        result.Should().NotBeSameAs(initialState);
        var deps = result.GetDependencies(mutation.Target);
        deps.Should().Contain(location);
        deps.Should().Contain(inputDependency);
        deps.Should().NotContain(previousDependency);
        initialState.GetDependencies(mutation.Target).Should().Contain(previousDependency);
    }

    [Fact]
    public void Apply_WithControlDependency_IncludesBranchCondition()
    {
        var code = @"
            class TestClass
            {
                void Method(bool flag, int value)
                {
                    int result = 0;
                    if (flag)
                    {
                        result = value;
                    }
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var placeExtractor = new RoslynPlaceExtractor();
        var aliasAnalyzer = new BasicAliasAnalyzer(placeExtractor);
        var mutationDetector = new RoslynMutationDetector(placeExtractor);
        var controlAnalyzer = new ControlFlowDependencyAnalyzer();
        var transfer = new DataflowTransferFunction(aliasAnalyzer, mutationDetector, controlAnalyzer, placeExtractor);

        transfer.Initialize(cfg);

        var mutation = mutationDetector.DetectMutations(cfg)
            .Where(m => m.Target.Symbol.Name == "result" && m.Kind == MutationKind.Assignment)
            .OrderBy(m => m.Location.Block.Ordinal)
            .ThenBy(m => m.Location.OperationIndex)
            .Last();
        var location = mutation.Location;

        var valueSymbol = CompilationHelper.GetSymbolByName(compilation, "value")!;
        var valuePlace = new Place(valueSymbol);
        var initialState = new FlowDomain();
        var incoming = new ProgramLocation(cfg.Blocks[0], 1);
        initialState.AddDependency(valuePlace, incoming);

        var resultState = transfer.Apply(initialState, location);
        var resultDeps = resultState.GetDependencies(mutation.Target);

        var controlDeps = controlAnalyzer.GetControlDependencies(location);
        controlDeps.Should().NotBeEmpty();
        resultDeps.Should().Contain(location);
        resultDeps.Should().Contain(incoming);
        resultDeps.Should().Contain(controlDeps.First());
    }

    [Fact]
    public void Apply_WithAliasedTarget_UsesWeakUpdate()
    {
        var code = @"
            class TestClass
            {
                void Method()
                {
                    object x = new object();
                    object y = x;
                    x = new object();
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var placeExtractor = new RoslynPlaceExtractor();
        var aliasAnalyzer = new BasicAliasAnalyzer(placeExtractor);
        var mutationDetector = new RoslynMutationDetector(placeExtractor);
        var controlAnalyzer = new ControlFlowDependencyAnalyzer();
        var transfer = new DataflowTransferFunction(aliasAnalyzer, mutationDetector, controlAnalyzer, placeExtractor);

        transfer.Initialize(cfg);

        var mutation = mutationDetector.DetectMutations(cfg)
            .First(m => m.Target.Symbol.Name == "x" && m.Kind == MutationKind.Assignment);
        var location = mutation.Location;

        var xSymbol = CompilationHelper.GetSymbolByName(compilation, "x")!;
        var ySymbol = CompilationHelper.GetSymbolByName(compilation, "y")!;
        var xPlace = new Place(xSymbol);
        var yPlace = new Place(ySymbol);

        var initialState = new FlowDomain();
        var oldX = new ProgramLocation(cfg.Blocks[0], 0);
        var oldY = new ProgramLocation(cfg.Blocks[0], 1);
        initialState.AddDependency(xPlace, oldX);
        initialState.AddDependency(yPlace, oldY);

        var resultState = transfer.Apply(initialState, location);

        var xDeps = resultState.GetDependencies(xPlace);
        var yDeps = resultState.GetDependencies(yPlace);
        xDeps.Should().Contain(oldX);
        xDeps.Should().Contain(location);
        yDeps.Should().Contain(oldY);
        yDeps.Should().Contain(location);
    }

    [Fact]
    public void Apply_WithNoMutations_ReturnsClone()
    {
        var code = @"
            class TestClass
            {
                void Method(bool flag)
                {
                    if (flag) { }
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var placeExtractor = new RoslynPlaceExtractor();
        var aliasAnalyzer = new BasicAliasAnalyzer(placeExtractor);
        var mutationDetector = new RoslynMutationDetector(placeExtractor);
        var controlAnalyzer = new ControlFlowDependencyAnalyzer();
        var transfer = new DataflowTransferFunction(aliasAnalyzer, mutationDetector, controlAnalyzer, placeExtractor);

        transfer.Initialize(cfg);

        var location = new ProgramLocation(cfg.Blocks[^1], 0);
        var initialState = new FlowDomain();
        var stateWithDependency = new ProgramLocation(cfg.Blocks[0], 0);
        var flagSymbol = CompilationHelper.CreateParameterSymbol("flag");
        var flagPlace = new Place(flagSymbol);
        initialState.AddDependency(flagPlace, stateWithDependency);

        var result = transfer.Apply(initialState, location);

        result.Should().NotBeSameAs(initialState);
        result.GetDependencies(flagPlace).Should().Contain(stateWithDependency);
    }

    private static DataflowTransferFunction CreateTransferFunction()
    {
        var placeExtractor = new RoslynPlaceExtractor();
        var aliasAnalyzer = new BasicAliasAnalyzer(placeExtractor);
        var mutationDetector = new RoslynMutationDetector(placeExtractor);
        var controlAnalyzer = new ControlFlowDependencyAnalyzer();
        return new DataflowTransferFunction(aliasAnalyzer, mutationDetector, controlAnalyzer, placeExtractor);
    }
}
