using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// The email channel's provider (<c>02</c> MSG-EMAIL-005): a dispatcher pass opens one session
/// at its first email delivery and disposes of it at its end. A host replaces the shipped SMTP
/// provider by registering its own.
/// </summary>
public interface IEmailProvider
{
    /// <summary>Opens a session through which a pass sends its email deliveries.</summary>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns>The session; it connects when its first message is sent, never here.</returns>
    ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken);
}