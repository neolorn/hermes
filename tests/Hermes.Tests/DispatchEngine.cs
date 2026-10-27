using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Tests;

/// <summary>A started host that dispatches when a test runs a pass, through recording providers.</summary>
internal sealed class DispatchEngine : IAsyncDisposable
{
    private EngineHost? host;

    internal EngineHost Host => host!;

    internal RecordingEmailProvider Email { get; } = new();

    internal RecordingSmsProvider Sms { get; } = new();

    internal static async Task<DispatchEngine> StartAsync(
        PostgresFixture postgres,
        Action<Dictionary<string, string?>>? configure = null,
        Action<DeclarationBuilder>? declare = null,
        Func<TemplateKey, string?>? templates = null,
        Action<IServiceCollection>? services = null,
        string? connection = null,
        ManualClock? clock = null,
        LogCapture? logs = null)
    {
        DispatchEngine engine = new();
        RecordingEmailProvider email = engine.Email;
        RecordingSmsProvider sms = engine.Sms;
        engine.host = await EngineHost.StartAsync(
            connection ?? await EngineHost.DatabaseAsync(postgres),
            declare,
            templates,
            configuration =>
            {
                EngineHost.Dispatching(configuration);
                configure?.Invoke(configuration);
            },
            collection =>
            {
                collection.AddSingleton<IEmailProvider>(email);
                collection.AddSingleton<ISmsProvider>(sms);
                services?.Invoke(collection);
            },
            clock: clock,
            logs: logs);
        return engine;
    }

    public ValueTask DisposeAsync()
    {
        return host?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}