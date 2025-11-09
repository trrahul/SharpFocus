using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Thread-safe in-memory cache for class dataflow summaries.
/// Implements build-on-miss semantics: if a summary isn't cached or is stale, it builds a new one automatically.
/// </summary>
public sealed class ClassSummaryCache : IClassSummaryCache
{
    private readonly IClassSummaryBuilder _builder;
    private readonly ILogger<ClassSummaryCache> _logger;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ClassSummaryCacheEntry>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private int _hitCount;
    private int _missCount;

    public ClassSummaryCache(IClassSummaryBuilder builder, ILogger<ClassSummaryCache> logger)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ClassDataflowSummary> GetOrBuildAsync(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        int documentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(classSymbol);
        ArgumentNullException.ThrowIfNull(compilation);

        var syntaxRef = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            _logger.LogWarning("Class {ClassName} has no syntax reference, building summary without caching",
                classSymbol.Name);
            return await _builder.AnalyzeClassAsync(classSymbol, compilation, cancellationToken);
        }

        var documentUri = syntaxRef.SyntaxTree.FilePath;
        var classSymbolKey = GetSymbolKey(classSymbol);

        if (_cache.TryGetValue(documentUri, out var classCache) &&
            classCache.TryGetValue(classSymbolKey, out var entry))
        {
            if (entry.DocumentVersion == documentVersion)
            {
                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug(
                    "Cache hit for class {ClassName} in {DocumentUri} (version {Version})",
                    classSymbol.Name, documentUri, documentVersion);
                return entry.Summary;
            }

            _logger.LogDebug(
                "Cache stale for class {ClassName} in {DocumentUri} (cached version {CachedVersion}, current {CurrentVersion})",
                classSymbol.Name, documentUri, entry.DocumentVersion, documentVersion);
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogDebug(
            "Cache miss for class {ClassName} in {DocumentUri}, building summary",
            classSymbol.Name, documentUri);

        var summary = await _builder.AnalyzeClassAsync(classSymbol, compilation, cancellationToken);

        var newEntry = new ClassSummaryCacheEntry(summary, documentVersion);
        var documentCache = _cache.GetOrAdd(documentUri,
            _ => new ConcurrentDictionary<string, ClassSummaryCacheEntry>(StringComparer.Ordinal));
        documentCache[classSymbolKey] = newEntry;

        _logger.LogDebug(
            "Cached summary for class {ClassName} in {DocumentUri} (version {Version}, {FieldCount} fields tracked)",
            classSymbol.Name, documentUri, documentVersion, summary.FieldAccesses.Count);

        return summary;
    }

    /// <inheritdoc />
    public void InvalidateDocument(string documentUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentUri);

        if (_cache.TryRemove(documentUri, out var removed))
            _logger.LogInformation(
                "Invalidated {Count} cached class summaries for document {DocumentUri}",
                removed.Count, documentUri);
        else
            _logger.LogDebug("No cached summaries to invalidate for document {DocumentUri}", documentUri);
    }

    /// <inheritdoc />
    public ClassSummaryCacheStatistics GetStatistics()
    {
        var entryCount = _cache.Sum(pair => pair.Value.Count);
        var hits = Volatile.Read(ref _hitCount);
        var misses = Volatile.Read(ref _missCount);
        return new ClassSummaryCacheStatistics(entryCount, hits, misses);
    }

    /// <summary>
    /// Generates a stable string key for a class symbol.
    /// Uses fully qualified metadata name to uniquely identify the class across compilations.
    /// </summary>
    private static string GetSymbolKey(INamedTypeSymbol classSymbol)
    {
        return classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
