using System;

using Hermes.Operations;
using Hermes.Sending;

using Xunit;

namespace Hermes.Tests;

/// <summary>The description by which a table joins key retirement and erasure (M-031), and the statements it makes.</summary>
[Trait("Category", "unit")]
public sealed class KeyedTableTests
{
    [Theory]
    [InlineData("Probes")]
    [InlineData("probes; DROP TABLE x")]
    [InlineData("1probes")]
    [InlineData("")]
    public void Constructor_ANameThatIsNoLowerCaseIdentifier_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new KeyedTable(name, "probes"));
    }

    [Fact]
    public void Constructor_ValuesWithoutAContentKeysVersionColumn_Throws()
    {
        Assert.Throws<ArgumentException>(() => new KeyedTable("probes", "probes", values: [new KeyedValue("secret", "secret")]));
    }

    [Fact]
    public void Erase_ATableThatDeletes_DeletesThePersonsRows()
    {
        KeyedTable table = new("probes", "probes", person: "person");

        Assert.Equal("DELETE FROM messaging.probes WHERE person = @person", table.Erase());
    }

    [Fact]
    public void Erase_ATableThatClears_EmptiesThePersonValuesAndVersion()
    {
        KeyedTable table = new("probes", "probes", person: "owner", values: [new KeyedValue("secret", "secret")], contentKeyVersion: "content_key_version", erasure: KeyedErasure.Clear);

        Assert.Equal("UPDATE messaging.probes SET owner = NULL, secret = NULL, content_key_version = NULL WHERE owner = @person", table.Erase());
    }

    [Fact]
    public void Erase_ATableWithoutAPersonColumn_ErasesNothing()
    {
        Assert.Null(new KeyedTable("probes", "probes", hashVersion: "hash_version").Erase());
    }

    [Fact]
    public void NoRowUnder_KeyedTables_AddEachTableRecordingTheColumnToTheKnownCondition()
    {
        KeyedTable recording = new("probes", "probes", hashVersion: "hash_version");
        KeyedTable other = new("others", "others", person: "person");

        string statement = RetentionKeys.NoRowUnder("NOT EXISTS (SELECT 1 FROM messaging.places WHERE hash_version = @version)", [recording, other], table => table.HashVersion);

        Assert.Equal(
            "SELECT NOT EXISTS (SELECT 1 FROM messaging.places WHERE hash_version = @version) AND NOT EXISTS (SELECT 1 FROM messaging.probes WHERE hash_version = @version) AS \"Value\"",
            statement);
    }
}