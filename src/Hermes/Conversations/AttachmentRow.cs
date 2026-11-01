using System;

namespace Hermes.Conversations;

/// <summary>
/// An attachment (<c>09</c> MSG-CONV-007, MSG-CONV-009; <c>08</c> section 8): uploaded by a
/// person or a visitor into a conversation, named by one message at most, its file name
/// protected in the row and its bytes protected in chunks and stored under a key the library
/// drew. Its conversation and message are set to none when their rows go, so that the
/// <c>attachment-scans</c> job still deletes its stored bytes before its row.
/// </summary>
internal sealed class AttachmentRow
{
    public required Guid Id { get; set; }

    /// <summary>The conversation it was uploaded to, or none once that conversation's row went.</summary>
    public Guid? ConversationId { get; set; }

    /// <summary>The message that names it, or none before it is named or once that message went.</summary>
    public Guid? MessageId { get; set; }

    /// <summary>The instant a message named it, or none.</summary>
    public DateTimeOffset? NamedAt { get; set; }

    /// <summary>The person who uploaded it, or none for a visitor or once erased.</summary>
    public string? Uploader { get; set; }

    /// <summary>The keyed hash of the visitor who uploaded it, or none.</summary>
    public byte[]? Visitor { get; set; }

    /// <summary>The <c>hashKeys</c> version of <see cref="Visitor"/>, or none.</summary>
    public int? HashVersion { get; set; }

    /// <summary>The person whose key protects it, or none for a visitor's conversation.</summary>
    public string? Owner { get; set; }

    /// <summary>The file name, protected, or none once an erasure cleared it.</summary>
    public byte[]? Name { get; set; }

    /// <summary>The <c>contentKeys</c> version of the name and every chunk, or none once cleared.</summary>
    public int? ContentKeyVersion { get; set; }

    public required string MediaType { get; set; }

    public required long Size { get; set; }

    /// <summary>How many 64 KiB chunks its stored bytes hold.</summary>
    public required int Chunks { get; set; }

    /// <summary>The key its bytes are stored under, or none once they are deleted.</summary>
    public string? StoreKey { get; set; }

    public required string Status { get; set; }

    public int ScanAttempts { get; set; }

    /// <summary>The instant of its next scan, or none once scanned.</summary>
    public DateTimeOffset? ScanAt { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}