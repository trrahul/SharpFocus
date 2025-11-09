using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpFocus.TestWorkspace;

/// <summary>
/// Tests field mutations in async/await contexts, including state transitions,
/// concurrent modifications, and task-based patterns.
/// </summary>
public class AsyncFieldMutationSamples
{
    private int _counter;
    private string? _status;
    private readonly List<string> _logs = new();
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;

    // Pattern: Simple async field update
    public async Task IncrementCounterAsync()
    {
        await Task.Delay(100);
        _counter++;
    }

    // Pattern: Multiple async mutations with state transitions
    public async Task ProcessWithStateTransitionsAsync()
    {
        _status = "Starting";
        _logs.Add($"Started at {DateTime.UtcNow}");

        await Task.Delay(50);
        _status = "Processing";
        _counter += 10;

        await Task.Delay(50);
        _status = "Finalizing";
        _logs.Add($"Finalized at {DateTime.UtcNow}");

        _status = "Complete";
    }

    // Pattern: Concurrent modifications (testing race conditions)
    public async Task ConcurrentModificationsAsync()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(10, 50));
                Interlocked.Increment(ref _counter);
                lock (_logs)
                {
                    _logs.Add($"Thread {Environment.CurrentManagedThreadId}");
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);
        _status = $"Completed {_counter} operations";
    }

    // Pattern: Cancellation with cleanup
    public async Task CancellableOperationAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;
        _status = "Running";

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                _counter++;
                _logs.Add($"Iteration {_counter}");
                await Task.Delay(100, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _status = "Cancelled";
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // Pattern: ValueTask optimization
    public async ValueTask<int> FastPathOrSlowPathAsync()
    {
        if (_counter > 100)
        {
            // Fast path: synchronous completion
            _status = "Fast path";
            return _counter;
        }

        // Slow path: actual async work
        await Task.Delay(50);
        _counter += 5;
        _status = "Slow path";
        return _counter;
    }

    // Pattern: Async LINQ with field mutations
    public async Task ProcessBatchAsync(IEnumerable<int> items)
    {
        _status = "Batch processing";
        _counter = 0;

        await foreach (var batch in items.ToAsyncEnumerable().Buffer(10))
        {
            _counter += batch.Count;
            _logs.Add($"Processed batch of {batch.Count}");
            await Task.Delay(10);
        }

        _status = $"Processed {_counter} items";
    }

    // Pattern: Exception handling with state recovery
    public async Task OperationWithRollbackAsync()
    {
        var originalCounter = _counter;
        var originalStatus = _status;

        try
        {
            _status = "Attempting operation";
            _counter += 100;

            await Task.Delay(50);

            if (_counter > 200)
                throw new InvalidOperationException("Counter exceeded threshold");

            _status = "Success";
        }
        catch (Exception ex)
        {
            // Rollback on failure
            _counter = originalCounter;
            _status = originalStatus;
            _logs.Add($"Rolled back: {ex.Message}");
            throw;
        }
    }
}

// Extension helper for async enumerable
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }

    public static async IAsyncEnumerable<List<T>> Buffer<T>(this IAsyncEnumerable<T> source, int size)
    {
        var buffer = new List<T>(size);
        await foreach (var item in source)
        {
            buffer.Add(item);
            if (buffer.Count >= size)
            {
                yield return buffer;
                buffer = new List<T>(size);
            }
        }

        if (buffer.Count > 0)
            yield return buffer;
    }
}
