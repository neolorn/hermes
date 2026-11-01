using System;
using System.Collections.Generic;

using Hermes.Core;

namespace Hermes.Conversations;

/// <summary>
/// The wire spellings of conversations' enumerations (<c>08</c> sections 2 and 3), written
/// wherever a member is written as text (a column, a body) and read back. Each table is in the
/// order of its enumeration's members.
/// </summary>
internal static class ConversationSpellings
{
    /// <summary>The only origin of release 2.</summary>
    internal const string Web = "web";

    private static readonly string[] Types = ["support", "group", "direct"];

    private static readonly string[] Statuses = ["open", "snoozed", "closed"];

    private static readonly string[] MessageTypes = ["text", "note", "automatic", "system"];

    private static readonly string[] ScopeKinds = ["organization", "group"];

    private static readonly string[] AuthorKinds = ["person", "visitor", "library"];

    internal static IReadOnlyList<string> AllTypes => Types;

    internal static IReadOnlyList<string> AllOrigins { get; } = [Web];

    internal static IReadOnlyList<string> AllStatuses => Statuses;

    internal static IReadOnlyList<string> AllMessageTypes => MessageTypes;

    internal static IReadOnlyList<string> AllScopeKinds => ScopeKinds;

    internal static IReadOnlyList<string> AllAuthorKinds => AuthorKinds;

    internal static string Of(ConversationType type)
    {
        return Types[(int)type];
    }

    internal static string Of(ConversationStatus status)
    {
        return Statuses[(int)status];
    }

    internal static string Of(MessageType type)
    {
        return MessageTypes[(int)type];
    }

    internal static string Of(ScopeKind kind)
    {
        return ScopeKinds[(int)kind];
    }

    internal static string Of(AuthorKind kind)
    {
        return AuthorKinds[(int)kind];
    }

    internal static ConversationType TypeOf(string spelling)
    {
        return (ConversationType)Index(Types, spelling);
    }

    internal static ConversationStatus StatusOf(string spelling)
    {
        return (ConversationStatus)Index(Statuses, spelling);
    }

    internal static MessageType MessageTypeOf(string spelling)
    {
        return (MessageType)Index(MessageTypes, spelling);
    }

    internal static AuthorKind AuthorKindOf(string spelling)
    {
        return (AuthorKind)Index(AuthorKinds, spelling);
    }

    internal static ScopeKind ScopeKindOf(string spelling)
    {
        return (ScopeKind)Index(ScopeKinds, spelling);
    }

    /// <summary>The scope kind <paramref name="spelling"/> spells, or none where it spells none.</summary>
    internal static ScopeKind? ReadScopeKind(string spelling)
    {
        int index = Array.IndexOf(ScopeKinds, spelling);
        return index < 0 ? null : (ScopeKind)index;
    }

    /// <summary>The message type <paramref name="spelling"/> spells, or none where it spells none.</summary>
    internal static MessageType? ReadMessageType(string spelling)
    {
        int index = Array.IndexOf(MessageTypes, spelling);
        return index < 0 ? null : (MessageType)index;
    }

    private static int Index(string[] spellings, string spelling)
    {
        int index = Array.IndexOf(spellings, spelling);
        return index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(spelling), spelling, "A spelling the table does not hold.");
    }
}