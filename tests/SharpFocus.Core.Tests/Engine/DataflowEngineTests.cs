using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Analyzers;
using SharpFocus.Core.Engine;
using SharpFocus.Core.Models;
using SharpFocus.Core.Tests.TestHelpers;
using SharpFocus.Core.Utilities;

namespace SharpFocus.Core.Tests.Engine;

public class DataflowEngineTests
{
    [Fact]
    public void Analyze_WithNullControlFlowGraph_ThrowsArgumentNullException()
    {
        var transfer = new RecordingTransferFunction();
        var engine = new DataflowEngine(transfer);

        var act = () => engine.Analyze(null!);

        act.Should().Throw<ArgumentNullException>();
        transfer.Initialized.Should().BeFalse();
    }

    [Fact]
    public void Analyze_VisitsEachProgramLocation()
    {
        const string code = @"
            class TestClass
            {
                void Method(int value)
                {
                    int x = value + 1;
                    value = x;
                }
            }";

        var cfg = CompilationHelper.CreateControlFlowGraph(code);
        var trackedSymbol = CompilationHelper.CreateTestSymbol("tracked");
        var trackedPlace = new Place(trackedSymbol);
        var transfer = new RecordingTransferFunction(trackedPlace);
        var engine = new DataflowEngine(transfer);

        var results = engine.Analyze(cfg);

        transfer.Initialized.Should().BeTrue();

        var expectedLocations = EnumerateAllLocations(cfg);
        transfer.AppliedLocations.Should().Equal(expectedLocations);

        foreach (var location in expectedLocations)
        {
            var state = results.GetState(location);
            state.GetDependencies(trackedPlace).Should().Contain(location);
        }
    }

    [Fact]
    public void Analyze_PropagatesDependenciesAcrossBlocks()
    {
        const string code = @"
            class TestClass
            {
                void Method(int input, bool flag)
                {
                    int local = input;
                    int result = 0;
                    if (flag)
                    {
                        result = local;
                    }
                }
            }";

        var (cfg, _, compilation) = CompilationHelper.CreateControlFlowGraphWithContext(code);
        var placeExtractor = new RoslynPlaceExtractor();
        var aliasAnalyzer = new BasicAliasAnalyzer(placeExtractor);
        var mutationDetector = new RoslynMutationDetector(placeExtractor);
        var controlAnalyzer = new ControlFlowDependencyAnalyzer();
        var transfer = new DataflowTransferFunction(aliasAnalyzer, mutationDetector, controlAnalyzer, placeExtractor);
        var engine = new DataflowEngine(transfer);

        var results = engine.Analyze(cfg);

        var mutations = mutationDetector.DetectMutations(cfg);
        var localMutation = mutations.First(m => m.Target.Symbol.Name == "local");
        var resultAssignment = mutations
            .Where(m => m.Target.Symbol.Name == "result" && m.Kind == MutationKind.Assignment)
            .OrderBy(m => m.Location.Block.Ordinal)
            .ThenBy(m => m.Location.OperationIndex)
            .Last();

        var state = results.GetState(resultAssignment.Location);
        var dependencies = state.GetDependencies(resultAssignment.Target);

        var dependencyKeys = dependencies
            .Select(dep => (dep.Block.Ordinal, dep.OperationIndex))
            .ToHashSet();
        var controlKeys = controlAnalyzer
            .GetControlDependencies(resultAssignment.Location)
            .Select(dep => (dep.Block.Ordinal, dep.OperationIndex))
            .ToHashSet();

        var resultKey = (resultAssignment.Location.Block.Ordinal, resultAssignment.Location.OperationIndex);
        var localKey = (localMutation.Location.Block.Ordinal, localMutation.Location.OperationIndex);

        dependencyKeys.Should().Contain(
            resultKey,
            "Dependencies should include the result assignment location");
        dependencyKeys.Should().Contain(
            localKey,
            "Dependencies should include the local initialization location");

        if (controlKeys.Count > 0)
        {
            foreach (var controlKey in controlKeys)
            {
                dependencyKeys.Should().Contain(
                    controlKey,
                    "Dependencies should include the controlling branch location");
            }
        }
    }

    private static IReadOnlyList<ProgramLocation> EnumerateAllLocations(ControlFlowGraph cfg)
    {
        var result = new List<ProgramLocation>();

        foreach (var block in cfg.Blocks)
        {
            for (var index = 0; index < block.Operations.Length; index++)
            {
                result.Add(new ProgramLocation(block, index));
            }

            if (block.BranchValue != null)
            {
                result.Add(new ProgramLocation(block, block.Operations.Length));
            }
        }

        return result;
    }

    private sealed class RecordingTransferFunction : IDataflowTransferFunction
    {
        private readonly Place? _trackedPlace;

        public RecordingTransferFunction()
        {
        }

        public RecordingTransferFunction(Place trackedPlace)
        {
            _trackedPlace = trackedPlace;
        }

        public bool Initialized { get; private set; }

        public List<ProgramLocation> AppliedLocations { get; } = new();

        public IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Place>> ReadsByLocation { get; } =
            new Dictionary<ProgramLocation, IReadOnlyList<Place>>();

        public IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Mutation>> MutationsByLocation { get; } =
            new Dictionary<ProgramLocation, IReadOnlyList<Mutation>>();

        public void Initialize(ControlFlowGraph cfg)
        {
            Initialized = true;
        }

        public FlowDomain Apply(FlowDomain inputState, ProgramLocation location)
        {
            AppliedLocations.Add(location);
            var result = inputState.Clone();

            if (_trackedPlace != null)
            {
                result.AddDependency(_trackedPlace, location);
            }

            return result;
        }
    }
}
