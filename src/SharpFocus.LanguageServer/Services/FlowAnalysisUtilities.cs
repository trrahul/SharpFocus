using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;
using SharpFocus.Core.Engine;

namespace SharpFocus.LanguageServer.Services;

internal static class FlowAnalysisUtilities
{
    public static bool TryFindBodyOwner(SyntaxNode node, out SyntaxNode bodyOwner)
    {
        if (node is null)
        {
            bodyOwner = default!;
            return false;
        }

        SyntaxNode? candidate =
            (SyntaxNode?)node.FirstAncestorOrSelf<MethodDeclarationSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<AccessorDeclarationSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<SimpleLambdaExpressionSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<ParenthesizedLambdaExpressionSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<AnonymousMethodExpressionSyntax>();

        if (candidate is null)
        {
            bodyOwner = default!;
            return false;
        }

        bodyOwner = candidate;
        return true;
    }

    public static IReadOnlyList<LspRange> CreateContainerRanges(SyntaxNode bodyOwner, SourceText sourceText)
    {
        ArgumentNullException.ThrowIfNull(bodyOwner);
        ArgumentNullException.ThrowIfNull(sourceText);

        var spans = new HashSet<TextSpan>();

        if (bodyOwner is BaseMethodDeclarationSyntax method && method.Body is BlockSyntax methodBody)
        {
            spans.Add(methodBody.Span);
        }
        else if (bodyOwner is AccessorDeclarationSyntax accessor && accessor.Body is BlockSyntax accessorBody)
        {
            spans.Add(accessorBody.Span);
        }
        else if (bodyOwner is LocalFunctionStatementSyntax localFunction && localFunction.Body is BlockSyntax localBody)
        {
            spans.Add(localBody.Span);
        }
        else if (bodyOwner is AnonymousFunctionExpressionSyntax anonymous && anonymous.Body is BlockSyntax anonymousBody)
        {
            spans.Add(anonymousBody.Span);
        }

        if (bodyOwner is BaseMethodDeclarationSyntax methodExpr && methodExpr.ExpressionBody is ArrowExpressionClauseSyntax arrowExpression)
        {
            spans.Add(arrowExpression.Expression.Span);
        }

        if (bodyOwner is AccessorDeclarationSyntax accessorExpr && accessorExpr.ExpressionBody is ArrowExpressionClauseSyntax accessorArrow)
        {
            spans.Add(accessorArrow.Expression.Span);
        }

        if (spans.Count == 0)
        {
            spans.Add(bodyOwner.Span);
        }

        return spans
            .Select(span => ToLspRange(sourceText, span))
            .OrderBy(range => range.Start.Line)
            .ThenBy(range => range.Start.Character)
            .ToArray();
    }

    public static bool TryResolveProgramLocation(
        ControlFlowGraph cfg,
        CachedProgramLocation cachedLocation,
        out ProgramLocation location)
    {
        if (cachedLocation.BlockOrdinal < 0 || cachedLocation.BlockOrdinal >= cfg.Blocks.Length)
        {
            location = default!;
            return false;
        }

        var block = cfg.Blocks[cachedLocation.BlockOrdinal];
        if (cachedLocation.OperationIndex < 0 || cachedLocation.OperationIndex > block.Operations.Length)
        {
            location = default!;
            return false;
        }

        location = new ProgramLocation(block, cachedLocation.OperationIndex);
        return true;
    }

    public static IOperation? TryGetOperation(ProgramLocation location)
    {
        var block = location.Block;

        if (location.OperationIndex < block.Operations.Length)
        {
            return block.Operations[location.OperationIndex];
        }

        if (location.OperationIndex == block.Operations.Length)
        {
            return block.BranchValue;
        }

        return null;
    }

    public static LspRange ToLspRange(SourceText sourceText, TextSpan span)
    {
        var lineSpan = sourceText.Lines.GetLinePositionSpan(span);
        return new LspRange
        {
            Start = new Position(lineSpan.Start.Line, lineSpan.Start.Character),
            End = new Position(lineSpan.End.Line, lineSpan.End.Character)
        };
    }

    /// <summary>
    /// Get a precise syntax span for an operation, preferring specific elements over broad structural spans.
    /// For loop operations, returns the iteration variable or condition instead of the entire loop block.
    /// </summary>
    public static TextSpan GetPreciseSyntaxSpan(IOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // For variable declarations, use just the identifier, not the entire initializer
        if (operation is IVariableDeclaratorOperation declarator && declarator.Symbol is not null)
        {
            if (declarator.Syntax is VariableDeclaratorSyntax declaratorSyntax && declaratorSyntax.Identifier != default)
            {
                // Highlight just "average", not "average = ..."
                return declaratorSyntax.Identifier.Span;
            }
        }

        if (operation is IVariableDeclarationOperation declaration && declaration.Declarators.Length > 0)
        {
            var first = declaration.Declarators[0];
            if (first.Syntax is VariableDeclaratorSyntax declaratorSyntax && declaratorSyntax.Identifier != default)
            {
                return declaratorSyntax.Identifier.Span;
            }
        }

        if (operation is IVariableDeclarationGroupOperation declarationGroup && !declarationGroup.Declarations.IsDefaultOrEmpty)
        {
            var firstDeclaration = declarationGroup.Declarations[0];
            if (firstDeclaration.Declarators.Length > 0)
            {
                var first = firstDeclaration.Declarators[0];
                if (first.Syntax is VariableDeclaratorSyntax declaratorSyntax && declaratorSyntax.Identifier != default)
                {
                    return declaratorSyntax.Identifier.Span;
                }
            }
        }

        // For local reference operations, use the identifier span
        if (operation is ILocalReferenceOperation localRef)
        {
            var syntax = localRef.Syntax;
            if (syntax is IdentifierNameSyntax identifier)
            {
                return identifier.Span;
            }
        }

        // For parameter reference operations, use the identifier span
        if (operation is IParameterReferenceOperation paramRef)
        {
            var syntax = paramRef.Syntax;
            if (syntax is IdentifierNameSyntax identifier)
            {
                return identifier.Span;
            }
        }

        // For field/property references, use just the member name
        if (operation is IFieldReferenceOperation fieldRef)
        {
            var syntax = fieldRef.Syntax;
            if (syntax is MemberAccessExpressionSyntax memberAccess)
            {
                // Highlight "Length" in "filtered.Length", not the entire expression
                return memberAccess.Name.Span;
            }
            if (syntax is IdentifierNameSyntax identifier)
            {
                return identifier.Span;
            }
        }

        // For property references, use just the property name
        if (operation is IPropertyReferenceOperation propRef)
        {
            var syntax = propRef.Syntax;
            if (syntax is MemberAccessExpressionSyntax memberAccess)
            {
                // Highlight "Length" in "filtered.Length"
                return memberAccess.Name.Span;
            }
            if (syntax is IdentifierNameSyntax identifier)
            {
                return identifier.Span;
            }
        }

        // For foreach loops, highlight the iteration variable usage, not the entire loop
        if (operation is IForEachLoopOperation forEachLoop)
        {
            // Try to get the iteration variable syntax
            var syntax = forEachLoop.Syntax;
            if (syntax is ForEachStatementSyntax forEachSyntax)
            {
                // Highlight just the "foreach (var item in collection)" header, not the body
                return TextSpan.FromBounds(forEachSyntax.ForEachKeyword.SpanStart, forEachSyntax.CloseParenToken.Span.End);
            }
        }

        // For for loops, highlight the loop header, not the entire body
        if (operation is IForLoopOperation forLoop)
        {
            var syntax = forLoop.Syntax;
            if (syntax is ForStatementSyntax forSyntax)
            {
                // Highlight just "for (int i = 0; i < n; i++)", not the body
                return TextSpan.FromBounds(forSyntax.ForKeyword.SpanStart, forSyntax.CloseParenToken.Span.End);
            }
        }

        // For while loops, highlight the condition, not the entire body
        if (operation is IWhileLoopOperation whileLoop)
        {
            var syntax = whileLoop.Syntax;
            if (syntax is WhileStatementSyntax whileSyntax)
            {
                // Highlight "while (condition)", not the body
                return TextSpan.FromBounds(whileSyntax.WhileKeyword.SpanStart, whileSyntax.CloseParenToken.Span.End);
            }
        }

        // For conditional access, prefer the actual access expression
        if (operation is IConditionalAccessOperation conditionalAccess)
        {
            // Highlight the ?. or ?[] part, not the entire chain
            return conditionalAccess.WhenNotNull.Syntax.Span;
        }

        // Default: use the operation's syntax span
        return operation.Syntax?.Span ?? default;
    }

    public static Place? TryCreateRepresentativePlace(IPlaceExtractor extractor, IOperation operation)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(operation);

        var direct = extractor.TryCreatePlace(operation);
        if (direct is not null)
        {
            return direct;
        }

        return operation switch
        {
            IAssignmentOperation assignment =>
                extractor.TryCreatePlace(assignment.Target) ?? extractor.TryCreatePlace(assignment.Value),
            IIncrementOrDecrementOperation increment => extractor.TryCreatePlace(increment.Target),
            IVariableDeclaratorOperation declarator when declarator.Symbol is not null => new Place(declarator.Symbol),
            IVariableDeclarationOperation declaration when declaration.Declarators.Length == 1 && declaration.Declarators[0].Symbol is not null
                => new Place(declaration.Declarators[0].Symbol!),
            _ => null
        };
    }
}
