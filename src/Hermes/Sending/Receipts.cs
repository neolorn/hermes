using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using Hermes.Core;

namespace Hermes.Sending;

/// <summary>A send's receipt as an idempotency row keeps it: the send's identifier and each delivery's, by channel spelling (<c>01</c> MSG-SEND-009).</summary>
internal static class Receipts
{
    private const string SendMember = "send";

    private const string DeliveriesMember = "deliveries";

    private const string ChannelMember = "channel";

    private const string DeliveryMember = "delivery";

    internal static string Write(SendReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(SendMember, receipt.Send.Value);
            writer.WriteStartArray(DeliveriesMember);
            foreach (DeliveryReceipt delivery in receipt.Deliveries)
            {
                writer.WriteStartObject();
                writer.WriteString(ChannelMember, Spellings.Of(delivery.Channel));
                writer.WriteString(DeliveryMember, delivery.Delivery.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The receipt <paramref name="text"/> holds, a delivery of a channel this release does not know left out.</summary>
    internal static SendReceipt Read(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement root = document.RootElement;
        List<DeliveryReceipt> deliveries = [];
        foreach (JsonElement delivery in root.GetProperty(DeliveriesMember).EnumerateArray())
        {
            if (Spellings.Channel(delivery.GetProperty(ChannelMember).GetString() ?? string.Empty) is { } channel)
            {
                deliveries.Add(new DeliveryReceipt { Channel = channel, Delivery = new DeliveryId(delivery.GetProperty(DeliveryMember).GetGuid()) });
            }
        }

        return new SendReceipt { Send = new SendId(root.GetProperty(SendMember).GetGuid()), Deliveries = deliveries };
    }
}