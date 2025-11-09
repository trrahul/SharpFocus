using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Creates control-flow graphs for method-like bodies.
/// </summary>
public sealed class ControlFlowGraphFactory
{
    private readonly ILogger<ControlFlowGraphFactory> _logger;

    public ControlFlowGraphFactory(ILogger<ControlFlowGraphFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ControlFlowGraphResult? Create(
        SemanticModel semanticModel,
        SyntaxNode bodyOwner,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(bodyOwner);

        var operation = semanticModel.GetOperation(bodyOwner, cancellationToken);
        var cfg = BuildControlFlowGraph(operation, cancellationToken);
        if (cfg is null)
        {
            ControlFlowGraphFactoryLog.ControlFlowGraphUnavailable(_logger, bodyOwner.GetType().Name);
            return null;
        }

        if (semanticModel.GetDeclaredSymbol(bodyOwner, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            ControlFlowGraphFactoryLog.MethodSymbolUnavailable(_logger, bodyOwner.GetType().Name);
            return null;
        }

        var memberIdentifier = methodSymbol.GetDocumentationCommentId()
            ?? methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var memberDisplayName = methodSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        return new ControlFlowGraphResult(cfg, methodSymbol, memberIdentifier, memberDisplayName);
    }

    private static ControlFlowGraph? BuildControlFlowGraph(IOperation? operation, CancellationToken cancellationToken)
    {
        return operation switch
        {
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
            IConstructorBodyOperation ctorBody => ControlFlowGraph.Create(ctorBody, cancellationToken),
            IBlockOperation block => ControlFlowGraph.Create(block, cancellationToken),
            _ => null
        };
    }
}

public sealed record ControlFlowGraphResult(
    ControlFlowGraph ControlFlowGraph,
    IMethodSymbol MethodSymbol,
    string MemberIdentifier,
    string MemberDisplayName);

internal static partial class ControlFlowGraphFactoryLog
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Unable to build control flow graph for syntax node kind {NodeKind}")]
    public static partial void ControlFlowGraphUnavailable(ILogger logger, string nodeKind);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "Unable to resolve method symbol for syntax node kind {NodeKind}")]
    public static partial void MethodSymbolUnavailable(ILogger logger, string nodeKind);
}
