using System;
using System.Text;

using Xunit;

namespace Hermes.Core.Tests;

[Trait("Category", "unit")]
public sealed class PrintMembersTests
{
    private const string Secret = "person-7f3a@example.test";

    [Fact]
    public void MSG_LIB_002_ConsentQueryPrintsNeitherItsRecipientNorItsDestination()
    {
        ConsentQuery query = new() { Recipient = new Recipient.Person(Secret), Destination = Secret, Purpose = "marketing", Channel = Channel.Email };

        string printed = query.ToString();

        Assert.DoesNotContain(Secret, printed, StringComparison.Ordinal);
        Assert.Contains("marketing", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_PersonPrintsNoReference()
    {
        Assert.DoesNotContain(Secret, new Recipient.Person(Secret).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_DeliveryNoticePrintsNoPerson()
    {
        DeliveryNotice notice = new()
        {
            Send = new SendId(Guid.CreateVersion7()),
            Delivery = new DeliveryId(Guid.CreateVersion7()),
            Kind = "order.placed",
            Category = "orders",
            Channel = Channel.Sms,
            Person = Secret,
            Status = DeliveryStatus.Sent,
            At = DateTimeOffset.UnixEpoch,
        };

        string printed = notice.ToString();

        Assert.DoesNotContain(Secret, printed, StringComparison.Ordinal);
        Assert.Contains("order.placed", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_SecretVersionPrintsNoValue()
    {
        SecretVersion version = new() { Version = 3, Value = Encoding.UTF8.GetBytes(Secret) };

        string printed = version.ToString();

        Assert.DoesNotContain("Value", printed, StringComparison.Ordinal);
        Assert.Contains("Version = 3", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_OpenedPrintsNoPlaintext()
    {
        Assert.DoesNotContain("Plaintext", new UnprotectResult.Opened(Encoding.UTF8.GetBytes(Secret)).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_EmailMessagePrintsNeitherItsDestinationNorItsContentNorItsUnsubscribeAddress()
    {
        EmailMessage message = new()
        {
            From = new EmailAddress { Address = "news@example.test" },
            ToAddress = Secret,
            Subject = Secret,
            Text = Secret,
            Html = Secret,
            MessageId = "0192b6a4-0000-7000-8000-000000000001@example.test",
            Unsubscribe = new Uri("https://example.test/unsubscribe/" + Uri.EscapeDataString(Secret)),
            Language = "en",
        };

        string printed = message.ToString();

        Assert.DoesNotContain("person-7f3a", printed, StringComparison.Ordinal);
        Assert.Contains("0192b6a4-0000-7000-8000-000000000001@example.test", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_SmsMessagePrintsNeitherItsNumberNorItsTextNorItsReferences()
    {
        SmsMessage message = new()
        {
            ToNumber = "+201000000007",
            Sender = "Example",
            Text = Secret,
            DeliveryReference = "c2VjcmV0LXJlZmVyZW5jZQ",
            Encoding = SmsEncoding.Gsm7,
            Segments = 1,
            ReportAddress = new Uri("https://example.test/sms/report/a2V5LWluLXRoZS1wYXRo"),
        };

        string printed = message.ToString();

        Assert.DoesNotContain("+201000000007", printed, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, printed, StringComparison.Ordinal);
        Assert.DoesNotContain("c2VjcmV0LXJlZmVyZW5jZQ", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("a2V5LWluLXRoZS1wYXRo", printed, StringComparison.Ordinal);
        Assert.Contains("Example", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void MSG_LIB_002_WebPushPrintsNeitherItsEndpointNorItsKeysNorItsPayloadNorItsTopic()
    {
        PushRequest request = new PushRequest.WebPush(
            "https://fcm.googleapis.com/fcm/send/c3Vic2NyaXB0aW9u",
            "cDI1NmRoLWtleQ",
            "YXV0aC1zZWNyZXQ",
            2,
            Encoding.UTF8.GetBytes(Secret),
            TimeSpan.FromHours(1),
            PushUrgency.High,
            "dG9waWMtb2YtdGhlLXNlbmQ");

        string printed = request.ToString();

        Assert.DoesNotContain("c3Vic2NyaXB0aW9u", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("cDI1NmRoLWtleQ", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("YXV0aC1zZWNyZXQ", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("Payload", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("dG9waWMtb2YtdGhlLXNlbmQ", printed, StringComparison.Ordinal);
        Assert.Contains("KeyVersion = 2", printed, StringComparison.Ordinal);
        Assert.Equal(PushPlatform.WebPush, request.Platform);
    }
}