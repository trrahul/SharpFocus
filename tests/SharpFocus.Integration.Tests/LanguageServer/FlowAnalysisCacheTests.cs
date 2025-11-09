using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpFocus.Core.Models;
using SharpFocus.LanguageServer.Services;

namespace SharpFocus.Integration.Tests.LanguageServer;

public class FlowAnalysisCacheTests
{
    [Fact]
    public void TryGet_WhenCacheIsEmpty_ReturnsFalseAndRecordsMiss()
    {
        var cache = new InMemoryFlowAnalysisCache();

        cache.TryGet("/path/File.cs", "M:Sample.C.M", out var result).Should().BeFalse();
        result.Should().BeNull();

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(0);
        stats.HitCount.Should().Be(0);
        stats.MissCount.Should().Be(1);
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsCachedResultAndRecordsHit()
    {
        var cache = new InMemoryFlowAnalysisCache();
        var (entry, key) = CreateSampleEntry();

        cache.Store("/path/File.cs", "M:Sample.C.M", entry);

        cache.TryGet("/path/File.cs", "M:Sample.C.M", out var cached).Should().BeTrue();
        cached.Should().NotBeNull();
        var cacheEntry = cached!;
        cacheEntry.TryGetDependencies(key, out var cachedLocations).Should().BeTrue();
        cachedLocations.Should().NotBeEmpty();
        cacheEntry.TryGetReads(key, out var readLocations).Should().BeTrue();
        readLocations.Should().NotBeEmpty();
        cacheEntry.TryGetAliases(key, out var aliases).Should().BeTrue();
        aliases.Should().NotBeEmpty();
        cacheEntry.TryGetMutationTargets(readLocations.First(), out var targets).Should().BeTrue();
        targets.Should().NotBeEmpty();

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(1);
        stats.HitCount.Should().Be(1);
        stats.MissCount.Should().Be(0);
    }

    [Fact]
    public void InvalidateDocument_RemovesEntriesAndRecordsMiss()
    {
        var cache = new InMemoryFlowAnalysisCache();
        cache.Store("/path/File.cs", "M:Sample.C.M", CreateSampleEntry().Entry);
        cache.TryGet("/path/File.cs", "M:Sample.C.M", out var cached).Should().BeTrue();
        cached.Should().NotBeNull();

        cache.InvalidateDocument("/path/File.cs");

        cache.TryGet("/path/File.cs", "M:Sample.C.M", out var result).Should().BeFalse();
        result.Should().BeNull();

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(0);
        stats.HitCount.Should().Be(1);
        stats.MissCount.Should().Be(1);
    }

    private static (FlowAnalysisCacheEntry Entry, string Key) CreateSampleEntry()
    {
        var (results, place, reads, aliases, mutations) = CreateSampleResults();
        var entry = FlowAnalysisCacheEntry.Create(results, reads, aliases, mutations);
        var key = FlowAnalysisCacheEntry.CreateCacheKey(place);
        return (entry, key);
    }

    private static (
        FlowAnalysisResults Results,
        Place Place,
        IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Place>> Reads,
        IReadOnlyDictionary<Place, IReadOnlyCollection<Place>> Aliases,
        IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Mutation>> Mutations) CreateSampleResults()
    {
        var (cfg, semanticModel, tree) = CreateControlFlowGraphWithContext(@"
            class Sample
            {
                int Compute(int value)
                {
                    int intermediate = value + 1;
                    return intermediate;
                }
            }");

        var block = cfg.Blocks.First(b => b.Operations.Length > 0);
        var location = new ProgramLocation(block, 0);

        var variable = tree.GetRoot()
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.Text == "intermediate");

        var symbol = semanticModel.GetDeclaredSymbol(variable) ?? throw new InvalidOperationException("Missing symbol");
        var place = new Place(symbol);
        var domain = new FlowDomain();
        domain.AddDependency(place, location);

        var snapshot = new Dictionary<ProgramLocation, FlowDomain>
        {
            [location] = domain
        };

        IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Place>> reads = new Dictionary<ProgramLocation, IReadOnlyList<Place>>
        {
            [location] = new List<Place> { place }
        };

        IReadOnlyDictionary<Place, IReadOnlyCollection<Place>> aliases = new Dictionary<Place, IReadOnlyCollection<Place>>
        {
            [place] = Array.Empty<Place>()
        };

        IReadOnlyDictionary<ProgramLocation, IReadOnlyList<Mutation>> mutations = new Dictionary<ProgramLocation, IReadOnlyList<Mutation>>
        {
            [location] = new List<Mutation>
            {
                new Mutation(place, location, MutationKind.Assignment)
            }
        };

        return (new FlowAnalysisResults(snapshot), place, reads, aliases, mutations);
    }

    private static (ControlFlowGraph cfg, SemanticModel semanticModel, SyntaxTree tree) CreateControlFlowGraphWithContext(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "SampleAssembly",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        IOperation operation = (semanticModel.GetOperation(method.Body ?? (SyntaxNode)method.ExpressionBody!) ?? throw new InvalidOperationException("Missing operation"));
        while (operation.Parent != null)
        {
            operation = operation.Parent;
        }

        var cfg = operation switch
        {
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody),
            IBlockOperation block => ControlFlowGraph.Create(block),
            _ => throw new InvalidOperationException($"Unexpected operation {operation.GetType().Name}")
        };

        return (cfg, semanticModel, tree);
    }
}
