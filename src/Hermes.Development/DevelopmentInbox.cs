using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Development;

/// <summary>
/// Reads, waits for and clears the messages the development inbox captured, without HTTP, for
/// a .NET test or tool (<c>02</c> MSG-DEV-006): the <c>.json</c> records of its folder, an
/// email's <c>.eml</c> beside its record. A filter's <see cref="InboxFilter.To"/> matches an
/// email address in any case and a number exactly, and its <see cref="InboxFilter.After"/> is
/// exclusive.
/// </summary>
public sealed class DevelopmentInbox
{
    /// <summary>How often <see cref="WaitForAsync"/> looks again.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

    private readonly TimeProvider clock;

    /// <summary>Reads the default folder: <c>hermes-inbox</c> in the operating system's temporary folder.</summary>
    public DevelopmentInbox()
        : this(InboxSettings.DefaultFolder)
    {
    }

    /// <summary>Reads <paramref name="folder"/>, the folder <c>Hermes:DevelopmentInbox:Folder</c> names.</summary>
    /// <param name="folder">The inbox's folder, an absolute path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="folder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is not an absolute path.</exception>
    public DevelopmentInbox(string folder)
        : this(folder, TimeProvider.System)
    {
    }

    internal DevelopmentInbox(string folder, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!Path.IsPathFullyQualified(folder))
        {
            throw new ArgumentException("The inbox's folder is an absolute path.", nameof(folder));
        }

        Folder = Path.GetFullPath(folder);
        this.clock = clock;
        DateTimeOffset now = clock.GetUtcNow();
        MadeAt = now.AddTicks(-(now.UtcTicks % TimeSpan.TicksPerMillisecond));
    }

    /// <summary>The folder read.</summary>
    public string Folder { get; }

    /// <summary>The instant, to the millisecond, this inbox was made; a wait whose filter gives no <see cref="InboxFilter.After"/> answers only a message captured from it on.</summary>
    public DateTimeOffset MadeAt { get; }

    /// <summary>The messages that match <paramref name="filter"/>, newest first.</summary>
    /// <param name="filter">The messages to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching messages, newest first; none where the folder does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is null.</exception>
    public ValueTask<IReadOnlyList<InboxMessage>> ListAsync(InboxFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(InboxFiles.List(Folder, filter, int.MaxValue));
    }

    /// <summary>The newest message that matches <paramref name="filter"/>, or none.</summary>
    /// <param name="filter">The messages to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The newest matching message, or null where none matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is null.</exception>
    public ValueTask<InboxMessage?> LatestAsync(InboxFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(InboxFiles.List(Folder, filter, 1) is [var newest, ..] ? newest : null);
    }

    /// <summary>
    /// Waits for a message that matches <paramref name="filter"/> and was captured after its
    /// <see cref="InboxFilter.After"/>, or, where it gives none, from <see cref="MadeAt"/> on,
    /// looking every 100 milliseconds, and answers the newest as soon as there is one.
    /// </summary>
    /// <param name="filter">The message to wait for.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The newest matching message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="TimeoutException">No message matched once <paramref name="timeout"/> had passed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ValueTask<InboxMessage> WaitForAsync(InboxFilter filter, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        return WaitAsync(filter.After is null ? filter with { After = MadeAt.AddTicks(-1) } : filter, timeout, cancellationToken);
    }

    /// <summary>Deletes every record of the folder with its <c>.eml</c>; every other file is left.</summary>
    /// <param name="cancellationToken">Cancels the clearing.</param>
    /// <returns>A task that completes once the folder is cleared.</returns>
    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InboxFiles.Clear(Folder);
        return ValueTask.CompletedTask;
    }

    private async ValueTask<InboxMessage> WaitAsync(InboxFilter filter, TimeSpan timeout, CancellationToken cancellationToken)
    {
        long started = clock.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (InboxFiles.List(Folder, filter, 1) is [var message, ..])
            {
                return message;
            }

            TimeSpan left = timeout - clock.GetElapsedTime(started);
            if (left <= TimeSpan.Zero)
            {
                throw new TimeoutException($"No message of the development inbox matched within {timeout}.");
            }

            await Task.Delay(left < Poll ? left : Poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }
}