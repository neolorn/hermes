using System;

using Fluid;

namespace Hermes.Content;

/// <summary>
/// The library's own subclass of Fluid's context a part renders on (<c>03</c> MSG-TPL-007): it
/// tells a render past its steps from a fault, since Fluid throws one exception for both its
/// limits, carries the part's budget for its buffers and its filters' results, and records the
/// refusal a buffer or a filter stops the render with.
/// </summary>
internal sealed class RenderContext(TemplateOptions options, RenderBudget budget, bool html) : TemplateContext(options)
{
    /// <summary>The steps the render has taken, which Fluid counts.</summary>
    internal int Steps => _steps;

    /// <summary>The budget the part's buffers share.</summary>
    internal RenderBudget Budget { get; } = budget;

    /// <summary>Whether the part is HTML, written under the HTML encoder.</summary>
    internal bool Html { get; } = html;

    /// <summary>Whether the render has passed its steps.</summary>
    internal bool PastSteps => Steps > MaxSteps;

    /// <summary>The code the render was stopped with, where a buffer or a filter stopped it.</summary>
    internal string? Refusal { get; private set; }

    /// <summary>
    /// Records <paramref name="code"/> as the render's refusal, the first one standing, and
    /// answers the exception that stops the render, which the renderer turns into that refusal.
    /// </summary>
    internal InvalidOperationException Refuse(string code)
    {
        Refusal ??= code;
        return new InvalidOperationException(code);
    }

    /// <summary>Stops the render with <c>toolong</c> where <paramref name="text"/>, a filter's result, is longer than the limit.</summary>
    internal void Check(string text)
    {
        Check(Budget.Measure(text));
    }

    /// <summary>Stops the render with <c>toolong</c> where a result of <paramref name="length"/> would be longer than the limit.</summary>
    internal void Check(long length)
    {
        if (length > Budget.Limit)
        {
            throw Refuse(Codes.TooLong);
        }
    }
}