using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Microsoft.AspNetCore.Http;

namespace Hermes.Tests;

/// <summary>An identity seam answering the person a request names in its <c>X-Person</c> header, or none.</summary>
internal sealed class RecipientIdentityFake(IHttpContextAccessor accessor) : IRecipientIdentity
{
    internal const string Header = "X-Person";

    public ValueTask<string?> CurrentAsync(CancellationToken cancellationToken)
    {
        string? person = accessor.HttpContext?.Request.Headers[Header].ToString();
        return ValueTask.FromResult(string.IsNullOrEmpty(person) ? null : person);
    }
}