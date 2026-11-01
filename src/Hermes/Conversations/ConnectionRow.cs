using System;

namespace Hermes.Conversations;

/// <summary>
/// A connection to the staff hub (<c>09</c> MSG-CONV-006): its person, the process holding it,
/// and the instant that process last saw it. A person is online while a row of theirs is
/// fresh; a process refreshes its rows every 30 seconds, and a row older than 90 is gone.
/// </summary>
internal sealed class ConnectionRow
{
    public required Guid Id { get; set; }

    public required string Person { get; set; }

    /// <summary>The process holding the connection.</summary>
    public required Guid Process { get; set; }

    public required DateTimeOffset LastSeenAt { get; set; }
}