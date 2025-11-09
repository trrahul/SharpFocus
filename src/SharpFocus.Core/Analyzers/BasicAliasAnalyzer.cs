using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Analyzers;

/// <summary>
/// A conservative alias analyzer that handles basic aliasing scenarios.
/// </summary>
/// <remarks>
/// This analyzer implements conservative alias detection for:
/// - Reference parameters (ref, out, in)
/// - Direct assignments between places
/// - Field projections (if fields alias, their projections may alias)
///
/// Future enhancements could include:
/// - Pointer analysis for unsafe code
/// - Array element aliasing
/// - Property setter analysis
/// - Interprocedural alias tracking
/// </remarks>
public class BasicAliasAnalyzer : IAliasAnalyzer
{
    private readonly IPlaceExtractor _placeExtractor;
    private readonly Dictionary<Place, HashSet<Place>> _aliasMap = new();
    private readonly SymbolEqualityComparer _symbolComparer = SymbolEqualityComparer.Default;

    public BasicAliasAnalyzer(IPlaceExtractor placeExtractor)
    {
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
    }

    /// <inheritdoc/>
    public void Analyze(ControlFlowGraph cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        _aliasMap.Clear();

        foreach (var block in cfg.Blocks)
        {
            foreach (var operation in block.Operations)
            {
                AnalyzeOperation(operation);
            }

            if (block.BranchValue != null)
            {
                AnalyzeOperation(block.BranchValue);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlySet<Place> GetAliases(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);

        var aliases = new HashSet<Place>();

        // A place always aliases with itself
        aliases.Add(place);

        if (_aliasMap.TryGetValue(place, out var trackedAliases))
        {
            aliases.UnionWith(trackedAliases);
        }

        // Check for projection-based aliases
        // If we have a projection (e.g., obj.field), the base might have aliases
        if (place.AccessPath.Count > 0)
        {
            var basePlaces = GetBasePlaces(place);
            foreach (var basePlace in basePlaces)
            {
                if (_aliasMap.TryGetValue(basePlace, out var baseAliases))
                {
                    // Project the aliases forward
                    foreach (var baseAlias in baseAliases)
                    {
                        var projectedAlias = ProjectPlace(baseAlias, place.AccessPath);
                        if (projectedAlias != null)
                        {
                            aliases.Add(projectedAlias);
                        }
                    }
                }
            }
        }

        return aliases;
    }

    /// <inheritdoc/>
    public bool AreAliased(Place left, Place right)
    {
        if (left == null || right == null)
            return false;

        // Same place definitely aliases
        if (left.Equals(right))
            return true;

        if (!_symbolComparer.Equals(left.Symbol, right.Symbol))
        {
            // Different base symbols - check if one is tracked as aliasing the other
            if (_aliasMap.TryGetValue(left, out var aliases1) && aliases1.Contains(right))
                return true;

            if (_aliasMap.TryGetValue(right, out var aliases2) && aliases2.Contains(left))
                return true;

            return false;
        }

        // Same base symbol - check projections
        // Conservative: if projections differ, they might still alias (arrays, indexers)
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Place, IReadOnlyCollection<Place>> ExportAliases()
    {
        var snapshot = new Dictionary<Place, IReadOnlyCollection<Place>>();

        foreach (var (place, aliases) in _aliasMap)
        {
            snapshot[place] = aliases.ToList();
        }

        return snapshot;
    }

    private void AnalyzeOperation(IOperation operation)
    {
        switch (operation)
        {
            case ISimpleAssignmentOperation assignment:
                AnalyzeAssignment(assignment);
                break;

            case IInvocationOperation invocation:
                AnalyzeInvocation(invocation);
                break;

            case IVariableDeclarationGroupOperation declGroup:
                foreach (var decl in declGroup.Declarations)
                {
                    foreach (var declarator in decl.Declarators)
                    {
                        if (declarator.Initializer?.Value != null)
                        {
                            AnalyzeInitializer(declarator.Symbol, declarator.Initializer.Value);
                        }
                    }
                }
                break;
        }

        foreach (var child in operation.ChildOperations)
        {
            AnalyzeOperation(child);
        }
    }

    private void AnalyzeAssignment(ISimpleAssignmentOperation assignment)
    {
        var targetPlace = _placeExtractor.TryCreatePlace(assignment.Target);
        var valuePlace = _placeExtractor.TryCreatePlace(assignment.Value);

        if (targetPlace == null || valuePlace == null)
            return;

        if (IsReferenceTypeOrRefParameter(assignment.Value))
        {
            AddAlias(targetPlace, valuePlace);
        }
    }

    private void AnalyzeInvocation(IInvocationOperation invocation)
    {
        // Check for ref/out parameters
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter == null)
                continue;

            var refKind = argument.Parameter.RefKind;
            if (refKind == RefKind.Ref || refKind == RefKind.Out)
            {
                var argPlace = _placeExtractor.TryCreatePlace(argument.Value);
                if (argPlace != null)
                {
                    // Conservative: assume ref/out parameters might alias with each other
                    foreach (var otherArg in invocation.Arguments)
                    {
                        if (otherArg == argument || otherArg.Parameter == null)
                            continue;

                        if (otherArg.Parameter.RefKind != RefKind.None)
                        {
                            var otherPlace = _placeExtractor.TryCreatePlace(otherArg.Value);
                            if (otherPlace != null)
                            {
                                AddAlias(argPlace, otherPlace);
                            }
                        }
                    }
                }
            }
        }
    }

    private void AnalyzeInitializer(ILocalSymbol local, IOperation initializer)
    {
        var localPlace = new Place(local);
        var valuePlace = _placeExtractor.TryCreatePlace(initializer);

        if (valuePlace != null && IsReferenceTypeOrRefParameter(initializer))
        {
            AddAlias(localPlace, valuePlace);
        }
    }

    private void AddAlias(Place place1, Place place2)
    {
        // Add bidirectional alias relationship
        if (!_aliasMap.TryGetValue(place1, out var aliases1))
        {
            aliases1 = new HashSet<Place>();
            _aliasMap[place1] = aliases1;
        }
        aliases1.Add(place2);

        if (!_aliasMap.TryGetValue(place2, out var aliases2))
        {
            aliases2 = new HashSet<Place>();
            _aliasMap[place2] = aliases2;
        }
        aliases2.Add(place1);
    }

    private static bool IsReferenceTypeOrRefParameter(IOperation operation)
    {
        if (operation.Type?.IsReferenceType == true)
            return true;

        if (operation is IParameterReferenceOperation paramRef)
        {
            return paramRef.Parameter.RefKind != RefKind.None;
        }

        return false;
    }

    private static IEnumerable<Place> GetBasePlaces(Place place)
    {
        // Extract all possible base places from the projection chain
        var current = new Place(place.Symbol);
        yield return current;

        for (int i = 0; i < place.AccessPath.Count; i++)
        {
            current = current.WithProjection(place.AccessPath[i]);
            yield return current;
        }
    }

    private static Place? ProjectPlace(Place basePlace, ImmutableList<ISymbol> projection)
    {
        if (projection.Count == 0)
            return basePlace;

        var result = basePlace;
        foreach (var symbol in projection)
        {
            result = result.WithProjection(symbol);
        }
        return result;
    }
}
