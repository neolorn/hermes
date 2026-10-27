using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Suppresses a destination for a category, and withdraws a suppression (<c>01</c> MSG-SEND-012).</summary>
public interface ISuppressions
{
    /// <summary>
    /// Suppresses <paramref name="destination"/> on <paramref name="channel"/> for
    /// <paramref name="category"/>, keeping only its keyed hash, so every later delivery of the
    /// category to it ends <c>suppressed</c>, whoever the recipient.
    /// </summary>
    /// <param name="channel">The channel: <see cref="Channel.Email"/> or <see cref="Channel.Sms"/>.</param>
    /// <param name="destination">The address or number.</param>
    /// <param name="category">A declared optional category.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// Success, whether or not it was already suppressed; or a failure, changing nothing:
    /// <c>messaging.suppression.essential</c>, <c>messaging.suppression.invalid</c> or
    /// <c>messaging.key.unavailable</c>.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    ValueTask<Result> AddAsync(Channel channel, string destination, string category, CancellationToken cancellationToken);

    /// <summary>Withdraws the suppression of <paramref name="destination"/> on <paramref name="channel"/> for <paramref name="category"/>, under every <c>hashKeys</c> version.</summary>
    /// <param name="channel">The channel: <see cref="Channel.Email"/> or <see cref="Channel.Sms"/>.</param>
    /// <param name="destination">The address or number.</param>
    /// <param name="category">A declared category.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// Success, whether or not one was held; or a failure, removing nothing:
    /// <c>messaging.suppression.invalid</c> or <c>messaging.key.unavailable</c>.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    ValueTask<Result> RemoveAsync(Channel channel, string destination, string category, CancellationToken cancellationToken);
}