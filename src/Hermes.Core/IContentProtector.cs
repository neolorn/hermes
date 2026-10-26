using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Protects the values the library stores for a person (<c>01</c> MSG-SEND-018).</summary>
public interface IContentProtector
{
    /// <summary>The protector's identifier: 1 to 32 lower-case letters and digits, never <c>messaging</c>.</summary>
    string Id { get; }

    /// <summary>Protects <paramref name="plaintext"/> for <paramref name="person"/>, bound to <paramref name="context"/>.</summary>
    /// <param name="person">The person's reference.</param>
    /// <param name="plaintext">The value.</param>
    /// <param name="context">The table, row and field the value is stored in.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The protected value.</returns>
    ValueTask<byte[]> ProtectAsync(string person, ReadOnlyMemory<byte> plaintext, string context, CancellationToken cancellationToken);

    /// <summary>Opens a value <see cref="ProtectAsync"/> made, under the same person and context.</summary>
    /// <param name="person">The person's reference.</param>
    /// <param name="protectedValue">The protected value.</param>
    /// <param name="context">The table, row and field the value is stored in.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The value, or why it does not open.</returns>
    ValueTask<UnprotectResult> UnprotectAsync(string person, ReadOnlyMemory<byte> protectedValue, string context, CancellationToken cancellationToken);
}