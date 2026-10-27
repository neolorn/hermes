using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

using Hermes.Channels;
using Hermes.Core;

namespace Hermes.Sending;

/// <summary>
/// What a push subscription is accepted with (<c>02</c> MSG-PUSH-004): an absolute
/// <c>https</c> endpoint of at most 2,048 characters, on port 443, with no user information,
/// whose host is a DNS name the allowed hosts match; <c>p256dh</c> a point on P-256 of 65
/// bytes and <c>auth</c> 16 bytes, both base64url.
/// </summary>
internal static class SubscriptionRule
{
    internal const int MostEndpointCharacters = 2_048;

    internal const int PointBytes = 65;

    internal const int AuthBytes = 16;

    /// <summary>Whether <paramref name="endpoint"/> is one the rule accepts under <paramref name="hosts"/>.</summary>
    internal static bool Allows(string endpoint, IEnumerable<string> hosts)
    {
        return Host(endpoint) is { } host && HostRule.Allows(hosts, host);
    }

    /// <summary>The host of <paramref name="endpoint"/> where it is an absolute <c>https</c> address of the rule's length, on port 443, with no user information and a DNS name for its host; none otherwise.</summary>
    internal static string? Host(string endpoint)
    {
        if (endpoint.Length > MostEndpointCharacters
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? address)
            || address.Scheme != Uri.UriSchemeHttps
            || address.Port != 443
            || address.UserInfo.Length > 0
            || address.HostNameType != UriHostNameType.Dns
            || !HostRule.IsHostName(address.IdnHost))
        {
            return null;
        }

        return address.IdnHost;
    }

    /// <summary>The push service of <paramref name="host"/> (<c>08</c> section 2).</summary>
    internal static PushService Service(string host)
    {
        return host switch
        {
            _ when string.Equals(host, "fcm.googleapis.com", StringComparison.OrdinalIgnoreCase) => PushService.Fcm,
            _ when host.EndsWith(".push.services.mozilla.com", StringComparison.OrdinalIgnoreCase) => PushService.Mozilla,
            _ when host.EndsWith(".push.apple.com", StringComparison.OrdinalIgnoreCase) => PushService.Apple,
            _ when host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase) => PushService.Windows,
            _ => PushService.Other,
        };
    }

    /// <summary>The 81 bytes of <paramref name="p256dh"/> and <paramref name="auth"/> where each reads as the rule says; none otherwise.</summary>
    internal static byte[]? Keys(string? p256dh, string? auth)
    {
        if (Decode(p256dh) is not { Length: PointBytes } point || Decode(auth) is not { Length: AuthBytes } secret || !OnCurve(point))
        {
            return null;
        }

        return [.. point, .. secret];
    }

    /// <summary>The base64url of <paramref name="keys"/>' <c>p256dh</c> and <c>auth</c>.</summary>
    internal static (string P256dh, string Auth) Split(ReadOnlySpan<byte> keys)
    {
        return (Base64Url.EncodeToString(keys[..PointBytes]), Base64Url.EncodeToString(keys[PointBytes..]));
    }

    private static byte[]? Decode(string? text)
    {
        if (text is null || !Base64Url.IsValid(text))
        {
            return null;
        }

        return Base64Url.DecodeFromChars(text);
    }

    /// <summary>Whether <paramref name="point"/> is an uncompressed point on P-256.</summary>
    private static bool OnCurve(byte[] point)
    {
        if (point[0] != 0x04)
        {
            return false;
        }

        try
        {
            using ECDiffieHellman key = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = point[1..33], Y = point[33..] },
            });
            return true;
        }
        catch (Exception failure) when (failure is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}