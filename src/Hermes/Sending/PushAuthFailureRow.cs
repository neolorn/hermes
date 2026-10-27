using System;

namespace Hermes.Sending;

/// <summary>The 400, 401 and 403 answers of one push service in one minute (<c>02</c> MSG-PUSH-005), summed over <c>Hermes:Push:AuthFailWindow</c>.</summary>
internal sealed class PushAuthFailureRow
{
    public required string Service { get; set; }

    /// <summary>The minute's start.</summary>
    public required DateTimeOffset Minute { get; set; }

    public required int Count { get; set; }
}