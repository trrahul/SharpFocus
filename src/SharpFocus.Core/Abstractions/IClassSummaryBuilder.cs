using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Builds comprehensive dataflow summaries for classes.
/// Analyzes all methods once and caches results for efficient querying.
/// </summary>
public interface IClassSummaryBuilder
{
    /// <summary>
    /// Analyzes a class and builds a comprehensive dataflow summary.
    /// </summary>
    /// <param name="classSymbol">The class symbol to analyze.</param>
    /// <param name="compilation">The compilation containing the class.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A complete dataflow summary for the class.</returns>
    public Task<ClassDataflowSummary> AnalyzeClassAsync(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        CancellationToken cancellationToken = default);
}
