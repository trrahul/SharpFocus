using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Resolves a <see cref="Place"/> from a semantic model and syntax node context.
/// </summary>
public interface IPlaceResolver
{
    /// <summary>
    /// Attempts to resolve a <see cref="Place"/> for the specified syntax node.
    /// </summary>
    /// <param name="semanticModel">The semantic model associated with the node.</param>
    /// <param name="node">The node representing the position of interest.</param>
    /// <param name="token">The exact token at the cursor position.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved <see cref="Place"/> or <c>null</c> when no place can be determined.</returns>
    public Place? Resolve(
        SemanticModel semanticModel,
        SyntaxNode node,
        SyntaxToken token,
        CancellationToken cancellationToken);
}
