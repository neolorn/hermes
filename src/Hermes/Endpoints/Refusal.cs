using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Http;

namespace Hermes.Endpoints;

/// <summary>
/// A refusal answered as <c>{ "code", "details" }</c> with its status (<c>05</c> MSG-API-001),
/// a 503 or 429 carrying its <c>Retry-After</c>.
/// </summary>
internal sealed class Refusal(int status, string code, IReadOnlyDictionary<string, string> details, int? retryAfter = null) : IResult
{
    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>(StringComparer.Ordinal);

    internal int Status { get; } = status;

    internal string Code { get; } = code;

    internal static Refusal Of(int status, string code, params (string Name, string Value)[] details)
    {
        Dictionary<string, string> named = new(StringComparer.Ordinal);
        foreach ((string name, string value) in details)
        {
            named[name] = value;
        }

        return new Refusal(status, code, named.Count == 0 ? None : named);
    }

    /// <summary>A failure a library call answered, with the status <c>08</c> section 1 gives its code.</summary>
    internal static Refusal Of(int status, Failure failure)
    {
        return new Refusal(status, failure.Code, failure.Details);
    }

    /// <summary>A 503 <c>messaging.request.unavailable</c>, retried after <paramref name="interval"/> in whole seconds, at least 1.</summary>
    internal static Refusal Unavailable(TimeSpan interval)
    {
        return new Refusal(StatusCodes.Status503ServiceUnavailable, Codes.RequestUnavailable, None, Seconds(interval));
    }

    /// <summary>The whole seconds of <paramref name="wait"/>, rounded up, at least 1.</summary>
    internal static int Seconds(TimeSpan wait)
    {
        return Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        HttpResponse response = httpContext.Response;
        response.StatusCode = Status;
        if (retryAfter is { } seconds)
        {
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(response.Body, new ErrorBody(Code, details), EndpointJson.Default.ErrorBody, httpContext.RequestAborted);
    }
}