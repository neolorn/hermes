using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Hermes.Sending;

namespace Hermes;

/// <summary>
/// Declares, in <see cref="MessagingServiceCollectionExtensions.AddHermes"/>, the kinds the
/// host sends, the categories they belong to, and the kinds it no longer sends. The
/// declarations are checked when the host starts: one outside its rule stops the start with
/// <c>messaging.startup.declarationinvalid</c>, naming the declaration and the member.
/// </summary>
public sealed class DeclarationBuilder
{
    private readonly List<KindBuilder> kinds = [];
    private readonly List<CategoryBuilder> categories = [];
    private readonly List<string> retired = [];
    private readonly List<Assembly> assemblies = [];

    internal DeclarationBuilder()
    {
    }

    /// <summary>
    /// Declares a kind: its name, 1 to 64 lower-case letters and digits separated by single
    /// <c>.</c>, <c>-</c> or <c>_</c>, declared once, and the declared category it belongs to.
    /// </summary>
    /// <param name="name">The kind's name.</param>
    /// <param name="category">The name of the category it belongs to.</param>
    /// <returns>The kind's builder, which declares the channels it may go out on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="category"/> is null.</exception>
    public KindBuilder Kind(string name, string category)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(category);
        KindBuilder kind = new(name, category);
        kinds.Add(kind);
        return kind;
    }

    /// <summary>
    /// Declares a kind the host no longer sends: a send of it is a fault of the calling code,
    /// and each of its deliveries not yet final ends <c>cancelled</c> when next claimed. The
    /// name follows a kind's rule, and a name also declared as a kind, or retired twice, stops
    /// the start.
    /// </summary>
    /// <param name="name">The retired kind's name.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public DeclarationBuilder Retire(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        retired.Add(name);
        return this;
    }

    /// <summary>
    /// Declares a category, whose name follows the rule of a kind's and is declared once; its
    /// builder declares the rules every kind in it shares.
    /// </summary>
    /// <param name="name">The category's name.</param>
    /// <returns>The category's builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public CategoryBuilder Category(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        CategoryBuilder category = new(name);
        categories.Add(category);
        return category;
    }

    /// <summary>
    /// Passes an assembly whose embedded resources hold templates, each found by a logical name
    /// equal to its file's path under <c>Hermes:Templates:Path</c>
    /// (<c>order.placed/email.ar.subject.liquid</c>; for a page,
    /// <c>pages/unsubscribe.ar.html.liquid</c>). A part the host's template source does not answer
    /// is read from that folder, then from these assemblies in the order they are passed.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
    public DeclarationBuilder Templates(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        assemblies.Add(assembly);
        return this;
    }

    /// <summary>What has been declared so far, in the order it was declared.</summary>
    internal Declarations Declarations()
    {
        return new([.. kinds.Select(kind => kind.Declaration())], [.. categories.Select(category => category.Declaration())], [.. retired], [.. assemblies]);
    }
}