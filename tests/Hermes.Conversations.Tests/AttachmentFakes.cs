using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Conversations.Tests;

/// <summary>An attachment store held in memory, each object under its key as the library wrote it; its deletions fail while <see cref="Away"/>.</summary>
internal sealed class StoreFake : IAttachmentStore
{
    internal ConcurrentDictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether a deletion fails, as a store out of reach does.</summary>
    internal bool Away { get; set; }

    public async ValueTask PutAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        using MemoryStream copy = new();
        await content.CopyToAsync(copy, cancellationToken);
        Objects[key] = copy.ToArray();
    }

    public ValueTask<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<Stream?>(Objects.TryGetValue(key, out byte[]? stored) ? new MemoryStream(stored, writable: false) : null);
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (Away)
        {
            throw new IOException("The store is out of reach.");
        }

        Objects.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}

/// <summary>A scanner answering <see cref="Answer"/>, keeping what it read of each content.</summary>
internal sealed class ScannerFake : IAttachmentScanner
{
    private readonly Lock gate = new();

    private readonly List<byte[]> scanned = [];

    internal ScanResult Answer { get; set; } = ScanResult.Clean;

    /// <summary>Each content scanned, read whole, in order.</summary>
    internal IReadOnlyList<byte[]> Scanned
    {
        get
        {
            lock (gate)
            {
                return [.. scanned];
            }
        }
    }

    public async ValueTask<ScanResult> ScanAsync(Stream content, string mediaType, CancellationToken cancellationToken)
    {
        using MemoryStream copy = new();
        await content.CopyToAsync(copy, cancellationToken);
        lock (gate)
        {
            scanned.Add(copy.ToArray());
        }

        return Answer;
    }
}