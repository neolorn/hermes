using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Hermes.Endpoints;

/// <summary>A page of the unsubscribe group, answered as HTML (<c>05</c> MSG-API-005).</summary>
internal sealed class PageAnswer(int status, string html) : IResult
{
    internal int Status { get; } = status;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.StatusCode = Status;
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        return httpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(html), httpContext.RequestAborted).AsTask();
    }
}