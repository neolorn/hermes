using System;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;

namespace Hermes.Development;

/// <summary>
/// The inbox's email provider (<c>02</c> MSG-DEV-001): it holds no connection, so its one
/// session, itself, keeps every message it is handed, and disposing it ends nothing.
/// </summary>
internal sealed class InboxEmailProvider(InboxCapture capture) : IEmailProvider, IEmailSession
{
    public ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IEmailSession>(this);
    }

    public ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        return capture.EmailAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The inbox's SMS provider (<c>02</c> MSG-DEV-001): it keeps every message, applies
/// MSG-SMS-002's sender rule and narrows it no further, reads no balance, so the floor and the
/// balance alerts are off, and reads no report, since none ever comes.
/// </summary>
internal sealed class InboxSmsProvider(InboxCapture capture) : ISmsProvider
{
    public ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        return capture.SmsAsync(message, cancellationToken);
    }

    public bool IsValidSender(string sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return SenderRule.IsSender(sender);
    }

    public ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<BalanceReading>(new BalanceReading.NotSupported());
    }

    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new SmsReportReading { Reads = false, Response = new SmsReportResponse { Status = 200, Body = ReadOnlyMemory<byte>.Empty } });
    }
}

/// <summary>The inbox's Web Push provider: it keeps every request, one per subscription (<c>02</c> MSG-DEV-001).</summary>
internal sealed class InboxPushProvider(InboxCapture capture) : IPushProvider
{
    public PushPlatform Platform => PushPlatform.WebPush;

    public ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken)
    {
        return capture.PushAsync(request, cancellationToken);
    }
}