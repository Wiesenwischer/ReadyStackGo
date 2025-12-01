using System.Collections.Concurrent;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.ValueObjects;

namespace ReadyStackGo.Infrastructure.Stacks;

/// <summary>
/// In-memory implementation of stack cache
/// </summary>
public class InMemoryStackCache : IStackCache
{
    private readonly ConcurrentDictionary<string, StackDefinition> _stacks = new();

    public int Count => _stacks.Count;

    public IEnumerable<StackDefinition> GetAll()
    {
        return _stacks.Values.ToList();
    }

    public IEnumerable<StackDefinition> GetBySource(string sourceId)
    {
        return _stacks.Values.Where(s => s.SourceId == sourceId).ToList();
    }

    public StackDefinition? Get(string stackId)
    {
        _stacks.TryGetValue(stackId, out var stack);
        return stack;
    }

    public void Set(StackDefinition stack)
    {
        _stacks[stack.Id] = stack;
    }

    public void SetMany(IEnumerable<StackDefinition> stacks)
    {
        foreach (var stack in stacks)
        {
            _stacks[stack.Id] = stack;
        }
    }

    public void Remove(string stackId)
    {
        _stacks.TryRemove(stackId, out _);
    }

    public void RemoveBySource(string sourceId)
    {
        var keysToRemove = _stacks
            .Where(kvp => kvp.Value.SourceId == sourceId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _stacks.TryRemove(key, out _);
        }
    }

    public void Clear()
    {
        _stacks.Clear();
    }
}
