using System;
using System.Collections.Generic;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes;

/// <summary>
/// Declares the rules every kind of a category shares: whether a person may turn it off,
/// whether it needs consent, whether quiet hours apply, its limits and its labels. A method
/// that sets one value, called again, replaces it; each limit is added to those before.
/// </summary>
public sealed class CategoryBuilder
{
    private readonly string name;
    private readonly List<LimitDeclaration> limits = [];
    private readonly List<LimitDeclaration> deploymentLimits = [];
    private readonly Dictionary<string, string> labels = new(StringComparer.OrdinalIgnoreCase);
    private bool essential;
    private string? consentPurpose;
    private bool? quietHours;

    internal CategoryBuilder(string name)
    {
        this.name = name;
    }

    /// <summary>
    /// Makes the category essential: the person cannot turn it off, no suppression applies to
    /// it, quiet hours never apply to it, and its SMS goes out below the balance floor. An
    /// essential category declares neither a consent purpose nor quiet hours.
    /// </summary>
    /// <returns>This builder.</returns>
    public CategoryBuilder Essential()
    {
        essential = true;
        return this;
    }

    /// <summary>Makes each delivery of the optional category need the recipient's consent to <paramref name="purpose"/> when it is dispatched.</summary>
    /// <param name="purpose">The purpose, which follows the rule of a kind's name.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="purpose"/> is null.</exception>
    public CategoryBuilder RequiresConsent(string purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        consentPurpose = purpose;
        return this;
    }

    /// <summary>Declares whether quiet hours apply to the optional category; by default they do where it requires consent.</summary>
    /// <param name="apply">Whether they apply.</param>
    /// <returns>This builder.</returns>
    public CategoryBuilder QuietHours(bool apply)
    {
        quietHours = apply;
        return this;
    }

    /// <summary>
    /// Adds a cap per destination, or per person for push: at most <paramref name="count"/>
    /// deliveries on <paramref name="channel"/> within <paramref name="window"/>.
    /// </summary>
    /// <param name="channel">A channel a kind of the category uses.</param>
    /// <param name="count">The count, at least 1.</param>
    /// <param name="window">The window, 1 minute to <c>Hermes:Retention:Places</c>, no other cap on the channel having the same.</param>
    /// <returns>This builder.</returns>
    public CategoryBuilder Limit(Channel channel, int count, TimeSpan window)
    {
        limits.Add(new(channel, count, window));
        return this;
    }

    /// <summary>
    /// Adds a cap across every destination: at most <paramref name="count"/> deliveries on
    /// <paramref name="channel"/> within <paramref name="window"/>.
    /// </summary>
    /// <param name="channel">A channel a kind of the category uses.</param>
    /// <param name="count">The count, at least 1.</param>
    /// <param name="window">The window, 1 minute to <c>Hermes:Retention:Places</c>, no other deployment cap on the channel having the same.</param>
    /// <returns>This builder.</returns>
    public CategoryBuilder DeploymentLimit(Channel channel, int count, TimeSpan window)
    {
        deploymentLimits.Add(new(channel, count, window));
        return this;
    }

    /// <summary>
    /// Declares the category's name as people read it in <paramref name="language"/>, used by
    /// the library's own pages; an optional category with an email kind declares one in each
    /// language of <c>Hermes:Languages</c>.
    /// </summary>
    /// <param name="language">A BCP 47 tag of <c>Hermes:Languages</c>, matched whatever its case.</param>
    /// <param name="text">The label, at most 128 characters counted as Unicode scalar values.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="language"/> or <paramref name="text"/> is null.</exception>
    public CategoryBuilder Label(string language, string text)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(text);
        labels[language] = text;
        return this;
    }

    internal CategoryDeclaration Declaration()
    {
        return new(name, essential, consentPurpose, quietHours, [.. limits], [.. deploymentLimits], new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase));
    }
}