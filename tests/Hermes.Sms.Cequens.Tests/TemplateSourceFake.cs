using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Sms.Cequens.Tests;

/// <summary>A host's templates: each kind's SMS text, reading the value <c>code</c>; the library's own pages are its shipped ones.</summary>
internal sealed class TemplateSourceFake : ITemplateSource
{
    public ValueTask<string?> ReadAsync(TemplateKey key, CancellationToken cancellationToken)
    {
        string? template = key switch
        {
            { Page: not null } => null,
            { Channel: Channel.Sms, Part: "text" } => "Your code is {{ code }}",
            _ => null,
        };
        return ValueTask.FromResult(template);
    }
}