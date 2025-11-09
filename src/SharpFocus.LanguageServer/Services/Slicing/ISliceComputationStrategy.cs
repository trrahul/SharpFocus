using SharpFocus.LanguageServer.Protocol;

namespace SharpFocus.LanguageServer.Services.Slicing;

/// <summary>
/// Defines a strategy for computing a slice in a specific direction.
/// </summary>
public interface ISliceComputationStrategy
{
    public SliceDirection Direction { get; }

    public SliceComputationResult Compute(AnalysisContext context);
}
