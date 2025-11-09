using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Thread-safe in-memory cache for storing flow analysis results keyed by document and member identifier.
/// </summary>
public sealed class InMemoryFlowAnalysisCache : IFlowAnalysisCache
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, FlowAnalysisCacheEntry>> _entries = new(StringComparer.OrdinalIgnoreCase);
    private int _hitCount;
    private int _missCount;

    public bool TryGet(string documentPath, string memberId, out FlowAnalysisCacheEntry? entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        if (_entries.TryGetValue(documentPath, out var members) && members.TryGetValue(memberId, out var stored))
        {
            Interlocked.Increment(ref _hitCount);
            entry = stored;
            return true;
        }

        Interlocked.Increment(ref _missCount);
        entry = null;
        return false;
    }

    public void Store(string documentPath, string memberId, FlowAnalysisCacheEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentNullException.ThrowIfNull(entry);

        var memberCache = _entries.GetOrAdd(documentPath, _ => new ConcurrentDictionary<string, FlowAnalysisCacheEntry>(StringComparer.Ordinal));
        memberCache[memberId] = entry;
    }

    public void InvalidateDocument(string documentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        _entries.TryRemove(documentPath, out _);
    }

    public FlowAnalysisCacheStatistics GetStatistics()
    {
        var entryCount = _entries.Sum(static pair => pair.Value.Count);
        var hits = Volatile.Read(ref _hitCount);
        var misses = Volatile.Read(ref _missCount);
        return new FlowAnalysisCacheStatistics(entryCount, hits, misses);
    }
}
