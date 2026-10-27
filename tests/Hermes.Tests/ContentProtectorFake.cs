using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>
/// A host protector that writes its <c>Id</c>, the person and the context before the value,
/// and opens only what it wrote under the same person and context.
/// </summary>
internal sealed class ContentProtectorFake(string id) : IContentProtector
{
    private int opens;

    public string Id => id;

    /// <summary>Whether protecting throws, as a vault that cannot be reached does.</summary>
    internal bool Throws { get; init; }

    /// <summary>A step before each opening, where a test sets one.</summary>
    internal Func<Task>? BeforeOpen { get; set; }

    /// <summary>How many values it was asked to open.</summary>
    internal int Opens => opens;

    public ValueTask<byte[]> ProtectAsync(string person, ReadOnlyMemory<byte> plaintext, string context, CancellationToken cancellationToken)
    {
        if (Throws)
        {
            throw new InvalidOperationException("The vault cannot be reached.");
        }

        return ValueTask.FromResult(Head(person, context).Concat(plaintext.ToArray()).ToArray());
    }

    public async ValueTask<UnprotectResult> UnprotectAsync(string person, ReadOnlyMemory<byte> protectedValue, string context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref opens);
        if (BeforeOpen is { } before)
        {
            await before();
        }

        byte[] head = Head(person, context);
        return protectedValue.Span.StartsWith(head) ? new UnprotectResult.Opened(protectedValue[head.Length..]) : new UnprotectResult.Rejected();
    }

    private byte[] Head(string person, string context)
    {
        return Encoding.UTF8.GetBytes($"{id}|{person}|{context}|");
    }
}