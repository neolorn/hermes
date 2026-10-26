using System;

namespace Hermes.Core;

/// <summary>The response to a delivery report, as the provider expects to receive it (<c>02</c> MSG-SMS-004).</summary>
public sealed record SmsReportResponse
{
    /// <summary>The HTTP status.</summary>
    public required int Status { get; init; }

    /// <summary>The body's content type, or none.</summary>
    public string? ContentType { get; init; }

    /// <summary>The body, empty for none.</summary>
    public required ReadOnlyMemory<byte> Body { get; init; }
}