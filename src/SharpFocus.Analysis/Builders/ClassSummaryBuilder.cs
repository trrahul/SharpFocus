using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;
using SharpFocus.Core.Utilities;

namespace SharpFocus.Analysis.Builders;

/// <summary>
/// Builds comprehensive dataflow summaries for classes by analyzing all members in a single pass.
/// </summary>
public sealed class ClassSummaryBuilder : IClassSummaryBuilder
{
    /// <inheritdoc/>
    public async Task<ClassDataflowSummary> AnalyzeClassAsync(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classSymbol);
        ArgumentNullException.ThrowIfNull(compilation);

        var syntaxRef = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            return CreateEmptySummary(classSymbol, string.Empty, 0);
        }

        var syntaxTree = syntaxRef.SyntaxTree;
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var documentUri = syntaxTree.FilePath ?? string.Empty;
        var documentVersion = DocumentVersionCalculator.Compute(await syntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false));

        var fieldAccesses = await AnalyzeFieldAccessesAsync(
            classSymbol,
            semanticModel,
            cancellationToken);

        return new ClassDataflowSummary(
            fieldAccesses,
            classSymbol,
            documentUri,
            documentVersion);
    }

    private static async Task<ImmutableDictionary<IFieldSymbol, ImmutableArray<FieldAccessSummary>>> AnalyzeFieldAccessesAsync(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var accessesByField = new Dictionary<IFieldSymbol, List<FieldAccessSummary>>(SymbolEqualityComparer.Default);

        var methods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind is
                MethodKind.Ordinary or
                MethodKind.Constructor or
                MethodKind.StaticConstructor or
                MethodKind.PropertyGet or
                MethodKind.PropertySet);

        foreach (var method in methods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
                continue;

            var syntax = await syntaxRef.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
            var operation = GetOperationForMethod(semanticModel, syntax, cancellationToken);

            if (operation is null)
                continue;

            var accessesInMethod = AnalyzeOperationTree(operation, method);

            foreach (var access in accessesInMethod)
            {
                if (!accessesByField.TryGetValue(access.Field, out var list))
                {
                    list = new List<FieldAccessSummary>();
                    accessesByField[access.Field] = list;
                }
                list.Add(access);
            }
        }

        await AddFieldInitializerAccessesAsync(
            classSymbol,
            semanticModel,
            accessesByField,
            cancellationToken).ConfigureAwait(false);

        var builder = ImmutableDictionary.CreateBuilder<IFieldSymbol, ImmutableArray<FieldAccessSummary>>(SymbolEqualityComparer.Default);
        foreach (var kvp in accessesByField)
        {
            builder.Add(kvp.Key, kvp.Value.ToImmutableArray());
        }
        return builder.ToImmutable();
    }

    private static async Task AddFieldInitializerAccessesAsync(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        Dictionary<IFieldSymbol, List<FieldAccessSummary>> accessesByField,
        CancellationToken cancellationToken)
    {
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var syntaxRef in field.DeclaringSyntaxReferences)
            {
                var syntax = await syntaxRef.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                if (syntax is not VariableDeclaratorSyntax declarator || declarator.Initializer is null)
                {
                    continue;
                }

                var initializerSyntax = declarator.Initializer;
                var operation = semanticModel.GetOperation(initializerSyntax, cancellationToken)
                                ?? semanticModel.GetOperation(declarator, cancellationToken)
                                ?? semanticModel.GetOperation(initializerSyntax.Value, cancellationToken);

                if (operation is null)
                {
                    continue;
                }

                var containingMethod = field.IsStatic
                    ? classSymbol.StaticConstructors.FirstOrDefault()
                    : classSymbol.InstanceConstructors.FirstOrDefault();

                containingMethod ??= classSymbol.Constructors.FirstOrDefault()
                                   ?? classSymbol.StaticConstructors.FirstOrDefault()
                                   ?? classSymbol.InstanceConstructors.FirstOrDefault();

                if (containingMethod is null)
                {
                    continue;
                }

                if (!accessesByField.TryGetValue(field, out var list))
                {
                    list = new List<FieldAccessSummary>();
                    accessesByField[field] = list;
                }

                var spanStart = declarator.Identifier.SpanStart;
                var spanEnd = initializerSyntax.Span.End;
                var locationSpan = TextSpan.FromBounds(spanStart, spanEnd);
                var location = Location.Create(declarator.SyntaxTree, locationSpan);

                list.Add(new FieldAccessSummary(
                    field,
                    AccessType.Write,
                    location,
                    containingMethod,
                    operation,
                    isFieldInitializer: true));
            }
        }
    }

    private static IOperation? GetOperationForMethod(
        SemanticModel semanticModel,
        SyntaxNode syntax,
        CancellationToken cancellationToken)
    {
        var operation = semanticModel.GetOperation(syntax, cancellationToken);
        if (operation is not null)
        {
            return operation;
        }

        switch (syntax)
        {
            case BaseMethodDeclarationSyntax methodSyntax:
                if (methodSyntax.Body is { } body)
                {
                    return semanticModel.GetOperation(body, cancellationToken);
                }

                if (methodSyntax.ExpressionBody is { } expressionBody)
                {
                    return semanticModel.GetOperation(expressionBody.Expression, cancellationToken);
                }
                break;

            case AccessorDeclarationSyntax accessorSyntax:
                if (accessorSyntax.Body is { } accessorBody)
                {
                    return semanticModel.GetOperation(accessorBody, cancellationToken);
                }

                if (accessorSyntax.ExpressionBody is { } accessorExpressionBody)
                {
                    return semanticModel.GetOperation(accessorExpressionBody.Expression, cancellationToken);
                }
                break;

            case LocalFunctionStatementSyntax localFunctionSyntax:
                if (localFunctionSyntax.Body is { } localBody)
                {
                    return semanticModel.GetOperation(localBody, cancellationToken);
                }

                if (localFunctionSyntax.ExpressionBody is { } localExpressionBody)
                {
                    return semanticModel.GetOperation(localExpressionBody.Expression, cancellationToken);
                }
                break;
        }

        return null;
    }

    private static List<FieldAccessSummary> AnalyzeOperationTree(
        IOperation operation,
        IMethodSymbol containingMethod)
    {
        var accesses = new List<FieldAccessSummary>();
        WalkOperations(operation, containingMethod, accesses);
        return accesses;
    }

    private static void WalkOperations(
        IOperation operation,
        IMethodSymbol containingMethod,
        List<FieldAccessSummary> accesses)
    {
        if (operation is IFieldReferenceOperation fieldRef)
        {
            var accessType = DetermineAccessType(fieldRef);
            var location = fieldRef.Syntax.GetLocation();

            accesses.Add(new FieldAccessSummary(
                fieldRef.Field,
                accessType,
                location,
                containingMethod,
                fieldRef));
        }
        // TODO: Add auto-property backing field resolution

        foreach (var child in operation.ChildOperations)
        {
            WalkOperations(child, containingMethod, accesses);
        }
    }

    private static AccessType DetermineAccessType(IFieldReferenceOperation fieldRef)
    {
        if (IsWriteContext(fieldRef))
        {
            if (IsReadContext(fieldRef))
                return AccessType.ReadWrite;

            return AccessType.Write;
        }

        return AccessType.Read;
    }

    private static bool IsWriteContext(IOperation operation)
    {
        var parent = operation.Parent;

        return parent switch
        {
            // Direct assignment: _field = value
            ISimpleAssignmentOperation assignment when assignment.Target == operation => true,

            // Compound assignment: _field += value
            ICompoundAssignmentOperation => true,

            // Increment/decrement: _field++
            IIncrementOrDecrementOperation => true,

            // ref/out parameter: Method(ref _field)
            IArgumentOperation arg when arg.Parameter?.RefKind is RefKind.Ref or RefKind.Out => true,

            _ => false
        };
    }

    private static bool IsReadContext(IOperation operation)
    {
        // Field is read if it's not in a pure write context
        // For compound operations (_field++), it's both read and written
        var parent = operation.Parent;

        return parent switch
        {
            // Compound assignment reads the old value
            ICompoundAssignmentOperation => true,

            // Increment/decrement reads the old value
            IIncrementOrDecrementOperation => true,

            // Simple assignment to the field itself is not a read
            ISimpleAssignmentOperation assignment when assignment.Target == operation => false,

            // Everything else is a read
            _ => true
        };
    }

    private static ClassDataflowSummary CreateEmptySummary(
        INamedTypeSymbol classSymbol,
        string documentUri,
        int documentVersion)
    {
        return new ClassDataflowSummary(
            ImmutableDictionary<IFieldSymbol, ImmutableArray<FieldAccessSummary>>.Empty,
            classSymbol,
            documentUri,
            documentVersion);
    }
}
