using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>An attachment of a message in a person's export (<c>09</c> MSG-CONV-010): its name, media type and size, never its bytes. It prints no name.</summary>
public sealed record AttachmentExport
{
    /// <summary>Its file name.</summary>
    public required string Name { get; init; }

    /// <summary>Its media type.</summary>
    public required string MediaType { get; init; }

    /// <summary>Its size in bytes.</summary>
    public required long Size { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"MediaType = {MediaType}, Size = {Size}");
        return true;
    }
}