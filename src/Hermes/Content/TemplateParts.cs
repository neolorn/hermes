using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Content;

/// <summary>The parts of each channel's template (<c>03</c> MSG-TPL-001), named as <c>08</c> section 8 gives them.</summary>
internal static class TemplateParts
{
    internal const string Subject = "subject";

    internal const string Text = "text";

    internal const string Html = "html";

    internal const string Title = "title";

    internal const string Body = "body";

    internal const string Url = "url";

    internal const string Tag = "tag";

    internal const string Icon = "icon";

    internal const string Badge = "badge";

    private static readonly string[] EmailRequired = [Subject, Text];

    private static readonly string[] EmailOptional = [Html];

    private static readonly string[] SmsRequired = [Text];

    private static readonly string[] PushRequired = [Title, Body];

    private static readonly string[] PushOptional = [Url, Tag, Icon, Badge];

    private static readonly string[] InAppRequired = [Title];

    private static readonly string[] InAppOptional = [Body];

    /// <summary>The parts a channel's template must have.</summary>
    internal static IReadOnlyList<string> Required(Channel channel)
    {
        return channel switch
        {
            Channel.Email => EmailRequired,
            Channel.Sms => SmsRequired,
            Channel.InApp => InAppRequired,
            _ => PushRequired,
        };
    }

    /// <summary>The parts a channel's template may have.</summary>
    internal static IReadOnlyList<string> Optional(Channel channel)
    {
        return channel switch
        {
            Channel.Email => EmailOptional,
            Channel.Sms => [],
            Channel.InApp => InAppOptional,
            _ => PushOptional,
        };
    }
}