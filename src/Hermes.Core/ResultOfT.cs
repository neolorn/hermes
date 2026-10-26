namespace Hermes.Core;

/// <summary>What a library call that answers a value answers: the value, or a failure with its code (<c>07</c> MSG-ENG-003).</summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed record Result<T>
{
    /// <summary>Whether the call succeeded.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The value, where the call succeeded.</summary>
    public T? Value { get; init; }

    /// <summary>The failure, where the call did not succeed.</summary>
    public Failure? Failure { get; init; }
}