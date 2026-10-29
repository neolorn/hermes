using System.Globalization;
using System.Text;

using Hermes.Core;

namespace Hermes.Development;

/// <summary>A captured SMS (<c>02</c> MSG-DEV-003).</summary>
public sealed record InboxSms
{
    /// <summary>The SMS's sender.</summary>
    public required string Sender { get; init; }

    /// <summary>The SMS's text.</summary>
    public required string Text { get; init; }

    /// <summary>The text's encoding.</summary>
    public required SmsEncoding Encoding { get; init; }

    /// <summary>The number of parts the text takes.</summary>
    public required int Segments { get; init; }

    /// <summary>The reference a delivery report of the SMS would carry.</summary>
    public required string DeliveryReference { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Encoding = {Encoding}, Segments = {Segments}");
        return true;
    }
}