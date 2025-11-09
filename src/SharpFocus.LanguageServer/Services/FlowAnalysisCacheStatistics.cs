namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Diagnostic information describing the current state of the analysis cache.
/// </summary>
public sealed record FlowAnalysisCacheStatistics(int EntryCount, int HitCount, int MissCount);
