using System;
using System.IO;
using System.Linq;

using Xunit;

namespace Hermes.Repository.Tests;

[Trait("Category", "contract")]
public sealed class CodeRuleTests
{
    public static TheoryData<string> Assemblies => [.. AssemblySurface.ShippedAssemblies().Select(path => Path.GetFileName(path))];

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void MSG_ENG_007_AC1_EveryCodeAnAssemblyHoldsIsOneSection1Lists(string assembly)
    {
        Assert.Empty(CodeRules.Violations(AssemblySurface.Strings(Path.Combine(AppContext.BaseDirectory, assembly)), CodeRules.Listed(RepositoryRoot.Combine("docs", "spec", "08-reference.md"))));
    }

    [Fact]
    public void MSG_ENG_007_AC1_CodeSection1DoesNotListIsFound()
    {
        string[] strings = ["messaging.send.unknown", "messaging.made.up", "messaging-key-check", "messaging.grant_role", "messaging"];

        Assert.Equal(["messaging.made.up"], CodeRules.Violations(strings, CodeRules.Listed(RepositoryRoot.Combine("docs", "spec", "08-reference.md"))));
    }

    [Fact]
    public void Listed_Section1_HoldsTheStartupResultAndToolCodes()
    {
        string[] expected = ["messaging.startup.declarationinvalid", "messaging.key.unavailable", "messaging.request.unavailable", "messaging.migrate.failed"];

        Assert.All(expected, code => Assert.Contains(code, CodeRules.Listed(RepositoryRoot.Combine("docs", "spec", "08-reference.md"))));
    }

    [Fact]
    public void Strings_EngineAssembly_HoldsTheToolsCodesAsConstants()
    {
        Assert.Contains("messaging.migrate.inputinvalid", AssemblySurface.Strings(Path.Combine(AppContext.BaseDirectory, "Hermes.dll")));
    }
}