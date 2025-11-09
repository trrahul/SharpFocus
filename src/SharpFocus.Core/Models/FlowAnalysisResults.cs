using System.Collections.Generic;
using System.Linq;

namespace SharpFocus.Core.Models;

/// <summary>
/// Immutable snapshot of flow domains tracked at each program location for a single analysis run.
/// </summary>
public sealed class FlowAnalysisResults
{
    private readonly IReadOnlyDictionary<ProgramLocation, FlowDomain> _stateByLocation;

    public FlowAnalysisResults(Dictionary<ProgramLocation, FlowDomain> stateByLocation)
    {
        ArgumentNullException.ThrowIfNull(stateByLocation);

        _stateByLocation = stateByLocation.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Clone());
    }

    /// <summary>
    /// Enumerates the locations captured in this result set.
    /// </summary>
    public IEnumerable<ProgramLocation> Locations => _stateByLocation.Keys;

    /// <summary>
    /// Gets the flow domain for the provided location.
    /// </summary>
    /// <param name="location">Program location to query.</param>
    /// <returns>A cloned flow domain representing dependencies at that point in the program.</returns>
    public FlowDomain GetState(ProgramLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return _stateByLocation.TryGetValue(location, out var state)
            ? state.Clone()
            : new FlowDomain();
    }

    /// <summary>
    /// Attempts to retrieve the flow domain for the provided location without throwing if missing.
    /// </summary>
    /// <param name="location">Program location to query.</param>
    /// <param name="state">The flow domain when present; otherwise an empty domain.</param>
    /// <returns>True when the location was present in the results.</returns>
    public bool TryGetState(ProgramLocation location, out FlowDomain state)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (_stateByLocation.TryGetValue(location, out var stored))
        {
            state = stored.Clone();
            return true;
        }

        state = new FlowDomain();
        return false;
    }
}
