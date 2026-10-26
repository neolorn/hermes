using System;
using System.Collections.Generic;

namespace Hermes.Core;

/// <summary>A delivery report request whose key matched, passed whole to the provider (<c>02</c> MSG-SMS-004).</summary>
public sealed record SmsReportRequest
{
    /// <summary>The request's HTTP method.</summary>
    public required string Method { get; init; }

    /// <summary>The query string's parameters, in order.</summary>
    public required IReadOnlyList<KeyValuePair<string, string>> Query { get; init; }

    /// <summary>The form body's fields, in order; none where the body is no form.</summary>
    public required IReadOnlyList<KeyValuePair<string, string>> Form { get; init; }

    /// <summary>The request's headers, in order.</summary>
    public required IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; }

    /// <summary>The body's content type, or none.</summary>
    public string? ContentType { get; init; }

    /// <summary>The body, at most 16 KiB.</summary>
    public required ReadOnlyMemory<byte> Body { get; init; }
}