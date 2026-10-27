using System;
using System.Collections.Generic;

using Hermes.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

/// <summary>The engine's registrations on a provider as ASP.NET Core builds it in development, which checks each service's lifetime.</summary>
[Trait("Category", "unit")]
public sealed class ServiceLifetimeTests
{
    [Fact]
    public void AddHermes_OnAProviderThatValidatesScopesWhenBuilt_HoldsNoSingletonTakingAScopedService()
    {
        Dictionary<string, string?> values = TestHost.Languages("en", "ar");
        UnsubscribeLinkTests.Configure(values);
        values["Hermes:Push:AllowedHosts:0"] = "push.example.com";
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddHermes(builder.Configuration, UnsubscribeLinkTests.Declare);
        Endpoints.Identity(builder.Services);
        builder.Services.AddSingleton<IPushProvider, RecordingPushProvider>();
        builder.Services.AddSingleton<ISecretSource>(Secrets.Held());
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));

        Exception? built = Record.Exception(() => builder.Build().Dispose());

        Assert.Null(built);
    }
}