using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Tests.TestHelpers;

internal sealed class NoopClassSummaryCache : IClassSummaryCache
{
    public Task<ClassDataflowSummary> GetOrBuildAsync(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        int documentVersion,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Class summary cache should not be exercised in these tests.");
    }

    public void InvalidateDocument(string documentUri)
    {
    }

    public ClassSummaryCacheStatistics GetStatistics()
    {
        return new ClassSummaryCacheStatistics(0, 0, 0);
    }
}

internal sealed class NoopCrossMethodSliceComposer : ICrossMethodSliceComposer
{
    public SliceResponse? BuildBackwardSlice(IFieldSymbol fieldSymbol, ClassDataflowSummary summary, PlaceInfo focusedPlace)
    {
        return null;
    }

    public SliceResponse? BuildForwardSlice(IFieldSymbol fieldSymbol, ClassDataflowSummary summary, PlaceInfo focusedPlace)
    {
        return null;
    }
}

internal sealed class NoopWorkspaceManager : IWorkspaceManager
{
    public Task UpdateDocumentAsync(string filePath, string content, CancellationToken cancellationToken = default)
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

    public Task<Compilation?> GetCompilationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Compilation?>(null);
    }

    public Task<int?> GetDocumentVersionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<int?>(null);
    }
}
