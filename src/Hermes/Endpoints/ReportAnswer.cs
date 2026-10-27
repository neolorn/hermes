using System;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Http;

namespace Hermes.Endpoints;

/// <summary>The response the SMS provider expects to a report, sent whatever the report's effect (<c>05</c> MSG-API-006).</summary>
internal sealed class ReportAnswer(SmsReportResponse response) : IResult
{
    internal SmsReportResponse Response { get; } = response;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.StatusCode = Response.Status;
        if (Response.ContentType is { } contentType)
        {
            httpContext.Response.ContentType = contentType;
        }

        if (!Response.Body.IsEmpty)
        {
            await httpContext.Response.Body.WriteAsync(Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}