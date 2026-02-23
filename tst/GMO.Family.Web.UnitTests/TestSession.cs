using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

namespace GMO.Family.Web.UnitTests;

/// <summary>
/// In-memory ISession for unit testing CurrentFamilyTreeService.
/// </summary>
internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public string Id { get; } = "test-session-id";
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _store.Remove(key);

    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
    {
        var result = _store.TryGetValue(key, out var v);
        value = v!;
        return result;
    }
}