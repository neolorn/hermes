using System;
using System.Linq;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
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
    public void MapHermesStaffConversations_WithoutAddHermesConversations_ThrowsNamingIt()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHermes(builder.Configuration, _ => { });
        using WebApplication application = builder.Build();

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => application.MapHermesStaffConversations("/staff"));

        Assert.Contains("AddHermesConversations", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapHermesStaffConversations_AfterAddHermesConversations_MapsItsTwelveRoutesUnderThePrefix()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHermes(builder.Configuration, _ => { });
        builder.Services.AddHermesConversations();
        using WebApplication application = builder.Build();

        application.MapHermesStaffConversations("/staff");

        Assert.Equal(12, ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints).OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().Count(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/staff/conversations", StringComparison.Ordinal)));
    }

    [Fact]
    public void MapHermesPublicConversations_WithoutAddHermesConversations_ThrowsNamingIt()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHermes(builder.Configuration, _ => { });
        using WebApplication application = builder.Build();

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => application.MapHermesPublicConversations("/public"));

        Assert.Contains("AddHermesConversations", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapHermesPublicConversations_AfterAddHermesConversations_MapsItsNineRoutesUnderThePrefixAllowingAnonymousCallers()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHermes(builder.Configuration, _ => { });
        builder.Services.AddHermesConversations();
        using WebApplication application = builder.Build();

        application.MapHermesPublicConversations("/public");

        Microsoft.AspNetCore.Routing.RouteEndpoint[] routes = [.. ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints).OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/public/support", StringComparison.Ordinal))];
        Assert.Equal(9, routes.Length);
        Assert.All(routes, route => Assert.NotNull(route.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()));
    }

    [Fact]
    public void AddHermesConversations_NoServices_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => MessagingConversationsServiceCollectionExtensions.AddHermesConversations(null!));
    }
}