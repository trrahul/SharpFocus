using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SharpFocus.Core.Models;

/// <summary>
/// Represents a comprehensive dataflow summary for a single class.
/// Built once per class and reused for all symbol queries.
/// </summary>
public sealed record ClassDataflowSummary
{
    /// <summary>
    /// Gets field accesses indexed by field symbol.
    /// Key: IFieldSymbol, Value: All accesses to that field across all methods.
    /// </summary>
    public ImmutableDictionary<IFieldSymbol, ImmutableArray<FieldAccessSummary>> FieldAccesses { get; init; }

    /// <summary>
    /// Gets the class symbol this summary describes.
    /// </summary>
    public INamedTypeSymbol ClassSymbol { get; init; }

    /// <summary>
    /// Gets the document URI where this class is defined.
    /// </summary>
    public string DocumentUri { get; init; }

    /// <summary>
    /// Gets the document version this summary was built from.
    /// Used for cache invalidation.
    /// </summary>
    public int DocumentVersion { get; init; }

    /// <summary>
    /// Creates a new ClassDataflowSummary.
    /// </summary>
    /// <param name="fieldAccesses">Field access information indexed by field symbol.</param>
    /// <param name="classSymbol">The class symbol.</param>
    /// <param name="documentUri">The document URI.</param>
    /// <param name="documentVersion">The document version.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ClassDataflowSummary(
        ImmutableDictionary<IFieldSymbol, ImmutableArray<FieldAccessSummary>> fieldAccesses,
        INamedTypeSymbol classSymbol,
        string documentUri,
        int documentVersion)
    {
        ArgumentNullException.ThrowIfNull(fieldAccesses);
        ArgumentNullException.ThrowIfNull(classSymbol);
        ArgumentNullException.ThrowIfNull(documentUri);

        FieldAccesses = fieldAccesses;
        ClassSymbol = classSymbol;
        DocumentUri = documentUri;
        DocumentVersion = documentVersion;
    }

    /// <summary>
    /// Gets all accesses for a specific field.
    /// </summary>
    /// <param name="field">The field symbol to query.</param>
    /// <returns>All accesses to the field, or an empty array if the field has no accesses.</returns>
    public ImmutableArray<FieldAccessSummary> GetFieldAccesses(IFieldSymbol field)
    {
        return FieldAccesses.GetValueOrDefault(field, ImmutableArray<FieldAccessSummary>.Empty);
    }

    /// <summary>
    /// Gets the total number of field accesses across all fields.
    /// </summary>
    public int TotalAccessCount => FieldAccesses.Values.Sum(accesses => accesses.Length);
}
