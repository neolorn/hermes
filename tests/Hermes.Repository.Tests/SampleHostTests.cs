using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace Hermes.Repository.Tests;

/// <summary>
/// The sample host's registration of conversations (<c>07</c> MSG-ENG-001; M-031). That it
/// starts with them is proven by its own tests, which the sample host's gate runs.
/// </summary>
[Trait("Category", "unit")]
public sealed class SampleHostTests
{
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
}