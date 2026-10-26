using System.ComponentModel.DataAnnotations;

namespace Hermes.Channels;

/// <summary><c>Hermes:Email</c>, the email channel's own keys (<c>08</c> section 4).</summary>
internal sealed class EmailChannelOptions
{
    public EmailFromOptions From { get; set; } = new();

    [Range(1, 1_000)]
    public int Rate { get; set; } = 10;

    [Range(10 * 1024, 25 * 1024 * 1024)]
    public int MaxBytes { get; set; } = 1024 * 1024;
}