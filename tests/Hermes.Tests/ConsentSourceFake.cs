using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

namespace Hermes.Tests;

/// <summary>A consent source that keeps each query and answers as a test sets: <see cref="ConsentAnswer.Given"/> unless told otherwise.</summary>
internal sealed class ConsentSourceFake : IConsentSource
{
    private readonly Lock gate = new();

    private readonly List<ConsentQuery> queries = [];

    internal Func<ConsentQuery, CancellationToken, Task<ConsentAnswer>> Answer { get; set; } = (_, _) => Task.FromResult(ConsentAnswer.Given);

    internal IReadOnlyList<ConsentQuery> Queries
    {
        get
        {
            lock (gate)
            {
                return [.. queries];
            }
        }
    }

    public async ValueTask<ConsentAnswer> ConsentsAsync(ConsentQuery query, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            queries.Add(query);
        }

        return await Answer(query, cancellationToken);
    }
}