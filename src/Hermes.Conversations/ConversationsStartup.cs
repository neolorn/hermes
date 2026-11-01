using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Conversations.Support;
using Hermes.Core;
using Hermes.Operations;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hermes.Conversations;

/// <summary>
/// Runs the package's checks in <see cref="StartingAsync"/>, after the engine's, which
/// <c>AddHermes</c> registers before it (M-031): no key under <c>Hermes:Conversations:</c> is
/// one the package does not know, read raw from the configuration given to <c>AddHermes</c>, and
/// <c>Conversations:AwayKind</c> names a declared kind where it is given; support's keys are
/// within their rules (<c>09</c> MSG-CONV-002); the membership seam is registered (<c>04</c>
/// MSG-LIB-005); and each automatic reply rule's template is held in each declared language
/// (<c>09</c> MSG-CONV-003).
/// </summary>
internal sealed class ConversationsStartup(IServiceProvider services) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        ConfigurationState state = services.GetRequiredService<ConfigurationState>();
        if (ConversationsConfiguration.UnknownKey(state.Section) is { } key)
        {
            throw new MessagingStartupException(Codes.ConfigurationInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = key });
        }

        string? away = state.Section[ConversationsConfiguration.AwayKind];
        if (away is not null && !services.GetRequiredService<Declarations>().Kinds.Any(kind => string.Equals(kind.Name, away, StringComparison.Ordinal)))
        {
            throw new MessagingStartupException(Codes.ConfigurationInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = ConversationsConfiguration.AwayKind });
        }

        string[] kinds = [.. services.GetRequiredService<Declarations>().Kinds.Select(kind => kind.Name)];
        (SupportSettings? support, string? invalid) = SupportConfiguration.Read(state.Section, kinds);
        if (invalid is not null)
        {
            throw new MessagingStartupException(Codes.ConfigurationInvalid, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = invalid });
        }

        if (!services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IConversationMembership)))
        {
            throw new MessagingStartupException(Codes.DeclarationMissing, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "conversationMembership" });
        }

        ConversationsSettings settings = services.GetRequiredService<ConversationsSettings>();
        settings.AwayKind = away;
        settings.Support = support!;

        // The transcript's page, in every declared language, where topics are configured (03 MSG-TPL-004, M-036).
        if (support!.Topics.Count > 0)
        {
            AsyncServiceScope scope = services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                Dictionary<string, string>? failed = await services.GetRequiredService<TranscriptTemplates>().LoadAsync(
                    services.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue,
                    scope.ServiceProvider.GetService<ITemplateSource>(),
                    services.GetRequiredService<IHostEnvironment>().ContentRootPath,
                    services.GetRequiredService<Declarations>().Assemblies,
                    services.GetRequiredService<Content.TemplateParser>(),
                    cancellationToken).ConfigureAwait(false);
                if (failed is not null)
                {
                    throw new MessagingStartupException(Codes.DeclarationInvalid, failed);
                }
            }
        }

        // Every rule's template, in every declared language, read and checked as kinds' are (03 MSG-TPL-004).
        if (support.Rules.Count > 0)
        {
            AsyncServiceScope scope = services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                (string Code, Dictionary<string, string> Details)? failed = await services.GetRequiredService<ReplyTemplates>().LoadAsync(
                    support.Rules,
                    services.GetRequiredService<IOptionsMonitor<MessagingOptions>>().CurrentValue,
                    scope.ServiceProvider.GetService<ITemplateSource>(),
                    services.GetRequiredService<IHostEnvironment>().ContentRootPath,
                    services.GetRequiredService<Declarations>().Assemblies,
                    services.GetRequiredService<Content.TemplateParser>(),
                    cancellationToken).ConfigureAwait(false);
                if (failed is { } refusal)
                {
                    throw new MessagingStartupException(refusal.Code, refusal.Details);
                }
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}