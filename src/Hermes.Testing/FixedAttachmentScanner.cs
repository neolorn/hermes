using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Testing;

/// <summary>
/// An attachment scanner that reads each file to its end and answers the same result for
/// every one (<c>04</c> MSG-LIB-008; <c>09</c> MSG-CONV-007).
/// </summary>
/// <param name="answer">What it answers for every file, <see cref="ScanResult.Clean"/> where none is given.</param>
public sealed class FixedAttachmentScanner(ScanResult answer = ScanResult.Clean) : IAttachmentScanner
{
    /// <summary>What it answers for every file.</summary>
    public ScanResult Answer { get; } = answer;

    /// <inheritdoc />
    public async ValueTask<ScanResult> ScanAsync(Stream content, string mediaType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        await content.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
        return Answer;
    }
}