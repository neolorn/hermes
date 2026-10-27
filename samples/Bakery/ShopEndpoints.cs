using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Bakery;

/// <summary>The bakery's own routes: signing in, placing an order and sending the week's offer.</summary>
internal static class ShopEndpoints
{
    internal const string StaffPolicy = "staff";

    internal static void MapShop(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/sign-in", SignInAsync);
        endpoints.MapPost("/orders", OrderAsync).RequireAuthorization();
        endpoints.MapPost("/offers", OfferAsync).RequireAuthorization(StaffPolicy);
    }

    /// <summary>Signs in a person the bakery knows; the host's own sign-in, with its credentials, stands in its place.</summary>
    private static async Task<Results<NoContent, NotFound>> SignInAsync(SignIn body, Customers customers, HttpContext context)
    {
        if (customers.Find(body.Person) is not { } person)
        {
            return TypedResults.NotFound();
        }

        List<Claim> claims = [new(ClaimTypes.NameIdentifier, person.Id)];
        if (person.Staff)
        {
            claims.Add(new(ClaimTypes.Role, StaffPolicy));
        }

        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>Takes the signed-in customer's order and tells them it is in, by email and SMS.</summary>
    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> OrderAsync(Order body, ClaimsPrincipal user, Customers customers, IMessages messages, CancellationToken cancellationToken)
    {
        if (customers.Find(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty) is not { } customer)
        {
            return TypedResults.NotFound();
        }

        Result<SendReceipt> sent = await messages.SendAsync(
            new Send
            {
                Kind = Notices.OrderReceived,
                Recipient = new Recipient.Person(customer.Id),
                Email = customer.Email,
                Phone = customer.Phone,
                Values = new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["item"] = new SendValue.PlainText(body.Item) },
            },
            cancellationToken).ConfigureAwait(false);
        return sent.IsSuccess ? TypedResults.NoContent() : TypedResults.Problem(sent.Failure!.Code, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>Sends the week's offer to every customer, by email and to each push subscription they hold.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> OfferAsync(Offer body, Customers customers, IMessages messages, CancellationToken cancellationToken)
    {
        foreach (Customer customer in customers.All())
        {
            Result<SendReceipt> sent = await messages.SendAsync(
                new Send
                {
                    Kind = Notices.WeeklyOffer,
                    Recipient = new Recipient.Person(customer.Id),
                    Email = customer.Email,
                    Values = new Dictionary<string, SendValue>(StringComparer.Ordinal) { ["offer"] = new SendValue.PlainText(body.Text) },
                },
                cancellationToken).ConfigureAwait(false);
            if (!sent.IsSuccess)
            {
                return TypedResults.Problem(sent.Failure!.Code, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }

        return TypedResults.NoContent();
    }
}