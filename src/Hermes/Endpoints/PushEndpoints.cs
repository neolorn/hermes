using System;
using System.Buffers.Text;
using System.Threading.Tasks;

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
/// The push subscription group (<c>05</c> MSG-API-004): the key a browser subscribes with,
/// which needs no signed-in person, and the registration and removal of the signed-in
/// person's subscriptions.
/// </summary>
internal static class PushEndpoints
{
    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/push/key", (Delegate)KeyAsync)
            .AllowAnonymous();
        group.MapPost("/push/subscriptions", (Delegate)RegisterAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
        group.MapDelete("/push/subscriptions", (Delegate)RemoveAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
    }

    private static Task<JsonAnswer<PublicKeyBody>> KeyAsync(HttpContext context)
    {
        byte[] key = context.RequestServices.GetRequiredService<PushSubscriptions>().PublicKey();
        return Task.FromResult(new JsonAnswer<PublicKeyBody>(new PublicKeyBody(Base64Url.EncodeToString(key)), EndpointJson.Default.PublicKeyBody));
    }

    private static async Task<Results<NoContent, Refusal>> RegisterAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (SubscriptionBody? body, Refusal? refusal) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.SubscriptionBody, Missing).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        PushSubscriptions subscriptions = context.RequestServices.GetRequiredService<PushSubscriptions>();
        if (SubscriptionRule.Keys(body!.Keys!.P256dh, body.Keys.Auth) is not { } keys)
        {
            return RequestBodies.Malformed("keys");
        }

        if (!Base64Url.IsValid(body.ApplicationServerKey!) || Base64Url.DecodeFromChars(body.ApplicationServerKey) is not { Length: SubscriptionRule.PointBytes } serverKey)
        {
            return RequestBodies.Malformed("applicationServerKey");
        }

        DateTimeOffset? expiresAt = null;
        if (body.ExpirationTime is { } milliseconds)
        {
            if (!double.IsFinite(milliseconds) || milliseconds < 0 || milliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            {
                return RequestBodies.Malformed("expirationTime");
            }

            expiresAt = DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds);
        }

        PushSubscriptions.Outcome outcome;
        try
        {
            outcome = await subscriptions.RegisterAsync(person, body.Endpoint!, keys, serverKey, expiresAt, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            outcome = PushSubscriptions.Outcome.Unavailable;
        }

        return outcome switch
        {
            PushSubscriptions.Outcome.Done => TypedResults.NoContent(),
            PushSubscriptions.Outcome.EndpointNotAllowed => Refusal.Of(StatusCodes.Status422UnprocessableEntity, Codes.PushEndpointNotAllowed),
            PushSubscriptions.Outcome.KeyUnknown => Refusal.Of(StatusCodes.Status422UnprocessableEntity, Codes.PushKeyUnknown),
            _ => Unavailable(context),
        };
    }

    private static async Task<Results<NoContent, Refusal>> RemoveAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (RemovalBody? body, Refusal? refusal) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.RemovalBody, removal => removal.Endpoint is null ? "endpoint" : null).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        PushSubscriptions.Outcome outcome;
        try
        {
            outcome = await context.RequestServices.GetRequiredService<PushSubscriptions>().RemoveAsync(person, body!.Endpoint!, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            outcome = PushSubscriptions.Outcome.Unavailable;
        }

        return outcome == PushSubscriptions.Outcome.Done ? TypedResults.NoContent() : Unavailable(context);
    }

    /// <summary>The member of a subscription body that is missing, or none.</summary>
    private static string? Missing(SubscriptionBody body)
    {
        return body switch
        {
            { Endpoint: null } => "endpoint",
            { Keys: null } => "keys",
            { Keys.P256dh: null } => "keys.p256dh",
            { Keys.Auth: null } => "keys.auth",
            { ApplicationServerKey: null } => "applicationServerKey",
            _ => null,
        };
    }

    private static Refusal Unavailable(HttpContext context)
    {
        return Refusal.Unavailable(context.RequestServices.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue.Dispatch.Interval);
    }
}