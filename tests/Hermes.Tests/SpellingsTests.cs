using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hermes.Core;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class SpellingsTests
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Of_EveryChannel_IsItsWireSpelling()
    {
        AssertSpelled(Spellings.AllChannels, Spellings.Of, Spellings.Channel);
    }

    [Fact]
    public void Of_EveryStatus_IsItsWireSpelling()
    {
        AssertSpelled(Spellings.AllStatuses, Spellings.Of, Spellings.Status);
    }

    [Fact]
    public void Of_EveryClass_IsItsWireSpelling()
    {
        AssertSpelled(Spellings.AllClasses, Spellings.Of, Spellings.Class);
    }

    [Fact]
    public void Of_EveryReason_IsItsWireSpelling()
    {
        AssertSpelled(Spellings.AllReasons, Spellings.Of, Spellings.Reason);
    }

    [Fact]
    public void Of_EveryPushService_IsItsWireSpelling()
    {
        AssertSpelled(Spellings.AllServices, Spellings.Of, Spellings.Service);
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("fax")]
    [InlineData("")]
    public void Channel_ASpellingThisReleaseDoesNotKnow_IsNone(string spelling)
    {
        Assert.Null(Spellings.Channel(spelling));
    }

    private static void AssertSpelled<T>(IReadOnlyList<string> all, Func<T, string> of, Func<string, T?> parse)
        where T : struct, Enum
    {
        T[] members = Enum.GetValues<T>();

        Assert.Equal(members.Length, all.Count);
        foreach (T member in members)
        {
            string wire = JsonSerializer.Serialize(member, Json).Trim('"');
            Assert.Equal(wire, of(member));
            Assert.Equal(member, parse(wire));
        }

        Assert.Equal(members.Select(of), all);
    }
}