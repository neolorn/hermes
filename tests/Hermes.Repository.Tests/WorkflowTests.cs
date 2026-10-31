using System;
using System.IO;

using Xunit;

namespace Hermes.Repository.Tests;

/// <summary>The pipeline's release job, read from the workflow (<c>07</c> MSG-ENG-011).</summary>
[Trait("Category", "unit")]
public sealed class WorkflowTests
{
    private static readonly string Workflow = File.ReadAllText(RepositoryRoot.Combine(WorkflowRules.Path)).ReplaceLineEndings("\n");

    [Fact]
    public void MSG_ENG_011_AC4_TheReleaseJobAloneCanWriteToTheContentsAndRunsOnlyForAReleaseTagAfterTheGates()
    {
        Assert.Empty(WorkflowRules.Violations(Workflow));
    }

    [Theory]
    [InlineData("permissions:\n  contents: read\n\njobs:", "permissions:\n  contents: write\n\njobs:", "the workflow's permissions are not contents: read alone")]
    [InlineData("permissions:\n  contents: read\n\njobs:", "permissions: write-all\n\njobs:", "the workflow's permissions are not contents: read alone")]
    [InlineData("      - 'v[0-9]+.[0-9]+.[0-9]+'\n      - 'v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+'\n", "      - 'v*'\n", "the workflow's tags are not the release tags alone")]
    [InlineData("      - 'v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+'\n", "", "the workflow's tags are not the release tags alone")]
    [InlineData("      - 'v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+'\n", "      - 'v[0-9]+.[0-9]+.[0-9]+-*'\n", "the workflow's tags are not the release tags alone")]
    [InlineData("  gates:\n    runs-on: ubuntu-24.04\n", "  gates:\n    runs-on: ubuntu-24.04\n    permissions:\n      contents: write\n", "the job gates sets its own permissions")]
    [InlineData("    needs: gates\n", "", "the release job does not wait for the gates job")]
    [InlineData("    if: github.ref_type == 'tag'\n", "", "the release job does not run only for a release tag")]
    [InlineData("      contents: write\n", "      contents: write\n      packages: write\n", "the release job's permissions are not contents: write alone")]
    [InlineData("dotnet pack Hermes.sln ", "dotnet pack src/Hermes/Hermes.csproj ", "the release job does not pack every package")]
    [InlineData("          persist-credentials: false\n", "", "the release job's checkout leaves its token in the repository's configuration")]
    [InlineData("          persist-credentials: false\n", "          persist-credentials: true\n", "the release job's checkout leaves its token in the repository's configuration")]
    [InlineData("\"chore: release $($env:TAG.Substring(1))\"", "$env:TAG", "the release job does not refuse, before it packs, a tag whose commit is not its release commit")]
    [InlineData("git log -1 --format=%s HEAD", "git log -1 --format=%s origin/main", "the release job does not refuse, before it packs, a tag whose commit is not its release commit")]
    [InlineData(" --notes-file RELEASE-NOTES.md ", " --generate-notes ", "the release job does not create the tag's release with RELEASE-NOTES.md as its notes")]
    [InlineData("  release:\n", "  publish:\n", "no release job releases a release tag")]
    [InlineData(" @prerelease @packages", " @packages", "the release job does not mark a release candidate's release, and it alone, as a pre-release")]
    [InlineData(" @prerelease @packages", " --prerelease @packages", "the release job does not mark a release candidate's release, and it alone, as a pre-release")]
    [InlineData("""-rc\.[1-9]\d*$') { '--prerelease' }""", "-') { '--prerelease' }", "the release job does not mark a release candidate's release, and it alone, as a pre-release")]
    [InlineData("          $prerelease = @(", "          $candidate = @(", "the release job does not mark a release candidate's release, and it alone, as a pre-release")]
    public void MSG_ENG_011_AC4_AWorkflowOutsideTheReleaseRuleFails(string text, string replacement, string violation)
    {
        Assert.Contains(text, Workflow, StringComparison.Ordinal);

        Assert.Contains(violation, WorkflowRules.Violations(Workflow.Replace(text, replacement, StringComparison.Ordinal)));
    }
}