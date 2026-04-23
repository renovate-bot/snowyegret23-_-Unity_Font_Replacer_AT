using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using UnityFontReplacer.CLI;

namespace UnityFontReplacer;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var sw = Stopwatch.StartNew();

        var rootCommand = CommandBuilder.Build();
        var parseResult = rootCommand.Parse(args);
        var result = await parseResult.InvokeAsync(new InvocationConfiguration(), CancellationToken.None);

        sw.Stop();
        AnsiConsole.MarkupLine($"[dim]Elapsed: {FormatElapsed(sw.Elapsed)}[/]");

        return result;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 1)
            return $"{elapsed.TotalMilliseconds:F0}ms";
        if (elapsed.TotalMinutes < 1)
            return $"{elapsed.TotalSeconds:F1}s";
        return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
    }
}
