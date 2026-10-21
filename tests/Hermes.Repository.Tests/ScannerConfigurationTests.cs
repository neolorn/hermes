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
        fixture.Write("build/gates/gitleaks.toml", Extend + """
            [[allowlists]]
            description = "RFC 8291 section 5: the receiver's authentication secret"
            condition = "AND"
            paths = ['''^tests/Hermes\.Channels\.Tests/Vectors\.cs$''']
            regexes = ['''^vector$''']
            """);

        Assert.Empty(ScannerConfigurationRules.Violations(fixture.Root));
    }

    [Theory]
    [InlineData("[[rules]]\nid = \"custom\"\nregex = '''x'''\n")]
    [InlineData("[allowlist]\npaths = ['''.*''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"OR\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Vectors\\.cs$''']\nregexes = ['''^vector$''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/.*$''']\nregexes = ['''^vector$''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Vectors\\.cs$''']\nregexes = ['''vector''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Vectors\\.cs$''', '''^src/x\\.cs$''']\nregexes = ['''^vector$''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"a test value\"\ncondition = \"AND\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Vectors\\.cs$''']\nregexes = ['''^vector$''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Missing\\.cs$''']\nregexes = ['''^vector$''']\n")]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/Hermes\\.Channels\\.Tests/Vectors\\.cs$''']\nregexes = ['''^vector$''']\nstopwords = ['''x''']\n")]
    public void MSG_ENG_001_AC1_EntryOutsideTheFormIsFound(string entry)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("tests/Hermes.Channels.Tests/Vectors.cs", "namespace Hermes.Channels.Tests;\n");
        fixture.Write("build/gates/gitleaks.toml", Extend + entry);

        Assert.NotEmpty(ScannerConfigurationRules.Violations(fixture.Root));
    }

    [Theory]
    [InlineData("[extend]\nuseDefault = false\n")]
    [InlineData("[extend]\nuseDefault = true\ndisabledRules = [\"generic-api-key\"]\n")]
    [InlineData("title = \"No extension\"\n")]
    public void MSG_ENG_001_AC1_ConfigurationThatDoesNotExtendTheDefaultAloneIsFound(string configuration)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("build/gates/gitleaks.toml", configuration);

        Assert.NotEmpty(ScannerConfigurationRules.Violations(fixture.Root));
    }
}