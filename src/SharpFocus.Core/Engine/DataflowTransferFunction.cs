using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Engine;

/// <summary>
/// Implements the SharpFocus transfer function that propagates dependencies through
/// mutations, alias relationships, and control dependencies.
/// </summary>
public sealed class DataflowTransferFunction : IDataflowTransferFunction
{
    private readonly IAliasAnalyzer _aliasAnalyzer;
    private readonly IMutationDetector _mutationDetector;
    private readonly IControlFlowDependencyAnalyzer _controlDependencies;
    private readonly IPlaceExtractor _placeExtractor;

    private ControlFlowGraph? _cfg;
    private Dictionary<ProgramLocation, IReadOnlyList<Mutation>> _mutationsByLocation = new();
    private Dictionary<ProgramLocation, IReadOnlyList<Place>> _readsByLocation = new();

    /// <summary>
    /// Gets the mutations detected during initialization, keyed by program location.
    /// </summary>
    public IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Mutation>> MutationsByLocation => _mutationsByLocation;

    /// <summary>
    /// Gets the places read at each program location.
    /// </summary>
    public IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Place>> ReadsByLocation => _readsByLocation;

    public DataflowTransferFunction(
        IAliasAnalyzer aliasAnalyzer,
        IMutationDetector mutationDetector,
        IControlFlowDependencyAnalyzer controlDependencies,
        IPlaceExtractor placeExtractor)
    {
        _aliasAnalyzer = aliasAnalyzer ?? throw new ArgumentNullException(nameof(aliasAnalyzer));
        _mutationDetector = mutationDetector ?? throw new ArgumentNullException(nameof(mutationDetector));
        _controlDependencies = controlDependencies ?? throw new ArgumentNullException(nameof(controlDependencies));
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
    }

    /// <inheritdoc />
    public void Initialize(ControlFlowGraph cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        _cfg = cfg;
        _mutationsByLocation.Clear();
        _readsByLocation.Clear();

        _aliasAnalyzer.Analyze(cfg);
        _controlDependencies.Analyze(cfg);

        var mutations = _mutationDetector.DetectMutations(cfg);
        _mutationsByLocation = mutations
            .GroupBy(mutation => mutation.Location)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Mutation>)group.ToList());

        _readsByLocation = BuildReadMap(cfg);
    }

    /// <inheritdoc />
    public FlowDomain Apply(FlowDomain inputState, ProgramLocation location)
    {
        ArgumentNullException.ThrowIfNull(inputState);
        ArgumentNullException.ThrowIfNull(location);

        if (_cfg == null)
            throw new InvalidOperationException("Initialize must be called before Apply.");

        if (!_mutationsByLocation.TryGetValue(location, out var mutations) || mutations.Count == 0)
        {
            return inputState.Clone();
        }

        var result = inputState.Clone();
        var readPlaces = _readsByLocation.TryGetValue(location, out var reads)
            ? reads
            : Array.Empty<Place>();
        var controlLocations = _controlDependencies.GetControlDependencies(location);

        foreach (var mutation in mutations)
        {
            var dependencies = new HashSet<ProgramLocation> { mutation.Location };
            dependencies.UnionWith(controlLocations);

            foreach (var read in readPlaces)
            {
                var aliases = _aliasAnalyzer.GetAliases(read);
                foreach (var alias in aliases)
                {
                    var existing = inputState.GetDependencies(alias);
                    dependencies.UnionWith(existing);
                }
            }

            var targetAliases = _aliasAnalyzer.GetAliases(mutation.Target);
            if (targetAliases.Count == 0)
            {
                targetAliases = new HashSet<Place> { mutation.Target };
            }

            if (targetAliases.Count == 1)
            {
                result.SetDependencies(targetAliases.First(), dependencies);
            }
            else
            {
                foreach (var alias in targetAliases)
                {
                    foreach (var dependency in dependencies)
                    {
                        result.AddDependency(alias, dependency);
                    }
                }
            }
        }

        return result;
    }

    private Dictionary<ProgramLocation, IReadOnlyList<Place>> BuildReadMap(ControlFlowGraph cfg)
    {
        var result = new Dictionary<ProgramLocation, IReadOnlyList<Place>>();

        foreach (var block in cfg.Blocks)
        {
            for (int i = 0; i < block.Operations.Length; i++)
            {
                var operation = block.Operations[i];
                if (operation == null)
                    continue;

                var location = new ProgramLocation(block, i);
                var reads = CollectReadPlaces(operation);
                if (reads.Count > 0)
                {
                    result[location] = reads;
                }
            }

            if (block.BranchValue != null)
            {
                var branchLocation = new ProgramLocation(block, block.Operations.Length);
                var reads = CollectReadPlaces(block.BranchValue);
                if (reads.Count > 0)
                {
                    result[branchLocation] = reads;
                }
            }
        }

        return result;
    }

    private IReadOnlyList<Place> CollectReadPlaces(IOperation? operation)
    {
        var reads = new HashSet<Place>();
        CollectReadPlacesCore(operation, reads);
        return reads.Count == 0 ? Array.Empty<Place>() : reads.ToList();
    }

    private void CollectReadPlacesCore(IOperation? operation, HashSet<Place> result)
    {
        if (operation == null)
            return;

        switch (operation)
        {
            case IExpressionStatementOperation expressionStatement:
                CollectReadPlacesCore(expressionStatement.Operation, result);
                break;
            case ISimpleAssignmentOperation assignment:
                CollectReadPlacesCore(assignment.Value, result);
                break;
            case ICompoundAssignmentOperation compoundAssignment:
                CollectReadPlacesCore(compoundAssignment.Target, result);
                CollectReadPlacesCore(compoundAssignment.Value, result);
                break;
            case IIncrementOrDecrementOperation incrementOrDecrement:
                CollectReadPlacesCore(incrementOrDecrement.Target, result);
                break;
            case IArgumentOperation argument:
                if (argument.Parameter?.RefKind == RefKind.Out)
                    return;

                CollectReadPlacesCore(argument.Value, result);
                break;
            case IBinaryOperation binaryOperation:
                CollectReadPlacesCore(binaryOperation.LeftOperand, result);
                CollectReadPlacesCore(binaryOperation.RightOperand, result);
                break;
            case IUnaryOperation unaryOperation:
                CollectReadPlacesCore(unaryOperation.Operand, result);
                break;
            case IConditionalAccessOperation conditionalAccess:
                CollectReadPlacesCore(conditionalAccess.Operation, result);
                CollectReadPlacesCore(conditionalAccess.WhenNotNull, result);
                break;
            case ICoalesceOperation coalesceOperation:
                CollectReadPlacesCore(coalesceOperation.Value, result);
                CollectReadPlacesCore(coalesceOperation.WhenNull, result);
                break;
            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
            case IFieldReferenceOperation:
            case IPropertyReferenceOperation:
            case IEventReferenceOperation:
            case IArrayElementReferenceOperation:
                var place = _placeExtractor.TryCreatePlace(operation);
                if (place != null)
                {
                    result.Add(place);
                }
                break;
            default:
                foreach (var child in operation.ChildOperations)
                {
                    CollectReadPlacesCore(child, result);
                }
                break;
        }
    }
}
