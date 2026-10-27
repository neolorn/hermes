using System;
using System.Linq;

using Hermes.Core;
using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class DeliveryRecordsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Delivery = Guid.CreateVersion7(At);

    [Theory]
    [InlineData("fax", "sent", null)]
    [InlineData("sms", "parked", null)]
    [InlineData("sms", "failed", "carrierlost")]
    public void Of_ADeliveryOfASpellingThisReleaseDoesNotKnow_IsLeftOut(string channel, string status, string? reason)
    {
        Assert.Null(DeliveryRecords.Of(Row(channel, status, reason), [], []));
    }

    [Fact]
    public void Of_AnAttemptOrCallOfASpellingThisReleaseDoesNotKnow_IsLeftOut()
    {
        AttemptRow known = new() { DeliveryId = Delivery, Number = 1, At = At, Class = "transient", Reason = "timeout" };
        AttemptRow later = new() { DeliveryId = Delivery, Number = 2, At = At, Class = "deferred" };
        AttemptRow open = new() { DeliveryId = Delivery, Number = 3, At = At };
        CallRow call = new() { DeliveryId = Delivery, Attempt = 1, Number = 1, Class = "transient", Reason = "timeout", Status = 503, PushService = "fcm", At = At };
        CallRow laterCall = new() { DeliveryId = Delivery, Attempt = 1, Number = 2, Class = "transient", PushService = "pigeon", At = At };

        DeliveryRecord record = DeliveryRecords.Of(Row("push", "inflight", null), [open, later, known], [laterCall, call])!;

        Assert.Equal([1, 3], record.Attempts.Select(attempt => attempt.Number));
        Assert.Equal(AttemptClass.Transient, record.Attempts[0].AnswerClass);
        Assert.Equal(AttemptReason.Timeout, record.Attempts[0].Reason);
        CallRecord made = Assert.Single(record.Attempts[0].Calls);
        Assert.Equal((AttemptClass.Transient, AttemptReason.Timeout, 503, PushService.Fcm), (made.AnswerClass!.Value, made.Reason!.Value, made.Status, made.Service));
        Assert.Null(record.Attempts[1].AnswerClass);
    }

    private static DeliveryRow Row(string channel, string status, string? reason)
    {
        return new DeliveryRow
        {
            Id = Delivery,
            SendId = Delivery,
            Kind = EngineHost.Kind,
            Category = EngineHost.Category,
            Channel = channel,
            Status = status,
            Reason = reason,
            Language = "en",
            TimeZone = "UTC",
            RecordedAt = At,
            DueAt = At,
            BecameDueAt = At,
            LastMovedAt = At,
        };
    }
}