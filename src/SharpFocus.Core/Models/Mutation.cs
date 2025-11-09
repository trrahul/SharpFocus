namespace SharpFocus.Core.Models;

/// <summary>
/// Represents the kind of mutation performed on a memory location.
/// </summary>
public enum MutationKind
{
    Assignment,
    RefArgument,
    OutArgument,
    Initialization,
    Increment,
    Decrement,
    CompoundAssignment
}

/// <summary>
/// Represents a mutation (modification) to a memory location in the program.
/// </summary>
/// <remarks>
/// Mutations track where and how variables, fields, and other places are modified.
/// This corresponds to Flowistry's concept of mutations in the Rust implementation.
/// </remarks>
public sealed record Mutation
{
    /// <summary>
    /// Gets the place (memory location) being mutated.
    /// </summary>
    public Place Target { get; init; }

    /// <summary>
    /// Gets the program location where the mutation occurs.
    /// </summary>
    public ProgramLocation Location { get; init; }

    /// <summary>
    /// Gets the kind of mutation being performed.
    /// </summary>
    public MutationKind Kind { get; init; }

    /// <summary>
    /// Gets whether this mutation represents a write operation.
    /// </summary>
    /// <remarks>
    /// Most mutations are writes. This property helps distinguish them from
    /// potential future mutation types that might not be pure writes.
    /// </remarks>
    public bool IsWrite => Kind switch
    {
        MutationKind.Assignment => true,
        MutationKind.RefArgument => true,
        MutationKind.OutArgument => true,
        MutationKind.Initialization => true,
        MutationKind.Increment => true,
        MutationKind.Decrement => true,
        MutationKind.CompoundAssignment => true,
        _ => false
    };

    /// <summary>
    /// Creates a new Mutation.
    /// </summary>
    /// <param name="target">The place being mutated.</param>
    /// <param name="location">The program location of the mutation.</param>
    /// <param name="kind">The kind of mutation.</param>
    /// <exception cref="ArgumentNullException">Thrown when target or location is null.</exception>
    public Mutation(Place target, ProgramLocation location, MutationKind kind)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(location);

        Target = target;
        Location = location;
        Kind = kind;
    }

    /// <summary>
    /// Returns a string representation of this mutation.
    /// </summary>
    public override string ToString()
    {
        return $"{Kind} of {Target} at {Location}";
    }
}
