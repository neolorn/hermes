using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>The scanning seam of attachments, required where they are enabled (<c>09</c> MSG-CONV-007; <c>04</c> MSG-LIB-005).</summary>
public interface IAttachmentScanner
{
    /// <summary>Scans an attachment's bytes before anyone can open it.</summary>
    /// <param name="content">The bytes, opened.</param>
    /// <param name="mediaType">Its declared media type.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see cref="ScanResult.Clean"/>, <see cref="ScanResult.Infected"/>, or <see cref="ScanResult.Failed"/> for a scan to try again.</returns>
    ValueTask<ScanResult> ScanAsync(Stream content, string mediaType, CancellationToken cancellationToken);
}