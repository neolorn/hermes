using System;

namespace Hermes.Channels.Tests;

/// <summary>A clock that moves only when a test moves it.</summary>
internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset now = now;

    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }

    internal void Advance(TimeSpan by)
    {
        now += by;
    }
}