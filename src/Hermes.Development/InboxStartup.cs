using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Channels;
using Hermes.Core;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hermes.Development;

/// <summary>
/// The inbox's start (<c>02</c> MSG-DEV-001): before any hosted service's <c>StartAsync</c>
/// it reads and checks its keys, makes its folder and checks that the host's services resolve
/// its providers, or the in-memory ones of <c>AddHermesTesting</c>; once the host has started,
/// it logs one line naming the folder and, where the group is mapped, its address, and
/// nothing of any message.
/// </summary>
internal sealed partial class InboxStartup(IServiceProvider services, InboxGroup group, ILogger<InboxStartup> logger) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        InboxWriter writer = services.GetRequiredService<InboxWriter>();
        writer.Prepare();
        if (services.GetService<TestProviders>() is not null)
        {
            return Task.CompletedTask;
        }

        bool inPlace = services.GetService<IEmailProvider>() is InboxEmailProvider
            && services.GetService<ISmsProvider>() is InboxSmsProvider
            && services.GetServices<IPushProvider>().All(provider => provider is InboxPushProvider);
        if (!inPlace)
        {
            throw new InvalidOperationException(
                "The development inbox's providers are not the ones the host's services resolve: a service provider factory the host set after AddHermesDevelopmentInbox replaced the inbox's, so its messages would be sent.");
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<TestProviders>() is not null)
        {
            return Task.CompletedTask;
        }

        string folder = services.GetRequiredService<InboxWriter>().Folder;
        if (group.Prefix is not { } prefix)
        {
            Started(logger, folder);
        }
        else
        {
            string address = Address(prefix);
            StartedWithPage(logger, folder, address);
        }

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

    /// <summary>The group's address: the server's first address and the prefix, or the prefix alone where the server has none.</summary>
    private string Address(string prefix)
    {
        string trimmed = prefix.Trim('/');
        string path = trimmed.Length == 0 ? "/" : "/" + trimmed + "/";
        string? server = services.GetService<IServer>()?.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        return server is null ? path : server.TrimEnd('/') + path;
    }

    [LoggerMessage(EventId = 1, EventName = "InboxStarted", Level = LogLevel.Information, Message = "The development inbox keeps every message in {Folder} instead of sending it.")]
    private static partial void Started(ILogger logger, string folder);

    [LoggerMessage(EventId = 2, EventName = "InboxStartedWithPage", Level = LogLevel.Information, Message = "The development inbox keeps every message in {Folder} instead of sending it; its page is at {Address}.")]
    private static partial void StartedWithPage(ILogger logger, string folder, string address);
}