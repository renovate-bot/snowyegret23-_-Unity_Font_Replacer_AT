using Spectre.Console;
using UnityFontReplacer.Core;
using UnityFontReplacer.Export;

namespace UnityFontReplacer.CLI;

public static class ExportFontsCommand
{
    public static async Task ExecuteAsync(string gamePath)
    {
        await Task.CompletedTask;

        var resolved = GamePathResolver.Resolve(gamePath);
        if (resolved == null)
        {
            AnsiConsole.MarkupLine($"[red]{Strings.Get("err_gamepath_not_found", gamePath)}[/]");
            return;
        }
        try
        {
            Il2CppManagedGenerator.EnsureManagedFolder(resolved);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return;
        }

        using var ctx = new AssetsContext(resolved.DataPath, resolved.ManagedPath);

        var version = ctx.DetectUnityVersion();
        if (version != null)
        {
            ctx.LoadClassDatabase(version);
            ctx.SetupMonoCecil();
        }

        var outputDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "exported_fonts");

        var extractor = new FontExtractor(ctx);
        var count = extractor.Extract(resolved.AssetFiles, outputDir);

        AnsiConsole.MarkupLine($"[green]Export complete: {count} font(s) → {Markup.Escape(outputDir)}[/]");
    }
}
