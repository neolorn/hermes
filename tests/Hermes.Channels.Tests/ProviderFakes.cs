using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Channels.Tests;

/// <summary>A host's own push provider, which accepts every request.</summary>
internal sealed class PushProviderFake : IPushProvider
{
    public PushPlatform Platform => PushPlatform.WebPush;

    public ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
    }
}

/// <summary>A host's own SMS provider, which narrows the sender rule to numbers.</summary>
internal sealed class SmsProviderFake : ISmsProvider
{
    public bool IsValidSender(string sender)
    {
        return sender.All(char.IsAsciiDigit);
    }

    public ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
    }

    public ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<BalanceReading>(new BalanceReading.NotSupported());
    }

    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The fake reads no report.");
    }
}
/// <summary>A host's own email provider, whose sessions accept every message.</summary>
internal sealed class EmailProviderFake : IEmailProvider
{
    private static readonly Session Accepting = new();

    public ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IEmailSession>(Accepting);
    }

    private sealed class Session : IEmailSession
    {
        public ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}