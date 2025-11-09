using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SharpFocus.LanguageServer.Protocol;
using SharpFocus.LanguageServer.Services;
using SharpFocus.LanguageServer.Tests.TestHelpers;
using Xunit;

namespace SharpFocus.LanguageServer.Tests;

public sealed class AnalysisOrchestratorTests
{
    [Fact]
    public async Task ComputeSliceAsync_ReusesContextFromBuilder()
    {
        var context = CreateContext();
        var builder = new CountingContextBuilder(context);
        var sliceService = new TestSliceService();
        var focusService = new FocusModeAnalysisService(
            sliceService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            new NoopWorkspaceManager(),
            NullLogger<FocusModeAnalysisService>.Instance);
        var flowService = new AggregatedFlowAnalysisService(sliceService, NullLogger<AggregatedFlowAnalysisService>.Instance);
        var orchestrator = new AnalysisOrchestrator(builder, sliceService, focusService, flowService, NullLogger<AnalysisOrchestrator>.Instance);

        var document = new TextDocumentIdentifier(new Uri("file:///test.cs"));
        var position = new Position(3, 2);

        var response = await orchestrator.ComputeSliceAsync(SliceDirection.Backward, document, position, CancellationToken.None);

        response.Should().NotBeNull();
        response.Should().BeSameAs(sliceService.BackwardResponse);
        builder.CallCount.Should().Be(1);
        sliceService.ComputeSliceAsyncCallCount.Should().Be(0);
        sliceService.Contexts.Should().ContainSingle().Which.Should().BeSameAs(context);
    }

    [Fact]
    public async Task AnalyzeFocusModeAsync_BuildsContextOnce()
    {
        var context = CreateContext();
        var builder = new CountingContextBuilder(context);
        var sliceService = new TestSliceService();
        var focusService = new FocusModeAnalysisService(
            sliceService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            new NoopWorkspaceManager(),
            NullLogger<FocusModeAnalysisService>.Instance);
        var flowService = new AggregatedFlowAnalysisService(sliceService, NullLogger<AggregatedFlowAnalysisService>.Instance);
        var orchestrator = new AnalysisOrchestrator(builder, sliceService, focusService, flowService, NullLogger<AnalysisOrchestrator>.Instance);

        var request = new FocusModeRequest
        {
            TextDocument = new TextDocumentIdentifier(new Uri("file:///test.cs")),
            Position = new Position(5, 1)
        };

        var response = await orchestrator.AnalyzeFocusModeAsync(request, CancellationToken.None);

        response.Should().NotBeNull();
        response!.BackwardSlice.Should().BeSameAs(sliceService.BackwardResponse);
        response.ForwardSlice.Should().BeSameAs(sliceService.ForwardResponse);
        builder.CallCount.Should().Be(1);
        sliceService.Contexts.Should().HaveCount(2);
        sliceService.Contexts.Should().OnlyContain(ctx => ReferenceEquals(ctx, context));
    }

    [Fact]
    public async Task AnalyzeFlowAsync_BuildsContextOnce()
    {
        var context = CreateContext();
        var builder = new CountingContextBuilder(context);
        var sliceService = new TestSliceService();
        var focusService = new FocusModeAnalysisService(
            sliceService,
            new NoopClassSummaryCache(),
            new NoopCrossMethodSliceComposer(),
            new NoopWorkspaceManager(),
            NullLogger<FocusModeAnalysisService>.Instance);
        var flowService = new AggregatedFlowAnalysisService(sliceService, NullLogger<AggregatedFlowAnalysisService>.Instance);
        var orchestrator = new AnalysisOrchestrator(builder, sliceService, focusService, flowService, NullLogger<AnalysisOrchestrator>.Instance);

        var request = new FlowAnalysisRequest
        {
            TextDocument = new TextDocumentIdentifier(new Uri("file:///test.cs")),
            Position = new Position(2, 4)
        };

        var response = await orchestrator.AnalyzeFlowAsync(request, CancellationToken.None);

        response.Should().NotBeNull();
        response!.BackwardSlice.Should().BeSameAs(sliceService.BackwardResponse);
        response.ForwardSlice.Should().BeSameAs(sliceService.ForwardResponse);
        builder.CallCount.Should().Be(1);
        sliceService.Contexts.Should().HaveCount(2);
        sliceService.Contexts.Should().OnlyContain(ctx => ReferenceEquals(ctx, context));
    }

    private static AnalysisContext CreateContext()
    {
        var focusPlace = new PlaceInfo
        {
            Name = "value",
            Kind = "Local",
            Range = new LspRange(new Position(0, 0), new Position(0, 1))
        };

        return new AnalysisContext(
            FilePath: "test.cs",
            BodyOwner: null!,
            FocusNode: null!,
            SourceText: SourceText.From(string.Empty),
            FocusedPlace: null!,
            ControlFlowGraph: null!,
            CacheEntry: null!,
            FocusInfo: focusPlace,
            ContainerRanges: Array.Empty<LspRange>(),
            CacheStatistics: new FlowAnalysisCacheStatistics(0, 0, 0),
            CacheHit: false,
            MemberIdentifier: "M:test",
            MemberDisplayName: "Test",
            MutationCount: 0);
    }

    private sealed class CountingContextBuilder : IAnalysisContextBuilder
    {
        private readonly AnalysisContext? _context;

        public CountingContextBuilder(AnalysisContext? context)
        {
            _context = context;
        }

        public int CallCount { get; private set; }

        public Task<AnalysisContext?> BuildAsync(TextDocumentIdentifier document, Position position, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_context);
        }
    }

    private sealed class TestSliceService : IDataflowSliceService
    {
        private readonly SliceResponse? _backwardResponse;
        private readonly SliceResponse? _forwardResponse;

        public TestSliceService()
        {
            var focusedPlace = new PlaceInfo
            {
                Name = "value",
                Kind = "Local",
                Range = new LspRange(new Position(0, 0), new Position(0, 1))
            };

            _backwardResponse = CreateSliceResponse(SliceDirection.Backward, focusedPlace, SliceRelation.Source);
            _forwardResponse = CreateSliceResponse(SliceDirection.Forward, focusedPlace, SliceRelation.Transform);
        }

        public SliceResponse? BackwardResponse => _backwardResponse;

        public SliceResponse? ForwardResponse => _forwardResponse;

        public int ComputeSliceAsyncCallCount { get; private set; }

        public List<AnalysisContext> Contexts { get; } = new();

        public bool ThrowOnComputeSliceAsync { get; set; } = true;

        public Task<SliceResponse?> ComputeSliceAsync(
            SliceDirection direction,
            TextDocumentIdentifier document,
            Position position,
            CancellationToken cancellationToken)
        {
            ComputeSliceAsyncCallCount++;

            if (ThrowOnComputeSliceAsync)
            {
                throw new InvalidOperationException("ComputeSliceAsync should not be invoked when reusing contexts.");
            }

            return Task.FromResult(direction == SliceDirection.Backward ? _backwardResponse : _forwardResponse);
        }

        public SliceResponse? ComputeSliceFromContext(
            SliceDirection direction,
            AnalysisContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            Contexts.Add(context);
            return direction == SliceDirection.Backward ? _backwardResponse : _forwardResponse;
        }

        private static SliceResponse CreateSliceResponse(
            SliceDirection direction,
            PlaceInfo focusedPlace,
            SliceRelation relation)
        {
            var range = new LspRange(new Position((int)direction + 1, 0), new Position((int)direction + 1, 2));

            return new SliceResponse
            {
                Direction = direction,
                FocusedPlace = focusedPlace,
                SliceRanges = new[] { range },
                ContainerRanges = new[] { range },
                SliceRangeDetails = new[]
                {
                    new SliceRangeInfo
                    {
                        Range = range,
                        Place = focusedPlace,
                        Relation = relation,
                        OperationKind = "Test",
                        Summary = "summary"
                    }
                }
            };
        }
    }
}
