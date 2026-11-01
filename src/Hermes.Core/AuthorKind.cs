using System.Text.Json.Serialization;

namespace Hermes.Core;

/// <summary>Who wrote a message (<c>09</c> MSG-CONV-001; <c>08</c> section 2).</summary>
public enum AuthorKind
{
    /// <summary>A person, by their person reference.</summary>
    [JsonStringEnumMemberName("person")]
    Person,

    /// <summary>A visitor.</summary>
    [JsonStringEnumMemberName("visitor")]
    Visitor,

    /// <summary>The library, for an <c>automatic</c> or <c>system</c> message.</summary>
    [JsonStringEnumMemberName("library")]
    Library,
}