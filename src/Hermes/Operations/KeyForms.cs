using System;

namespace Hermes.Operations;

/// <summary>The forms of the five keys' versions (<c>06</c> MSG-OPS-008).</summary>
internal static class KeyForms
{
    /// <summary>The order of P-256's base point, above which no scalar is (SEC 2, section 2.4.2).</summary>
    private static readonly byte[] P256Order = Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");

    /// <summary>
    /// Whether <paramref name="value"/> is a version of <paramref name="key"/> in its form:
    /// 32 bytes for <c>contentKeys</c>, at least 32 for <c>hashKeys</c> and <c>tokenKeys</c>,
    /// at least 16 for <c>smsReportKey</c>, and for <c>pushVapidKeys</c> a P-256 scalar of 32 bytes.
    /// </summary>
    internal static bool InForm(string key, ReadOnlySpan<byte> value)
    {
        return key switch
        {
            KeyNeeds.ContentKeys => value.Length == 32,
            KeyNeeds.HashKeys or KeyNeeds.TokenKeys => value.Length >= 32,
            KeyNeeds.SmsReportKey => value.Length >= 16,
            KeyNeeds.PushVapidKeys => IsP256Scalar(value),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not one of the five keys."),
        };
    }

    /// <summary>Whether <paramref name="value"/> is 32 bytes, big-endian, from 1 to the order of P-256 less one.</summary>
    private static bool IsP256Scalar(ReadOnlySpan<byte> value)
    {
        return value.Length == 32 && value.ContainsAnyExcept((byte)0) && value.SequenceCompareTo(P256Order) < 0;
    }
}