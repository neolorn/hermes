namespace Hermes.Development;

/// <summary>Where the host mapped the inbox's group, which the start's line names (<c>02</c> MSG-DEV-001); none where it is not mapped.</summary>
internal sealed class InboxGroup
{
    /// <summary>The group's prefix as the host mapped it.</summary>
    internal string? Prefix { get; set; }
}