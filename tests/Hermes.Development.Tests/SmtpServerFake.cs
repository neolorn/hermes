using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Development.Tests;

/// <summary>How the scripted server takes TLS.</summary>
public enum SmtpTls
{
    /// <summary>No TLS at all.</summary>
    None,

    /// <summary>TLS from the connection's start.</summary>
    Implicit,

    /// <summary>TLS by <c>STARTTLS</c>.</summary>
    StartTls,
}

/// <summary>A message the scripted server took: its envelope and its data, as sent.</summary>
internal sealed record SmtpReceived(string MailFrom, IReadOnlyList<string> Recipients, byte[] Data);

/// <summary>
/// A scripted SMTP submission server in the test process (<c>07</c>), on the loopback
/// addresses of <c>localhost</c>: it answers each command as a server that takes everything
/// would, save the replies a test sets in <see cref="Replies"/>, and records what it was
/// sent. Its certificate is a test authority's.
/// </summary>
internal sealed class SmtpServerFake : IAsyncDisposable
{
    private readonly TcpListener v4;
    private readonly TcpListener? v6;
    private readonly SmtpTls tls;
    private readonly SslStreamCertificateContext? certificate;
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentBag<Task> sessions = [];
    private int connections;
    private int closed;

    /// <summary>Listens on the IPv4 and, where the host has it, the IPv6 loopback address at one port, as <c>localhost</c> names both.</summary>
    private SmtpServerFake(SmtpTls tls, SslStreamCertificateContext? certificate)
    {
        this.tls = tls;
        this.certificate = certificate;
        for (int attempt = 0; ; attempt++)
        {
            v4 = new TcpListener(IPAddress.Loopback, 0);
            v4.Start();
            Port = ((IPEndPoint)v4.LocalEndpoint).Port;
            v6 = new TcpListener(IPAddress.IPv6Loopback, Port);
            try
            {
                v6.Start();
                return;
            }
            catch (SocketException failure) when (failure.SocketErrorCode is SocketError.AddressFamilyNotSupported or SocketError.AddressNotAvailable)
            {
                v6.Dispose();
                v6 = null;
                return;
            }
            catch (SocketException) when (attempt < 10)
            {
                v6.Dispose();
                v4.Dispose();
            }
        }
    }

    /// <summary>The port it listens on.</summary>
    internal int Port { get; }

    /// <summary>
    /// The replies that replace the server's own, by the command they answer:
    /// <c>greeting</c>, <c>EHLO</c>, <c>STARTTLS</c>, <c>AUTH</c>, <c>MAIL</c>, <c>RCPT</c>,
    /// <c>DATA</c>, and <c>END</c> for the end of the data.
    /// </summary>
    internal ConcurrentDictionary<string, string> Replies { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether its <c>EHLO</c> offers <c>AUTH PLAIN</c> once TLS is up.</summary>
    internal bool OffersAuthentication { get; set; } = true;

    /// <summary>Whether its <c>EHLO</c> offers <c>STARTTLS</c>, where it takes TLS by it.</summary>
    internal bool OffersStartTls { get; set; } = true;

    /// <summary>The messages a connection takes before the server closes it, or none.</summary>
    internal int? CloseAfter { get; set; }

    /// <summary>The reply the server sends, unasked, as it closes a connection after <see cref="CloseAfter"/>, or none.</summary>
    internal string? ClosingReply { get; set; }

    /// <summary>Whether the server never answers the end of the data.</summary>
    internal bool HangsAtEnd { get; set; }

    /// <summary>The connections opened to it.</summary>
    internal int Connections => Volatile.Read(ref connections);

    /// <summary>The connections that have ended.</summary>
    internal int Closed => Volatile.Read(ref closed);

    /// <summary>The commands received, by their verb, in order.</summary>
    internal ConcurrentQueue<string> Commands { get; } = new();

    /// <summary>The user names and passwords given to <c>AUTH PLAIN</c>, in order.</summary>
    internal ConcurrentQueue<(string User, string Password)> Authentications { get; } = new();

    /// <summary>The messages taken, in order.</summary>
    internal ConcurrentQueue<SmtpReceived> Messages { get; } = new();

    /// <summary>A server taking TLS as <paramref name="tls"/> gives, presenting <paramref name="certificate"/> with <paramref name="chain"/>.</summary>
    internal static SmtpServerFake Start(SmtpTls tls, X509Certificate2? certificate, params X509Certificate2[] chain)
    {
        SslStreamCertificateContext? context = certificate is null ? null : SslStreamCertificateContext.Create(certificate, [.. chain], offline: true);
        SmtpServerFake server = new(tls, context);
        server.sessions.Add(server.AcceptAsync(server.v4));
        if (server.v6 is not null)
        {
            server.sessions.Add(server.AcceptAsync(server.v6));
        }

        return server;
    }

    /// <summary>A port no server listens on.</summary>
    internal static int UnusedPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Waits until <paramref name="count"/> connections have ended.</summary>
    internal async Task WaitClosedAsync(int count)
    {
        using CancellationTokenSource limit = new(TimeSpan.FromSeconds(10));
        while (Closed < count)
        {
            await Task.Delay(10, limit.Token).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        v4.Dispose();
        v6?.Dispose();

        foreach (Task session in sessions)
        {
            try
            {
                await session.ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
                // A session ends as the server stops.
            }
        }

        stopping.Dispose();
    }

    private static async Task WriteAsync(Stream stream, string reply, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes(reply + "\r\n"), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One line, without its end, or none at the end of the stream.</summary>
    private static async Task<byte[]?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        List<byte> line = [];
        byte[] one = new byte[1];
        while (true)
        {
            if (await stream.ReadAsync(one, cancellationToken).ConfigureAwait(false) == 0)
            {
                return null;
            }

            if (one[0] == (byte)'\n')
            {
                if (line.Count > 0 && line[^1] == (byte)'\r')
                {
                    line.RemoveAt(line.Count - 1);
                }

                return [.. line];
            }

            line.Add(one[0]);
        }
    }

    private async Task AcceptAsync(TcpListener listener)
    {
        while (!stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopping.Token).ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            sessions.Add(SessionAsync(client));
        }
    }

    private async Task SessionAsync(TcpClient client)
    {
        Interlocked.Increment(ref connections);
        try
        {
            using (client)
            {
                await ConverseAsync(client.GetStream(), stopping.Token).ConfigureAwait(false);
            }
        }
        catch (Exception failure) when (failure is IOException or SocketException or OperationCanceledException or System.Security.Authentication.AuthenticationException)
        {
            // The client went away, or the server stopped.
        }
        finally
        {
            Interlocked.Increment(ref closed);
        }
    }

    private async Task<Stream> SecureAsync(Stream stream, CancellationToken cancellationToken)
    {
        SslStream secured = new(stream, leaveInnerStreamOpen: false);
        await secured.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificateContext = certificate }, cancellationToken).ConfigureAwait(false);
        return secured;
    }

    private string Reply(string command, string reply)
    {
        return Replies.TryGetValue(command, out string? set) ? set : reply;
    }

    private async Task ConverseAsync(Stream network, CancellationToken cancellationToken)
    {
        Stream stream = network;
        bool secured = false;
        if (tls == SmtpTls.Implicit)
        {
            stream = await SecureAsync(stream, cancellationToken).ConfigureAwait(false);
            secured = true;
        }

        await using (stream.ConfigureAwait(false))
        {
            await WriteAsync(stream, Reply("greeting", "220 localhost ESMTP"), cancellationToken).ConfigureAwait(false);
            int taken = 0;
            string? from = null;
            List<string> recipients = [];
            while (await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false) is { } bytes)
            {
                string line = Encoding.ASCII.GetString(bytes);
                string verb = line.Split(' ', 2)[0].ToUpperInvariant();
                Commands.Enqueue(verb);
                switch (verb)
                {
                    case "EHLO" or "HELO":
                        List<string> offered = ["localhost"];
                        if (tls == SmtpTls.StartTls && !secured && OffersStartTls)
                        {
                            offered.Add("STARTTLS");
                        }

                        if (OffersAuthentication && secured)
                        {
                            offered.Add("AUTH PLAIN");
                        }

                        offered.Add("8BITMIME");
                        string ehlo = string.Join("\r\n", offered.Select((item, index) => (index == offered.Count - 1 ? "250 " : "250-") + item));
                        await WriteAsync(stream, Reply("EHLO", ehlo), cancellationToken).ConfigureAwait(false);
                        break;
                    case "STARTTLS":
                        if (Replies.TryGetValue("STARTTLS", out string? refusal))
                        {
                            await WriteAsync(stream, refusal, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await WriteAsync(stream, "220 2.0.0 Ready to start TLS", cancellationToken).ConfigureAwait(false);
                        stream = await SecureAsync(stream, cancellationToken).ConfigureAwait(false);
                        secured = true;
                        break;
                    case "AUTH":
                        string[] parts = line.Split(' ');
                        string initial = parts.Length > 2 ? parts[2] : string.Empty;
                        if (initial.Length == 0)
                        {
                            await WriteAsync(stream, "334 ", cancellationToken).ConfigureAwait(false);
                            initial = Encoding.ASCII.GetString(await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false) ?? []);
                        }

                        string[] account = Encoding.UTF8.GetString(Convert.FromBase64String(initial)).Split('\0');
                        Authentications.Enqueue((account[1], account[2]));
                        await WriteAsync(stream, Reply("AUTH", "235 2.7.0 Authentication successful"), cancellationToken).ConfigureAwait(false);
                        break;
                    case "MAIL":
                        from = line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..];
                        await WriteAsync(stream, Reply("MAIL", "250 2.1.0 Ok"), cancellationToken).ConfigureAwait(false);
                        break;
                    case "RCPT":
                        recipients.Add(line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..]);
                        await WriteAsync(stream, Reply("RCPT", "250 2.1.5 Ok"), cancellationToken).ConfigureAwait(false);
                        break;
                    case "DATA":
                        if (Replies.TryGetValue("DATA", out string? data))
                        {
                            await WriteAsync(stream, data, cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        await WriteAsync(stream, "354 End data with <CR><LF>.<CR><LF>", cancellationToken).ConfigureAwait(false);
                        byte[] message = await ReadDataAsync(stream, cancellationToken).ConfigureAwait(false);
                        if (HangsAtEnd)
                        {
                            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                        }

                        string end = Reply("END", "250 2.0.0 Ok: queued");
                        if (end.StartsWith("250", StringComparison.Ordinal))
                        {
                            Messages.Enqueue(new SmtpReceived(from ?? string.Empty, [.. recipients], message));
                            taken++;
                        }

                        from = null;
                        recipients.Clear();
                        await WriteAsync(stream, end, cancellationToken).ConfigureAwait(false);
                        if (taken == CloseAfter)
                        {
                            if (ClosingReply is not null)
                            {
                                await WriteAsync(stream, ClosingReply, cancellationToken).ConfigureAwait(false);
                            }

                            return;
                        }

                        break;
                    case "RSET":
                        from = null;
                        recipients.Clear();
                        await WriteAsync(stream, "250 2.0.0 Ok", cancellationToken).ConfigureAwait(false);
                        break;
                    case "NOOP":
                        await WriteAsync(stream, "250 2.0.0 Ok", cancellationToken).ConfigureAwait(false);
                        break;
                    case "QUIT":
                        await WriteAsync(stream, "221 2.0.0 Bye", cancellationToken).ConfigureAwait(false);
                        return;
                    default:
                        await WriteAsync(stream, "502 5.5.2 Command not recognised", cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }
    }

    /// <summary>The data up to its ending line, its leading dots taken off again.</summary>
    private static async Task<byte[]> ReadDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        using MemoryStream data = new();
        while (await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line is [(byte)'.'])
            {
                break;
            }

            ReadOnlySpan<byte> content = line is [(byte)'.', ..] ? line.AsSpan(1) : line;
            data.Write(content);
            data.Write("\r\n"u8);
        }

        return data.ToArray();
    }
}