using System;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The call that registers the package, before any host is built (M-031; <c>07</c> MSG-ENG-003).</summary>
[Trait("Category", "unit")]
public sealed class RegistrationCallTests
{
    [Fact]
    public void MSG_LIB_003_AC7_AddHermesConversationsWithoutAddHermesThrowsAtTheCallNamingIt()
    {
        ServiceCollection services = new();

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => services.AddHermesConversations());

        Assert.Contains("AddHermes ", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHermesConversations_NoServices_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => MessagingConversationsServiceCollectionExtensions.AddHermesConversations(null!));
    }
}