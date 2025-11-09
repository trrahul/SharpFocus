namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Encapsulates artifacts produced by a single flow-analysis execution.
/// </summary>
public sealed record FlowAnalysisRunResult(FlowAnalysisCacheEntry CacheEntry, int MutationCount);
