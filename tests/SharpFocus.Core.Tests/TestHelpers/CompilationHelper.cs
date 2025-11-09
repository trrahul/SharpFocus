using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace SharpFocus.Core.Tests.TestHelpers;

/// <summary>
/// Helper utilities for creating Roslyn compilations and analyzing code in tests.
/// </summary>
public static class CompilationHelper
{
    /// <summary>
    /// Creates a compilation from source code with necessary references.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        // Add basic .NET references
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        return CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Gets the semantic model for the first syntax tree in a compilation.
    /// </summary>
    public static SemanticModel GetSemanticModel(CSharpCompilation compilation)
    {
        return compilation.GetSemanticModel(compilation.SyntaxTrees.First());
    }

    /// <summary>
    /// Creates a control flow graph from a method in source code.
    /// </summary>
    public static ControlFlowGraph CreateControlFlowGraph(string source)
    {
        var (cfg, _, _) = CreateControlFlowGraphWithContext(source);
        return cfg;
    }

    /// <summary>
    /// Creates a control flow graph and semantic model from a method in source code.
    /// </summary>
    public static (ControlFlowGraph cfg, SemanticModel semanticModel) CreateControlFlowGraphWithSemanticModel(string source)
    {
        var (cfg, semanticModel, _) = CreateControlFlowGraphWithContext(source);
        return (cfg, semanticModel);
    }

    /// <summary>
    /// Creates a control flow graph, semantic model, and compilation from a method in source code.
    /// </summary>
    public static (ControlFlowGraph cfg, SemanticModel semanticModel, CSharpCompilation compilation) CreateControlFlowGraphWithContext(string source)
    {
        var compilation = CreateCompilation(source);
        var semanticModel = GetSemanticModel(compilation);
        var tree = compilation.SyntaxTrees.First();

        // Find the first method declaration
        var methodDecl = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No method found in source code");

        // Get the method symbol and operation
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl)
            ?? throw new InvalidOperationException("Could not get method symbol");

        // Get the operation tree rooted at the method
        // We need to get the operation and ensure it's a root (parent == null)
        var root = (IOperation)semanticModel.GetOperation(methodDecl.Body ?? (SyntaxNode)methodDecl.ExpressionBody!)!;

        if (root == null)
            throw new InvalidOperationException("Could not get operation for method body");

        // The operation we got might be a child of IMethodBodyOperation
        // We need to walk up to find the root operation
        while (root.Parent != null)
            root = root.Parent;

        // Now create the CFG from the root operation
        var cfg = root switch
        {
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody),
            IConstructorBodyOperation ctorBody => ControlFlowGraph.Create(ctorBody),
            IBlockOperation block => ControlFlowGraph.Create(block),
            _ => throw new InvalidOperationException($"Unexpected root operation type: {root.GetType().Name}")
        };

        return (cfg, semanticModel, compilation);
    }

    /// <summary>
    /// Creates a control flow graph from a lambda or local function.
    /// </summary>
    public static ControlFlowGraph CreateControlFlowGraphFromLambda(string source)
    {
        var compilation = CreateCompilation(source);
        var semanticModel = GetSemanticModel(compilation);
        var tree = compilation.SyntaxTrees.First();

        // Find the first lambda expression
        var lambda = tree.GetRoot()
            .DescendantNodes()
            .OfType<ParenthesizedLambdaExpressionSyntax>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No lambda found in source code");

        var operation = semanticModel.GetOperation(lambda) as IAnonymousFunctionOperation
            ?? throw new InvalidOperationException("Could not get lambda operation");

        return ControlFlowGraph.Create(operation.Body);
    }

    /// <summary>
    /// Gets a symbol by name from the compilation.
    /// </summary>
    public static ISymbol? GetSymbolByName(CSharpCompilation compilation, string name)
    {
        var semanticModel = GetSemanticModel(compilation);
        var tree = compilation.SyntaxTrees.First();

        // Try to find as variable declarator
        var variable = tree.GetRoot()
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(v => v.Identifier.Text == name);

        if (variable != null)
            return semanticModel.GetDeclaredSymbol(variable);

        // Try to find as parameter
        var parameter = tree.GetRoot()
            .DescendantNodes()
            .OfType<ParameterSyntax>()
            .FirstOrDefault(p => p.Identifier.Text == name);

        if (parameter != null)
            return semanticModel.GetDeclaredSymbol(parameter);

        // Try to find as field
        var field = tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .FirstOrDefault(v => v.Identifier.Text == name);

        if (field != null)
            return semanticModel.GetDeclaredSymbol(field);

        return null;
    }

    /// <summary>
    /// Creates a simple symbol (local variable) for testing.
    /// </summary>
    public static ISymbol CreateTestSymbol(string name, string type = "int")
    {
        var source = $@"
            class TestClass
            {{
                void TestMethod()
                {{
                    {type} {name} = default;
                }}
            }}";

        var compilation = CreateCompilation(source);
        var symbol = GetSymbolByName(compilation, name);

        return symbol ?? throw new InvalidOperationException($"Could not create symbol '{name}'");
    }

    /// <summary>
    /// Creates a field symbol for testing.
    /// </summary>
    public static IFieldSymbol CreateFieldSymbol(string name, string type = "int")
    {
        var source = $@"
            class TestClass
            {{
                {type} {name};
            }}";

        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = GetSemanticModel(compilation);

        var field = tree.GetRoot()
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .First(v => v.Identifier.Text == name);

        return semanticModel.GetDeclaredSymbol(field) as IFieldSymbol
            ?? throw new InvalidOperationException($"Could not create field symbol '{name}'");
    }

    /// <summary>
    /// Creates a parameter symbol for testing.
    /// </summary>
    public static IParameterSymbol CreateParameterSymbol(string name, string type = "int")
    {
        var source = $@"
            class TestClass
            {{
                void TestMethod({type} {name})
                {{
                }}
            }}";

        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = GetSemanticModel(compilation);

        var parameter = tree.GetRoot()
            .DescendantNodes()
            .OfType<ParameterSyntax>()
            .First(p => p.Identifier.Text == name);

        return semanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol
            ?? throw new InvalidOperationException($"Could not create parameter symbol '{name}'");
    }
}
