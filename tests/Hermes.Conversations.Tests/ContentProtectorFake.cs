using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Conversations.Tests;

/// <summary>
/// A host protector that writes its <c>Id</c>, the person and the context before the value,
/// and opens only what it wrote under the same person and context.
/// </summary>
internal sealed class ContentProtectorFake(string id) : IContentProtector
{
    public string Id => id;

    public ValueTask<byte[]> ProtectAsync(string person, ReadOnlyMemory<byte> plaintext, string context, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Head(person, context).Concat(plaintext.ToArray()).ToArray());
    }

    public ValueTask<UnprotectResult> UnprotectAsync(string person, ReadOnlyMemory<byte> protectedValue, string context, CancellationToken cancellationToken)
    {
        byte[] head = Head(person, context);
        return ValueTask.FromResult<UnprotectResult>(protectedValue.Span.StartsWith(head) ? new UnprotectResult.Opened(protectedValue[head.Length..]) : new UnprotectResult.Rejected());
    }

    private byte[] Head(string person, string context)
    {
        return Encoding.UTF8.GetBytes($"{id}|{person}|{context}|");
    }
}