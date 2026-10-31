using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>The preferences group (<c>05</c> MSG-API-003): the person signed in reads and sets, per optional category and channel, whether they receive it.</summary>
internal static class PreferenceEndpoints
{
    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/preferences", (Delegate)ReadAsync)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized);
        group.MapPut("/preferences/{category}/{channel}", SetAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound)
            .Produces<ErrorBody>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorBody>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ErrorBody>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<JsonAnswer<PreferencesBody>, Refusal>> ReadAsync(HttpContext context)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        IReadOnlyList<CategoryPreferences> read = await context.RequestServices.GetRequiredService<IPreferences>().ReadAsync(person, context.RequestAborted).ConfigureAwait(false);
        PreferencesBody body = new([.. read.Select(category => new CategoryBody(
            category.Category,
            category.Essential,
            [.. category.Channels.Select(channel => new ChannelBody(Spellings.Of(channel.Channel), channel.On))]))]);
        return new JsonAnswer<PreferencesBody>(body, EndpointJson.Default.PreferencesBody);
    }

    private static async Task<Results<NoContent, Refusal>> SetAsync(HttpContext context, string category, string channel)
    {
        if (await Identity.PersonAsync(context).ConfigureAwait(false) is not { } person)
        {
            return Identity.Unauthenticated();
        }

        (PreferenceChange? change, Refusal? refusal) = await RequestBodies.JsonAsync(context.Request, EndpointJson.Default.PreferenceChange, body => body.On is null ? "on" : null).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        if (Spellings.Channel(channel) is not { } named)
        {
            return Refusal.Of(StatusCodes.Status404NotFound, Codes.PreferenceUnknown, ("category", category), ("channel", channel));
        }

        Result result;
        try
        {
            result = await context.RequestServices.GetRequiredService<IPreferences>().SetAsync(person, category, named, change!.On!.Value, context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception failure) when (DispatchPasses.IsLockTimeout(failure))
        {
            return Refusal.Unavailable(context.RequestServices.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue.Dispatch.Interval);
        }

        if (result.Failure is { } refused)
        {
            return Refusal.Of(refused.Code is Codes.PreferenceEssential or Codes.PreferenceFixed ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status404NotFound, refused);
        }

        return TypedResults.NoContent();
    }
}