using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Channels;

/// <summary>
/// Runs the providers' own checks in <see cref="StartingAsync"/> where the application
/// registers the providers alone (<c>04</c> MSG-LIB-003), before any hosted service's
/// <c>StartAsync</c>: a channel is used where its configuration section is present, and its
/// shipped provider where the host registers no provider of its own for it. The keys the use
/// reads are checked, then the secrets its shipped providers need are read.
/// </summary>
internal sealed class ChannelsStartup(ChannelsConfiguration state, EmailSubmission account, PushKeys keys, IServiceProvider services) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<ChannelsUnderEngine>() is not null)
        {
            return;
        }

        IConfigurationSection hermes = state.Section;
        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            bool email = hermes.GetSection("Email").Exists();
            bool push = hermes.GetSection("Push").Exists();
            ChannelUse use = new(
                ProvidersAlone: true,
                Email: email,
                Sms: hermes.GetSection("Sms").Exists(),
                Push: push,
                ShippedEmail: email && scope.ServiceProvider.GetService<IEmailProvider>() is SmtpProvider,
                ShippedPush: push && scope.ServiceProvider.GetService<IPushProvider>() is WebPushProvider);

            ISmsProvider? sms = use.Sms ? scope.ServiceProvider.GetService<ISmsProvider>() : null;
            Func<string, bool> isSender = sender => sms?.IsValidSender(sender) ?? true;
            if (ChannelsCheck.InvalidKey(hermes, use, isSender) is { } key)
            {
                throw StartupFailure.Of(ChannelCodes.ConfigurationInvalid, ("key", key));
            }

            ISecretSource? secrets = use.ShippedEmail || use.ShippedPush
                ? scope.ServiceProvider.GetService<ISecretSource>() ?? throw StartupFailure.Of(ChannelCodes.DeclarationMissing, ("key", "secretSource"))
                : null;

            // The library ships no SMS provider (02 MSG-CHAN-001), so SMS used needs the host's.
            if (use.Sms && sms is null)
            {
                throw StartupFailure.Of(ChannelCodes.DeclarationMissing, ("key", "smsProvider"));
            }
            if (use.ShippedEmail)
            {
                account.Hold(await EmailSubmission.ReadAsync(secrets!, cancellationToken).ConfigureAwait(false));
            }

            if (use.ShippedPush)
            {
                SortedDictionary<int, byte[]> versions = await PushKeyCheck.ReadAsync(secrets!, cancellationToken).ConfigureAwait(false);
                keys.Hold(versions);
                foreach (byte[] value in versions.Values)
                {
                    Array.Clear(value);
                }
            }

            state.Start(use, isSender);
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