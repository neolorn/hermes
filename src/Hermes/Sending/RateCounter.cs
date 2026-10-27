using System;

namespace Hermes.Sending;

/// <summary>A row of the rate counter: the places a channel's passes took in one second of <c>@now</c> (<c>01</c> MSG-SEND-014).</summary>
internal sealed class RateCounter
{
    public required string Channel { get; set; }

    public required DateTimeOffset Second { get; set; }

    public int Count { get; set; }
}