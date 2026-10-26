using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Content;

/// <summary>
/// A buffer a render writes into, which stops the render once the buffers sharing its budget
/// hold more than the channel's limit (<c>03</c> MSG-TPL-007). Every write is synchronous, so a
/// refusal is thrown where the write is made.
/// </summary>
internal sealed class BoundedWriter(RenderBudget budget, RenderContext context) : TextWriter
{
    private readonly StringBuilder text = new();

    private bool afterHigh;

    public override Encoding Encoding => Encoding.Unicode;

    public override void Write(char value)
    {
        Write(new ReadOnlySpan<char>(in value));
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Write(buffer.AsSpan(index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (!budget.Take(buffer, ref afterHigh))
        {
            throw context.Refuse(Codes.TooLong);
        }

        text.Append(buffer);
    }

    public override void Write(string? value)
    {
        Write(value.AsSpan());
    }

    public override Task WriteAsync(char value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(string? value)
    {
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(char[] buffer, int index, int count)
    {
        Write(buffer, index, count);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return Task.CompletedTask;
    }

    public override string ToString()
    {
        return text.ToString();
    }
}