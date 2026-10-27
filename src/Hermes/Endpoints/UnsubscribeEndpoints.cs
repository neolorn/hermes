using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Content;
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
/// The unsubscribe group (<c>05</c> MSG-API-005): RFC 8058's one-click <c>POST</c> suppresses
/// the destination a valid token names by hash for its category, and a <c>GET</c> offers a page
/// that asks before it does, suppressing nothing itself. Called by mail providers, which hold
/// no session, the group needs no signed-in person, takes no forgery protection, sets no
/// cookie and never redirects.
/// </summary>
internal static class UnsubscribeEndpoints
{
    /// <summary>The field the <c>unsubscribe</c> page's form carries.</summary>
    internal const string PageField = "page";

    private const string Route = "/unsubscribe/{token}";

    internal static void Map(RouteGroupBuilder group)
    {
        group.AllowAnonymous();
        group.DisableAntiforgery();
        group.AddEndpointFilter(async (invocation, next) =>
        {
            PageHeaders.Apply(invocation.HttpContext.Response);
            return await next(invocation).ConfigureAwait(false);
        });
        group.MapPost(Route, PostAsync)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces(StatusCodes.Status404NotFound, contentType: "text/html")
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet(Route, GetAsync)
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces(StatusCodes.Status404NotFound, contentType: "text/html")
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<Ok, PageAnswer, Refusal>> PostAsync(HttpContext context, string token)
    {
        CancellationToken cancellationToken = context.RequestAborted;
        if (await RequestBodies.ReadAsync(context.Request, cancellationToken).ConfigureAwait(false) is not { } body)
        {
            return RequestBodies.TooLarge();
        }

        bool page = await CarriesPageAsync(context.Request, body, cancellationToken).ConfigureAwait(false);
        (TokenReading? reading, Refusal? unavailable) = await VerifyAsync(context, token).ConfigureAwait(false);
        if (unavailable is not null)
        {
            return unavailable;
        }

        PageRenderer pages = context.RequestServices.GetRequiredService<PageRenderer>();
        if (reading is null)
        {
            return page && await pages.RenderAsync(Pages.UnsubscribeInvalid, language: null).ConfigureAwait(false) is { } invalid
                ? new PageAnswer(StatusCodes.Status404NotFound, invalid)
                : Invalid();
        }

        await SuppressAsync(context.RequestServices, reading, cancellationToken).ConfigureAwait(false);
        if (page)
        {
            CategoryDeclaration category = Category(context.RequestServices, reading.Category!);
            if (await pages.RenderAsync(Pages.Unsubscribed, reading.Language, pages.Label(category, reading.Language)).ConfigureAwait(false) is { } done)
            {
                return new PageAnswer(StatusCodes.Status200OK, done);
            }
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, PageAnswer, Refusal>> GetAsync(HttpContext context, string token)
    {
        (TokenReading? reading, Refusal? unavailable) = await VerifyAsync(context, token).ConfigureAwait(false);
        if (unavailable is not null)
        {
            return unavailable;
        }

        PageRenderer pages = context.RequestServices.GetRequiredService<PageRenderer>();
        if (reading is null)
        {
            return await pages.RenderAsync(Pages.UnsubscribeInvalid, language: null).ConfigureAwait(false) is { } invalid
                ? new PageAnswer(StatusCodes.Status404NotFound, invalid)
                : Invalid();
        }

        CategoryDeclaration category = Category(context.RequestServices, reading.Category!);
        string action = (context.Request.PathBase + context.Request.Path).ToUriComponent();
        return await pages.RenderAsync(Pages.Unsubscribe, reading.Language, pages.Label(category, reading.Language), action).ConfigureAwait(false) is { } asks
            ? new PageAnswer(StatusCodes.Status200OK, asks)
            : TypedResults.Ok();
    }

    /// <summary>
    /// Reads the token: one that verifies; none for one that does not, or that names a version
    /// neither held nor in use; or 503 for one naming a version in use but not held here, which
    /// raises <c>key-unavailable</c>, or of a format kept for a later release, which anyone can
    /// write and so raises nothing.
    /// </summary>
    private static async Task<(TokenReading? Reading, Refusal? Unavailable)> VerifyAsync(HttpContext context, string token)
    {
        IServiceProvider services = context.RequestServices;
        MessagingOptions current = services.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue;
        Declarations declarations = services.GetRequiredService<Declarations>();
        TokenReading reading = services.GetRequiredService<UnsubscribeTokens>().Read(
            token,
            services.GetRequiredService<TimeProvider>().GetUtcNow(),
            current.Unsubscribe.TokenLifetime,
            name => declarations.Categories.Any(category => !category.Essential && string.Equals(category.Name, name, StringComparison.Ordinal)));
        switch (reading.Status)
        {
            case TokenStatus.Valid:
                return (reading, null);
            case TokenStatus.Later:
                return (null, Refusal.Unavailable(current.Dispatch.Interval));
            case TokenStatus.NotHeld when await services.GetRequiredService<KeyTable>().InUseAsync(reading.Key!, reading.TokenVersion, context.RequestAborted).ConfigureAwait(false):
                await services.GetRequiredService<Alerts>().HoldsUntilAsync(
                    "key-unavailable",
                    reading.Key,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["version"] = reading.TokenVersion.ToString(CultureInfo.InvariantCulture) },
                    context.RequestAborted).ConfigureAwait(false);
                return (null, Refusal.Unavailable(current.Dispatch.Interval));
            default:
                return (null, null);
        }
    }

    /// <summary>
    /// Marks the token's <c>hashKeys</c> version made in its own transaction, so the retention job
    /// never deletes the suppression under a version it has marked, then suppresses the hash
    /// the token carries for its category.
    /// </summary>
    private static async Task SuppressAsync(IServiceProvider services, TokenReading reading, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<KeyTable>().MarkMadeAsync(KeyNeeds.HashKeys, reading.HashVersion, cancellationToken).ConfigureAwait(false);
        await LibraryTransactions.RunAsync(
            services.GetRequiredService<MessagingDatabase>(),
            services.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue,
            (context, token) => Suppressions.SuppressHashAsync(context, reading.Category!, reading.Hash!, reading.HashVersion, services.GetRequiredService<TimeProvider>().GetUtcNow(), token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether the body is a form carrying the field <c>page</c>; a body that does not read as a form carries none.</summary>
    private static async Task<bool> CarriesPageAsync(HttpRequest request, byte[] body, CancellationToken cancellationToken)
    {
        if (body.Length == 0 || !request.HasFormContentType)
        {
            return false;
        }

        request.Body = new MemoryStream(body, writable: false);
        request.ContentLength = body.Length;
        try
        {
            IFormCollection form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            return form.ContainsKey(PageField);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static CategoryDeclaration Category(IServiceProvider services, string name)
    {
        return services.GetRequiredService<Declarations>().Categories.First(category => string.Equals(category.Name, name, StringComparison.Ordinal));
    }

    private static Refusal Invalid()
    {
        return Refusal.Of(StatusCodes.Status404NotFound, Codes.UnsubscribeInvalid);
    }
}