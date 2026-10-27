using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Testing.Tests;

/// <summary>A host's templates: each kind's email subject and text and its SMS text, reading the value <c>code</c>; the library's own pages are its shipped ones.</summary>
internal sealed class TemplateSourceFake : ITemplateSource
{
    public ValueTask<string?> ReadAsync(TemplateKey key, CancellationToken cancellationToken)
    {
        string? template = key switch
        {
            { Page: not null } => null,
            { Channel: Channel.Email, Part: "subject" } => "Your code",
            { Channel: Channel.Email, Part: "text" } => "Your code is {{ code }}.",
            { Channel: Channel.Sms, Part: "text" } => "Code {{ code }}",
            _ => null,
        };
        return ValueTask.FromResult(template);
    }
}