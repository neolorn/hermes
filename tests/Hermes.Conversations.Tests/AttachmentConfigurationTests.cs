using System;
using System.Collections.Generic;

using Hermes.Conversations.Attachments;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace Hermes.Conversations.Tests;

/// <summary>The attachments' keys under <c>Hermes:Conversations:Attachments:</c> (<c>08</c> section 4; <c>09</c> MSG-CONV-007).</summary>
[Trait("Category", "unit")]
public sealed class AttachmentConfigurationTests
{
    private const string Key = "Hermes:Conversations:Attachments:";

    [Fact]
    public void Read_NoKey_IsOffWithTenMebibytesAndThreeTypes()
    {
        (AttachmentSettings? settings, string? invalid) = AttachmentConfiguration.Read(Hermes([]));

        Assert.Null(invalid);
        Assert.Equal((false, false, 10L * 1024 * 1024), (settings!.Enabled, settings.Public, settings.MaxBytes));
        Assert.Equal(["image/jpeg", "image/png", "application/pdf"], settings.Types);
    }

    [Fact]
    public void Read_EveryKey_ReadsThemThePublicGroupOnlyWhereAttachmentsAre()
    {
        (AttachmentSettings? settings, string? invalid) = AttachmentConfiguration.Read(Hermes(new()
        {
            [Key + "Enabled"] = "true",
            [Key + "Public"] = "true",
            [Key + "MaxBytes"] = "1024",
            [Key + "Types:0"] = "Image/WebP",
            [Key + "Types:1"] = "application/pdf",
        }));

        Assert.Null(invalid);
        Assert.Equal((true, true, 1024L), (settings!.Enabled, settings.Public, settings.MaxBytes));
        Assert.Equal(["image/webp", "application/pdf"], settings.Types);
        Assert.False(AttachmentConfiguration.Read(Hermes(new() { [Key + "Public"] = "true" })).Settings!.Public);
    }

    [Theory]
    [InlineData("Enabled", "yes", "Conversations:Attachments:Enabled")]
    [InlineData("Public", "1", "Conversations:Attachments:Public")]
    [InlineData("MaxBytes", "1023", "Conversations:Attachments:MaxBytes")]
    [InlineData("MaxBytes", "52428801", "Conversations:Attachments:MaxBytes")]
    [InlineData("MaxBytes", "ten", "Conversations:Attachments:MaxBytes")]
    [InlineData("Types:0", "image", "Conversations:Attachments:Types:0")]
    [InlineData("Types:0", "image/png; q=1", "Conversations:Attachments:Types:0")]
    public void Read_AKeyOutsideItsRule_IsThatKey(string key, string value, string named)
    {
        Assert.Equal(named, AttachmentConfiguration.Read(Hermes(new() { [Key + key] = value })).Invalid);
    }

    [Fact]
    public void Read_ATypeNamedTwice_IsTheSecond()
    {
        Assert.Equal("Conversations:Attachments:Types:1", AttachmentConfiguration.Read(Hermes(new() { [Key + "Types:0"] = "image/png", [Key + "Types:1"] = "IMAGE/PNG" })).Invalid);
    }

    [Fact]
    public void UnknownKey_TheAttachmentsKeys_AreKnownAndAnotherBesideThemIsNot()
    {
        Assert.Null(ConversationsConfiguration.UnknownKey(Hermes(new() { [Key + "Enabled"] = "true", [Key + "MaxBytes"] = "2048", [Key + "Types:0"] = "image/png", [Key + "Public"] = "false" })));
        Assert.Equal("Conversations:Attachments:Size", ConversationsConfiguration.UnknownKey(Hermes(new() { [Key + "Size"] = "2048" })));
        Assert.Equal("Conversations:Attachments:Types:0:Name", ConversationsConfiguration.UnknownKey(Hermes(new() { [Key + "Types:0:Name"] = "image/png" })));
    }

    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("Résumé final (2).pdf", true)]
    [InlineData("", false)]
    [InlineData("../photo.png", false)]
    [InlineData("dir\\photo.png", false)]
    [InlineData("photo\u0000.png", false)]
    [InlineData("photo\n.png", false)]
    public void IsName_AllowsOneToTwoHundredFiftyFiveCharactersWithoutSeparatorsOrControls(string name, bool allowed)
    {
        Assert.Equal(allowed, AttachmentFiles.IsName(name));
        Assert.True(AttachmentFiles.IsName(new string('a', 255)));
        Assert.False(AttachmentFiles.IsName(new string('a', 256)));
    }

    [Theory]
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData("image/jpeg", new byte[] { 0x89, 0x50, 0x4E, 0x47 }, false)]
    [InlineData("image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, true)]
    [InlineData("image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x38, 0x61 }, false)]
    [InlineData("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, true)]
    [InlineData("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 }, false)]
    [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, true)]
    [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44 }, false)]
    [InlineData("text/plain", new byte[] { 0x00 }, true)]
    public void Matches_TheTypesTheLibraryKnowsByTheirFirstBytesAndAnyOtherByNone(string type, byte[] first, bool matches)
    {
        Assert.Equal(matches, AttachmentFiles.Matches(type, first));
    }

    private static IConfigurationSection Hermes(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection("Hermes");
    }
}