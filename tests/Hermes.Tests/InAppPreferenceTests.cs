using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "integration")]
public sealed class InAppPreferenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task MSG_INAPP_004_WithTheKeyOnSettingTheInAppChannelIsRefusedFixedAndReadingLeavesItOut()
    {
        await using DispatchEngine engine = await StartAsync(alwaysOn: true);

        Result refused = await SetAsync(engine.Host, Channel.InApp, isOn: false);
        Result sms = await SetAsync(engine.Host, Channel.Sms, isOn: false);
        IReadOnlyList<CategoryPreferences> read = await ReadAsync(engine.Host);

        Assert.Equal("messaging.preference.fixed", refused.Failure!.Code);
        Assert.Equal((InApp.Category, "inapp"), (refused.Failure.Details["category"], refused.Failure.Details["channel"]));
        Assert.True(sms.IsSuccess);
        Assert.Equal([(Channel.Sms, false)], read.Single(category => category.Category == InApp.Category).Channels.Select(channel => (channel.Channel, channel.On)));
    }

    [Fact]
    public async Task MSG_INAPP_004_WithTheKeyOffTheInAppChannelIsSetAndReadAsAnyOther()
    {
        await using DispatchEngine engine = await StartAsync(alwaysOn: false);

        Result set = await SetAsync(engine.Host, Channel.InApp, isOn: false);
        IReadOnlyList<CategoryPreferences> read = await ReadAsync(engine.Host);

        Assert.True(set.IsSuccess);
        Assert.Equal([(Channel.Sms, true), (Channel.InApp, false)], read.Single(category => category.Category == InApp.Category).Channels.Select(channel => (channel.Channel, channel.On)));
    }

    [Fact]
    public async Task MSG_API_003_WithTheKeyOnAPutOfTheInAppChannelIs422FixedAndGetLeavesItOut()
    {
        await using DispatchEngine engine = await StartAsync(alwaysOn: true);
        using HttpClient client = engine.Host.Client();

        using HttpResponseMessage put = await Endpoints.SendAsync(client, HttpMethod.Put, $"/preferences/{InApp.Category}/inapp", InApp.Person, Endpoints.Json("""{ "on": false }"""));
        using HttpResponseMessage get = await Endpoints.SendAsync(client, HttpMethod.Get, "/preferences", InApp.Person);
        using JsonDocument read = JsonDocument.Parse(await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, put.StatusCode);
        Assert.Equal(("messaging.preference.fixed", "inapp"), await PreferenceEndpointTests.RefusalAsync(put, "channel"));
        Assert.Equal(["sms"], read.RootElement.GetProperty("categories")[0].GetProperty("channels").EnumerateArray().Select(channel => channel.GetProperty("channel").GetString()));
    }

    private static async Task<Result> SetAsync(EngineHost host, Channel channel, bool isOn)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await scope.ServiceProvider.GetRequiredService<IPreferences>().SetAsync(InApp.Person, InApp.Category, channel, isOn, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<IReadOnlyList<CategoryPreferences>> ReadAsync(EngineHost host)
    {
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            return await scope.ServiceProvider.GetRequiredService<IPreferences>().ReadAsync(InApp.Person, TestContext.Current.CancellationToken);
        }
    }

    private Task<DispatchEngine> StartAsync(bool alwaysOn)
    {
        return DispatchEngine.StartAsync(
            postgres,
            configure: configuration => configuration["Hermes:InApp:AlwaysOn"] = alwaysOn ? "true" : "false",
            declare: InApp.Declare,
            templates: InApp.Templates,
            services: Endpoints.Identity,
            map: application => application.MapHermesPreferences(Endpoints.Prefix));
    }
}