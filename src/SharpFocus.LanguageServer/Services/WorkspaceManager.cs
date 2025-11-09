using System.IO;
using SharpFocus.Core.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SharpFocus.LanguageServer.Services;

/// <summary>
/// Manages Roslyn workspace and document synchronization for LSP.
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>
    /// Updates or adds a document to the workspace.
    /// </summary>
    public Task UpdateDocumentAsync(string filePath, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the semantic model for a document.
    /// </summary>
    public Task<SemanticModel?> GetSemanticModelAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the syntax tree for a document.
    /// </summary>
    public Task<SyntaxTree?> GetSyntaxTreeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the compilation for the workspace.
    /// </summary>
    public Task<Compilation?> GetCompilationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cached document version for the specified file, if available.
    /// </summary>
    public Task<int?> GetDocumentVersionAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple in-memory workspace manager for C# documents.
/// </summary>
public sealed class InMemoryWorkspaceManager : IWorkspaceManager
{
    private readonly Dictionary<string, DocumentEntry> _documents = new(StringComparer.OrdinalIgnoreCase);
    private CSharpCompilation? _compilation;
    private readonly object _lock = new();

    public Task UpdateDocumentAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _documents[filePath] = new DocumentEntry(content, DocumentVersionCalculator.Compute(content));
            _compilation = null; // Invalidate compilation
        }

        return Task.CompletedTask;
    }

    public Task<SemanticModel?> GetSemanticModelAsync(string filePath, CancellationToken cancellationToken = default)
    {
        EnsureDocumentLoaded(filePath);
        var compilation = GetOrCreateCompilation();
        if (compilation == null)
            return Task.FromResult<SemanticModel?>(null);

        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(syntaxTree != null ? compilation.GetSemanticModel(syntaxTree) : null);
    }

    public Task<SyntaxTree?> GetSyntaxTreeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        EnsureDocumentLoaded(filePath);
        var compilation = GetOrCreateCompilation();
        if (compilation == null)
            return Task.FromResult<SyntaxTree?>(null);

        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(syntaxTree);
    }

    public Task<Compilation?> GetCompilationAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Compilation?>(GetOrCreateCompilation());
    }

    public Task<int?> GetDocumentVersionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult<int?>(null);
        }

        lock (_lock)
        {
            if (_documents.TryGetValue(filePath, out var entry))
            {
                return Task.FromResult<int?>(entry.Version);
            }
        }

        EnsureDocumentLoaded(filePath);

        lock (_lock)
        {
            return Task.FromResult(_documents.TryGetValue(filePath, out var entry) ? (int?)entry.Version : null);
        }
    }

    private CSharpCompilation? GetOrCreateCompilation()
    {
        lock (_lock)
        {
            if (_compilation != null)
                return _compilation;

            if (_documents.Count == 0)
                return null;

            var syntaxTrees = _documents.Select(kvp =>
                CSharpSyntaxTree.ParseText(
                    SourceText.From(kvp.Value.Content),
                    path: kvp.Key)).ToList();

            _compilation = CSharpCompilation.Create(
                "SharpFocusAnalysis",
                syntaxTrees,
                GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return _compilation;
        }
    }

    private void EnsureDocumentLoaded(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        lock (_lock)
        {
            if (_documents.ContainsKey(filePath))
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                return;
            }

            var content = File.ReadAllText(filePath);
            _documents[filePath] = new DocumentEntry(content, DocumentVersionCalculator.Compute(content));
            _compilation = null;
        }
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Enumerable).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                yield return MetadataReference.CreateFromFile(assembly.Location);
            }
        }
    }

    private sealed record DocumentEntry(string Content, int Version);
}
