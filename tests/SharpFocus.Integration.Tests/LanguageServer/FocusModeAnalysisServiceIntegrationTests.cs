using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Utilities;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;
using SharpFocus.LanguageServer.Services.Slicing;
using SharpFocus.Integration.Tests.TestHelpers;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace SharpFocus.Integration.Tests.LanguageServer;

public sealed class FocusModeAnalysisServiceIntegrationTests
{
    [Fact]
    public async Task AnalyzeAsync_LargeMethod_ProducesContainerWithGaps()
    {
        const string code = "using System;" +
                            "\nusing System.Collections.Generic;" +
                            "\n" +
                            "\nclass Sample" +
                            "\n{" +
                            "\n    public int Compute(int[] values)" +
                            "\n    {" +
                            "\n        int total = 0;" +
                            "\n        int multiplier = 1;" +
                            "\n        for (int i = 0; i < values.Length; i++)" +
                            "\n        {" +
                            "\n            int current = values[i];" +
                            "\n            if (current % 2 == 0)" +
                            "\n            {" +
                            "\n                total += current;" +
                            "\n            }" +
                            "\n            else" +
                            "\n            {" +
                            "\n                total += current * multiplier;" +
                            "\n            }" +
                            "\n            multiplier = multiplier + 1;" +
                            "\n        }" +
                            "\n" +
                            "\n        int extra = 0;" +
                            "\n        foreach (var value in values)" +
                            "\n        {" +
                            "\n            extra += value;" +
                            "\n        }" +
                            "\n" +
                            "\n        int Adjust(int seed)" +
                            "\n        {" +
                            "\n            for (int j = 0; j < values.Length; j++)" +
                            "\n            {" +
                            "\n                seed += j;" +
                            "\n            }" +
                            "\n            return seed;" +
                            "\n        }" +
                            "\n" +
                            "\n        extra = Adjust(extra);" +
                            "\n        total += extra;" +
                            "\n" +
                            "\n        return total + extra;" +
                            "\n    }" +
                            "\n}";

        var workspace = new InMemoryWorkspaceManager();
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs");
        await workspace.UpdateDocumentAsync(filePath, code, CancellationToken.None);

        var syntaxTree = await workspace.GetSyntaxTreeAsync(filePath, CancellationToken.None);
        syntaxTree.Should().NotBeNull();
        var sourceText = await syntaxTree!.GetTextAsync(CancellationToken.None);

        var returnLineIndex = FindLineContaining(sourceText, "return total + extra;");
        var returnLine = sourceText.Lines[returnLineIndex];
        var totalOffset = returnLine.ToString().IndexOf("total", StringComparison.Ordinal);
        totalOffset.Should().BeGreaterThanOrEqualTo(0);

        var position = new Position(returnLineIndex, totalOffset);
        var textDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath));

        var orchestrator = CreateOrchestrator(workspace);

        var request = new FocusModeRequest
        {
            TextDocument = textDocument,
            Position = position
        };

        var response = await orchestrator.AnalyzeFocusModeAsync(request, CancellationToken.None);

        response.Should().NotBeNull();
        response!.RelevantRanges.Should().HaveCountGreaterThan(5);
        response.ContainerRanges.Should().NotBeEmpty();

        foreach (var container in response.ContainerRanges)
        {
            foreach (var relevant in response.RelevantRanges)
            {
                relevant.Start.Line.Should().BeGreaterThanOrEqualTo(container.Start.Line);
                relevant.End.Line.Should().BeLessThanOrEqualTo(container.End.Line);
            }
        }

        var containerRange = response.ContainerRanges.First();
        var linesWithHighlights = CollectCoveredLines(response.RelevantRanges);
        var containerHasGap = ContainerHasNonHighlightedLines(containerRange, linesWithHighlights, sourceText);

        containerHasGap.Should().BeTrue("container should contain non-highlighted code to drive fade ranges");
    }

    [Fact]
    public async Task AnalyzeAsync_SimpleDataflow_ProducesRelationDetails()
    {
        const string code = """
        using System;

        class Sample
        {
            string Process(string input)
            {
                var trimmed = input.Trim();
                var upper = trimmed.ToUpperInvariant();
                var message = $"Value: {upper}";
                Console.WriteLine(message);
                return message;
            }
        }
        """;

        var workspace = new InMemoryWorkspaceManager();
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cs");
        await workspace.UpdateDocumentAsync(filePath, code, CancellationToken.None);

        var syntaxTree = await workspace.GetSyntaxTreeAsync(filePath, CancellationToken.None);
        syntaxTree.Should().NotBeNull();
        var sourceText = await syntaxTree!.GetTextAsync(CancellationToken.None);

        var focusLineIndex = FindLineContaining(sourceText, "var upper = trimmed.ToUpperInvariant();");
        var focusLine = sourceText.Lines[focusLineIndex];
        var upperColumn = focusLine.ToString().IndexOf("upper", StringComparison.Ordinal);
        upperColumn.Should().BeGreaterThanOrEqualTo(0);

        var request = new FocusModeRequest
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)),
            Position = new Position(focusLineIndex, upperColumn + 1)
        };

        var orchestrator = CreateOrchestrator(workspace);

        var response = await orchestrator.AnalyzeFocusModeAsync(request, CancellationToken.None);

        response.Should().NotBeNull();
        response!.FocusedPlace.Name.Should().Be("upper");

        response.BackwardSlice.Should().NotBeNull();
        var backwardDetails = response.BackwardSlice!.SliceRangeDetails;
        backwardDetails.Should().NotBeNullOrEmpty();
        backwardDetails!.Should().OnlyContain(detail => detail.Relation == SliceRelation.Source);
        var backwardNames = backwardDetails.Select(detail => detail.Place.Name).ToList();
        backwardNames.Should().Contain("trimmed");
        backwardNames.Should().Contain("upper");
        backwardDetails
            .Select(detail => detail.Summary ?? string.Empty)
            .Should().Contain(summary => summary.Contains("trimmed", StringComparison.Ordinal) && summary.Contains("upper", StringComparison.Ordinal));

        response.ForwardSlice.Should().NotBeNull();
        var forwardDetails = response.ForwardSlice!.SliceRangeDetails;
        forwardDetails.Should().NotBeNullOrEmpty();
        forwardDetails!.Select(detail => detail.Relation).Should().Contain(SliceRelation.Transform);
        forwardDetails.Select(detail => detail.Relation).Should().Contain(SliceRelation.Sink);

        forwardDetails
            .Where(detail => detail.Relation == SliceRelation.Transform)
            .Select(detail => detail.Place.Name)
            .Should().Contain(name => name.Contains("message", StringComparison.Ordinal));

        forwardDetails
            .Where(detail => detail.Relation == SliceRelation.Sink)
            .Select(detail => detail.Place.Name)
            .Should().Contain(name => name.Contains("Console.WriteLine", StringComparison.Ordinal));

        response.RelevantRanges.Should().Contain(range => range.Start.Line == focusLineIndex);
    }

    private static int FindLineContaining(SourceText sourceText, string value)
    {
        for (var index = 0; index < sourceText.Lines.Count; index++)
        {
            if (sourceText.Lines[index].ToString().Contains(value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Line containing '{value}' was not found.");
    }

    private static HashSet<int> CollectCoveredLines(IReadOnlyList<LspRange> ranges)
    {
        var covered = new HashSet<int>();

        foreach (var range in ranges)
        {
            for (var line = (int)range.Start.Line; line <= (int)range.End.Line; line++)
            {
                covered.Add(line);
            }
        }

        return covered;
    }

    private static bool ContainerHasNonHighlightedLines(LspRange container, HashSet<int> highlightedLines, SourceText sourceText)
    {
        for (var line = (int)container.Start.Line; line <= (int)container.End.Line; line++)
        {
            if (highlightedLines.Contains(line))
            {
                continue;
            }

            var text = sourceText.Lines[line].ToString().Trim();
            if (text.Length == 0 || text == "{" || text == "}")
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static AnalysisOrchestrator CreateOrchestrator(IWorkspaceManager workspace)
    {
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
        var dataflowService = new DataflowSliceService(
            contextBuilder,
            strategies,
            NullLogger<DataflowSliceService>.Instance);
        var focusModeService = new FocusModeAnalysisService(
            dataflowService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            workspace,
            NullLogger<FocusModeAnalysisService>.Instance);
        var aggregatedService = new AggregatedFlowAnalysisService(dataflowService, NullLogger<AggregatedFlowAnalysisService>.Instance);

        return new AnalysisOrchestrator(
            contextBuilder,
            dataflowService,
            focusModeService,
            aggregatedService,
            NullLogger<AnalysisOrchestrator>.Instance);
    }
}
