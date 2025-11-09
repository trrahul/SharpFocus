using System.Buffers.Binary;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SharpFocus.Core.Utilities;

/// <summary>
/// Provides utilities for computing stable document version identifiers from source content.
/// Versions are derived from Roslyn checksums to ensure consistency across processes.
/// </summary>
public static class DocumentVersionCalculator
{
    /// <summary>
    /// Computes a stable integer version for the provided source text.
    /// </summary>
    /// <param name="text">The source text to hash.</param>
    /// <returns>A deterministic version identifier derived from the text checksum.</returns>
    public static int Compute(SourceText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ComputeFromChecksum(text.GetChecksum());
    }

    /// <summary>
    /// Computes a stable integer version for the provided source content.
    /// </summary>
    /// <param name="content">The raw source content.</param>
    /// <returns>A deterministic version identifier derived from the content checksum.</returns>
    public static int Compute(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var text = SourceText.From(content);
        return Compute(text);
    }

    private static int ComputeFromChecksum(ImmutableArray<byte> checksum)
    {
        if (checksum.IsDefaultOrEmpty)
        {
            return 0;
        }

        var span = checksum.AsSpan();
        if (span.Length >= sizeof(int))
        {
            return BinaryPrimitives.ReadInt32LittleEndian(span);
        }

        var hash = 0;
        for (var i = 0; i < span.Length; i++)
        {
            hash = (hash << 5) - hash + span[i];
        }

        return hash;
    }
}
