namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Provides caching for flow analysis results keyed by document and member identifier.
/// </summary>
public interface IFlowAnalysisCache
{
    /// <summary>
    /// Attempts to retrieve cached analysis results for the given document/member pair.
    /// </summary>
    public bool TryGet(string documentPath, string memberId, out FlowAnalysisCacheEntry? entry);

    /// <summary>
    /// Stores analysis results for the given document/member pair.
    /// </summary>
    public void Store(string documentPath, string memberId, FlowAnalysisCacheEntry entry);

    /// <summary>
    /// Invalidates all cached entries associated with the specified document.
    /// </summary>
    public void InvalidateDocument(string documentPath);

    /// <summary>
    /// Returns current cache statistics for diagnostics and testing.
    /// </summary>
    public FlowAnalysisCacheStatistics GetStatistics();
}
