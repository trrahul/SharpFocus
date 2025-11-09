using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Represents a cached class dataflow summary with its associated document version.
/// Used to determine cache validity when the document changes.
/// </summary>
/// <param name="Summary">The cached class dataflow summary.</param>
/// <param name="DocumentVersion">The document version when this summary was built.</param>
internal sealed record ClassSummaryCacheEntry(
    ClassDataflowSummary Summary,
    int DocumentVersion);
