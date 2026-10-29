using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Logging;

namespace Hermes.Development.Tests;

/// <summary>A host's own email provider, which counts the messages it is handed and sends none.</summary>
internal sealed class EmailProviderFake : IEmailProvider, IEmailSession
{
    private int calls;

    internal int Calls => calls;

    public ValueTask<IEmailSession> OpenAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IEmailSession>(this);
    }

    public ValueTask<Attempt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>A host's own SMS provider, which counts the messages it is handed and sends none.</summary>
internal sealed class SmsProviderFake : ISmsProvider
{
    private int calls;

    internal int Calls => calls;

    public ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
    }

    public bool IsValidSender(string sender)
    {
        return true;
    }

    public ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<BalanceReading>(new BalanceReading.NotSupported());
    }

    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new SmsReportReading { Reads = false, Response = new SmsReportResponse { Status = 200, Body = ReadOnlyMemory<byte>.Empty } });
    }
}

/// <summary>A host's own push provider, which counts the requests it is handed and sends none.</summary>
internal sealed class PushProviderFake : IPushProvider
{
    private int calls;

    internal int Calls => calls;

    public PushPlatform Platform => PushPlatform.WebPush;

    public ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return ValueTask.FromResult<Attempt>(new Attempt.Accepted(null));
    }
}

/// <summary>The host's own providers of the three channels, registered around the inbox's call.</summary>
internal sealed class HostProviders
{
    internal EmailProviderFake Email { get; } = new();

    internal SmsProviderFake Sms { get; } = new();

    internal PushProviderFake Push { get; } = new();

    internal int Calls => Email.Calls + Sms.Calls + Push.Calls;
}

/// <summary>A host's templates: each kind's email subject, text and HTML, its SMS text in its language, and its push title, body and address.</summary>
internal sealed class TemplateSourceFake : ITemplateSource
{
    public ValueTask<string?> ReadAsync(TemplateKey key, CancellationToken cancellationToken)
    {
        string? template = key switch
        {
            { Page: not null } => null,
            { Channel: Channel.Email, Part: "subject" } => "Your code",
            { Channel: Channel.Email, Part: "text" } => "Your code is {{ code }}. Sign in at https://bakery.example/sign-in?code={{ code }}.",
            { Channel: Channel.Email, Part: "html" } => "<html><head><title>Code</title></head><body><p>Your code is {{ code }}.</p><a href=\"https://bakery.example/sign-in?code={{ code }}\">Sign in</a> <a href=\"https://bakery.example/help\">Help</a></body></html>",
            { Channel: Channel.Sms, Part: "text", Language: "ar" } => "رمز الدخول {{ code }}",
            { Channel: Channel.Sms, Part: "text" } => "Code {{ code }}",
            { Channel: Channel.Push, Part: "title" } => "Your order",
            { Channel: Channel.Push, Part: "body" } => "Order {{ code }} is ready.",
            { Channel: Channel.Push, Part: "url" } => "https://bakery.example/orders/{{ code }}",
            _ => null,
        };
        return ValueTask.FromResult(template);
    }
}

/// <summary>The signed-in person of every request, for the push subscription group.</summary>
internal sealed class IdentityFake(string person) : IRecipientIdentity
{
    public ValueTask<string?> CurrentAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<string?>(person);
    }
}

/// <summary>Every line the host logs, by level and text.</summary>
internal sealed class LogSink : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Category, string Text)> lines = new();

    internal IReadOnlyCollection<(LogLevel Level, string Category, string Text)> Lines => lines;

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class Logger(LogSink sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            sink.lines.Enqueue((logLevel, category, formatter(state, exception)));
        }
    }
}

/// <summary>A folder of its own under the system's temporary folder, deleted with everything in it.</summary>
internal sealed class ScratchFolder : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hermes-development-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Path))
        {
            System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}