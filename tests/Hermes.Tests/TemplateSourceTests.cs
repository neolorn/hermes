using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Fluid;

using Hermes.Content;
using Hermes.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace Hermes.Tests;

[Trait("Category", "unit")]
public sealed class TemplateSourceTests
{
    private const string Kind = "order.placed";

    [Fact]
    public async Task MSG_TPL_006_AC1_ATemplateFileUnderTheFolderIsUsedForItsKindChannelLanguageAndPart()
    {
        using TempFolder folder = new();
        Write(folder, "templates/order.placed/email.en.subject.liquid", "From the folder");

        Templates templates = await Started(folder.Path, Except(key => key is { Channel: Channel.Email, Language: "en", Part: "subject" }));

        Assert.Equal("From the folder", Text(templates.Kind(Kind, Channel.Email, "en", "subject")));
        Assert.Equal("Text", Text(templates.Kind(Kind, Channel.Email, "en", "text")));
    }

    [Fact]
    public async Task MSG_TPL_006_AC1_AFolderNamedByTemplatesPathIsTheOneRead()
    {
        using TempFolder folder = new();
        Write(folder, "elsewhere/order.placed/email.en.subject.liquid", "From elsewhere");
        Dictionary<string, string?> configuration = TestHost.Languages("en", "ar");
        configuration["Hermes:Templates:Path"] = "elsewhere";

        Templates templates = await Started(folder.Path, Except(key => key is { Channel: Channel.Email, Language: "en", Part: "subject" }), configuration);

        Assert.Equal("From elsewhere", Text(templates.Kind(Kind, Channel.Email, "en", "subject")));
    }

    [Fact]
    public async Task MSG_TPL_006_AC1_TheHostsSourceIsAskedBeforeTheFolder()
    {
        using TempFolder folder = new();
        Write(folder, "templates/order.placed/email.en.subject.liquid", "From the folder");

        Templates templates = await Started(folder.Path, Except(_ => false));

        Assert.Equal("Subject", Text(templates.Kind(Kind, Channel.Email, "en", "subject")));
    }

    [Fact]
    public async Task MSG_TPL_006_AC2_AnEmbeddedResourceNamedByItsRelativePathIsUsedForItsKind()
    {
        using TempFolder folder = new();

        Templates templates = await Started(folder.Path, Except(key => key is { Channel: Channel.Email, Language: "ar", Part: "subject" }), assembly: true);

        Assert.Equal("تم استلام طلبك", Text(templates.Kind(Kind, Channel.Email, "ar", "subject")));
    }

    [Fact]
    public async Task MSG_TPL_006_AC2_TheFolderIsAskedBeforeAnAssembly()
    {
        using TempFolder folder = new();
        Write(folder, "templates/order.placed/email.ar.subject.liquid", "From the folder");

        Templates templates = await Started(folder.Path, Except(key => key is { Channel: Channel.Email, Language: "ar", Part: "subject" }), assembly: true);

        Assert.Equal("From the folder", Text(templates.Kind(Kind, Channel.Email, "ar", "subject")));
    }

    [Fact]
    public async Task MSG_TPL_006_AC2_AnAssemblyNotPassedIsNotRead()
    {
        using TempFolder folder = new();

        MessagingStartupException failure = await Assert.ThrowsAsync<MessagingStartupException>(
            () => Started(folder.Path, Except(key => key is { Channel: Channel.Email, Language: "ar", Part: "subject" })));

        Assert.Equal("messaging.startup.declarationinvalid", failure.Code);
        Assert.Equal("email.ar.subject", failure.Details["field"]);
    }

    [Fact]
    public async Task MSG_TPL_006_AC3_AHostsOwnUnsubscribePageInEnglishReplacesTheShippedOne()
    {
        using TempFolder folder = new();
        Func<TemplateKey, string?> every = Except(_ => false);

        Templates templates = await Started(folder.Path, key => key is { Page: "unsubscribe", Language: "en" } ? "<p>Ours {{ action }}</p>" : every(key));

        Assert.Equal("<p>Ours </p>", Text(templates.Page("unsubscribe", "en")));
        Assert.Contains("name=\"page\"", Text(templates.Page("unsubscribe", "ar")), StringComparison.Ordinal);
        Assert.Contains("unsubscribe link no longer works", Text(templates.Page("unsubscribeinvalid", "en")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unsubscribe", "en", "action")]
    [InlineData("unsubscribe", "ar", "action")]
    [InlineData("unsubscribed", "en", "category")]
    [InlineData("unsubscribed", "ar", "category")]
    [InlineData("unsubscribeinvalid", "en", "direction")]
    [InlineData("unsubscribeinvalid", "ar", "direction")]
    public async Task MSG_TPL_006_TheLibraryShipsEachPageInEnglishAndArabic(string page, string language, string value)
    {
        using TempFolder folder = new();

        Templates templates = await Started(folder.Path, Except(_ => false));
        TemplatePart? part = templates.Page(page, language);

        Assert.NotNull(part);
        Assert.Contains(value, part.Reads.All);
    }

    [Fact]
    public async Task MSG_TPL_006_TheUnsubscribePagesFormCarriesAFieldNamedPage()
    {
        using TempFolder folder = new();

        Templates templates = await Started(folder.Path, Except(_ => false));

        Assert.Contains("name=\"page\"", Text(templates.Page("unsubscribe", "en")), StringComparison.Ordinal);
        Assert.Contains("name=\"page\"", Text(templates.Page("unsubscribe", "ar")), StringComparison.Ordinal);
    }

    private static void Declare(DeclarationBuilder declare)
    {
        declare.Category("news").Label("en", "News").Label("ar", "الأخبار");
        declare.Kind(Kind, "news").Email();
    }

    /// <summary>The least templates of the declarations, none for a key <paramref name="left"/> picks, and no page, so the shipped pages are read.</summary>
    private static Func<TemplateKey, string?> Except(Func<TemplateKey, bool> left)
    {
        Func<TemplateKey, string?> every = TemplateSourceFake.Every(Declare).Answer;
        return key => key.Page is not null || left(key) ? null : Plain(key, every(key));
    }

    /// <summary>The kind's text parts answered as plain words; its unsubscribe value read in each.</summary>
    private static string? Plain(TemplateKey key, string? text)
    {
        return key is { Channel: Channel.Email, Part: "text" } ? "Text{{ unsubscribe }}" : text;
    }

    private static void Write(TempFolder folder, string relativePath, string text)
    {
        string file = Path.Combine(folder.Path, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    private static async Task<Templates> Started(string contentRoot, Func<TemplateKey, string?> answer, Dictionary<string, string?>? configuration = null, bool assembly = false)
    {
        configuration ??= TestHost.Languages("en", "ar");
        configuration["Hermes:PublicBaseAddress"] = "https://messages.example.com/notifications";
        using IHost host = TestHost.Build(
            configuration,
            declare =>
            {
                Declare(declare);
                if (assembly)
                {
                    declare.Templates(typeof(TemplateSourceTests).Assembly);
                }
            },
            services => services.AddSingleton<ITemplateSource>(new TemplateSourceFake(answer)),
            contentRoot: contentRoot,
            hostTemplates: false);

        Exception? failure = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken));
        if (failure is MessagingStartupException { Code: "messaging.startup.declarationinvalid" or "messaging.startup.configurationinvalid" } stopped)
        {
            throw stopped;
        }

        return host.Services.GetRequiredService<Templates>();
    }

    private static string? Text(TemplatePart? part)
    {
        return part?.Template.Render();
    }
}