using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The keys under <c>Hermes:Conversations:</c> the package does not know (M-031; <c>06</c> MSG-OPS-001).</summary>
[Trait("Category", "unit")]
public sealed class ConversationsConfigurationTests
{
    [Fact]
    public void UnknownKey_NoKeyUnderConversations_IsNone()
    {
        Assert.Null(ConversationsConfiguration.UnknownKey(Hermes(new() { ["Hermes:Languages:0"] = "en" })));
    }

    [Fact]
    public void UnknownKey_ASectionGivenAnEmptyValueAsACommandLineGivesItsParents_IsTheKeyBeneathIt()
    {
        Dictionary<string, string?> settings = new() { ["Hermes:Conversations"] = string.Empty, ["Hermes:Conversations:Unknown"] = "on" };

        Assert.Equal("Conversations:Unknown", ConversationsConfiguration.UnknownKey(Hermes(settings)));
    }

    [Fact]
    public void UnknownKey_TheSectionItselfHoldingAnEmptyValue_IsTheSection()
    {
        Assert.Equal("Conversations", ConversationsConfiguration.UnknownKey(Hermes(new() { ["Hermes:Conversations"] = string.Empty })));
    }

    [Fact]
    public void UnknownKey_SeveralKeys_IsTheFirstInOrdinalOrder()
    {
        Dictionary<string, string?> settings = new() { ["Hermes:Conversations:b"] = "on", ["Hermes:Conversations:C"] = "on", ["Hermes:Conversations:A:x"] = "on" };

        Assert.Equal("Conversations:A:x", ConversationsConfiguration.UnknownKey(Hermes(settings)));
    }

    private static IConfigurationSection Hermes(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection("Hermes");
    }
}