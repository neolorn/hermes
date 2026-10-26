namespace Hermes.Core;

/// <summary>What a library call answers: success, or a failure with its code (<c>07</c> MSG-ENG-003).</summary>
public sealed record Result
{
    /// <summary>Whether the call succeeded.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The failure, where the call did not succeed.</summary>
    public Failure? Failure { get; init; }

    /// <summary>A success.</summary>
    /// <returns>The result.</returns>
    public static Result Success()
    {
        return new Result { IsSuccess = true };
    }

    /// <summary>A failure.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The result.</returns>
    public static Result Fail(Failure failure)
    {
        return new Result { IsSuccess = false, Failure = failure };
    }

    /// <summary>A success carrying <paramref name="value"/>.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T> { IsSuccess = true, Value = value };
    }

    /// <summary>A failure of a call that answers a value.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="failure">The failure.</param>
    /// <returns>The result.</returns>
    public static Result<T> Fail<T>(Failure failure)
    {
        return new Result<T> { IsSuccess = false, Failure = failure };
    }
}