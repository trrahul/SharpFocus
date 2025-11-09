using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using SharpFocus.Core.Abstractions;
using SharpFocus.Core.Utilities;
using SharpFocus.LanguageServer.Services;
using Xunit;

namespace SharpFocus.Integration.Tests.LanguageServer;

public class RoslynPlaceResolverTests
{
    [Fact]
    public void Resolve_PropertyAccess_ReturnsBasePlaceWithProjection()
    {
        const string source = @"class Engine { public int Horsepower { get; set; } }
class Car { public Engine Engine { get; set; } }
class Sample
{
    int Compute(Car car)
    {
        return car.Engine.Horsepower;
    }
}";

        var (semanticModel, identifier, token) = GetSemanticModelAndIdentifier(source, syntax =>
            syntax is IdentifierNameSyntax name && name.Identifier.Text == "Engine" && name.Parent is MemberAccessExpressionSyntax);

        var resolver = CreateResolver();
        var place = resolver.Resolve(semanticModel, identifier, token, CancellationToken.None);

        place.Should().NotBeNull();
        place!.Symbol.Name.Should().Be("car");
        place.AccessPath.Should().ContainSingle().Which.Name.Should().Be("Engine");
    }

    [Fact]
    public void Resolve_LocalIdentifier_ReturnsLocalPlace()
    {
        const string source = @"class Sample
{
    void M()
    {
        int value = 42;
        _ = value + 1;
    }
}";

        var (semanticModel, identifier, token) = GetSemanticModelAndIdentifier(source, syntax =>
            syntax is IdentifierNameSyntax name && name.Identifier.Text == "value" && name.Parent is BinaryExpressionSyntax);

        var resolver = CreateResolver();
        var place = resolver.Resolve(semanticModel, identifier, token, CancellationToken.None);

        place.Should().NotBeNull();
        place!.Symbol.Kind.Should().Be(SymbolKind.Local);
        place.AccessPath.Should().BeEmpty();
    }

    private static RoslynPlaceResolver CreateResolver()
    {
        IPlaceExtractor extractor = new RoslynPlaceExtractor();
        return new RoslynPlaceResolver(extractor, NullLogger<RoslynPlaceResolver>.Instance);
    }

    [Fact]
    public void Resolve_ForeachKeyword_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        const string source = @"class Sample
{
    void M(int[] values)
    {
        foreach (var value in values)
        {
        }
    }
}";

        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "SampleAssembly",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);
        var foreachStatement = tree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<ForEachStatementSyntax>()
            .Single();

        var resolver = CreateResolver();
        var place = resolver.Resolve(semanticModel, foreachStatement, foreachStatement.ForEachKeyword, CancellationToken.None);

        place.Should().BeNull();
    }

    [Fact]
    public void Resolve_FieldInAssignmentExpression_ReturnsFieldPlace()
    {
        const string source = @"
using System.Threading;

class Sample
{
    private CancellationTokenSource? _cts;

    void M(CancellationToken token)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
    }
}";

        var (semanticModel, identifier, token) = GetSemanticModelAndIdentifier(source, syntax =>
            syntax is IdentifierNameSyntax name &&
            name.Identifier.Text == "_cts" &&
            name.Parent is AssignmentExpressionSyntax assignment &&
            assignment.Left == name);

        var resolver = CreateResolver();
        var place = resolver.Resolve(semanticModel, identifier, token, CancellationToken.None);

        place.Should().NotBeNull();
        place!.Symbol.Kind.Should().Be(SymbolKind.Field);
        place.Symbol.Name.Should().Be("_cts");
        place.AccessPath.Should().BeEmpty();
    }

    private static (SemanticModel SemanticModel, SyntaxNode Node, SyntaxToken Token) GetSemanticModelAndIdentifier(
        string source,
        Func<SyntaxNode, bool> predicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "SampleAssembly",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);
        var node = tree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .First(predicate);
        var token = node.GetFirstToken();
        return (semanticModel, node, token);
    }
}
