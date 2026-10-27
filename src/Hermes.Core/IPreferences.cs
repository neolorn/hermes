using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Core;

/// <summary>Reads and sets whether a person receives each optional category on each channel (<c>01</c> MSG-SEND-010).</summary>
public interface IPreferences
{
    /// <summary>Reads the person's preferences: every declared category, in declaration order, with the channels its kinds use, each on unless the person turned it off.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preferences.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    ValueTask<IReadOnlyList<CategoryPreferences>> ReadAsync(string person, CancellationToken cancellationToken);

    /// <summary>
    /// Sets whether the person receives <paramref name="category"/> on
    /// <paramref name="channel"/>, under the person's locks taken shared, waiting for them no
    /// longer than <c>Hermes:Dispatch:Timeout</c>; it changes only deliveries checked after it.
    /// </summary>
    /// <param name="person">The person reference.</param>
    /// <param name="category">A declared optional category.</param>
    /// <param name="channel">A channel the category's kinds use.</param>
    /// <param name="isOn">Whether the person receives it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success; or a failure, changing nothing: <c>messaging.preference.unknown</c> or <c>messaging.preference.essential</c>.</returns>
    /// <exception cref="System.ArgumentException">A person reference outside its bounds.</exception>
    /// <exception cref="System.InvalidOperationException">The library's startup checks have not completed.</exception>
    /// <exception cref="System.Data.Common.DbException">The person's locks were not had within <c>Hermes:Dispatch:Timeout</c>.</exception>
    ValueTask<Result> SetAsync(string person, string category, Channel channel, bool isOn, CancellationToken cancellationToken);
}