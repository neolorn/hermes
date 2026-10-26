using System;
using System.Collections.Generic;

namespace Hermes.Operations;

/// <summary>A row of the key table: one version of one of the five keys (<c>06</c> MSG-OPS-008).</summary>
internal sealed class KeyVersion
{
    /// <summary>The five keys the key table records, as <c>08</c> section 7 names them.</summary>
    internal static IReadOnlyList<string> Keys { get; } = ["contentKeys", "hashKeys", "tokenKeys", "smsReportKey", "pushVapidKeys"];

    public required string Key { get; set; }

    public required int Version { get; set; }

    /// <summary>The first 16 bytes of HMAC-SHA-256 of <c>messaging-key-check</c> under the version; for <c>pushVapidKeys</c>, the 65-byte public key.</summary>
    public required byte[] CheckValue { get; set; }

    /// <summary>When values were first made under the version, or none.</summary>
    public DateTimeOffset? FirstMadeAt { get; set; }

    /// <summary>When values were last made under the version, or none.</summary>
    public DateTimeOffset? LastMadeAt { get; set; }

    public bool Retired { get; set; }

    /// <summary>For <c>contentKeys</c>, the values reserved under the version.</summary>
    public long ValueCount { get; set; }
}