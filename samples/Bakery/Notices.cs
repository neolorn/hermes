using System;

using Hermes;
using Hermes.Core;

namespace Bakery;

/// <summary>
/// The bakery's notices: an order's receipt, in the essential category <c>orders</c>, by email
/// and SMS; and the week's offer, in the optional category <c>offers</c>, by email and push.
/// </summary>
internal static class Notices
{
    internal const string Orders = "orders";

    internal const string Offers = "offers";

    internal const string OrderReceived = "order.received";

    internal const string WeeklyOffer = "offer.weekly";

    /// <summary>Declares the categories and kinds, their templates embedded in this assembly.</summary>
    internal static void Declare(DeclarationBuilder declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        declare.Category(Orders).Essential().Label("en", "Orders");
        declare.Category(Offers).Label("en", "Offers");
        declare.Kind(OrderReceived, Orders).Email().Sms();
        declare.Kind(WeeklyOffer, Offers).Email().Push();
        declare.Templates(typeof(Notices).Assembly);
    }
}