using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Tests;

/// <summary>Secrets of their forms for a test host.</summary>
internal static class Secrets
{
    /// <summary>A connection string to a port nothing listens on, so a start past the secrets stops at the database.</summary>
    internal const string Unreachable = "Host=127.0.0.1;Port=1;Timeout=1;Username=none;Database=none";

    /// <summary>A P-256 scalar, the private key of a VAPID key pair: 32 bytes of 0x04, below the group's order.</summary>
    internal static readonly string VapidScalar = Convert.ToHexString(Bytes(32, 4));

    /// <summary>The submission account's password, made at run time.</summary>
    internal static readonly string Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>A source holding every secret the engine's parts read, each of its form, the connection given or one that does not connect.</summary>
    internal static SecretSourceFake Held(string? connection = null)
    {
        return new SecretSourceFake()
            .With("connection", Encoding.UTF8.GetBytes(connection ?? Unreachable))
            .With("contentKeys", Bytes(32, 1))
            .With("hashKeys", Bytes(32, 2))
            .With("tokenKeys", Bytes(32, 3))
            .With("emailSubmission", Account(Password))
            .With("pushVapidKeys", Convert.FromHexString(VapidScalar));
    }

    /// <summary>The secret <c>emailSubmission</c> of the user <c>notices</c> and <paramref name="password"/>.</summary>
    internal static byte[] Account(string password)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["userName"] = "notices", ["password"] = password });
    }

    /// <summary><paramref name="length"/> bytes, each <paramref name="value"/>.</summary>
    internal static byte[] Bytes(int length, byte value)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }
}