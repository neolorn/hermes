using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Testing;

/// <summary>
/// An attachment store that keeps each object it is given in memory, under its key, as the
/// library wrote it (<c>04</c> MSG-LIB-008; <c>09</c> MSG-CONV-007). It is safe to use from
/// several threads at once.
/// </summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    private readonly ConcurrentDictionary<string, byte[]> objects = new(StringComparer.Ordinal);

    /// <summary>How many objects it holds.</summary>
    public int Count => objects.Count;

    /// <inheritdoc />
    public async ValueTask PutAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        using MemoryStream copy = new();
        await content.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        objects[key] = copy.ToArray();
    }

    /// <inheritdoc />
    public ValueTask<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Stream?>(objects.TryGetValue(key, out byte[]? stored) ? new MemoryStream(stored, writable: false) : null);
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        objects.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}