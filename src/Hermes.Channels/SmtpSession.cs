using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;
using MimeKit.Text;

using TlsAuthenticationException = System.Security.Authentication.AuthenticationException;

namespace Hermes.Channels;

/// <summary>
/// One pass's SMTP session (<c>02</c> MSG-EMAIL-005): it connects and authenticates at its
/// first message, and again before the next message once its connection broke or the server
/// closed it, a 421 included. Each answer is classed as MSG-EMAIL-004 gives it, its reply
/// code kept as the status and none of its text. A session sends one message at a time.
/// </summary>
internal sealed class SmtpSession(IOptionsMonitor<ChannelsOptions> options, EmailSubmission account, CallTimeout timeout, TimeProvider time) : IEmailSession
{
    private SmtpClient? client;
    private Socket? socket;
    private bool disposed;

    /// <summary>Where in the session an answer came.</summary>
    private enum Step
    {
        Connect,
        Authenticate,
        Send,
    }

    public async ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(disposed, this);
        TimeSpan bound = timeout.Current;
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(bound);
        Step step = Step.Connect;
        try
        {
            if (!IsOpen())
            {
                Drop();
                SmtpClient opened = await ConnectAsync(bound, limit.Token).ConfigureAwait(false);
                step = Step.Authenticate;
                if (!opened.Capabilities.HasFlag(SmtpCapabilities.Authentication))
                {
                    Drop();
                    return new Attempt.Transient(AttemptReason.AuthFailed, null, null);
                }

                await opened.AuthenticateAsync(account.Credential(), limit.Token).ConfigureAwait(false);
            }

            step = Step.Send;
            using MimeMessage mime = Mime(message);
            await client!.SendAsync(FormatOptions.Default, mime, Mailbox(message.From), [new MailboxAddress(string.Empty, message.ToAddress)], limit.Token).ConfigureAwait(false);
            return new Attempt.Accepted(null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Drop();
            throw;
        }
        catch (Exception failure) when (Class(failure, step) is not null)
        {
            Attempt attempt = Class(failure, step)!;
            if (failure is SmtpCommandException && step == Step.Send && client is { IsConnected: true, IsAuthenticated: true } kept)
            {
                await ResetAsync(kept, limit.Token).ConfigureAwait(false);
            }
            else
            {
                Drop();
            }

            return attempt;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (client is { IsConnected: true } open)
        {
            using CancellationTokenSource limit = new(timeout.Current);
            try
            {
                await open.DisconnectAsync(true, limit.Token).ConfigureAwait(false);
            }
            catch (Exception failure) when (IsConnectionFailure(failure))
            {
                // The session ends either way; a server that does not answer QUIT changes nothing.
            }
        }

        Drop();
    }

    /// <summary>The class of <paramref name="failure"/>, raised at <paramref name="step"/>, or none where it is not an answer.</summary>
    private static Attempt? Class(Exception failure, Step step)
    {
        return failure switch
        {
            SmtpCommandException command => Command(command, step),

            // MailKit raises a refused credential with the reply inside it.
            AuthenticationException { InnerException: SmtpCommandException command } => Command(command, Step.Authenticate),
            OperationCanceledException or TimeoutException => new Attempt.Transient(AttemptReason.Timeout, null, null),
            IOException { InnerException: SocketException { SocketErrorCode: SocketError.TimedOut } } => new Attempt.Transient(AttemptReason.Timeout, null, null),
            SmtpProtocolException => new Attempt.Transient(AttemptReason.ConnectionFailed, null, null),

            // MailKit raises a 530 without its step or code.
            ServiceNotAuthenticatedException => new Attempt.Transient(AttemptReason.AuthFailed, null, null),
            SslHandshakeException or TlsAuthenticationException => new Attempt.Transient(AttemptReason.TlsFailed, null, null),

            // A server that does not offer STARTTLS is never used without it.
            NotSupportedException when step == Step.Connect => new Attempt.Transient(AttemptReason.TlsFailed, null, null),

            // MailKit raises no mechanism it supports, and a SASL exchange that does not read, without a reply code.
            NotSupportedException or AuthenticationException or SaslException => new Attempt.Transient(AttemptReason.AuthFailed, null, null),
            ServiceNotConnectedException or SocketException or IOException => new Attempt.Transient(AttemptReason.ConnectionFailed, null, null),
            _ => null,
        };
    }

    /// <summary>The class of a reply that is not the one the step needed.</summary>
    private static Attempt Command(SmtpCommandException command, Step step)
    {
        int code = (int)command.StatusCode;
        return code switch
        {
            >= 400 and < 500 => new Attempt.Transient(AttemptReason.Temporary, code, null),
            530 => new Attempt.Transient(AttemptReason.AuthFailed, null, null),
            _ when step == Step.Authenticate => new Attempt.Transient(AttemptReason.AuthFailed, code, null),
            _ when step == Step.Send && command.ErrorCode == SmtpErrorCode.RecipientNotAccepted => new Attempt.Permanent(AttemptReason.RecipientRefused, code),
            _ when step == Step.Send && command.ErrorCode == SmtpErrorCode.MessageNotAccepted => new Attempt.Permanent(AttemptReason.MessageRefused, code),
            _ when step == Step.Send && command.ErrorCode == SmtpErrorCode.SenderNotAccepted => new Attempt.Transient(AttemptReason.SenderRefused, code, null),

            // MailKit names the sender, the recipient and the end of the data; the reply it does not name in a send is the one to the data.
            _ when step == Step.Send => new Attempt.Permanent(AttemptReason.MessageRefused, code),
            _ => new Attempt.Transient(AttemptReason.ServerRefused, code, null),
        };
    }

    private static bool IsConnectionFailure(Exception failure)
    {
        return failure is IOException or SocketException or OperationCanceledException or SmtpCommandException or SmtpProtocolException or ServiceNotConnectedException;
    }

    private static MailboxAddress Mailbox(EmailAddress address)
    {
        return new MailboxAddress(address.DisplayName ?? string.Empty, address.Address);
    }

    private static TextPart Part(TextFormat format, string text)
    {
        TextPart part = new(format);
        part.SetText(Encoding.UTF8, text);
        return part;
    }

    /// <summary>The MIME message of <paramref name="message"/> (MSG-EMAIL-003), dated now, as it is submitted.</summary>
    private MimeMessage Mime(EmailMessage message)
    {
        MimeMessage mime = new();
        mime.From.Add(Mailbox(message.From));
        if (message.ReplyTo is { } replyTo)
        {
            mime.ReplyTo.Add(Mailbox(replyTo));
        }

        mime.To.Add(new MailboxAddress(string.Empty, message.ToAddress));
        mime.Subject = message.Subject;
        mime.MessageId = message.MessageId;
        mime.Date = time.GetUtcNow();
        mime.Headers.Add("Auto-Submitted", "auto-generated");
        if (message.Unsubscribe is { } unsubscribe)
        {
            // RFC 8058: the address in angle brackets, and the one-click POST it takes.
            mime.Headers.Add("List-Unsubscribe", "<" + unsubscribe.AbsoluteUri + ">");
            mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        TextPart text = Part(TextFormat.Plain, message.Text);
        mime.Body = message.Html is { } html ? new MultipartAlternative { text, Part(TextFormat.Html, html) } : text;
        return mime;
    }

    /// <summary>
    /// Whether the connection can take the next message: open, and with nothing to read, since
    /// a server sends nothing unasked between commands save a 421 as it closes.
    /// </summary>
    private bool IsOpen()
    {
        if (client is not { IsConnected: true, IsAuthenticated: true } || socket is null)
        {
            return false;
        }

        try
        {
            return !socket.Poll(0, SelectMode.SelectRead);
        }
        catch (Exception failure) when (failure is SocketException or ObjectDisposedException)
        {
            return false;
        }
    }

    private async Task<SmtpClient> ConnectAsync(TimeSpan bound, CancellationToken cancellationToken)
    {
        EmailChannelOptions email = options.CurrentValue.Email;
        string host = email.Host ?? throw new InvalidOperationException("Email is not used: Hermes:Email:Host is not configured.");
        using X509Certificate2? authority = email.TrustedAuthority is { } pem ? EmailAuthority.Read(pem) : null;
        SmtpCertificateCheck check = new(host, authority, email.TrustedAuthorityRevocation, email.ShortLivedMaxValidity, bound, time);
        socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(host, email.PortInForce, cancellationToken).ConfigureAwait(false);
        client = new SmtpClient
        {
            CheckCertificateRevocation = false,
            ServerCertificateValidationCallback = (_, certificate, chain, _) => check.IsValid(certificate, chain),
            // The session bounds each call itself, by Dispatch:Timeout.
            Timeout = System.Threading.Timeout.Infinite,
        };
        SecureSocketOptions security = email.Security == EmailSecurity.ImplicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(socket, host, email.PortInForce, security, cancellationToken).ConfigureAwait(false);
        return client;
    }

    /// <summary>Checks the connection still answers after a refused message, so it takes the next; one that does not is dropped.</summary>
    private async Task ResetAsync(SmtpClient kept, CancellationToken cancellationToken)
    {
        try
        {
            await kept.NoOpAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (IsConnectionFailure(failure))
        {
            Drop();
        }
    }

    private void Drop()
    {
        client?.Dispose();
        client = null;
        socket?.Dispose();
        socket = null;
    }
}