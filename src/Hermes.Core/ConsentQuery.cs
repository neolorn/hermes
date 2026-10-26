using System.Text;

namespace Hermes.Core;

/// <summary>What a consent source is asked; it prints neither the recipient nor the destination.</summary>
public sealed record ConsentQuery
{
    /// <summary>The recipient.</summary>
    public required Recipient Recipient { get; init; }

    /// <summary>The destination the delivery goes to, or none.</summary>
    public string? Destination { get; init; }

    /// <summary>The purpose the category requires consent to.</summary>
    public required string Purpose { get; init; }

    /// <summary>The channel the delivery goes out on.</summary>
    public required Channel Channel { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Purpose = ").Append(Purpose).Append(", Channel = ").Append(Channel);
        return true;
    }
}