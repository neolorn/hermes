using System;
using System.Globalization;
using System.Text;

namespace Hermes.Sending;

/// <summary>A live subscription the dispatcher opened for an attempt (<c>02</c> MSG-PUSH-005); it never prints its endpoint or keys.</summary>
/// <param name="Id">Its row.</param>
/// <param name="Endpoint">Its endpoint.</param>
/// <param name="Keys">Its 65 bytes of <c>p256dh</c> and 16 of <c>auth</c>.</param>
/// <param name="VapidVersion">The <c>pushVapidKeys</c> version it was made with.</param>
/// <param name="Service">Its push service's spelling.</param>
internal sealed record OpenedSubscription(Guid Id, string Endpoint, byte[] Keys, int VapidVersion, string Service)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Id = {Id}, VapidVersion = {VapidVersion}, Service = {Service}");
        return true;
    }
}