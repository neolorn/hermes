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
}