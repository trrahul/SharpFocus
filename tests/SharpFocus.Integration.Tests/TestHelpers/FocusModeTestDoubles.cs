using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Services;
using SharpFocus.LanguageServer.Services.Slicing;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.Integration.Tests.TestHelpers;

internal sealed class NoopClassSummaryCache : IClassSummaryCache
{
    public Task<ClassDataflowSummary> GetOrBuildAsync(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        int documentVersion,
        CancellationToken cancellationToken = default)
    {
        var summary = new ClassDataflowSummary(
            ImmutableDictionary<IFieldSymbol, ImmutableArray<FieldAccessSummary>>.Empty,
            classSymbol,
            classSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? classSymbol.Name,
            documentVersion);

        return Task.FromResult(summary);
    }

    public void InvalidateDocument(string documentUri)
    {
    }

    public ClassSummaryCacheStatistics GetStatistics() => new(0, 0, 0);
}

internal sealed class NoopCrossMethodSliceComposer : ICrossMethodSliceComposer
{
    public SliceResponse? BuildBackwardSlice(
        IFieldSymbol fieldSymbol,
        ClassDataflowSummary summary,
        PlaceInfo focusInfo)
    {
        return null;
    }

    public SliceResponse? BuildForwardSlice(
        IFieldSymbol fieldSymbol,
        ClassDataflowSummary summary,
        PlaceInfo focusInfo)
    {
        return null;
    }
}

internal sealed class NoopWorkspaceManager : IWorkspaceManager
{
    public Task UpdateDocumentAsync(string filePath, string text, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<SemanticModel?> GetSemanticModelAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SemanticModel?>(null);
    }

    public Task<SyntaxTree?> GetSyntaxTreeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SyntaxTree?>(null);
    }

    public Task<int?> GetDocumentVersionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<int?>(null);
    }

    public Task<Compilation?> GetCompilationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Compilation?>(null);
    }
}
