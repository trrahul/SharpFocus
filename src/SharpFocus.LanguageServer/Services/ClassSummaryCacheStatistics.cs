namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Statistics about class summary cache performance.
/// </summary>
/// <param name="EntryCount">Total number of cached summaries.</param>
/// <param name="HitCount">Number of successful cache hits.</param>
/// <param name="MissCount">Number of cache misses (requiring rebuild).</param>
public record ClassSummaryCacheStatistics(int EntryCount, int HitCount, int MissCount)
{
    /// <summary>
    /// Cache hit rate (0.0 to 1.0), or 0 if no requests yet.
    /// </summary>
    public double HitRate => HitCount + MissCount > 0
        ? (double)HitCount / (HitCount + MissCount)
        : 0.0;
}
