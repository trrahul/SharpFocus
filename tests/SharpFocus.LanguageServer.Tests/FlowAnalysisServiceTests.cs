using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;
using SharpFocus.LanguageServer.Tests.TestHelpers;

namespace SharpFocus.LanguageServer.Tests;

public sealed class AggregatedFlowAnalysisServiceTests
{
    [Fact]
    public async Task Analyze_ReturnsBothSlices()
    {
        var focusedPlace = new PlaceInfo
        {
            Name = "value",
            Kind = "Local",
            Range = new LspRange(new Position(0, 0), new Position(0, 5))
        };

        var backwardSlice = new SliceResponse
        {
            Direction = SliceDirection.Backward,
            FocusedPlace = focusedPlace,
            SliceRanges = new[] { new LspRange(new Position(1, 0), new Position(1, 5)) },
            ContainerRanges = Array.Empty<LspRange>()
        };

        var forwardSlice = new SliceResponse
        {
            Direction = SliceDirection.Forward,
            FocusedPlace = focusedPlace,
            SliceRanges = new[] { new LspRange(new Position(2, 0), new Position(2, 5)) },
            ContainerRanges = Array.Empty<LspRange>()
        };

        var sliceService = new StubSliceService(backwardSlice, forwardSlice);
        var contextBuilder = new StubContextBuilder();
        var focusModeService = new FocusModeAnalysisService(
            sliceService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            new NoopWorkspaceManager(),
            NullLogger<FocusModeAnalysisService>.Instance);
        var aggregatedService = new AggregatedFlowAnalysisService(sliceService, NullLogger<AggregatedFlowAnalysisService>.Instance);
        var orchestrator = new AnalysisOrchestrator(
            contextBuilder,
            sliceService,
            focusModeService,
            aggregatedService,
            NullLogger<AnalysisOrchestrator>.Instance);

        var request = new FlowAnalysisRequest
        {
            TextDocument = new TextDocumentIdentifier(new Uri("file:///test.cs")),
            Position = new Position(0, 0)
        };

        var response = await orchestrator.AnalyzeFlowAsync(request, CancellationToken.None);

        response.Should().NotBeNull();
        response!.BackwardSlice.Should().Be(backwardSlice);
        response.ForwardSlice.Should().Be(forwardSlice);
    }

    [Fact]
    public async Task Analyze_WhenNoSlices_ReturnsNull()
    {
        var sliceService = new StubSliceService(null, null);
        var contextBuilder = new StubContextBuilder();
        var focusModeService = new FocusModeAnalysisService(
            sliceService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            new NoopWorkspaceManager(),
            NullLogger<FocusModeAnalysisService>.Instance);
        var aggregatedService = new AggregatedFlowAnalysisService(sliceService, NullLogger<AggregatedFlowAnalysisService>.Instance);
        var orchestrator = new AnalysisOrchestrator(
            contextBuilder,
            sliceService,
            focusModeService,
            aggregatedService,
            NullLogger<AnalysisOrchestrator>.Instance);

        var request = new FlowAnalysisRequest
        {
            TextDocument = new TextDocumentIdentifier(new Uri("file:///test.cs")),
            Position = new Position(0, 0)
        };

        var response = await orchestrator.AnalyzeFlowAsync(request, CancellationToken.None);

        response.Should().BeNull();
    }

    private sealed class StubContextBuilder : IAnalysisContextBuilder
    {
        public Task<AnalysisContext?> BuildAsync(TextDocumentIdentifier document, Position position, CancellationToken cancellationToken)
        {
            // Create a minimal mock context - the services don't actually use most of these fields in the stub scenario
            var mockContext = new AnalysisContext(
                FilePath: "test.cs",
                BodyOwner: null!,
                FocusNode: null!,
                SourceText: null!,
                FocusedPlace: null!,
                ControlFlowGraph: null!,
                CacheEntry: null!,
                FocusInfo: new PlaceInfo { Name = "test", Kind = "Local", Range = new LspRange(new Position(0, 0), new Position(0, 4)) },
                ContainerRanges: Array.Empty<LspRange>(),
                CacheStatistics: new FlowAnalysisCacheStatistics(0, 0, 0),
                CacheHit: false,
                MemberIdentifier: "test",
                MemberDisplayName: "test",
                MutationCount: 0
            );
            return Task.FromResult<AnalysisContext?>(mockContext);
        }
    }

    private sealed class StubSliceService : IDataflowSliceService
    {
        private readonly SliceResponse? _backward;
        private readonly SliceResponse? _forward;

        public StubSliceService(SliceResponse? backward, SliceResponse? forward)
        {
            _backward = backward;
            _forward = forward;
        }

        public Task<SliceResponse?> ComputeSliceAsync(
            SliceDirection direction,
            TextDocumentIdentifier document,
            Position position,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(direction == SliceDirection.Backward ? _backward : _forward);
        }

        public SliceResponse? ComputeSliceFromContext(
            SliceDirection direction,
            AnalysisContext context)
        {
            return direction == SliceDirection.Backward ? _backward : _forward;
        }
    }
}
