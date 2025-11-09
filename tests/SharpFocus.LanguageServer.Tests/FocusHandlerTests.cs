using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Utilities;
using SharpFocus.LanguageServer.Handlers;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.LanguageServer.Tests;

public sealed class FocusHandlerTests
{
    [Fact]
    public async Task Handle_WhenLocalVariable_ReturnsDependencyDetailsAndContainers()
    {
        const string code = """
class Sample
{
    int Compute(int input)
    {
        var doubled = input * 2;
        var result = doubled + 1;
        return result;
    }
}
""";

        var filePath = CreateTempFilePath();
        var workspace = new InMemoryWorkspaceManager();
        var cancellationToken = TestContext.Current.CancellationToken;
        await workspace.UpdateDocumentAsync(filePath, code, cancellationToken);

        var handler = CreateHandler(workspace);
        var position = GetPosition(code, "result =");

        var request = new FocusRequest
        {
            TextDocument = new TextDocumentIdentifier(new Uri(filePath)),
            Position = position
        };

        var response = await handler.Handle(request, cancellationToken);

        response.Should().NotBeNull();
        response!.FocusedPlace.Name.Should().Contain("result");
        response.DependencyRanges.Should().NotBeEmpty();
        response.DependencyRangeDetails.Should().NotBeNull();
        response.DependencyRangeDetails!.Count.Should().Be(response.DependencyRanges.Count);
        response.ContainerRanges.Should().NotBeEmpty();
        response.ContainerRanges.All(range => range.Start.Line <= range.End.Line).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAccessorBody_ReturnsResponse()
    {
        const string code = """
class Sample
{
    private int _value;

    int Value
    {
        get => _value;
        set
        {
            _value = value;
        }
    }
}
""";

        var filePath = CreateTempFilePath();
        var workspace = new InMemoryWorkspaceManager();
        var cancellationToken = TestContext.Current.CancellationToken;
        await workspace.UpdateDocumentAsync(filePath, code, cancellationToken);

        var handler = CreateHandler(workspace);
        var position = GetPosition(code, "_value =");

        var request = new FocusRequest
        {
            TextDocument = new TextDocumentIdentifier(new Uri(filePath)),
            Position = position
        };

        var response = await handler.Handle(request, cancellationToken);

        response.Should().NotBeNull();
        response!.ContainerRanges.Should().NotBeEmpty();
    }

    private static FocusHandler CreateHandler(IWorkspaceManager workspace)
    {
        var cache = new InMemoryFlowAnalysisCache();
        var extractor = new RoslynPlaceExtractor();
        var placeResolver = new RoslynPlaceResolver(extractor, NullLogger<RoslynPlaceResolver>.Instance);
        var analysisRunner = new DataflowAnalysisRunner(extractor, NullLogger<DataflowAnalysisRunner>.Instance);
        var documentLoader = new DocumentContextLoader(
            workspace,
            placeResolver,
            NullLogger<DocumentContextLoader>.Instance);
        var graphFactory = new ControlFlowGraphFactory(NullLogger<ControlFlowGraphFactory>.Instance);
        var cacheCoordinator = new AnalysisCacheCoordinator(
            cache,
            analysisRunner,
            NullLogger<AnalysisCacheCoordinator>.Instance);
        var contextBuilder = new AnalysisContextBuilder(
            documentLoader,
            graphFactory,
            cacheCoordinator,
            NullLogger<AnalysisContextBuilder>.Instance);

        return new FocusHandler(
            contextBuilder,
            extractor,
            NullLogger<FocusHandler>.Instance);
    }

    private static Position GetPosition(string source, string marker)
    {
        var index = source.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException($"Marker '{marker}' not found in source");
        }

        var offset = index;
        var line = 0;
        var character = 0;

        for (var i = 0; i < offset; i++)
        {
            if (source[i] == '\r')
            {
                continue;
            }

            if (source[i] == '\n')
            {
                line++;
                character = 0;
            }
            else
            {
                character++;
            }
        }

        return new Position(line, character);
    }

    private static string CreateTempFilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SharpFocus", "FocusHandlerTests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.cs");
    }
}
