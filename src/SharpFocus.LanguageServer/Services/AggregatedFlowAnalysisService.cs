using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Aggregates backward and forward slices for Flow Analysis mode.
/// </summary>
public sealed class AggregatedFlowAnalysisService
{
    private readonly IDataflowSliceService _sliceService;
    private readonly ILogger<AggregatedFlowAnalysisService> _logger;

    public AggregatedFlowAnalysisService(
        IDataflowSliceService sliceService,
        ILogger<AggregatedFlowAnalysisService> logger)
    {
        _sliceService = sliceService ?? throw new ArgumentNullException(nameof(sliceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public FlowAnalysisResponse? Analyze(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var backward = _sliceService.ComputeSliceFromContext(SliceDirection.Backward, context);
        var forward = _sliceService.ComputeSliceFromContext(SliceDirection.Forward, context);

        if (backward is null && forward is null)
        {
            _logger.LogInformation("Flow Analysis produced no slice results for {FilePath}", context.FilePath);
            return null;
        }

        return new FlowAnalysisResponse
        {
            BackwardSlice = backward,
            ForwardSlice = forward
        };
    }
}
