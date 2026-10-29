using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Hermes.Development;

/// <summary>The notification a captured push's payload holds (<c>02</c> MSG-PUSH-002).</summary>
public sealed record InboxNotification
{
    /// <summary>The notification's title.</summary>
    public required string Title { get; init; }

    /// <summary>The notification's body.</summary>
    public required string Body { get; init; }

    /// <summary>The address the notification opens, none where it has none.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "04 MSG-LIB-002 gives it as the payload's text, which no rule makes an absolute address.")]
    public string? Url { get; init; }

    /// <summary>The notification's tag, none where it has none.</summary>
    public string? Tag { get; init; }

    /// <summary>The notification's language.</summary>
    public string? Lang { get; init; }

    /// <summary>The notification's direction, <c>ltr</c> or <c>rtl</c>.</summary>
    public string? Dir { get; init; }

    /// <summary>The notification's icon, none where it has none.</summary>
    public string? Icon { get; init; }

    /// <summary>The notification's badge, none where it has none.</summary>
    public string? Badge { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Lang = {Lang}, Dir = {Dir}");
        return true;
    }
}