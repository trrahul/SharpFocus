using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Protocol;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Captures Roslyn artifacts and metadata required to perform focus/slice computations.
/// </summary>
public sealed record AnalysisContext(
    string FilePath,
    SyntaxNode BodyOwner,
    SyntaxNode FocusNode,
    SourceText SourceText,
    Place FocusedPlace,
    ControlFlowGraph ControlFlowGraph,
    FlowAnalysisCacheEntry CacheEntry,
    PlaceInfo FocusInfo,
    IReadOnlyList<LspRange> ContainerRanges,
    FlowAnalysisCacheStatistics CacheStatistics,
    bool CacheHit,
    string MemberIdentifier,
    string MemberDisplayName,
    int MutationCount)
{
    public string FocusCacheKey => FlowAnalysisCacheEntry.CreateCacheKey(FocusedPlace);
}
