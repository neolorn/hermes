using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;
using Hermes.Sending;

namespace Hermes.Tests;

/// <summary>A host's template source answering each key as a test sets, and recording the keys it was asked.</summary>
internal sealed class TemplateSourceFake(Func<TemplateKey, string?> answer) : ITemplateSource
{
    private readonly ConcurrentQueue<TemplateKey> asked = new();

    /// <summary>The keys asked, in order.</summary>
    internal IReadOnlyList<TemplateKey> Asked => [.. asked];

    /// <summary>The answer for a key, without recording it.</summary>
    internal Func<TemplateKey, string?> Answer => answer;

    /// <summary>
    /// A source answering every required part of every kind <paramref name="declare"/> declares,
    /// and every page, with the least template the startup check passes: an optional category's
    /// email text reads <c>unsubscribe</c>, and no optional part is answered.
    /// </summary>
    internal static TemplateSourceFake Every(Action<DeclarationBuilder>? declare)
    {
        DeclarationBuilder builder = new();
        declare?.Invoke(builder);
        Declarations declarations = builder.Declarations();
        HashSet<string> optional = [.. declarations.Kinds
            .Where(kind => declarations.Categories.Any(category => category.Name == kind.Category && !category.Essential))
            .Select(kind => kind.Name)];

        return new TemplateSourceFake(key => key switch
        {
            { Page: not null } => "<p>{{ language }}</p>",
            { Channel: Channel.Email, Part: "subject" } => "Subject",
            { Channel: Channel.Email, Part: "text" } => optional.Contains(key.Kind!) ? "{{ unsubscribe }}" : "Text",
            { Channel: Channel.Sms, Part: "text" } => "Text",
            { Channel: Channel.Push, Part: "title" } => "Title",
            { Channel: Channel.Push, Part: "body" } => "Body",
            { Channel: Channel.InApp, Part: "title" } => "Title",
            _ => null,
        });
    }

    public ValueTask<string?> ReadAsync(TemplateKey key, CancellationToken cancellationToken)
    {
        asked.Enqueue(key);
        return ValueTask.FromResult(answer(key));
    }
}