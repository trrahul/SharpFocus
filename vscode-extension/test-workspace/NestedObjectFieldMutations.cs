using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpFocus.TestWorkspace;

/// <summary>
/// Tests field mutations in nested object hierarchies, including property chains,
/// object graph traversals, and complex state management.
/// </summary>
public class NestedObjectFieldMutations
{
    private readonly Configuration _config = new();
    private readonly CacheManager _cache = new();
    private readonly UserSession _session = new();
    private int _operationCount;

    // Pattern: Deep property chain mutation
    public void UpdateNestedConfiguration(string value)
    {
        _operationCount++;
        _config.Database.ConnectionString = value;
        _config.Database.MaxRetries = _operationCount;
        _config.Logging.Level = _operationCount > 10 ? "Error" : "Info";
    }

    // Pattern: Object graph traversal with mutations
    public void TraverseAndUpdate()
    {
        var current = _session.CurrentUser;
        while (current != null)
        {
            _operationCount++;
            current.LastAccessed = DateTime.UtcNow;
            current.AccessCount++;
            current = current.Manager;
        }
    }

    // Pattern: Bidirectional navigation
    public void UpdateParentChild(Node node, string data)
    {
        _operationCount++;

        // Update node
        node.Data = data;
        node.LastModified = DateTime.UtcNow;

        // Update parent reference
        if (node.Parent != null)
        {
            node.Parent.ChildModificationCount++;
            _cache.InvalidateKey(node.Parent.Id);
        }

        // Update children
        foreach (var child in node.Children)
        {
            child.ParentData = data;
            _cache.InvalidateKey(child.Id);
        }
    }

    // Pattern: Nested collection mutations
    public void UpdateMultipleLevels()
    {
        foreach (var department in _session.Departments)
        {
            _operationCount++;
            department.Budget += 1000;

            foreach (var employee in department.Employees)
            {
                employee.Salary *= 1.05m;
                _cache.StoreValue(employee.Id, employee.Salary);

                foreach (var project in employee.Projects)
                {
                    project.LastReviewDate = DateTime.UtcNow;
                }
            }
        }
    }

    // Pattern: Circular reference handling
    public void HandleCircularReferences(Node start)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<Node>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node.Id))
                continue;

            _operationCount++;
            node.VisitCount++;
            _cache.StoreValue(node.Id, node.VisitCount);

            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }

            if (node.Parent != null && !visited.Contains(node.Parent.Id))
            {
                queue.Enqueue(node.Parent);
            }
        }
    }

    // Pattern: Lazy initialization with nested objects
    public void EnsureInitialized()
    {
        _session.CurrentUser ??= new User { Id = 1, Name = "Default" };
        _session.CurrentUser.Preferences ??= new UserPreferences();
        _session.CurrentUser.Preferences.Theme ??= new Theme { Name = "Default" };

        _operationCount++;
        _cache.StoreValue("user_init", _operationCount);
    }

    // Pattern: Proxy pattern with field tracking
    public void UpdateThroughProxy(string key, object value)
    {
        _operationCount++;

        var proxy = _cache.GetOrCreateProxy(key);
        proxy.RealValue = value;
        proxy.LastAccess = DateTime.UtcNow;
        proxy.AccessCount++;

        // Update backing field
        _cache.StoreValue(key, value);
    }

    // Pattern: Nested builder pattern
    public Configuration BuildConfiguration()
    {
        _operationCount++;
        return new Configuration
        {
            Database = new DatabaseConfig
            {
                ConnectionString = $"Server=localhost;Count={_operationCount}",
                MaxRetries = _operationCount,
                Timeout = TimeSpan.FromSeconds(30)
            },
            Logging = new LoggingConfig
            {
                Level = "Info",
                Targets = new List<string> { "Console", "File" }
            }
        };
    }
}

// Supporting types
public class Configuration
{
    public DatabaseConfig Database { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; } = "";
    public int MaxRetries { get; set; }
    public TimeSpan Timeout { get; set; }
}

public class LoggingConfig
{
    public string Level { get; set; } = "Info";
    public List<string> Targets { get; set; } = new();
}

public class CacheManager
{
    private readonly Dictionary<string, object> _data = new();
    private readonly Dictionary<string, CacheProxy> _proxies = new();
    private int _hitCount;
    private int _missCount;

    public void StoreValue(object key, object value)
    {
        _data[key.ToString()!] = value;
    }

    public void InvalidateKey(object key)
    {
        _data.Remove(key.ToString()!);
        _missCount++;
    }

    public CacheProxy GetOrCreateProxy(string key)
    {
        if (!_proxies.TryGetValue(key, out var proxy))
        {
            proxy = new CacheProxy { Key = key };
            _proxies[key] = proxy;
            _missCount++;
        }
        else
        {
            _hitCount++;
        }

        return proxy;
    }
}

public class CacheProxy
{
    public string Key { get; set; } = "";
    public object? RealValue { get; set; }
    public DateTime LastAccess { get; set; }
    public int AccessCount { get; set; }
}

public class UserSession
{
    public User? CurrentUser { get; set; }
    public List<Department> Departments { get; set; } = new();
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
    public User? Manager { get; set; }
    public UserPreferences? Preferences { get; set; }
}

public class UserPreferences
{
    public Theme? Theme { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}

public class Theme
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Budget { get; set; }
    public List<Employee> Employees { get; set; } = new();
}

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Salary { get; set; }
    public List<Project> Projects { get; set; } = new();
}

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime LastReviewDate { get; set; }
}

public class Node
{
    public int Id { get; set; }
    public string Data { get; set; } = "";
    public DateTime LastModified { get; set; }
    public int VisitCount { get; set; }
    public int ChildModificationCount { get; set; }
    public string ParentData { get; set; } = "";
    public Node? Parent { get; set; }
    public List<Node> Children { get; set; } = new();
}
