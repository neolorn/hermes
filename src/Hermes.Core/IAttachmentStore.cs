using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// The storage seam of attachments, required where they are enabled (<c>09</c>
/// MSG-CONV-007; <c>04</c> MSG-LIB-005). It holds only protected bytes, under keys the library
/// draws, 128 random bits in base64url that name nothing.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>Stores <paramref name="content"/> under <paramref name="key"/>, reading it to its end.</summary>
    /// <param name="key">The key.</param>
    /// <param name="content">The bytes, already protected.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes once the bytes are stored.</returns>
    ValueTask PutAsync(string key, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens what <paramref name="key"/> holds, the caller disposing it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The bytes, or none where the key holds nothing.</returns>
    ValueTask<Stream?> OpenAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes what <paramref name="key"/> holds; deleting a key that holds nothing succeeds.</summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes once the bytes are deleted.</returns>
    ValueTask DeleteAsync(string key, CancellationToken cancellationToken);
}