using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermes.Endpoints;

/// <summary>
/// The report group (<c>05</c> MSG-API-006): takes the SMS provider's delivery reports, the
/// key in the path compared in constant time with every held version of <c>smsReportKey</c>,
/// and answers each as the provider expects, whatever its effect, so a forger learns nothing.
/// Called by the provider, which holds no session, the group needs no signed-in person and
/// takes no forgery protection.
/// </summary>
internal static class SmsReportEndpoints
{
    private const string Route = "/sms/report/{key}";

    internal static void Map(RouteGroupBuilder group)
    {
        group.AllowAnonymous();
        group.DisableAntiforgery();
        group.MapMethods(Route, [HttpMethods.Get, HttpMethods.Post], ReportAsync)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status429TooManyRequests);
    }

    /// <summary>The held version of <c>smsReportKey</c> whose base64url <paramref name="key"/> is, every version compared in constant time; none where it is none of them.</summary>
    internal static int? Matched(HeldKeys keys, string key)
    {
        if (keys.Find(KeyNeeds.SmsReportKey) is not { } held)
        {
            return null;
        }

        byte[] given = Encoding.ASCII.GetBytes(key);
        int? matched = null;
        foreach ((int version, byte[] value) in held.Versions)
        {
            if (CryptographicOperations.FixedTimeEquals(given, Encoding.ASCII.GetBytes(Base64Url.EncodeToString(value))))
            {
                matched = version;
            }
        }

        return matched;
    }

    /// <summary>The report address under <paramref name="baseAddress"/>, the mapped group's absolute address: it, <c>/sms/report/</c> and the base64url of <paramref name="key"/>'s write version.</summary>
    internal static Uri Address(Uri baseAddress, HeldKey key)
    {
        return new Uri(baseAddress.AbsoluteUri.TrimEnd('/') + "/sms/report/" + Base64Url.EncodeToString(key.Write), UriKind.Absolute);
    }

    private static async Task<Results<NotFound, Refusal, ReportAnswer>> ReportAsync(HttpContext context, string key)
    {
        IServiceProvider services = context.RequestServices;
        CancellationToken cancellationToken = context.RequestAborted;
        string source = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (Matched(services.GetRequiredService<HeldKeys>(), key) is not { } version)
        {
            if (services.GetRequiredService<WrongKeyLimiter>().Take(source) is { } wait)
            {
                return new Refusal(StatusCodes.Status429TooManyRequests, Codes.RequestRateLimited, new Dictionary<string, string>(StringComparer.Ordinal), Refusal.Seconds(wait));
            }

            await services.GetRequiredService<SmsReports>().RejectAsync(source, cancellationToken).ConfigureAwait(false);
            return TypedResults.NotFound();
        }

        await services.GetRequiredService<KeyTable>().MarkMadeAsync(KeyNeeds.SmsReportKey, version, cancellationToken).ConfigureAwait(false);
        if (await RequestBodies.ReadAsync(context.Request, cancellationToken).ConfigureAwait(false) is not { } body)
        {
            return RequestBodies.TooLarge();
        }

        SmsReportRequest request = new()
        {
            Method = context.Request.Method,
            Query = [.. context.Request.Query.SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string>(field.Key, value ?? string.Empty)))],
            Form = await FormAsync(context.Request, body, cancellationToken).ConfigureAwait(false),
            Headers = [.. context.Request.Headers.SelectMany(header => header.Value.Select(value => new KeyValuePair<string, string>(header.Key, value ?? string.Empty)))],
            ContentType = context.Request.ContentType,
            Body = body,
        };
        TimeSpan timeout = services.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue.Dispatch.Timeout;
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(timeout);
        SmsReportReading reading = await services.GetRequiredService<ISmsProvider>().ReadReportAsync(request, bound.Token).ConfigureAwait(false);
        await services.GetRequiredService<SmsReports>().ApplyAsync(reading, source, cancellationToken).ConfigureAwait(false);
        return new ReportAnswer(reading.Response);
    }

    /// <summary>The form body's fields, in order; none where the body is no form, or does not read as one.</summary>
    private static async Task<IReadOnlyList<KeyValuePair<string, string>>> FormAsync(HttpRequest request, byte[] body, CancellationToken cancellationToken)
    {
        if (body.Length == 0 || !request.HasFormContentType)
        {
            return [];
        }

        request.Body = new MemoryStream(body, writable: false);
        request.ContentLength = body.Length;
        try
        {
            IFormCollection form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return [.. form.SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string>(field.Key, value ?? string.Empty)))];
        }
        catch (InvalidDataException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}