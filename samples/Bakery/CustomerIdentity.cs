using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Http;

namespace Bakery;

/// <summary>The identity seam: the person a request's sign-in names, or none.</summary>
internal sealed class CustomerIdentity(IHttpContextAccessor accessor) : IRecipientIdentity
{
    public ValueTask<string?> CurrentAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier));
    }
}