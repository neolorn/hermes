using System.Globalization;
using System.IO;
using System.Text;

namespace Hermes.Core;

/// <summary>
/// A clean attachment to stream (<c>09</c> MSG-CONV-007): its record and its content, read
/// from the store and opened chunk by chunk, which the caller disposes. It prints neither
/// the file name nor the content.
/// </summary>
public sealed record AttachmentDownload
{
    /// <summary>The attachment.</summary>
    public required AttachmentRecord Attachment { get; init; }

    /// <summary>Its bytes, opened as they are read; the caller disposes it.</summary>
    public required Stream Content { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Attachment = {Attachment}");
        return true;
    }
}