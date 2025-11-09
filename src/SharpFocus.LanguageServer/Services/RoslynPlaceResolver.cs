using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Resolves <see cref="Place"/> instances using Roslyn semantic information.
/// </summary>
public sealed class RoslynPlaceResolver : IPlaceResolver
{
    private readonly IPlaceExtractor _placeExtractor;
    private readonly ILogger<RoslynPlaceResolver> _logger;

    public RoslynPlaceResolver(IPlaceExtractor placeExtractor, ILogger<RoslynPlaceResolver> logger)
    {
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Place? Resolve(SemanticModel semanticModel, SyntaxNode node, SyntaxToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(node);

        if (ShouldIgnoreToken(token))
        {
            _logger.LogDebug("Skipping place resolution for keyword token {TokenKind}", token.Kind());
            return null;
        }

        var place = TryFromOperation(semanticModel, node, cancellationToken);
        if (place != null)
        {
            return place;
        }

        // Try GetDeclaredSymbol, but only for actual declaration contexts (not references)
        var declaredSymbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
        if (declaredSymbol != null && IsValidDeclaredSymbol(declaredSymbol, node))
        {
            _logger.LogDebug("Resolved place from declared symbol {Symbol} ({Kind})", declaredSymbol.Name, declaredSymbol.Kind);
            return new Place(declaredSymbol);
        }

        place = TryFromAncestorOperation(semanticModel, node, cancellationToken);
        if (place != null)
        {
            return place;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
        if (symbolInfo is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
        {
            _logger.LogDebug("Resolved place from referenced symbol {Symbol} ({Kind})", symbolInfo.Name, symbolInfo.Kind);
            return new Place(symbolInfo);
        }

        _logger.LogDebug("Unable to resolve place for node of kind {Kind}", node.Kind());
        return null;
    }

    private Place? TryFromOperation(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
    {
        var operation = semanticModel.GetOperation(node, cancellationToken);
        if (operation == null)
        {
            return null;
        }

        var place = _placeExtractor.TryCreatePlace(operation);
        if (place != null)
        {
            _logger.LogDebug("Resolved place from operation {OperationKind}", operation.Kind);
        }

        return place;
    }

    private Place? TryFromAncestorOperation(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
    {
        var current = node.Parent;
        while (current != null)
        {
            var operation = semanticModel.GetOperation(current, cancellationToken);
            if (operation != null)
            {
                var place = _placeExtractor.TryCreatePlace(operation);
                if (place != null)
                {
                    _logger.LogDebug("Resolved place from ancestor operation {OperationKind}", operation.Kind);
                    return place;
                }
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool ShouldIgnoreToken(SyntaxToken token)
    {
        if (token == default || token.IsMissing)
        {
            return false;
        }

        var kind = (SyntaxKind)token.RawKind;

        if (!SyntaxFacts.IsKeywordKind(kind))
        {
            return false;
        }

        return kind is not SyntaxKind.ThisKeyword and not SyntaxKind.BaseKeyword;
    }

    /// <summary>
    /// Validates that a symbol returned by GetDeclaredSymbol is actually in a declaration context.
    /// This prevents accepting symbols when the cursor is on a reference (like an assignment target).
    /// </summary>
    private static bool IsValidDeclaredSymbol(ISymbol declaredSymbol, SyntaxNode node)
    {
        // Fields should only be declared in field declaration syntax, not in assignments
        if (declaredSymbol is IFieldSymbol)
        {
            // Accept only if the node is actually a field declarator
            return node is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax declarator
                && declarator.Parent?.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax;
        }

        // Local variables should only be accepted in declaration context
        if (declaredSymbol is ILocalSymbol)
        {
            // Accept if it's a variable declarator
            return node is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax;
        }

        // For parameters, methods, types, etc., GetDeclaredSymbol is always valid
        return true;
    }
}
