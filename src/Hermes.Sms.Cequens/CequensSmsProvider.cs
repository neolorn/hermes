using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Sms.Cequens;

/// <summary>
/// The SMS provider for the Cequens SMS API (<c>02</c> MSG-SMS-007): one request to the
/// gateway for each send, through the named client <c>Hermes.Sms.Cequens</c>, with the
/// account's access token read at each call, so a new version is used without a restart.
/// Every answer is classed as the answer table gives it, and none of its text is kept or
/// logged.
/// </summary>
internal sealed class CequensSmsProvider(IHttpClientFactory clients, ISecretSource secrets) : ISmsProvider
{
    public async ValueTask<Attempt> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Segments > CequensAnswer.MaxSegments)
        {
            return new Attempt.Permanent(AttemptReason.MessageRefused, null);
        }

        MessageBody body = new(message.Text, message.Sender, MessageType(message.Encoding), Recipient(message.ToNumber), message.ReportAddress?.AbsoluteUri);
        using HttpRequestMessage request = await RequestAsync(HttpMethod.Post, CequensClient.Messages, cancellationToken).ConfigureAwait(false);
        request.Content = JsonContent.Create(body, CequensJson.Default.MessageBody);
        try
        {
            using HttpClient client = clients.CreateClient(CequensClient.Name);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            AnswerBody? answer = status is 200 or 400 ? await ReadAsync(response, cancellationToken).ConfigureAwait(false) : null;
            return CequensAnswer.Send(status, answer);
        }
        catch (HttpRequestException failure)
        {
            return new Attempt.Transient(Reason(failure), null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client gave up on the call, not the caller.
            return new Attempt.Transient(AttemptReason.Timeout, null, null);
        }
    }

    public bool IsValidSender(string sender)
    {
        // Cequens documents no rule narrower than 02 MSG-SMS-002's; it refuses a name not
        // registered for the destination's networks with code -12.
        ArgumentNullException.ThrowIfNull(sender);
        return true;
    }

    public async ValueTask<BalanceReading> BalanceAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = await RequestAsync(HttpMethod.Get, CequensClient.Balance, cancellationToken).ConfigureAwait(false);
        try
        {
            using HttpClient client = clients.CreateClient(CequensClient.Name);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            AnswerBody? answer = status is 200 ? await ReadAsync(response, cancellationToken).ConfigureAwait(false) : null;
            return CequensAnswer.Balance(status, answer);
        }
        catch (HttpRequestException failure)
        {
            return new BalanceReading.Failed(Reason(failure));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client gave up on the call, not the caller.
            return new BalanceReading.Failed(AttemptReason.Timeout);
        }
    }

    public ValueTask<SmsReportReading> ReadReportAsync(SmsReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Whatever the method, a report is answered 200 with an empty body, whether or not it reads.
        SmsReportResponse response = new() { Status = 200, Body = ReadOnlyMemory<byte>.Empty };
        if (Single(request.Query, "msgid") is not { Length: > 0 } reference || Single(request.Query, "statusid") is not { } status)
        {
            return ValueTask.FromResult(new SmsReportReading { Reads = false, Response = response });
        }

        SmsReportOutcome outcome = status switch
        {
            "1" => SmsReportOutcome.Delivered,
            "2" => SmsReportOutcome.Failed,
            _ => SmsReportOutcome.NotFinal,
        };
        return ValueTask.FromResult(new SmsReportReading { Reads = true, ProviderReference = reference, Outcome = outcome, Response = response });
    }

    private static string MessageType(SmsEncoding encoding)
    {
        return encoding switch
        {
            SmsEncoding.Gsm7 => "text",
            SmsEncoding.Ucs2 => "unicode",
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Not an encoding of 08 section 2."),
        };
    }

    /// <summary>The number as the gateway takes it, without its <c>+</c>.</summary>
    private static string Recipient(string number)
    {
        return number.StartsWith('+') ? number[1..] : number;
    }

    private static AttemptReason Reason(HttpRequestException failure)
    {
        return failure.HttpRequestError == HttpRequestError.SecureConnectionError ? AttemptReason.TlsFailed : AttemptReason.ConnectionFailed;
    }

    /// <summary>The body of <paramref name="response"/>, or none where it does not read as the gateway's answer.</summary>
    private static async Task<AnswerBody?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonSerializer.DeserializeAsync(stream, CequensJson.Default.AnswerBody, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>The one value of <paramref name="name"/> in <paramref name="fields"/>, or none where it is absent or given more than once.</summary>
    private static string? Single(IReadOnlyList<KeyValuePair<string, string>> fields, string name)
    {
        string? found = null;
        foreach ((string key, string value) in fields)
        {
            if (string.Equals(key, name, StringComparison.Ordinal))
            {
                if (found is not null)
                {
                    return null;
                }

                found = value;
            }
        }

        return found;
    }

    /// <summary>
    /// A request to <paramref name="address"/> taking JSON, with the bearer token of the
    /// highest version of <c>smsCequens</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The secret source answers no version, or its highest is outside the token's form, which the start checked.</exception>
    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, Uri address, CancellationToken cancellationToken)
    {
        SecretVersion? highest = await CequensToken.HighestAsync(secrets, cancellationToken).ConfigureAwait(false);
        if (highest is null || !CequensToken.IsInForm(highest.Value.Span))
        {
            throw new InvalidOperationException($"The secret {CequensToken.Name} is not answered, or its highest version is outside its form.");
        }

        HttpRequestMessage request = new(method, address);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Encoding.ASCII.GetString(highest.Value.Span));
        return request;
    }
}