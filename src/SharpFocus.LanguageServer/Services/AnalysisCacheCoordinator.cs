using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Extensions.Logging;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Manages retrieval and population of flow-analysis cache entries.
/// </summary>
public sealed class AnalysisCacheCoordinator
{
    private readonly IFlowAnalysisCache _cache;
    private readonly IDataflowAnalysisRunner _analysisRunner;
    private readonly ILogger<AnalysisCacheCoordinator> _logger;

    public AnalysisCacheCoordinator(
        IFlowAnalysisCache cache,
        IDataflowAnalysisRunner analysisRunner,
        ILogger<AnalysisCacheCoordinator> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _analysisRunner = analysisRunner ?? throw new ArgumentNullException(nameof(analysisRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CacheResolution EnsureCache(
        string filePath,
        ControlFlowGraphResult graphResult,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(graphResult);

        var memberId = graphResult.MemberIdentifier;
        var cacheHit = _cache.TryGet(filePath, memberId, out var cachedEntry) && cachedEntry is not null;
        FlowAnalysisCacheEntry entry;
        int mutationCount = 0;

        if (cacheHit)
        {
            entry = cachedEntry!;
            CacheLog.CacheHit(_logger, memberId, filePath);
        }
        else
        {
            CacheLog.CacheMiss(_logger, memberId, filePath);
            var runResult = _analysisRunner.Run(graphResult.ControlFlowGraph, cancellationToken);
            entry = runResult.CacheEntry;
            mutationCount = runResult.MutationCount;
            _cache.Store(filePath, memberId, entry);
        }

        return new CacheResolution(
            entry,
            cacheHit,
            mutationCount,
            graphResult.MemberIdentifier,
            graphResult.MemberDisplayName,
            graphResult.MethodSymbol);
    }

    public FlowAnalysisCacheStatistics GetStatistics()
    {
        return _cache.GetStatistics();
    }
}

public sealed record CacheResolution(
    FlowAnalysisCacheEntry CacheEntry,
    bool CacheHit,
    int MutationCount,
    string MemberIdentifier,
    string MemberDisplayName,
    IMethodSymbol MethodSymbol);

internal static partial class CacheLog
{
    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Cache hit for member {MemberId} in {FilePath}")]
    public static partial void CacheHit(ILogger logger, string memberId, string filePath);

    [LoggerMessage(EventId = 21, Level = LogLevel.Debug, Message = "Cache miss for member {MemberId} in {FilePath}, executing analysis")]
    public static partial void CacheMiss(ILogger logger, string memberId, string filePath);
}
