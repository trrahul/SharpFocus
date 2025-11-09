using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpFocus.TestWorkspace;

/// <summary>
/// Tests field mutations through LINQ queries, including deferred execution,
/// materialization points, and closure captures.
/// </summary>
public class LinqFieldMutationSamples
{
    private int _queryCount;
    private int _totalProcessed;
    private readonly List<string> _results = new();
    private readonly Dictionary<string, int> _cache = new();

    // Pattern: Simple LINQ with side effects
    public IEnumerable<int> QueryWithSideEffect(int[] values)
    {
        _queryCount++;
        return values.Where(x =>
        {
            _totalProcessed++;
            return x > 0;
        });
    }

    // Pattern: Deferred execution mutations (happens at materialization)
    public void DeferredExecutionPattern()
    {
        var query = Enumerable.Range(1, 10)
            .Where(x =>
            {
                _queryCount++;  // Executed when enumerated
                return x % 2 == 0;
            })
            .Select(x =>
            {
                _totalProcessed++;  // Also deferred
                return x * 2;
            });

        // Mutations happen here, not above
        var result = query.ToList();
        _results.Add($"Processed {_totalProcessed} items");
    }

    // Pattern: Multiple enumerations cause multiple mutations
    public void MultipleEnumerationProblem(int[] values)
    {
        var query = values
            .Where(x =>
            {
                _queryCount++;  // Will be called multiple times!
                return x > 5;
            });

        // First enumeration
        var count = query.Count();

        // Second enumeration - mutations happen again!
        var list = query.ToList();

        _results.Add($"Query executed {_queryCount} times");
    }

    // Pattern: LINQ with closure capture
    public IEnumerable<string> QueryWithClosure(string[] items)
    {
        var localCounter = 0;

        var query = items.Select(item =>
        {
            localCounter++;        // Captures local
            _totalProcessed++;     // Mutates field
            _cache[item] = localCounter;
            return $"{item}_{localCounter}";
        });

        return query;
    }

    // Pattern: Complex query composition
    public void ComposedQueryWithMutations(int[] numbers)
    {
        _queryCount = 0;
        _totalProcessed = 0;

        var filtered = numbers
            .Where(x =>
            {
                _queryCount++;
                return x % 2 == 0;
            });

        var grouped = filtered
            .GroupBy(x => x / 10)
            .Select(g =>
            {
                _totalProcessed += g.Count();
                return new
                {
                    Key = g.Key,
                    Values = g.ToList(),
                    Count = g.Count()
                };
            });

        var materialized = grouped.ToList();
        _results.Add($"Grouped {materialized.Count} buckets");
    }

    // Pattern: Aggregate with accumulator mutations
    public int AggregateWithFieldMutation(int[] values)
    {
        return values.Aggregate(0, (acc, x) =>
        {
            _totalProcessed++;
            _cache[$"item_{_totalProcessed}"] = x;
            return acc + x;
        });
    }

    // Pattern: Nested LINQ with field access
    public void NestedQueryPattern(int[][] matrix)
    {
        var flattened = matrix
            .SelectMany(row =>
            {
                _queryCount++;
                return row.Where(cell =>
                {
                    _totalProcessed++;
                    return cell > _queryCount; // Field read in nested query
                });
            });

        var result = flattened.ToList();
        _results.Add($"Flattened {result.Count} cells");
    }

    // Pattern: LINQ with exception and cleanup
    public List<int> QueryWithErrorHandling(int[] values)
    {
        var processed = new List<int>();

        try
        {
            processed = values
                .Where(x =>
                {
                    _queryCount++;
                    if (x < 0)
                        throw new ArgumentException("Negative value");
                    return true;
                })
                .Select(x =>
                {
                    _totalProcessed++;
                    return x * 2;
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _results.Add($"Error after processing {_totalProcessed} items: {ex.Message}");
            throw;
        }

        return processed;
    }

    // Pattern: Let clause and intermediate results
    public void LetClausePattern(int[] values)
    {
        var query = from x in values
                    let doubled = x * 2
                    where doubled > 10
                    let incremented = (Action)(() => _totalProcessed++)
                    select new
                    {
                        Original = x,
                        Doubled = doubled,
                        SideEffect = incremented
                    };

        foreach (var item in query)
        {
            item.SideEffect();
            _results.Add($"{item.Original} -> {item.Doubled}");
        }
    }

    // Pattern: OrderBy with field mutation (stable vs unstable)
    public void OrderByWithComparison(string[] items)
    {
        _queryCount = 0;

        var ordered = items.OrderBy(x =>
        {
            _queryCount++;  // Called many times during sorting
            return x.Length;
        }).ThenBy(x =>
        {
            _totalProcessed++;
            return x;
        });

        var result = ordered.ToList();
        _results.Add($"Sort required {_queryCount} comparisons");
    }

    // Pattern: Parallel LINQ with shared state
    public void ParallelLinqPattern(int[] values)
    {
        var lockObject = new object();

        values.AsParallel()
            .WithDegreeOfParallelism(4)
            .ForAll(x =>
            {
                // Thread-safe mutation required
                lock (lockObject)
                {
                    _totalProcessed++;
                    _cache[$"parallel_{x}"] = _totalProcessed;
                }
            });

        _results.Add($"Processed {_totalProcessed} items in parallel");
    }
}
