using System;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "unit")]
public sealed class ScannerConfigurationTests
{
    private const string Extend = """
        title = "Secret scanning of the whole history"

        [extend]
        useDefault = true

        """;

    private const string VectorsPath = @"'''^tests/Hermes\.Channels\.Tests/Vectors\.cs$'''";

    [Fact]
    public void MSG_ENG_001_AC1_ScannerConfigurationAddsOnlyTestVectorEntries()
    {
        Assert.Empty(ScannerConfigurationRules.Violations(RepositoryRoot.Path));
    }

    [Fact]
    public void MSG_ENG_001_AC1_TestVectorEntryOfTheFormIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Hermes.Channels.Tests/Vectors.cs", "namespace Hermes.Channels.Tests;\n");
        fixture.Write("build/gates/gitleaks.toml", Extend + $"""
            [[allowlists]]
            description = "RFC 8291 section 5: the receiver's authentication secret"
            condition = "AND"
            paths = [{VectorsPath}]
            regexes = ['''^vector$''']
            """);

        Assert.Empty(ScannerConfigurationRules.Violations(fixture.Root));
    }

    [Theory]
    [InlineData("[[rules]]\nid = \"custom\"\nregex = '''x'''\n", "adds the table [[rules]], which is not of the rule's form")]
    [InlineData("[allowlist]\npaths = ['''.*''']\n", "adds the table [allowlist], which is not of the rule's form")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"OR\"\npaths = [" + VectorsPath + "]\nregexes = ['''^vector$''']\n", "does not require both its file and its value to match")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/.*$''']\nregexes = ['''^vector$''']\n", "does not name one file exactly")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = [" + VectorsPath + "]\nregexes = ['''vector''']\n", "does not name one value exactly")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = [" + VectorsPath + ", '''^src/x\\.cs$''']\nregexes = ['''^vector$''']\n", "does not name one file exactly")]
    [InlineData("[[allowlists]]\ndescription = \"a test value\"\ncondition = \"AND\"\npaths = [" + VectorsPath + "]\nregexes = ['''^vector$''']\n", "does not give a document and its section as its reason")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Missing\\.cs$''']\nregexes = ['''^vector$''']\n", "names tests/Hermes.Channels.Tests/Missing.cs, which does not exist")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^src/Hermes\\.Channels/Vectors\\.cs$''']\nregexes = ['''^vector$''']\n", "names src/Hermes.Channels/Vectors.cs, which is not under tests/")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = [" + VectorsPath + "]\nregexes = ['''^vector$''']\nstopwords = ['''x''']\n", "does not hold exactly description, condition, paths and regexes")]
    public void MSG_ENG_001_AC1_EntryOutsideTheFormIsFound(string entry, string reason)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Hermes.Channels.Tests/Vectors.cs", "namespace Hermes.Channels.Tests;\n");
        fixture.Write("src/Hermes.Channels/Vectors.cs", "namespace Hermes.Channels;\n");
        fixture.Write("build/gates/gitleaks.toml", Extend + entry);

        Assert.Contains(ScannerConfigurationRules.Violations(fixture.Root), violation => violation.Contains(reason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[extend]\nuseDefault = false\n", "[extend] table holds more than useDefault = true")]
    [InlineData("[extend]\nuseDefault = true\ndisabledRules = [\"generic-api-key\"]\n", "[extend] table holds more than useDefault = true")]
    [InlineData("title = \"No extension\"\n", "does not extend gitleaks' default")]
    public void MSG_ENG_001_AC1_ConfigurationThatDoesNotExtendTheDefaultAloneIsFound(string configuration, string reason)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("build/gates/gitleaks.toml", configuration);

        Assert.Contains(ScannerConfigurationRules.Violations(fixture.Root), violation => violation.Contains(reason, StringComparison.Ordinal));
    }
}