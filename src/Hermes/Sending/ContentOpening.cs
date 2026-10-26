using System;

namespace Hermes.Sending;

/// <summary>What opening a stored value answers (<c>01</c> MSG-SEND-018).</summary>
internal enum ContentOpeningStatus
{
    /// <summary>The value opened.</summary>
    Opened,

    /// <summary>The person's key was erased.</summary>
    Erased,

    /// <summary>The value cannot open yet: its key or protector is not held here, or a newer release wrote its format.</summary>
    Held,

    /// <summary>The value does not open under its binding, or is in no format.</summary>
    Rejected,
}

/// <summary>What opening a stored value answers, with the value where it opened.</summary>
/// <param name="Status">How it answered.</param>
/// <param name="Plaintext">The value, where it opened.</param>
/// <param name="Key">For a value held, the secret or the protector's <c>Id</c> it waits for; none for a format kept for a later release.</param>
internal readonly record struct ContentOpening(ContentOpeningStatus Status, ReadOnlyMemory<byte> Plaintext, string? Key)
{
    internal static ContentOpening Rejected => new(ContentOpeningStatus.Rejected, ReadOnlyMemory<byte>.Empty, null);

    internal static ContentOpening Erased => new(ContentOpeningStatus.Erased, ReadOnlyMemory<byte>.Empty, null);

    internal static ContentOpening Opened(ReadOnlyMemory<byte> plaintext)
    {
        return new ContentOpening(ContentOpeningStatus.Opened, plaintext, null);
    }

    internal static ContentOpening Held(string? key)
    {
        return new ContentOpening(ContentOpeningStatus.Held, ReadOnlyMemory<byte>.Empty, key);
    }

    /// <summary>Prints the status and the key, never the value.</summary>
    public override string ToString()
    {
        return Key is null ? Status.ToString() : $"{Status} ({Key})";
    }
}