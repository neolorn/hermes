using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Hermes.Channels.Tests;

/// <summary>A logger provider that keeps every entry and every scope written, at every level.</summary>
internal sealed class LogCapture : ILoggerProvider
{
    /// <summary>The text of each entry and each scope, with its category.</summary>
    internal ConcurrentQueue<string> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private sealed class Logger(LogCapture capture, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            capture.Lines.Enqueue($"{category} scope: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            capture.Lines.Enqueue($"{category} {logLevel}: {formatter(state, exception)} {state} {exception}");
        }
    }
}