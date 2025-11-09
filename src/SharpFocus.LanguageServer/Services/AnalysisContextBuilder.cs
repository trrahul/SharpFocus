using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Creates reusable analysis contexts that encapsulate Roslyn state and cached flow-analysis results.
/// </summary>
public sealed class AnalysisContextBuilder : IAnalysisContextBuilder
{
    private readonly DocumentContextLoader _documentLoader;
    private readonly ControlFlowGraphFactory _graphFactory;
    private readonly AnalysisCacheCoordinator _cacheCoordinator;
    private readonly ILogger<AnalysisContextBuilder> _logger;

    public AnalysisContextBuilder(
        DocumentContextLoader documentLoader,
        ControlFlowGraphFactory graphFactory,
        AnalysisCacheCoordinator cacheCoordinator,
        ILogger<AnalysisContextBuilder> logger)
    {
        _documentLoader = documentLoader ?? throw new ArgumentNullException(nameof(documentLoader));
        _graphFactory = graphFactory ?? throw new ArgumentNullException(nameof(graphFactory));
        _cacheCoordinator = cacheCoordinator ?? throw new ArgumentNullException(nameof(cacheCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AnalysisContext?> BuildAsync(
        TextDocumentIdentifier document,
        Position position,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var documentContext = await _documentLoader
            .LoadAsync(document, position, cancellationToken)
            .ConfigureAwait(false);

        if (documentContext is null)
        {
            return null;
        }

        if (documentContext.BodyOwner is null)
        {
            return BuildFieldContext(documentContext);
        }

        return BuildMemberContext(documentContext, cancellationToken);
    }

    private AnalysisContext? BuildFieldContext(DocumentContextResult context)
    {
        if (context.FieldSymbol is not { } fieldSymbol)
        {
            AnalysisContextBuilderLog.BodyOwnerMissing(
                _logger,
                context.FocusedPlace.ToString(),
                context.FilePath);
            return null;
        }

        AnalysisContextBuilderLog.FieldContextCreated(_logger, fieldSymbol.Name);

        var focusInfo = PlaceInfoFactory.CreatePlaceInfo(context.FocusNode, context.SourceText, context.FocusedPlace);
        var cacheStatistics = _cacheCoordinator.GetStatistics();

        return new AnalysisContext(
            FilePath: context.FilePath,
            BodyOwner: null!,
            FocusNode: context.FocusNode,
            SourceText: context.SourceText,
            FocusedPlace: context.FocusedPlace,
            ControlFlowGraph: null!,
            CacheEntry: null!,
            FocusInfo: focusInfo,
            ContainerRanges: Array.Empty<OmniSharp.Extensions.LanguageServer.Protocol.Models.Range>(),
            CacheStatistics: cacheStatistics,
            CacheHit: false,
            MemberIdentifier: string.Empty,
            MemberDisplayName: string.Empty,
            MutationCount: 0);
    }

    private AnalysisContext? BuildMemberContext(
        DocumentContextResult context,
        CancellationToken cancellationToken)
    {
        var bodyOwner = context.BodyOwner;
        if (bodyOwner is null)
        {
            return null;
        }

        var graphResult = _graphFactory.Create(context.SemanticModel, bodyOwner, cancellationToken);
        if (graphResult is null)
        {
            return null;
        }

        var cacheResolution = _cacheCoordinator.EnsureCache(
            context.FilePath,
            graphResult,
            cancellationToken);

        var focusInfo = PlaceInfoFactory.CreatePlaceInfo(context.FocusNode, context.SourceText, context.FocusedPlace);
        var containerRanges = FlowAnalysisUtilities.CreateContainerRanges(bodyOwner, context.SourceText);
        var cacheStatistics = _cacheCoordinator.GetStatistics();

        return new AnalysisContext(
            context.FilePath,
            bodyOwner,
            context.FocusNode,
            context.SourceText,
            context.FocusedPlace,
            graphResult.ControlFlowGraph,
            cacheResolution.CacheEntry,
            focusInfo,
            containerRanges,
            cacheStatistics,
            cacheResolution.CacheHit,
            cacheResolution.MemberIdentifier,
            cacheResolution.MemberDisplayName,
            cacheResolution.MutationCount);
    }
}

internal static partial class AnalysisContextBuilderLog
{
    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Detected field symbol {FieldName} without body owner, creating minimal context for cross-method analysis")]
    public static partial void FieldContextCreated(ILogger logger, string fieldName);

    [LoggerMessage(EventId = 31, Level = LogLevel.Warning, Message = "No containing member found for place {Place} in {FilePath}")]
    public static partial void BodyOwnerMissing(ILogger logger, string place, string filePath);
}
