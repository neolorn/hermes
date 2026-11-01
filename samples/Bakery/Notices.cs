using System;

using Hermes;
using Hermes.Core;

namespace Bakery;

/// <summary>
/// The bakery's notices: an order's receipt, in the essential category <c>orders</c>, by email
/// and SMS; the week's offer, in the optional category <c>offers</c>, by email and push; and,
/// in the essential category <c>support</c>, support's reply to a question left through the
/// offline form and a closed conversation's transcript, by email.
/// </summary>
internal static class Notices
{
    internal const string Orders = "orders";

    internal const string Offers = "offers";

    internal const string OrderReceived = "order.received";

    internal const string WeeklyOffer = "offer.weekly";

    internal const string Support = "support";

    internal const string SupportReply = "support.reply";

    internal const string SupportTranscript = "support.transcript";

    /// <summary>Declares the categories and kinds, their templates embedded in this assembly.</summary>
    internal static void Declare(DeclarationBuilder declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        declare.Category(Orders).Essential().Label("en", "Orders");
        declare.Category(Offers).Label("en", "Offers");
        declare.Kind(OrderReceived, Orders).Email().Sms();
        declare.Kind(WeeklyOffer, Offers).Email().Push();
        declare.Category(Support).Essential().Label("en", "Support");
        declare.Kind(SupportReply, Support).Email();
        declare.Kind(SupportTranscript, Support).Email();
        declare.Templates(typeof(Notices).Assembly);
    }
}