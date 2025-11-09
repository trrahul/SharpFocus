using Microsoft.CodeAnalysis;

namespace SharpFocus.Core.Models;

/// <summary>
/// Represents the dataflow domain that tracks dependencies between places and program locations.
/// This is a mutable lattice structure used during fixpoint iteration in dataflow analysis.
/// Maps each Place to the set of ProgramLocations where it depends on values.
/// </summary>
public class FlowDomain
{
    // Use SymbolEqualityComparer for Place keys since Place uses symbol-based equality
    private readonly Dictionary<Place, HashSet<ProgramLocation>> _dependencies =
        new(PlaceComparer.Instance);

    /// <summary>
    /// Gets whether this domain has no tracked dependencies.
    /// </summary>
    public bool IsEmpty => _dependencies.Count == 0;

    /// <summary>
    /// Gets all places tracked in this domain.
    /// </summary>
    public IEnumerable<Place> Places => _dependencies.Keys;

    /// <summary>
    /// Adds a dependency: the given place depends on the value at the given location.
    /// </summary>
    /// <param name="place">The place that has a dependency.</param>
    /// <param name="location">The program location where the dependency originates.</param>
    public void AddDependency(Place place, ProgramLocation location)
    {
        if (!_dependencies.TryGetValue(place, out var locations))
        {
            locations = new HashSet<ProgramLocation>();
            _dependencies[place] = locations;
        }

        locations.Add(location);
    }

    /// <summary>
    /// Creates a deep copy of this domain.
    /// </summary>
    /// <returns>A new <see cref="FlowDomain"/> containing the same dependencies.</returns>
    public FlowDomain Clone()
    {
        var clone = new FlowDomain();

        foreach (var (place, locations) in _dependencies)
        {
            clone._dependencies[place] = new HashSet<ProgramLocation>(locations);
        }

        return clone;
    }

    /// <summary>
    /// Replaces the dependencies tracked for a specific place.
    /// </summary>
    /// <param name="place">The place whose dependencies should be replaced.</param>
    /// <param name="locations">The new set of dependency locations. An empty set removes the place.</param>
    public void SetDependencies(Place place, IEnumerable<ProgramLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(locations);

        var locationSet = new HashSet<ProgramLocation>(locations);

        if (locationSet.Count == 0)
        {
            _dependencies.Remove(place);
        }
        else
        {
            _dependencies[place] = locationSet;
        }
    }

    /// <summary>
    /// Gets all dependencies for a given place.
    /// Returns empty set if place has no tracked dependencies.
    /// </summary>
    /// <param name="place">The place to get dependencies for.</param>
    /// <returns>Set of program locations this place depends on.</returns>
    public IReadOnlySet<ProgramLocation> GetDependencies(Place place)
    {
        return _dependencies.TryGetValue(place, out var locations)
            ? locations
            : new HashSet<ProgramLocation>();
    }

    /// <summary>
    /// Checks if a place has any tracked dependencies.
    /// </summary>
    /// <param name="place">The place to check.</param>
    /// <returns>True if the place has dependencies, false otherwise.</returns>
    public bool HasDependencies(Place place)
    {
        return _dependencies.ContainsKey(place) && _dependencies[place].Count > 0;
    }

    /// <summary>
    /// Joins (merges) this domain with another, creating a new domain.
    /// The join operation unions the dependencies for each place (lattice meet).
    /// This is immutable - does not modify either original domain.
    /// </summary>
    /// <param name="other">The domain to join with.</param>
    /// <returns>A new domain containing the union of both domains' dependencies.</returns>
    public FlowDomain Join(FlowDomain other)
    {
        var result = new FlowDomain();

        // Copy all dependencies from this domain
        foreach (var (place, locations) in _dependencies)
        {
            foreach (var location in locations)
            {
                result.AddDependency(place, location);
            }
        }

        // Add all dependencies from the other domain
        foreach (var (place, locations) in other._dependencies)
        {
            foreach (var location in locations)
            {
                result.AddDependency(place, location);
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether this domain represents the same dependencies as another.
    /// </summary>
    /// <param name="other">The domain to compare with.</param>
    /// <returns>True when both domains contain identical dependency sets.</returns>
    public bool EquivalentTo(FlowDomain other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(this, other))
            return true;

        if (_dependencies.Count != other._dependencies.Count)
            return false;

        foreach (var (place, locations) in _dependencies)
        {
            if (!other._dependencies.TryGetValue(place, out var otherLocations))
                return false;

            if (!locations.SetEquals(otherLocations))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Clears all tracked dependencies from this domain.
    /// </summary>
    public void Clear()
    {
        _dependencies.Clear();
    }

    /// <summary>
    /// Custom equality comparer for Place that uses SymbolEqualityComparer.
    /// This ensures proper dictionary lookups when Place uses symbol-based equality.
    /// </summary>
    private sealed class PlaceComparer : IEqualityComparer<Place>
    {
        public static readonly PlaceComparer Instance = new();

        public bool Equals(Place? x, Place? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Equals(y);
        }

        public int GetHashCode(Place obj)
        {
            return obj.GetHashCode();
        }
    }
}
