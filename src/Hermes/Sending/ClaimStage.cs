namespace Hermes.Sending;

/// <summary>Where a pass is in a claimed delivery's work.</summary>
internal enum ClaimStage
{
    /// <summary>Not yet begun.</summary>
    NotReached,

    /// <summary>Its checks run; a stop hands it back.</summary>
    Checking,

    /// <summary>Its renewal has begun; a stop lets its call finish.</summary>
    Renewing,

    /// <summary>Handed back, or its work is done.</summary>
    Done,
}