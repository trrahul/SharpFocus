using Microsoft.CodeAnalysis.FlowAnalysis;
using SharpFocus.Core.Models;

namespace SharpFocus.Core.Abstractions;

/// <summary>
/// Coordinates the dataflow analysis fixpoint over a control-flow graph.
/// </summary>
public interface IDataflowEngine
{
    /// <summary>
    /// Executes the dataflow analysis for the supplied control-flow graph.
    /// </summary>
    /// <param name="cfg">Control-flow graph to analyze.</param>
    /// <returns>Analysis results containing the flow domain at each program location.</returns>
    public FlowAnalysisResults Analyze(ControlFlowGraph cfg);
}
