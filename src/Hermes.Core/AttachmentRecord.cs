using System.Globalization;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// An attachment (<c>09</c> MSG-CONV-007; <c>05</c> MSG-API-009, MSG-API-010): its file name,
/// media type, size and status. It prints no file name.
/// </summary>
public sealed record AttachmentRecord
{
    /// <summary>The attachment's identifier.</summary>
    public required AttachmentId Attachment { get; init; }

    /// <summary>Its file name, 1 to 255 characters.</summary>
    public required string Name { get; init; }

    /// <summary>Its media type, one of <c>Hermes:Conversations:Attachments:Types</c>.</summary>
    public required string MediaType { get; init; }

    /// <summary>Its size in bytes.</summary>
    public required long Size { get; init; }

    /// <summary>Its status.</summary>
    public required AttachmentStatus Status { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Attachment = {Attachment.Value}, MediaType = {MediaType}, Size = {Size}, Status = {Status}");
        return true;
    }
}