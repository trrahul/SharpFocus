using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Extensions.Logging;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Analyzers;
using SharpFocus.Core.Engine;
using SharpFocus.Core.Models;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Default implementation that wires together alias analysis, mutation detection and dataflow execution.
/// </summary>
public sealed class DataflowAnalysisRunner : IDataflowAnalysisRunner
{
    private readonly IPlaceExtractor _placeExtractor;
    private readonly Func<IPlaceExtractor, IAliasAnalyzer> _aliasAnalyzerFactory;
    private readonly Func<IPlaceExtractor, IMutationDetector> _mutationDetectorFactory;
    private readonly Func<IControlFlowDependencyAnalyzer> _controlDependencyFactory;
    private readonly Func<IAliasAnalyzer, IMutationDetector, IControlFlowDependencyAnalyzer, IPlaceExtractor, IDataflowTransferFunction> _transferFunctionFactory;
    private readonly Func<IDataflowTransferFunction, IDataflowEngine> _engineFactory;
    private readonly ILogger<DataflowAnalysisRunner> _logger;

    public DataflowAnalysisRunner(
        IPlaceExtractor placeExtractor,
        ILogger<DataflowAnalysisRunner> logger,
        Func<IPlaceExtractor, IAliasAnalyzer>? aliasAnalyzerFactory = null,
        Func<IPlaceExtractor, IMutationDetector>? mutationDetectorFactory = null,
        Func<IControlFlowDependencyAnalyzer>? controlDependencyFactory = null,
        Func<IAliasAnalyzer, IMutationDetector, IControlFlowDependencyAnalyzer, IPlaceExtractor, IDataflowTransferFunction>? transferFunctionFactory = null,
        Func<IDataflowTransferFunction, IDataflowEngine>? engineFactory = null)
    {
        _placeExtractor = placeExtractor ?? throw new ArgumentNullException(nameof(placeExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aliasAnalyzerFactory = aliasAnalyzerFactory ?? (extractor => new BasicAliasAnalyzer(extractor));
        _mutationDetectorFactory = mutationDetectorFactory ?? (extractor => new RoslynMutationDetector(extractor));
        _controlDependencyFactory = controlDependencyFactory ?? (() => new ControlFlowDependencyAnalyzer());
        _transferFunctionFactory = transferFunctionFactory ?? ((alias, mutation, control, extractor) => new DataflowTransferFunction(alias, mutation, control, extractor));
        _engineFactory = engineFactory ?? (transfer => new DataflowEngine(transfer));
    }

    public FlowAnalysisRunResult Run(ControlFlowGraph controlFlowGraph, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(controlFlowGraph);

        var aliasAnalyzer = _aliasAnalyzerFactory(_placeExtractor);
        var mutationDetector = _mutationDetectorFactory(_placeExtractor);
        var controlDependencyAnalyzer = _controlDependencyFactory();
        var transferFunction = _transferFunctionFactory(aliasAnalyzer, mutationDetector, controlDependencyAnalyzer, _placeExtractor);
        var engine = _engineFactory(transferFunction);

        var results = engine.Analyze(controlFlowGraph);
        var cacheEntry = FlowAnalysisCacheEntry.Create(
            results,
            transferFunction.ReadsByLocation,
            aliasAnalyzer.ExportAliases(),
            transferFunction.MutationsByLocation);

        var mutationCount = transferFunction.MutationsByLocation.Sum(pair => pair.Value.Count);

        _logger.LogDebug(
            "Flow analysis completed. Blocks={BlockCount}, MutationLocations={MutationLocations}",
            controlFlowGraph.Blocks.Length,
            transferFunction.MutationsByLocation.Count);

        return new FlowAnalysisRunResult(cacheEntry, mutationCount);
    }
}
