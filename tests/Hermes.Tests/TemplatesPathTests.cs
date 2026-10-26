using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Operations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class TemplatesPathTests
{
    [Fact]
    public async Task MSG_OPS_001_AC1_TemplatesPathNamingAFileStopsTheStart()
    {
        using TempFolder folder = new();

        await AssertStopsTheStart(folder.Path, folder.File("templates.liquid"));
    }

    [Fact]
    public async Task MSG_OPS_001_AC1_TemplatesPathNamingAFolderTheProcessCannotReadStopsTheStart()
    {
        using TempFolder folder = new();

        await AssertStopsTheStart(folder.Path, folder.UnreadableFolder("templates"));
    }

    [Fact]
    public async Task MSG_OPS_001_AC1_TemplatesPathIsTakenFromTheHostsContentRoot()
    {
        using TempFolder folder = new();
        folder.File("templates");

        await AssertStopsTheStart(folder.Path, path: null);
    }

    [Fact]
    public void MSG_OPS_001_AC1_TemplatesPathNamingNothingIsWithinTheRule()
    {
        using TempFolder folder = new();
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Templates:Path"] = Path.Combine("missing", "templates");

        Assert.Null(ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), folder.Path));
    }

    [Fact]
    public void MSG_OPS_001_AC1_TemplatesPathNamingAReadableFolderIsWithinTheRule()
    {
        using TempFolder folder = new();
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Templates:Path"] = folder.Folder("templates");

        Assert.Null(ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), folder.Path));
    }

    [Fact]
    public void TemplatesPath_PathThatDoesNotResolve_IsOutsideTheRule()
    {
        using TempFolder folder = new();
        Dictionary<string, string?> configuration = TestHost.Languages();
        configuration["Hermes:Templates:Path"] = "templates\0";

        Assert.Equal("Templates:Path", ConfigurationCheck.InvalidKey(TestHost.Section(configuration), TimeZoneInfo.GetSystemTimeZones(), folder.Path));
    }

    [Fact]
    public void TemplatesPath_AbsentOrPresent_IsBoundAsGiven()
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        using IHost absent = TestHost.Build(configuration);
        configuration["Hermes:Templates:Path"] = "content/templates";
        using IHost present = TestHost.Build(configuration);

        Assert.Equal("templates", absent.Services.GetRequiredService<IOptions<MessagingOptions>>().Value.Templates.Path);
        Assert.Equal("content/templates", present.Services.GetRequiredService<IOptions<MessagingOptions>>().Value.Templates.Path);
    }

    private static async Task AssertStopsTheStart(string contentRoot, string? path)
    {
        Dictionary<string, string?> configuration = TestHost.Languages();
        if (path is not null)
        {
            configuration["Hermes:Templates:Path"] = path;
        }

        using IHost host = TestHost.Build(configuration, contentRoot: contentRoot);

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("messaging.startup.configurationinvalid", failure.Code);
        Assert.Equal("Templates:Path", failure.Details["key"]);
    }
}