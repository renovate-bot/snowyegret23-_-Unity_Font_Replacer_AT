using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;
using UnityFontReplacer.CLI;

namespace UnityFontReplacer;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                PrintException("UnhandledException", ex);
            else
                AnsiConsole.MarkupLine($"[red]UnhandledException (non-Exception): {Markup.Escape(e.ExceptionObject?.ToString() ?? "null")}[/]");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            PrintException("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        var sw = Stopwatch.StartNew();

        int result;
        try
        {
            var rootCommand = CommandBuilder.Build();
            var parseResult = rootCommand.Parse(args);
            result = await parseResult.InvokeAsync(new InvocationConfiguration(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            PrintException("Fatal", ex);
            result = 1;
        }

        sw.Stop();
        AnsiConsole.MarkupLine($"[dim]Elapsed: {FormatElapsed(sw.Elapsed)}[/]");

        return result;
    }

    private static void PrintException(string label, Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{label}: {Markup.Escape(ex.GetType().FullName ?? "Exception")}: {Markup.Escape(ex.Message)}[/]");
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            AnsiConsole.WriteLine(ex.StackTrace);
        var inner = ex.InnerException;
        while (inner != null)
        {
            AnsiConsole.MarkupLine($"[red]  Caused by: {Markup.Escape(inner.GetType().FullName ?? "Exception")}: {Markup.Escape(inner.Message)}[/]");
            if (!string.IsNullOrWhiteSpace(inner.StackTrace))
                AnsiConsole.WriteLine(inner.StackTrace);
            inner = inner.InnerException;
        }
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
