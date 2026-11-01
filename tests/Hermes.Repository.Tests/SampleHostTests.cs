using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Xunit;

namespace Hermes.Repository.Tests;

/// <summary>
/// The sample host's registration of conversations (<c>07</c> MSG-ENG-001; M-031, M-039). That
/// it starts with them, and that each group it maps answers, is proven by its own tests, which
/// the sample host's gate runs from the packed packages.
/// </summary>
[Trait("Category", "unit")]
public sealed partial class SampleHostTests
{
    [Fact]
    public void MSG_ENG_001_AC4_TheSampleHostMapsEveryGroupOfTheLibraryWithAttachmentsOnAndTheTestingFakes()
    {
        string program = File.ReadAllText(RepositoryRoot.Combine("samples", "Bakery", "Program.cs"));
        string[] groups = [.. Directory.EnumerateFiles(RepositoryRoot.Combine("src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => MapMethod().Matches(File.ReadAllText(file)).Select(match => match.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal)];
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.Combine("samples", "Bakery", "appsettings.json")));
        JsonElement attachments = settings.RootElement.GetProperty("Hermes").GetProperty("Conversations").GetProperty("Attachments");

        Assert.Contains("MapHermesStaffConversations", groups);
        Assert.Contains("MapHermesPublicConversations", groups);
        Assert.All(groups, group => Assert.Contains("app." + group + "(", program, StringComparison.Ordinal));
        Assert.Contains("AddSingleton<IAttachmentStore, InMemoryAttachmentStore>()", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IAttachmentScanner>(new FixedAttachmentScanner())", program, StringComparison.Ordinal);
        Assert.True(attachments.GetProperty("Enabled").GetBoolean() && attachments.GetProperty("Public").GetBoolean());
    }

    [Fact]
    public void MSG_ENG_001_AC3_TheSampleHostReferencesConversationsAndRegistersThemAfterTheEngine()
    {
        XDocument project = XDocument.Load(RepositoryRoot.Combine("samples", "Bakery", "Bakery.csproj"));
        string program = File.ReadAllText(RepositoryRoot.Combine("samples", "Bakery", "Program.cs"));

        Assert.Contains(project.Descendants("PackageReference"), reference => string.Equals((string?)reference.Attribute("Include"), Packages.Conversations, StringComparison.Ordinal));
        int engine = program.IndexOf(".AddHermes(", StringComparison.Ordinal);
        int conversations = program.IndexOf(".AddHermesConversations()", StringComparison.Ordinal);
        Assert.True(engine >= 0, "The sample host does not call AddHermes.");
        Assert.True(conversations > engine, "The sample host does not call AddHermesConversations after AddHermes.");
    }

    /// <summary>A public method of the library that maps one of its endpoint groups.</summary>
    [GeneratedRegex(@"public static RouteGroupBuilder (?<name>MapHermes\w+)\(this IEndpointRouteBuilder", RegexOptions.CultureInvariant)]
    private static partial Regex MapMethod();
}