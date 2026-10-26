using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Channels;

/// <summary>
/// The submission account, the secret <c>emailSubmission</c> (<c>06</c> MSG-OPS-008): UTF-8
/// JSON <c>{ "userName", "password" }</c>, used at its highest version. It is held as its
/// bytes, and read into the strings MailKit takes only when a session authenticates.
/// </summary>
internal sealed class EmailSubmission
{
    internal const string Name = "emailSubmission";

    private readonly Lock gate = new();
    private byte[] held = [];

    /// <summary>
    /// The highest version of <c>emailSubmission</c>: one not answered stops the start as
    /// unavailable, and JSON that does not read as the account as invalid.
    /// </summary>
    internal static async Task<byte[]> ReadAsync(ISecretSource secrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        SecretVersions? read = await secrets.ReadAsync(Name, cancellationToken).ConfigureAwait(false);
        if (read is not { Versions.Count: > 0 })
        {
            throw StartupFailure.Of(ChannelCodes.SecretUnavailable, ("key", Name));
        }

        SecretVersion highest = read.Versions.MaxBy(version => version.Version)!;
        if (Account(highest.Value.Span) is null)
        {
            throw StartupFailure.Of(ChannelCodes.SecretInvalid, ("key", Name));
        }

        return highest.Value.ToArray();
    }

    /// <summary>Holds <paramref name="value"/>, clearing the bytes held before.</summary>
    internal void Hold(byte[] value)
    {
        lock (gate)
        {
            Array.Clear(held);
            held = value;
        }
    }

    /// <summary>The account held, as the credential a session authenticates with.</summary>
    /// <exception cref="InvalidOperationException">No account is held.</exception>
    internal NetworkCredential Credential()
    {
        lock (gate)
        {
            (string user, string password) = Account(held) ?? throw new InvalidOperationException("No submission account is held.");
            return new NetworkCredential(user, password);
        }
    }

    /// <summary>The user name and password of <paramref name="json"/>, each a non-empty string, or none where it does not read.</summary>
    private static (string User, string Password)? Account(ReadOnlySpan<byte> json)
    {
        try
        {
            Utf8JsonReader reader = new(json);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            string? user = null;
            string? password = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isUser = reader.ValueTextEquals("userName"u8);
                bool isPassword = reader.ValueTextEquals("password"u8);
                reader.Read();
                if (!isUser && !isPassword)
                {
                    reader.Skip();
                    continue;
                }

                if (reader.TokenType != JsonTokenType.String)
                {
                    return null;
                }

                if (isUser)
                {
                    user = reader.GetString();
                }
                else
                {
                    password = reader.GetString();
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                return null;
            }

            return user is { Length: > 0 } && password is { Length: > 0 } ? (user, password) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}