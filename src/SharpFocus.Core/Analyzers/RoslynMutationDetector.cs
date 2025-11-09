using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;
using SharpFocus.Core.Utilities;

namespace SharpFocus.Core.Analyzers;

/// <summary>
/// Detects mutations (writes) in C# code using Roslyn's IOperation tree.
/// Implements a visitor pattern to traverse operations and identify writes.
/// </summary>
public sealed class RoslynMutationDetector : IMutationDetector
{
    private readonly IPlaceExtractor _placeExtractor;

    public RoslynMutationDetector()
        : this(new RoslynPlaceExtractor())
    {
    }

    public RoslynMutationDetector(IPlaceExtractor placeExtractor)
    {
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
    }

    /// <inheritdoc/>
    public IReadOnlyList<Mutation> DetectMutations(ControlFlowGraph cfg)
    {
        var mutations = new List<Mutation>();

        foreach (var block in cfg.Blocks)
        {
            if (block.Operations.IsEmpty)
                continue;

            for (int i = 0; i < block.Operations.Length; i++)
            {
                var operation = block.Operations[i];
                switch (operation)
                {
                    case IVariableDeclarationGroupOperation declarationGroup:
                        AddVariableDeclarationMutations(declarationGroup, block, i, mutations);
                        break;

                    case IExpressionStatementOperation expressionStatement:
                        AddMutationIfNotNull(mutations, DetectMutation(expressionStatement.Operation, block, i));
                        CollectArgumentMutations(expressionStatement.Operation, block, i, mutations);
                        break;

                    default:
                        AddMutationIfNotNull(mutations, DetectMutation(operation, block, i));
                        CollectArgumentMutations(operation, block, i, mutations);
                        break;
                }
            }
        }

        return mutations;
    }

    /// <inheritdoc/>
    public Mutation? DetectMutation(IOperation operation, BasicBlock containingBlock, int operationIndex)
    {
        if (operation == null)
            return null;

        if (operation is IExpressionStatementOperation expressionStatement)
        {
            return DetectMutation(expressionStatement.Operation, containingBlock, operationIndex);
        }

        var location = new ProgramLocation(containingBlock, operationIndex);

        return operation switch
        {
            // Simple assignment: x = value
            ISimpleAssignmentOperation assignment => CreateMutation(
                assignment.Target,
                location,
                MutationKind.Assignment),

            // Compound assignment: x += value, x *= value, etc.
            ICompoundAssignmentOperation compound => CreateMutation(
                compound.Target,
                location,
                MutationKind.CompoundAssignment),

            // Increment: x++, ++x
            IIncrementOrDecrementOperation { Kind: OperationKind.Increment } increment => CreateMutation(
                increment.Target,
                location,
                MutationKind.Increment),

            // Decrement: x--, --x
            IIncrementOrDecrementOperation { Kind: OperationKind.Decrement } decrement => CreateMutation(
                decrement.Target,
                location,
                MutationKind.Decrement),

            // Variable declaration with initializer: int x = 5
            IVariableDeclaratorOperation { Initializer: not null } declarator => CreateMutationFromDeclarator(
                declarator,
                location),

            // Argument passed by ref or out
            IArgumentOperation argument => DetectArgumentMutation(argument, location),

            _ => null
        };
    }

    private static void AddVariableDeclarationMutations(
        IVariableDeclarationGroupOperation declarationGroup,
        BasicBlock block,
        int operationIndex,
        List<Mutation> mutations)
    {
        foreach (var declaration in declarationGroup.Declarations)
        {
            foreach (var declarator in declaration.Declarators)
            {
                var mutation = CreateMutationFromDeclarator(declarator, new ProgramLocation(block, operationIndex));
                AddMutationIfNotNull(mutations, mutation);
            }
        }
    }

    private void CollectArgumentMutations(
        IOperation operation,
        BasicBlock block,
        int operationIndex,
        List<Mutation> mutations)
    {
        if (operation == null)
            return;

        var location = new ProgramLocation(block, operationIndex);

        if (operation is IInvocationOperation invocation)
        {
            foreach (var argument in invocation.Arguments)
            {
                var mutation = DetectArgumentMutation(argument, location);
                AddMutationIfNotNull(mutations, mutation);
                CollectArgumentMutations(argument.Value, block, operationIndex, mutations);
            }

            return;
        }

        foreach (var child in operation.ChildOperations)
        {
            CollectArgumentMutations(child, block, operationIndex, mutations);
        }
    }

    private static void AddMutationIfNotNull(List<Mutation> mutations, Mutation? mutation)
    {
        if (mutation != null)
        {
            mutations.Add(mutation);
        }
    }

    /// <summary>
    /// Creates a mutation for a variable declarator with an initializer.
    /// </summary>
    private static Mutation? CreateMutationFromDeclarator(IVariableDeclaratorOperation declarator, ProgramLocation location)
    {
        var symbol = declarator.Symbol;
        if (symbol == null)
            return null;

        var place = new Place(symbol);
        return new Mutation(place, location, MutationKind.Initialization);
    }

    /// <summary>
    /// Detects mutations from ref or out arguments.
    /// </summary>
    private Mutation? DetectArgumentMutation(IArgumentOperation argument, ProgramLocation location)
    {
        var kind = argument.Parameter?.RefKind switch
        {
            RefKind.Ref => MutationKind.RefArgument,
            RefKind.Out => MutationKind.OutArgument,
            _ => (MutationKind?)null
        };

        if (kind == null)
            return null;

        // The argument value is what's being passed by ref/out
        var target = argument.Value;
        return CreateMutation(target, location, kind.Value);
    }

    /// <summary>
    /// Creates a mutation from an operation that represents a writable location.
    /// </summary>
    private Mutation? CreateMutation(IOperation target, ProgramLocation location, MutationKind kind)
    {
        var place = _placeExtractor.TryCreatePlace(target);
        if (place == null)
            return null;

        return new Mutation(place, location, kind);
    }
}
