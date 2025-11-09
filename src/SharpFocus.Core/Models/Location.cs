using Microsoft.CodeAnalysis.FlowAnalysis;

namespace SharpFocus.Core.Models;

/// <summary>
/// Represents a specific location (statement) in the control flow graph.
/// A ProgramLocation identifies a particular operation within a basic block.
/// </summary>
/// <remarks>
/// Locations are used to track where data dependencies occur in the program.
/// Named ProgramLocation to avoid conflict with Microsoft.CodeAnalysis.Location.
/// </remarks>
public sealed record ProgramLocation : IComparable<ProgramLocation>
{
    /// <summary>
    /// Gets the basic block containing this location.
    /// </summary>
    public BasicBlock Block { get; init; }

    /// <summary>
    /// Gets the index of the operation within the block.
    /// </summary>
    public int OperationIndex { get; init; }

    /// <summary>
    /// Creates a new ProgramLocation at the specified block and operation index.
    /// </summary>
    /// <param name="block">The basic block.</param>
    /// <param name="operationIndex">The index of the operation within the block.</param>
    /// <exception cref="ArgumentNullException">Thrown when block is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when operationIndex is negative.</exception>
    public ProgramLocation(BasicBlock block, int operationIndex)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (operationIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(operationIndex), "Operation index cannot be negative.");

        Block = block;
        OperationIndex = operationIndex;
    }

    /// <summary>
    /// Compares this location to another based on block ordinal and operation index.
    /// </summary>
    /// <param name="other">The location to compare to.</param>
    /// <returns>
    /// Less than 0 if this location comes before the other;
    /// 0 if they are equal;
    /// Greater than 0 if this location comes after the other.
    /// </returns>
    public int CompareTo(ProgramLocation? other)
    {
        if (other is null)
            return 1;

        // First compare by block ordinal
        var blockComparison = Block.Ordinal.CompareTo(other.Block.Ordinal);
        if (blockComparison != 0)
            return blockComparison;

        // Then compare by operation index within the block
        return OperationIndex.CompareTo(other.OperationIndex);
    }

    /// <summary>
    /// Returns a string representation of this location.
    /// </summary>
    public override string ToString()
    {
        return $"Block {Block.Ordinal}, Operation {OperationIndex}";
    }

    #region Equality

    public bool Equals(ProgramLocation? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Block.Ordinal == other.Block.Ordinal &&
               OperationIndex == other.OperationIndex;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Block.Ordinal, OperationIndex);
    }

    #endregion

    #region Comparison Operators

    public static bool operator <(ProgramLocation left, ProgramLocation right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(ProgramLocation left, ProgramLocation right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(ProgramLocation left, ProgramLocation right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(ProgramLocation left, ProgramLocation right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
