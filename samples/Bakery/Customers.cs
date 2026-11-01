using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Bakery;

/// <summary>The people the bakery knows, held in memory; a host reads them from its own store.</summary>
internal sealed class Customers
{
    private readonly FrozenDictionary<string, Customer> known = new Customer[]
    {
        new("c-1", "amal@example.com", "+201000000001", Staff: false),
        new("c-2", "omar@example.net", "+201000000002", Staff: false),
        new("s-1", "counter@example.com", "+201000000003", Staff: true),
    }.ToFrozenDictionary(customer => customer.Id, StringComparer.Ordinal);

    /// <summary>The person <paramref name="id"/> names, or none.</summary>
    internal Customer? Find(string id)
    {
        return known.GetValueOrDefault(id);
    }

    /// <summary>The staff alone.</summary>
    internal IReadOnlyList<Customer> Staff()
    {
        return [.. known.Values.Where(customer => customer.Staff).OrderBy(customer => customer.Id, StringComparer.Ordinal)];
    }

    /// <summary>Every customer, staff aside.</summary>
    internal IReadOnlyList<Customer> All()
    {
        return [.. known.Values.Where(customer => !customer.Staff).OrderBy(customer => customer.Id, StringComparer.Ordinal)];
    }
}