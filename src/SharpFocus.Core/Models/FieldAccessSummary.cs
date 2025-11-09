using Microsoft.CodeAnalysis;

namespace SharpFocus.Core.Models;

/// <summary>
/// Represents a single field access (read or write) within a method.
/// Used for tracking cross-method field usage patterns.
/// </summary>
public sealed record FieldAccessSummary
{
    /// <summary>
    /// Gets the field being accessed.
    /// </summary>
    public IFieldSymbol Field { get; init; }

    /// <summary>
    /// Gets the type of access (read, write, or both).
    /// </summary>
    public AccessType Type { get; init; }

    /// <summary>
    /// Gets the location of the field access in the source code.
    /// </summary>
    public Location Location { get; init; }

    /// <summary>
    /// Gets the method containing this field access.
    /// </summary>
    public IMethodSymbol ContainingMethod { get; init; }

    /// <summary>
    /// Gets the operation representing this field access.
    /// </summary>
    public IOperation Operation { get; init; }

    /// <summary>
    /// Gets a value indicating whether this access originates from a field initializer.
    /// </summary>
    public bool IsFieldInitializer { get; init; }

    /// <summary>
    /// Creates a new field access summary.
    /// </summary>
    /// <param name="field">The field being accessed.</param>
    /// <param name="type">The type of access.</param>
    /// <param name="location">The source location.</param>
    /// <param name="containingMethod">The method containing the access.</param>
    /// <param name="operation">The operation representing the access.</param>
    /// <param name="isFieldInitializer">Indicates whether the access originated from a field initializer.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public FieldAccessSummary(
        IFieldSymbol field,
        AccessType type,
        Location location,
        IMethodSymbol containingMethod,
        IOperation operation,
        bool isFieldInitializer = false)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(containingMethod);
        ArgumentNullException.ThrowIfNull(operation);

        Field = field;
        Type = type;
        Location = location;
        ContainingMethod = containingMethod;
        Operation = operation;
        IsFieldInitializer = isFieldInitializer;
    }
}

/// <summary>
/// Describes how a field is accessed in an operation.
/// </summary>
public enum AccessType
{
    /// <summary>
    /// Field value is read (e.g., var x = _field;).
    /// </summary>
    Read,

    /// <summary>
    /// Field is assigned a new value (e.g., _field = 5;).
    /// </summary>
    Write,

    /// <summary>
    /// Field is both read and written in the same operation (e.g., _field++;).
    /// </summary>
    ReadWrite
}
