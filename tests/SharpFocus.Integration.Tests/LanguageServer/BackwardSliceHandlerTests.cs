using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Utilities;
using SharpFocus.LanguageServer.Handlers;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;
using SharpFocus.LanguageServer.Services.Slicing;
using SharpFocus.Integration.Tests.TestHelpers;

namespace SharpFocus.Integration.Tests.LanguageServer;

public class BackwardSliceHandlerTests
{
    [Fact]
    public async Task Handle_SimpleAssignment_ReturnsDependencyRanges()
    {
        const string code = @"using System;

class Sample
{
    int Compute(int input)
    {
        int y = input + 1;
        int z = y * 2;
        return z;
    }
}";

        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs");
        var workspace = new InMemoryWorkspaceManager();
        await workspace.UpdateDocumentAsync(filePath, code, CancellationToken.None);

        var syntaxTree = await workspace.GetSyntaxTreeAsync(filePath, CancellationToken.None);
        syntaxTree.Should().NotBeNull();
        var tree = (CSharpSyntaxTree)syntaxTree!;
        var root = await tree.GetRootAsync(CancellationToken.None);
        var declarator = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().First(d => d.Identifier.Text == "z");
        var text = await tree.GetTextAsync(CancellationToken.None);
        var position = text.Lines.GetLinePosition(declarator.Identifier.SpanStart);
        var assignmentLine = text.Lines.GetLinePosition(declarator.SpanStart).Line;

        IPlaceExtractor placeExtractor = new RoslynPlaceExtractor();
        var placeResolver = new RoslynPlaceResolver(placeExtractor, NullLogger<RoslynPlaceResolver>.Instance);
        var cache = new InMemoryFlowAnalysisCache();
        var analysisRunner = new DataflowAnalysisRunner(placeExtractor, NullLogger<DataflowAnalysisRunner>.Instance);
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
        var aliasResolver = new SliceAliasResolver();
        var strategies = new ISliceComputationStrategy[]
        {
            new BackwardSliceStrategy(placeExtractor, NullLogger<BackwardSliceStrategy>.Instance),
            new ForwardSliceStrategy(placeExtractor, aliasResolver, NullLogger<ForwardSliceStrategy>.Instance)
        };
        var sliceService = new DataflowSliceService(
            contextBuilder,
            strategies,
            NullLogger<DataflowSliceService>.Instance);
        var focusModeService = new FocusModeAnalysisService(
            sliceService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            workspace,
            NullLogger<FocusModeAnalysisService>.Instance);
        var aggregatedService = new AggregatedFlowAnalysisService(sliceService, NullLogger<AggregatedFlowAnalysisService>.Instance);
        var orchestrator = new AnalysisOrchestrator(
            contextBuilder,
            sliceService,
            focusModeService,
            aggregatedService,
            NullLogger<AnalysisOrchestrator>.Instance);
        var handler = new BackwardSliceHandler(orchestrator, NullLogger<BackwardSliceHandler>.Instance);

        var request = new BackwardSliceRequest
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath(filePath)
            },
            Position = new Position(position.Line, position.Character)
        };

        var response = await handler.Handle(request, CancellationToken.None);

        response.Should().NotBeNull();
        response!.Direction.Should().Be(SliceDirection.Backward);
        response.SliceRanges.Should().NotBeEmpty();
        response.SliceRanges.Should().Contain(range => range.Start.Line == assignmentLine);
    }
}
