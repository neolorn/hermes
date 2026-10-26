using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Options;

namespace Hermes.Channels;

/// <summary>
/// The shipped email provider (<c>02</c> MSG-EMAIL-001, MSG-EMAIL-005): SMTP submission
/// through MailKit to <c>Email:Host</c>, over TLS, authenticated with <c>emailSubmission</c>,
/// the server's certificate validated by the library itself. A session connects at its
/// first message, never when it is opened.
/// </summary>
internal sealed class SmtpProvider(IOptionsMonitor<ChannelsOptions> options, EmailSubmission account, CallTimeout timeout, TimeProvider time, Action throwIfNotStarted) : IEmailProvider
{
    public ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken)
    {
        throwIfNotStarted();
        return ValueTask.FromResult<IEmailSession>(Open());
    }

    /// <summary>A new session; the pass that opened it disposes of it.</summary>
    private SmtpSession Open()
    {
        return new SmtpSession(options, account, timeout, time);
    }
}