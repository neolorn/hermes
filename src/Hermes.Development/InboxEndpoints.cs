using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Development;

/// <summary>
/// The inbox's group (<c>05</c> MSG-API-007): its page, script and style, the messages the
/// inbox holds as JSON, an email's <c>.eml</c> and HTML, and the clearing of the inbox; every
/// route behind the guard of <c>02</c> MSG-DEV-005.
/// </summary>
internal static partial class InboxEndpoints
{
    /// <summary>The header <c>DELETE /messages</c> needs, and its value.</summary>
    internal const string ClearHeader = "X-Hermes-Inbox";

    private const string ClearValue = "clear";

    /// <summary>How often <c>GET /messages/latest</c> looks again while it waits.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

    internal static void Map(RouteGroupBuilder group)
    {
        group.AllowAnonymous();
        group.AddEndpointFilter(InboxGuard.FilterAsync);

        group.MapGet("/", (Delegate)Page)
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces<InboxError>(StatusCodes.Status403Forbidden);
        group.MapGet("/assets/inbox.js", (Delegate)Script)
            .Produces(StatusCodes.Status200OK, contentType: "text/javascript")
            .Produces<InboxError>(StatusCodes.Status403Forbidden);
        group.MapGet("/assets/inbox.css", (Delegate)Style)
            .Produces(StatusCodes.Status200OK, contentType: "text/css")
            .Produces<InboxError>(StatusCodes.Status403Forbidden);
        group.MapGet("/messages", (Delegate)List)
            .Produces<InboxError>(StatusCodes.Status400BadRequest)
            .Produces<InboxError>(StatusCodes.Status403Forbidden);
        group.MapGet("/messages/latest", (Delegate)LatestAsync)
            .Produces<InboxError>(StatusCodes.Status400BadRequest)
            .Produces<InboxError>(StatusCodes.Status403Forbidden)
            .Produces<InboxError>(StatusCodes.Status404NotFound);
        group.MapGet("/messages/{id}", (Delegate)Message)
            .Produces<InboxError>(StatusCodes.Status403Forbidden)
            .Produces<InboxError>(StatusCodes.Status404NotFound);
        group.MapGet("/messages/{id}/eml", (Delegate)Eml)
            .Produces(StatusCodes.Status200OK, contentType: "message/rfc822")
            .Produces<InboxError>(StatusCodes.Status403Forbidden)
            .Produces<InboxError>(StatusCodes.Status404NotFound);
        group.MapGet("/messages/{id}/html", (Delegate)Html)
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces<InboxError>(StatusCodes.Status403Forbidden)
            .Produces<InboxError>(StatusCodes.Status404NotFound);
        group.MapDelete("/messages", (Delegate)Clear)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<InboxError>(StatusCodes.Status400BadRequest)
            .Produces<InboxError>(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// <paramref name="html"/> with <c>&lt;base target="_blank"&gt;</c> at the start of its
    /// <c>head</c>, or, where it has none, at the start of the document, after its doctype where
    /// it opens with one; so a link opens in a new tab.
    /// </summary>
    internal static string WithBase(string html)
    {
        const string Base = "<base target=\"_blank\">";
        Match at = Head().Match(html) is { Success: true } head ? head : Doctype().Match(html);
        return at.Success ? html.Insert(at.Index + at.Length, Base) : Base + html;
    }

    private static ContentHttpResult Page(HttpContext context)
    {
        string prefix = context.RequestServices.GetRequiredService<InboxGroup>().Prefix ?? string.Empty;
        string root = System.Net.WebUtility.HtmlEncode(context.Request.PathBase.Add(new PathString(prefix.TrimEnd('/'))).Value ?? string.Empty);
        context.Response.Headers.ContentSecurityPolicy = InboxGuard.PagePolicy;
        return TypedResults.Text(InboxPage.Html.Replace(InboxPage.Root, root, StringComparison.Ordinal), "text/html; charset=utf-8");
    }

    private static ContentHttpResult Script(HttpContext context)
    {
        context.Response.Headers.ContentSecurityPolicy = InboxGuard.PagePolicy;
        return TypedResults.Text(InboxPage.Script, "text/javascript; charset=utf-8");
    }

    private static ContentHttpResult Style(HttpContext context)
    {
        context.Response.Headers.ContentSecurityPolicy = InboxGuard.PagePolicy;
        return TypedResults.Text(InboxPage.Style, "text/css; charset=utf-8");
    }

    private static Results<InboxAnswer<ListBody>, InboxRefusal> List(HttpContext context)
    {
        IQueryCollection query = context.Request.Query;
        (InboxFilter filter, InboxRefusal? refusal) = InboxQuery.Filter(query);
        if (refusal is not null)
        {
            return refusal;
        }

        if (InboxQuery.Whole(query, "limit", 1, InboxQuery.MostLimit, InboxQuery.DefaultLimit) is not { } limit)
        {
            return InboxRefusal.Malformed("limit");
        }

        SummaryBody[] messages = [.. InboxFiles.Bodies(Folder(context), filter, limit).Select(found => Summary(found.Body))];
        return new InboxAnswer<ListBody>(new ListBody(messages), InboxJson.Default.ListBody);
    }

    private static async Task<Results<InboxAnswer<MessageBody>, InboxRefusal>> LatestAsync(HttpContext context)
    {
        IQueryCollection query = context.Request.Query;
        (InboxFilter filter, InboxRefusal? refusal) = InboxQuery.Filter(query);
        if (refusal is not null)
        {
            return refusal;
        }

        if (InboxQuery.Whole(query, "wait", 0, InboxQuery.MostWait, 0) is not { } wait)
        {
            return InboxRefusal.Malformed("wait");
        }

        string folder = Folder(context);
        TimeProvider clock = context.RequestServices.GetRequiredService<TimeProvider>();
        long started = clock.GetTimestamp();
        while (true)
        {
            foreach ((RecordFile _, MessageBody body) in InboxFiles.Bodies(folder, filter, 1))
            {
                return new InboxAnswer<MessageBody>(body, InboxJson.Default.MessageBody);
            }

            if (clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(wait))
            {
                return InboxRefusal.NotFound;
            }

            await Task.Delay(Poll, clock, context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static Results<InboxAnswer<MessageBody>, InboxRefusal> Message(HttpContext context, string id)
    {
        return InboxFiles.Find(Folder(context), id) is { } found
            ? new InboxAnswer<MessageBody>(found.Body, InboxJson.Default.MessageBody)
            : InboxRefusal.NotFound;
    }

    private static Results<FileContentHttpResult, InboxRefusal> Eml(HttpContext context, string id)
    {
        if (InboxFiles.Find(Folder(context), id) is not { Body.Email: not null } found)
        {
            return InboxRefusal.NotFound;
        }

        try
        {
            using FileStream stream = new(InboxFiles.EmlPath(found.File.Path), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            return TypedResults.File(copy.ToArray(), "message/rfc822", id + ".eml");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return InboxRefusal.NotFound;
        }
    }

    private static Results<ContentHttpResult, InboxRefusal> Html(HttpContext context, string id)
    {
        if (InboxFiles.Find(Folder(context), id) is not { Body.Email.Html: { } html })
        {
            return InboxRefusal.NotFound;
        }

        context.Response.Headers.ContentSecurityPolicy = InboxGuard.HtmlPolicy;
        return TypedResults.Text(WithBase(html), "text/html; charset=utf-8");
    }

    private static Results<NoContent, InboxRefusal> Clear(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(ClearHeader, out Microsoft.Extensions.Primitives.StringValues values)
            || values.Count != 1
            || !string.Equals(values[0], ClearValue, StringComparison.Ordinal))
        {
            return InboxRefusal.Malformed(ClearHeader);
        }

        InboxFiles.Clear(Folder(context));
        return TypedResults.NoContent();
    }

    private static string Folder(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<InboxWriter>().Folder;
    }

    private static SummaryBody Summary(MessageBody body)
    {
        return new SummaryBody(body.Id, body.Channel, body.App, body.At, body.To, body.Person, body.Kind, body.Category, body.Language, body.Summary);
    }

    /// <summary>A <c>head</c> start tag, not <c>header</c>'s.</summary>
    [GeneratedRegex(@"<head(?=[\s>/])[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Head();

    /// <summary>A doctype at the document's start, after any white space.</summary>
    [GeneratedRegex(@"^\s*<!doctype[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Doctype();
}