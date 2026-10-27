namespace Hermes.Sending;

/// <summary>What a delivery report did (<c>02</c> MSG-SMS-004).</summary>
internal enum ReportEffect
{
    /// <summary>Nothing changed.</summary>
    None,

    /// <summary>The outcome was kept on its attempt of an <c>inflight</c> delivery.</summary>
    Kept,

    /// <summary>The delivery moved to <c>delivered</c>.</summary>
    Delivered,

    /// <summary>The delivery moved to <c>failed</c>, its places under its category's per-destination limits released.</summary>
    Failed,

    /// <summary>The report was held for a reference no delivery holds yet.</summary>
    Held,

    /// <summary>The report was counted as rejected.</summary>
    Rejected,
}