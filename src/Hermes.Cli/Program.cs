using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Operations;

namespace Hermes.Cli;

/// <summary>The command <c>hermes</c>, whose one command, <c>migrate</c>, applies the release's migrations (<c>06</c> MSG-OPS-002).</summary>
internal static class Program
{
    private const string Usage = "Usage: hermes migrate, reading {\"connection\": \"...\", \"applicationRoles\": [\"...\"]} on standard input.";

    private static async Task<int> Main(string[] args)
    {
        using StreamReader input = new(Console.OpenStandardInput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return await RunAsync(args, input, Console.Error, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the command <paramref name="arguments"/> name, reading its input from
    /// <paramref name="input"/>; answers 0 on success, and on a failure 1, having written one
    /// JSON line on <paramref name="error"/>.
    /// </summary>
    internal static async Task<int> RunAsync(IReadOnlyList<string> arguments, TextReader input, TextWriter error, CancellationToken cancellationToken)
    {
        if (arguments is not ["migrate"])
        {
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 1;
        }

        string text = await input.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        MigrationFailure? failure = MigrationInput.Read(text, out MigrationInput? read) ?? await SchemaMigration.ApplyAsync(read!.Connection, read.ApplicationRoles, cancellationToken).ConfigureAwait(false);
        if (failure is null)
        {
            return 0;
        }

        await error.WriteAsync(JsonSerializer.Serialize(new Dictionary<string, object> { ["code"] = failure.Code, ["details"] = failure.Details }) + "\n").ConfigureAwait(false);
        return 1;
    }
}