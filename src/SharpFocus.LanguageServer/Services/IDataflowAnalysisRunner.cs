using System.Threading;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Executes the SharpFocus flow analysis pipeline and returns cacheable artifacts.
/// </summary>
public interface IDataflowAnalysisRunner
{
    public FlowAnalysisRunResult Run(ControlFlowGraph controlFlowGraph, CancellationToken cancellationToken);
}
