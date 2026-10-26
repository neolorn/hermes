using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>One pass's session with the email provider (<c>02</c> MSG-EMAIL-005).</summary>
public interface IEmailSession : IAsyncDisposable
{
    /// <summary>Submits <paramref name="message"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the submission.</param>
    /// <returns>The provider's answer, classed as <c>02</c> MSG-EMAIL-004 gives; never text the provider answered.</returns>
    ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}