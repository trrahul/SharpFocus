using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpFocus.TestWorkspace;

/// <summary>
/// Tests field mutations in exception handling scenarios, including try-catch-finally,
/// using statements, error recovery, and cleanup patterns.
/// </summary>
public class ExceptionHandlingFieldMutations
{
    private int _operationCount;
    private int _successCount;
    private int _failureCount;
    private readonly List<string> _errorLog = new();
    private ResourceHandle? _activeResource;
    private TransactionContext? _currentTransaction;

    // Pattern: Basic try-catch with field mutations
    public void BasicTryCatch()
    {
        try
        {
            _operationCount++;
            PerformRiskyOperation();
            _successCount++;
        }
        catch (InvalidOperationException ex)
        {
            _failureCount++;
            _errorLog.Add($"InvalidOp at op {_operationCount}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _failureCount++;
            _errorLog.Add($"General error: {ex.Message}");
            throw;
        }
    }

    // Pattern: Try-finally cleanup
    public void TryFinallyCleanup()
    {
        _operationCount++;
        _activeResource = new ResourceHandle();

        try
        {
            _activeResource.IsActive = true;
            PerformWork();
            _successCount++;
        }
        finally
        {
            // Always executes, even on exception or return
            _activeResource.IsActive = false;
            _activeResource = null;
            _operationCount++;
        }
    }

    // Pattern: Using statement (IDisposable)
    public void UsingStatementPattern()
    {
        _operationCount++;

        using (var resource = new ResourceHandle())
        {
            _activeResource = resource;
            resource.IsActive = true;

            PerformWork();
            _successCount++;

            // Dispose called here automatically, which may mutate fields
        }

        _activeResource = null;
    }

    // Pattern: Nested try-catch with different mutations
    public void NestedExceptionHandling()
    {
        try
        {
            _operationCount++;

            try
            {
                _currentTransaction = new TransactionContext { IsActive = true };
                PerformDatabaseOperation();
                _currentTransaction.Commit();
                _successCount++;
            }
            catch (IOException ioEx)
            {
                _errorLog.Add($"IO Error: {ioEx.Message}");
                _currentTransaction?.Rollback();
                throw new ApplicationException("Database operation failed", ioEx);
            }
        }
        catch (ApplicationException appEx)
        {
            _failureCount++;
            _errorLog.Add($"Application error: {appEx.Message}");
            _currentTransaction = null;
        }
    }

    // Pattern: Exception filters
    public void ExceptionFiltersPattern()
    {
        try
        {
            _operationCount++;
            PerformRiskyOperation();
        }
        catch (Exception ex) when (ex.Message.Contains("timeout"))
        {
            _failureCount++;
            _errorLog.Add("Timeout occurred");
            // Filter expression evaluated before catch block
        }
        catch (Exception ex) when (LogAndReturnFalse(ex))
        {
            // This block never executes, but LogAndReturnFalse mutates fields
            _failureCount++;
        }
    }

    // Pattern: Rethrow with modification
    public void RethrowPattern()
    {
        var checkpoint = _operationCount;

        try
        {
            _operationCount++;
            PerformRiskyOperation();
        }
        catch (Exception ex)
        {
            _failureCount++;
            _errorLog.Add($"Error at checkpoint {checkpoint}");

            // Restore state before rethrowing
            _operationCount = checkpoint;
            throw;
        }
    }

    // Pattern: Multiple catch handlers with shared finally
    public void MultipleCatchWithFinally()
    {
        var operationStarted = false;

        try
        {
            _operationCount++;
            operationStarted = true;
            PerformRiskyOperation();
            _successCount++;
        }
        catch (ArgumentException)
        {
            _failureCount++;
            _errorLog.Add("Invalid arguments");
        }
        catch (InvalidOperationException)
        {
            _failureCount++;
            _errorLog.Add("Invalid state");
        }
        catch (Exception)
        {
            _failureCount++;
            _errorLog.Add("Unexpected error");
            throw;
        }
        finally
        {
            if (operationStarted)
            {
                _operationCount++;
                CleanupResources();
            }
        }
    }

    // Pattern: Exception in catch block
    public void ExceptionInCatchBlock()
    {
        try
        {
            _operationCount++;
            PerformRiskyOperation();
        }
        catch (Exception ex)
        {
            try
            {
                _failureCount++;
                // Attempt to log, which might fail
                LogToExternalSystem(ex.Message);
            }
            catch (Exception logEx)
            {
                // Logging failed, use fallback
                _errorLog.Add($"Log failure: {logEx.Message}");
                _errorLog.Add($"Original: {ex.Message}");
            }
        }
    }

    // Pattern: Transaction rollback
    public void TransactionPattern()
    {
        var savepoint = new
        {
            OperationCount = _operationCount,
            SuccessCount = _successCount,
            FailureCount = _failureCount
        };

        _currentTransaction = new TransactionContext { IsActive = true };

        try
        {
            _operationCount++;
            PerformDatabaseOperation();

            _operationCount++;
            PerformAnotherOperation();

            _currentTransaction.Commit();
            _successCount++;
        }
        catch (Exception ex)
        {
            // Rollback all field changes
            _operationCount = savepoint.OperationCount;
            _successCount = savepoint.SuccessCount;
            _failureCount = savepoint.FailureCount + 1;

            _currentTransaction?.Rollback();
            _errorLog.Add($"Transaction rolled back: {ex.Message}");
            throw;
        }
        finally
        {
            _currentTransaction = null;
        }
    }

    // Pattern: Early return in try
    public int EarlyReturnPattern()
    {
        try
        {
            _operationCount++;

            if (_operationCount > 100)
            {
                _successCount++;
                return _operationCount; // Finally still executes!
            }

            PerformRiskyOperation();
            _successCount++;
            return _operationCount;
        }
        catch (Exception ex)
        {
            _failureCount++;
            _errorLog.Add(ex.Message);
            return -1;
        }
        finally
        {
            // Always executes, even with early return
            _operationCount++;
        }
    }

    // Pattern: Async exception handling (synchronous version for testing)
    public void SimulatedAsyncExceptionHandling()
    {
        var asyncState = new { InProgress = false };

        try
        {
            _operationCount++;
            asyncState = new { InProgress = true };

            PerformRiskyOperation();

            _successCount++;
        }
        catch (OperationCanceledException)
        {
            _errorLog.Add("Operation cancelled");
        }
        catch (Exception ex)
        {
            _failureCount++;
            _errorLog.Add($"Async error: {ex.Message}");
        }
        finally
        {
            if (asyncState.InProgress)
            {
                _operationCount++;
            }
        }
    }

    // Helper methods
    private void PerformRiskyOperation()
    {
        if (_operationCount % 5 == 0)
            throw new InvalidOperationException("Simulated failure");
    }

    private void PerformWork() { }
    private void PerformDatabaseOperation() { }
    private void PerformAnotherOperation() { }
    private void CleanupResources() { }
    private void LogToExternalSystem(string message) { }

    private bool LogAndReturnFalse(Exception ex)
    {
        _errorLog.Add($"Filter logged: {ex.Message}");
        _failureCount++;
        return false;
    }
}

public class ResourceHandle : IDisposable
{
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }

    public void Dispose()
    {
        IsActive = false;
        UsageCount++;
    }
}

public class TransactionContext
{
    public bool IsActive { get; set; }
    private bool _committed;
    private bool _rolledBack;

    public void Commit()
    {
        IsActive = false;
        _committed = true;
    }

    public void Rollback()
    {
        IsActive = false;
        _rolledBack = true;
    }
}
