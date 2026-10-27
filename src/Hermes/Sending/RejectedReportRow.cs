using System;

namespace Hermes.Sending;

/// <summary>The rejected delivery reports of one minute (<c>02</c> MSG-SMS-004), summed over <c>Hermes:Sms:ReportRejectWindow</c>.</summary>
internal sealed class RejectedReportRow
{
    /// <summary>The minute's start.</summary>
    public required DateTimeOffset Minute { get; set; }

    public required int Count { get; set; }
}