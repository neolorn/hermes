using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>
/// Reads a person's in-application list and marks its entries read (<c>02</c>
/// MSG-INAPP-003), every call under the person's locks taken shared, waiting for them no
/// longer than <c>Hermes:Dispatch:Timeout</c>.
/// </summary>
public interface IInApp
{
    /// <summary>Reads a page of the person's list, newest first, in the language asked for.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="language">The language to read in, matched to the declared languages as a send's is; none, or one that matches none, reads in <c>Hermes:DefaultLanguage</c>.</param>
    /// <param name="cursor">The cursor of the page to read, as an earlier page answered it, or none for the first.</param>
    /// <param name="size">The most entries the page holds, 1 to 100, or none for 20.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page; or a failure: <c>messaging.inapp.cursorinvalid</c> or <c>messaging.key.unavailable</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">A size outside 1 to 100.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result<InAppPage>> ListAsync(string person, string? language, string? cursor, int? size, CancellationToken cancellationToken);

    /// <summary>Counts the person's entries not yet read.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The number of the person's entries with no read instant.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<int> UnreadCountAsync(string person, CancellationToken cancellationToken);

    /// <summary>Marks one of the person's entries read and moves its delivery from <c>sent</c> to <c>read</c>, in one transaction; an entry already read changes nothing.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure, changing nothing: <c>messaging.inapp.unknown</c>, for an entry that does not exist or is another person's.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> MarkReadAsync(string person, EntryId entry, CancellationToken cancellationToken);

    /// <summary>Marks every entry of the person not yet read, read, and moves their deliveries to <c>read</c>, in one transaction.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The number of entries it marked.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<int> MarkAllReadAsync(string person, CancellationToken cancellationToken);
}