using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Hermes.Development;

/// <summary>The development inbox's group (<c>05</c> MSG-API-007), guarded as <c>02</c> MSG-DEV-005 gives.</summary>
internal static class InboxEndpoints
{
    internal static void Map(RouteGroupBuilder group)
    {
        group.AllowAnonymous();
    }
}