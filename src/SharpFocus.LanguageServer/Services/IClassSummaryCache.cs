using Microsoft.CodeAnalysis;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Provides caching for class dataflow summaries with automatic build-on-miss semantics.
/// </summary>
public interface IClassSummaryCache
{
    /// <summary>
    /// Retrieves or builds a class summary for the specified class symbol.
    /// If a cached summary exists with matching document version, returns it immediately.
    /// Otherwise, builds a new summary using the provided builder and stores it in the cache.
    /// </summary>
    /// <param name="classSymbol">The class symbol to analyze.</param>
    /// <param name="compilation">The compilation containing the class.</param>
    /// <param name="documentVersion">The current version of the document (for cache invalidation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A class dataflow summary (either cached or freshly built).</returns>
    public Task<ClassDataflowSummary> GetOrBuildAsync(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        int documentVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached summaries associated with the specified document URI.
    /// Call this when a document changes to ensure stale summaries are rebuilt.
    /// </summary>
    /// <param name="documentUri">The URI of the document to invalidate.</param>
    public void InvalidateDocument(string documentUri);

    /// <summary>
    /// Returns current cache statistics for diagnostics and performance monitoring.
    /// </summary>
    public ClassSummaryCacheStatistics GetStatistics();
}
