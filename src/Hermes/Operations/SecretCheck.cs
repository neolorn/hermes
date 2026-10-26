using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Npgsql;

namespace Hermes.Operations;

/// <summary>Reads the secrets the used parts need when the host starts (<c>06</c> MSG-OPS-008).</summary>
internal static class SecretCheck
{
    internal const string Connection = "connection";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The connection string, the highest version of <c>connection</c>; a secret not answered
    /// stops the start as unavailable, one that is not a connection string in UTF-8 as invalid.
    /// </summary>
    internal static async Task<string> ConnectionAsync(ISecretSource secrets, CancellationToken cancellationToken)
    {
        SecretVersions? read = await secrets.ReadAsync(Connection, cancellationToken).ConfigureAwait(false);
        if (read is not { Versions.Count: > 0 })
        {
            throw MessagingStartup.Failure(Codes.SecretUnavailable, ("key", Connection));
        }

        SecretVersion highest = read.Versions.MaxBy(version => version.Version)!;
        try
        {
            string connection = StrictUtf8.GetString(highest.Value.Span);
            _ = new NpgsqlConnectionStringBuilder(connection);
            return connection;
        }
        catch (Exception invalid) when (invalid is DecoderFallbackException or ArgumentException)
        {
            throw MessagingStartup.Failure(Codes.SecretInvalid, ("key", Connection));
        }
    }
}