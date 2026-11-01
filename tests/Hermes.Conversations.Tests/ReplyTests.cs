using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>
/// Automatic replies (<c>09</c> MSG-CONV-003): rules under <c>Hermes:Conversations:Replies</c>
/// on the topics of <see cref="SupportTests"/>, their templates served by the test host's
/// source.
/// </summary>
[Trait("Category", "integration")]
public sealed class ReplyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_CONV_003_AC2_ARulesMissingTemplateFailsTheStartNamingIt()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => StartAsync(configuration =>
        {
            configuration["Hermes:Conversations:Replies:welcome:Trigger"] = "started";
            configuration["Hermes:Conversations:Replies:missing:Trigger"] = "closing";
        }));

        Assert.Equal(("messaging.startup.templatemissing", "missing", "en"), (failure.Code, failure.Details["rule"], failure.Details["language"]));
    }

    [Fact]
    public async Task Start_ARulesTemplateThatDoesNotParse_IsDeclarationInvalidNamingTheRuleAndItsField()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => StartAsync(configuration => configuration["Hermes:Conversations:Replies:broken:Trigger"] = "started"));

        Assert.Equal(("messaging.startup.declarationinvalid", "broken", "en.text"), (failure.Code, failure.Details["declaration"], failure.Details["field"]));
    }

    [Fact]
    public async Task Start_ARuleNamingATopicNotConfigured_IsConfigurationInvalid()
    {
        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => StartAsync(configuration =>
        {
            configuration["Hermes:Conversations:Replies:welcome:Trigger"] = "started";
            configuration["Hermes:Conversations:Replies:welcome:Topics:0"] = "unknown";
        }));

        Assert.Equal(("messaging.startup.configurationinvalid", "Conversations:Replies:welcome:Topics:0"), (failure.Code, failure.Details["key"]));
    }

    private Task<ChatHost> StartAsync(Action<Dictionary<string, string?>>? configure = null)
    {
        return SupportTests.StartSupportAsync(postgres, configure);
    }
}