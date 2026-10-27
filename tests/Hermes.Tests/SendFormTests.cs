using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class SendFormTests
{
    [Fact]
    public void Of_ASend_IsTheBytesOfMsgSend009InTheirOrder()
    {
        Send send = new()
        {
            Kind = "order.placed",
            Recipient = new Recipient.Person("p-1"),
            Email = "a@example.com",
            Values = new Dictionary<string, SendValue>
            {
                ["total"] = new SendValue.DecimalNumber(12.50m),
                ["count"] = new SendValue.WholeNumber(-2),
                ["paid"] = new SendValue.Flag(true),
                ["at"] = new SendValue.Instant(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(2))),
                ["lines"] = new SendValue.List([new Dictionary<string, SendValue> { ["name"] = new SendValue.PlainText("x") }]),
            },
            Channels = [Channel.Sms, Channel.Email],
            NotBefore = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
        };

        List<byte> expected = [0x01];
        expected.AddRange([0x01, .. Text("order.placed")]);
        expected.AddRange([0x02, .. Text("p-1")]);
        expected.AddRange([0x01, .. Text("a@example.com")]);
        expected.Add(0x00);
        expected.Add(0x00);
        expected.Add(0x00);
        expected.Add(0x09);
        expected.AddRange(Count(5));
        expected.AddRange([.. Text("at"), 0x07, .. Long(new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc).Ticks)]);
        expected.AddRange([.. Text("count"), 0x04, .. Long(-2)]);
        expected.AddRange([.. Text("lines"), 0x08, .. Count(1), .. Count(1), .. Text("name"), 0x01, .. Text("x")]);
        expected.AddRange([.. Text("paid"), 0x06, 0x01]);
        expected.AddRange([.. Text("total"), 0x05, .. Text("12.5")]);
        expected.AddRange([0x0A, .. Count(2), .. Text("email"), .. Text("sms")]);
        expected.AddRange([0x07, .. Long(new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc).Ticks)]);
        expected.Add(0x00);

        Assert.Equal(expected, SendForm.Of(send));
    }

    [Fact]
    public void Of_AnAnonymousSendWithNoChannelsNamed_WritesTheirAbsence()
    {
        Send send = new() { Kind = "k", Recipient = new Recipient.Anonymous(), Values = new Dictionary<string, SendValue>(), PushTopic = "t" };

        byte[] form = SendForm.Of(send);

        Assert.Equal([0x01, 0x01, .. Text("k"), 0x03, 0x00, 0x00, 0x00, 0x00, 0x09, .. Count(0), 0x00, 0x00, 0x01, .. Text("t")], form);
    }

    [Fact]
    public void MSG_SEND_009_AC2_TheSameValuesWrittenInAnotherOrderHaveOneForm()
    {
        Send first = Send(new Dictionary<string, SendValue> { ["a"] = new SendValue.PlainText("1"), ["b"] = new SendValue.WholeNumber(2) }, [Channel.Email, Channel.Sms]);
        Send second = Send(new Dictionary<string, SendValue> { ["b"] = new SendValue.WholeNumber(2), ["a"] = new SendValue.PlainText("1") }, [Channel.Sms, Channel.Email]);

        Assert.Equal(SendForm.Of(first), SendForm.Of(second));
    }

    [Fact]
    public void MSG_SEND_009_AC2_AnotherValueHasAnotherForm()
    {
        Send first = Send(new Dictionary<string, SendValue> { ["a"] = new SendValue.PlainText("1") }, null);
        Send second = Send(new Dictionary<string, SendValue> { ["a"] = new SendValue.PlainText("2") }, null);

        Assert.NotEqual(SendForm.Of(first), SendForm.Of(second));
    }

    [Theory]
    [InlineData("1.50", "1.5")]
    [InlineData("2.0", "2")]
    [InlineData("100", "100")]
    [InlineData("-0.010", "-0.01")]
    [InlineData("0.000", "0")]
    public void DecimalText_ADecimal_LosesItsTrailingZerosAndPoint(string value, string expected)
    {
        Assert.Equal(expected, SendForm.DecimalText(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Of_KeyedHash_IsHmacSha256OfTheDomainAZeroByteAndTheValue()
    {
        byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes("hash key"));
        byte[] value = Encoding.UTF8.GetBytes("+201000000000");

        byte[] hash = KeyedHashes.Of(key, KeyedHashes.Sms, value);

        byte[] message = [.. Encoding.ASCII.GetBytes("sms"), 0x00, .. value];
        Assert.Equal(HMACSHA256.HashData(key, message), hash);
    }

    [Fact]
    public void Every_SeveralHeldVersions_HashesUnderEachInAscendingOrder()
    {
        byte[] one = SHA256.HashData(Encoding.UTF8.GetBytes("one"));
        byte[] two = SHA256.HashData(Encoding.UTF8.GetBytes("two"));
        HeldKeys held = new();
        held.Hold([new HeldKey(KeyNeeds.HashKeys, new Dictionary<int, byte[]> { [2] = two, [1] = one }, 2)]);
        KeyedHashes hashes = new(held);
        byte[] value = Encoding.UTF8.GetBytes("p-1");

        IReadOnlyList<(int Version, byte[] Hash)> every = hashes.Every(KeyedHashes.Person, value);

        Assert.Equal([1, 2], every.Select(hash => hash.Version));
        Assert.Equal(KeyedHashes.Of(one, "person", value), every[0].Hash);
        Assert.Equal(KeyedHashes.Of(two, "person", value), hashes.Write("person", value));
    }

    [Fact]
    public void Idempotency_AKindAndKey_IsTheFirstEightBytesOfTheirSha256ReadBigEndian()
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes("idempotency\0order.placed\0key-1"));

        Assert.Equal(BinaryPrimitives.ReadInt64BigEndian(digest), AdvisoryLocks.Idempotency("order.placed", "key-1"));
    }

    [Fact]
    public void Person_SeveralHeldVersions_LocksTheSha256OfEachVersionsHashInAscendingOrder()
    {
        byte[] one = SHA256.HashData(Encoding.UTF8.GetBytes("one"));
        byte[] two = SHA256.HashData(Encoding.UTF8.GetBytes("two"));
        HeldKeys held = new();
        held.Hold([new HeldKey(KeyNeeds.HashKeys, new Dictionary<int, byte[]> { [2] = two, [1] = one }, 2)]);
        byte[] value = Encoding.UTF8.GetBytes("p-1");

        IReadOnlyList<long> locks = AdvisoryLocks.Person(new KeyedHashes(held), "p-1");

        Assert.Equal(
            [BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(KeyedHashes.Of(one, "person", value))), BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(KeyedHashes.Of(two, "person", value)))],
            locks);
    }

    private static Send Send(Dictionary<string, SendValue> values, IReadOnlyList<Channel>? channels)
    {
        return new Send { Kind = "k", Recipient = new Recipient.Person("p"), Values = values, Channels = channels };
    }

    private static byte[] Text(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return [.. Count(bytes.Length), .. bytes];
    }

    private static byte[] Count(int count)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, count);
        return bytes;
    }

    private static byte[] Long(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}