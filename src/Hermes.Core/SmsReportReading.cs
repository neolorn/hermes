namespace Hermes.Core;

/// <summary>What a provider reads from a delivery report (<c>02</c> MSG-SMS-004).</summary>
public sealed record SmsReportReading
{
    /// <summary>Whether the report reads; one that does not changes nothing and is counted as rejected.</summary>
    public required bool Reads { get; init; }

    /// <summary>The delivery reference the report names, or none.</summary>
    public string? DeliveryReference { get; init; }

    /// <summary>The provider reference the report names, where it names no delivery reference.</summary>
    public string? ProviderReference { get; init; }

    /// <summary>The report's outcome, where it reads.</summary>
    public SmsReportOutcome? Outcome { get; init; }

    /// <summary>The response the provider expects to receive, sent whatever the report's effect.</summary>
    public required SmsReportResponse Response { get; init; }
}