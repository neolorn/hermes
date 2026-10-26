using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Content;
using Hermes.Core;
using Hermes.Sending;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hermes.Operations;

/// <summary>
/// Runs the engine's startup checks in <see cref="StartingAsync"/>, which the host calls on
/// every lifecycle service before any hosted service's <c>StartAsync</c>, so they complete
/// before a host's own hosted service starts or a request is served (<c>04</c> MSG-LIB-003):
/// the engine's configuration, the channels' configuration for the channels the kinds use, the
/// declarations, the seams, the secrets, the schema, then the key table. The configuration read
/// is the one given to <c>AddHermes</c>, which the options are bound from.
/// </summary>
internal sealed class MessagingStartup(
    ConfigurationState state,
    ChannelsConfiguration channels,
    IOptionsMonitor<MessagingOptions> options,
    Declarations declarations,
    MessagingDatabase database,
    HeldKeys keys,
    EmailSubmission submission,
    PushKeys pushKeys,
    KeyTable table,
    TemplateParser parser,
    Templates templates,
    IHostEnvironment environment,
    IServiceProvider services) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        string? key = ConfigurationCheck.InvalidKey(state.Section, TimeZoneInfo.GetSystemTimeZones(), environment.ContentRootPath, declarations.NeedsPublicBaseAddress);
        if (key is not null)
        {
            throw Failure(Codes.ConfigurationInvalid, ("key", key));
        }

        state.Start();
        MessagingOptions current = options.CurrentValue;

        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            ChannelUse use = Use(declarations, current, scope.ServiceProvider);
            ISmsProvider? sms = use.Sms ? scope.ServiceProvider.GetService<ISmsProvider>() : null;
            Func<string, bool> isSender = sender => sms?.IsValidSender(sender) ?? true;
            if (ChannelsCheck.InvalidKey(channels.Section, use, isSender) is { } channelKey)
            {
                throw Failure(Codes.ConfigurationInvalid, ("key", channelKey));
            }

            channels.Start(use, isSender);

            if (DeclarationCheck.Invalid(declarations, current.Languages, current.Retention.Places, isSender) is var (declaration, field))
            {
                throw Failure(Codes.DeclarationInvalid, ("declaration", declaration), ("field", field));
            }

            Dictionary<TemplateKey, TemplatePart> loaded = [];
            string folder = Path.GetFullPath(current.Templates.Path, Path.GetFullPath(environment.ContentRootPath));
            TemplateSources sources = new(scope.ServiceProvider.GetService<ITemplateSource>(), folder, declarations.Assemblies);
            if (await TemplateCheck.LoadAsync(declarations, current.Languages, current.Templates.MaxSteps, current.Templates.MaxRecursion, sources, parser, loaded, cancellationToken).ConfigureAwait(false) is var (template, part))
            {
                throw Failure(Codes.DeclarationInvalid, ("declaration", template), ("field", part));
            }

            templates.Hold(loaded);

            if (SeamCheck.Missing(services.GetRequiredService<IServiceProviderIsService>(), declarations, current) is { } seam)
            {
                throw Failure(Codes.DeclarationMissing, ("key", seam));
            }

            if (SeamCheck.InvalidProtector(scope.ServiceProvider.GetServices<IContentProtector>()) is { } protector)
            {
                throw Failure(Codes.DeclarationInvalid, ("declaration", protector), ("field", "Id"));
            }

            ISecretSource secrets = scope.ServiceProvider.GetRequiredService<ISecretSource>();
            database.Open(await SecretCheck.ConnectionAsync(secrets, cancellationToken).ConfigureAwait(false));
            keys.Hold(await SecretCheck.KeysAsync(secrets, state.Section, KeyNeeds.Needed(declarations, current), cancellationToken).ConfigureAwait(false));
            if (use.ShippedPush && keys.Find(KeyNeeds.PushVapidKeys) is { } vapid)
            {
                pushKeys.Hold(vapid.Versions);
            }

            if (use.ShippedEmail)
            {
                submission.Hold(await EmailSubmission.ReadAsync(secrets, cancellationToken).ConfigureAwait(false));
            }
        }

        if (await SchemaCheck.MissingAsync(database, cancellationToken).ConfigureAwait(false) is { } migration)
        {
            throw Failure(Codes.SchemaMismatch, ("migration", migration));
        }

        await table.StartAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// The channels the declared kinds use, and whether each shipped provider is used: its
    /// channel is used, the host registers no provider of its own for it, and the application
    /// dispatches (<c>04</c> MSG-LIB-003).
    /// </summary>
    internal static ChannelUse Use(Declarations declarations, MessagingOptions options, IServiceProvider scoped)
    {
        bool email = declarations.Kinds.Any(kind => kind.Email is not null);
        bool push = declarations.Kinds.Any(kind => kind.Push is not null);
        return new ChannelUse(
            ProvidersAlone: false,
            Email: email,
            Sms: declarations.Kinds.Any(kind => kind.Sms is not null),
            Push: push,
            ShippedEmail: email && options.Dispatch.Enabled && scoped.GetService<IEmailProvider>() is SmtpProvider,
            ShippedPush: push && options.Dispatch.Enabled && scoped.GetService<IPushProvider>() is WebPushProvider);
    }

    internal static MessagingStartupException Failure(string code, params (string Name, string Value)[] details)
    {
        Dictionary<string, string> named = new(StringComparer.Ordinal);
        foreach ((string name, string value) in details)
        {
            named[name] = value;
        }

        return new MessagingStartupException(code, named);
    }
}