using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Endpoints;

/// <summary>The person signed in, as the host's identity seam answers it (<c>05</c> MSG-API-002).</summary>
internal static class Identity
{
    /// <summary>The person reference the seam answers for the request, or none.</summary>
    internal static async Task<string?> PersonAsync(HttpContext context)
    {
        string? person = await context.RequestServices.GetRequiredService<IRecipientIdentity>().CurrentAsync(context.RequestAborted).ConfigureAwait(false);
        return string.IsNullOrEmpty(person) ? null : person;
    }

    internal static Refusal Unauthenticated()
    {
        return Refusal.Of(StatusCodes.Status401Unauthorized, Codes.RequestUnauthenticated);
    }
}