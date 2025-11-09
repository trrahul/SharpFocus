using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SharpFocus.TestWorkspace;

/// <summary>
/// Tests real-world service layer patterns including caching, state machines,
/// event handlers, dependency injection, and business logic workflows.
/// </summary>
public class ServiceLayerPatterns
{
    private readonly ICache _cache;
    private readonly ILogger _logger;
    private readonly INotificationService _notificationService;
    private OrderStateMachine? _currentOrder;
    private int _requestCount;
    private int _cacheHits;
    private int _cacheMisses;

    public ServiceLayerPatterns(ICache cache, ILogger logger, INotificationService notificationService)
    {
        _cache = cache;
        _logger = logger;
        _notificationService = notificationService;
    }

    // Pattern: Cached data access
    public CustomerData GetCustomerData(int customerId)
    {
        _requestCount++;
        var cacheKey = $"customer_{customerId}";

        if (_cache.TryGet(cacheKey, out CustomerData? cached))
        {
            _cacheHits++;
            _logger.Log($"Cache hit for customer {customerId}");
            return cached!;
        }

        _cacheMisses++;
        _logger.Log($"Cache miss for customer {customerId}");

        var data = FetchFromDatabase(customerId);
        _cache.Set(cacheKey, data, TimeSpan.FromMinutes(5));

        return data;
    }

    // Pattern: State machine with transitions
    public void ProcessOrder(int orderId, OrderAction action)
    {
        _requestCount++;
        _currentOrder ??= new OrderStateMachine(orderId);

        var previousState = _currentOrder.State;

        switch (action)
        {
            case OrderAction.Submit:
                _currentOrder.Submit();
                _logger.Log($"Order {orderId} submitted");
                _notificationService.Notify("Order submitted");
                break;

            case OrderAction.Approve:
                _currentOrder.Approve();
                _logger.Log($"Order {orderId} approved");
                _notificationService.Notify("Order approved");
                break;

            case OrderAction.Ship:
                _currentOrder.Ship();
                _logger.Log($"Order {orderId} shipped");
                _notificationService.Notify("Order shipped");
                break;

            case OrderAction.Cancel:
                _currentOrder.Cancel();
                _logger.Log($"Order {orderId} cancelled");
                _notificationService.Notify("Order cancelled");
                break;
        }

        if (_currentOrder.State != previousState)
        {
            _cache.Invalidate($"order_{orderId}");
        }
    }

    // Pattern: Event aggregation
    public void HandleCustomerEvent(CustomerEvent evt)
    {
        _requestCount++;
        _logger.Log($"Processing event: {evt.Type}");

        switch (evt.Type)
        {
            case CustomerEventType.ProfileUpdated:
                _cache.Invalidate($"customer_{evt.CustomerId}");
                _notificationService.Notify($"Customer {evt.CustomerId} profile updated");
                break;

            case CustomerEventType.OrderPlaced:
                _cacheHits++; // Track as cache interaction
                var customerData = GetCustomerData(evt.CustomerId);
                customerData.OrderCount++;
                _cache.Set($"customer_{evt.CustomerId}", customerData, TimeSpan.FromMinutes(5));
                break;

            case CustomerEventType.PaymentReceived:
                _logger.Log($"Payment received for customer {evt.CustomerId}");
                _notificationService.Notify("Payment received");
                break;
        }
    }

    // Pattern: Retry with exponential backoff
    public void RetryableOperation(string operation)
    {
        var maxRetries = 3;
        var retryCount = 0;
        var delay = 100;

        while (retryCount < maxRetries)
        {
            _requestCount++;

            try
            {
                ExecuteOperation(operation);
                _logger.Log($"Operation {operation} succeeded on attempt {retryCount + 1}");
                return;
            }
            catch (TransientException)
            {
                retryCount++;
                _cacheMisses++; // Track failure

                if (retryCount >= maxRetries)
                {
                    _logger.Log($"Operation {operation} failed after {maxRetries} attempts");
                    throw;
                }

                _logger.Log($"Retry {retryCount} for {operation} after {delay}ms");
                Thread.Sleep(delay);
                delay *= 2; // Exponential backoff
            }
        }
    }

    // Pattern: Circuit breaker
    private CircuitBreakerState _circuitState = CircuitBreakerState.Closed;
    private int _consecutiveFailures;
    private DateTime _circuitOpenedAt;

    public void CircuitBreakerOperation()
    {
        _requestCount++;

        if (_circuitState == CircuitBreakerState.Open)
        {
            if ((DateTime.UtcNow - _circuitOpenedAt).TotalSeconds > 30)
            {
                _circuitState = CircuitBreakerState.HalfOpen;
                _logger.Log("Circuit breaker entering half-open state");
            }
            else
            {
                _cacheMisses++;
                throw new CircuitBreakerOpenException("Circuit breaker is open");
            }
        }

        try
        {
            ExecuteOperation("CircuitBreaker");

            if (_circuitState == CircuitBreakerState.HalfOpen)
            {
                _circuitState = CircuitBreakerState.Closed;
                _consecutiveFailures = 0;
                _logger.Log("Circuit breaker closed");
            }

            _cacheHits++;
        }
        catch (Exception)
        {
            _consecutiveFailures++;
            _cacheMisses++;

            if (_consecutiveFailures >= 5)
            {
                _circuitState = CircuitBreakerState.Open;
                _circuitOpenedAt = DateTime.UtcNow;
                _logger.Log("Circuit breaker opened due to failures");
            }

            throw;
        }
    }

    // Pattern: Batch processing with progress tracking
    public void ProcessBatch(List<int> items)
    {
        var totalItems = items.Count;
        var processedItems = 0;
        var failedItems = 0;

        foreach (var item in items)
        {
            _requestCount++;

            try
            {
                ProcessItem(item);
                processedItems++;
                _cacheHits++;

                if (processedItems % 10 == 0)
                {
                    _logger.Log($"Progress: {processedItems}/{totalItems}");
                    _notificationService.Notify($"Processed {processedItems} items");
                }
            }
            catch (Exception ex)
            {
                failedItems++;
                _cacheMisses++;
                _logger.Log($"Failed to process item {item}: {ex.Message}");
            }
        }

        _logger.Log($"Batch complete: {processedItems} succeeded, {failedItems} failed");
    }

    // Pattern: Lazy loading with double-check locking
    private volatile CustomerData? _singletonData;
    private readonly object _lockObject = new();

    public CustomerData GetSingletonData()
    {
        if (_singletonData != null)
        {
            _cacheHits++;
            return _singletonData;
        }

        lock (_lockObject)
        {
            if (_singletonData != null)
            {
                _cacheHits++;
                return _singletonData;
            }

            _requestCount++;
            _cacheMisses++;
            _singletonData = FetchFromDatabase(1);
            _logger.Log("Singleton data initialized");
            return _singletonData;
        }
    }

    // Helper methods
    private CustomerData FetchFromDatabase(int customerId) =>
        new CustomerData { Id = customerId, Name = $"Customer {customerId}" };

    private void ExecuteOperation(string operation)
    {
        if (_requestCount % 3 == 0)
            throw new TransientException("Simulated transient failure");
    }

    private void ProcessItem(int item) { }
}

// Supporting types
public interface ICache
{
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value, TimeSpan expiration);
    void Invalidate(string key);
}

public interface ILogger
{
    void Log(string message);
}

public interface INotificationService
{
    void Notify(string message);
}

public class CustomerData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int OrderCount { get; set; }
}

public class OrderStateMachine
{
    public int OrderId { get; }
    public OrderState State { get; private set; }

    public OrderStateMachine(int orderId)
    {
        OrderId = orderId;
        State = OrderState.Draft;
    }

    public void Submit()
    {
        if (State != OrderState.Draft)
            throw new InvalidOperationException($"Cannot submit order in state {State}");
        State = OrderState.Submitted;
    }

    public void Approve()
    {
        if (State != OrderState.Submitted)
            throw new InvalidOperationException($"Cannot approve order in state {State}");
        State = OrderState.Approved;
    }

    public void Ship()
    {
        if (State != OrderState.Approved)
            throw new InvalidOperationException($"Cannot ship order in state {State}");
        State = OrderState.Shipped;
    }

    public void Cancel()
    {
        if (State == OrderState.Shipped)
            throw new InvalidOperationException("Cannot cancel shipped order");
        State = OrderState.Cancelled;
    }
}

public enum OrderState
{
    Draft,
    Submitted,
    Approved,
    Shipped,
    Cancelled
}

public enum OrderAction
{
    Submit,
    Approve,
    Ship,
    Cancel
}

public class CustomerEvent
{
    public int CustomerId { get; set; }
    public CustomerEventType Type { get; set; }
}

public enum CustomerEventType
{
    ProfileUpdated,
    OrderPlaced,
    PaymentReceived
}

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public class TransientException : Exception
{
    public TransientException(string message) : base(message) { }
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}
