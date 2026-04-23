using System.CommandLine;
using Spectre.Console;
using UnityFontReplacer.Core;
using UnityFontReplacer.Models;
using UnityFontReplacer.SDF;

namespace UnityFontReplacer.CLI;

public static class OneShotCommand
{
    private const int DefaultPadding = 7;

    public static Command Build(Option<string> gamePathOption)
    {
        var oneShotCommand = new Command("oneshot", Strings.Get("cmd_oneshot"));

        var fontOption = new Option<string>(
            aliases: ["--font", "-f"],
            description: "TTF/OTF file path or resolvable font name")
        { IsRequired = true };

        var ps5Option = new Option<bool>("--ps5-swizzle", "Handle PS5 texture swizzle");
        var outputOption = new Option<string?>("--output-only", "Write modified files to this directory instead of in-place");
        var sdfOnlyOption = new Option<bool>("--sdfonly", "Replace SDF fonts only");
        var ttfOnlyOption = new Option<bool>("--ttfonly", "Replace TTF fonts only");

        oneShotCommand.AddOption(gamePathOption);
        oneShotCommand.AddOption(fontOption);
        oneShotCommand.AddOption(ps5Option);
        oneShotCommand.AddOption(outputOption);
        oneShotCommand.AddOption(sdfOnlyOption);
        oneShotCommand.AddOption(ttfOnlyOption);

        oneShotCommand.SetHandler(async (gamePath, font, ps5, output, sdfOnly, ttfOnly) =>
        {
            await ExecuteAsync(gamePath, font, ps5, output, sdfOnly, ttfOnly);
        }, gamePathOption, fontOption, ps5Option, outputOption, sdfOnlyOption, ttfOnlyOption);

        return oneShotCommand;
    }

    public static async Task ExecuteAsync(
        string gamePath,
        string fontName,
        bool ps5Swizzle,
        string? outputDir,
        bool sdfOnly,
        bool ttfOnly)
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

        var ttfPath = TtfFontHandler.ResolveTtfPath(fontName);
        if (ttfPath == null)
        {
            AnsiConsole.MarkupLine($"[red]TTF not found: {Markup.Escape(fontName)}[/]");
            return;
        }

        byte[] ttfData;
        try
        {
            ttfData = File.ReadAllBytes(ttfPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to read TTF: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        using var ctx = new AssetsContext(resolved.DataPath, resolved.ManagedPath);

        var version = ctx.DetectUnityVersion();
        if (version != null)
        {
            ctx.LoadClassDatabase(version);
            ctx.SetupMonoCecil();
        }

        var scanner = new FontScanner(ctx);
        var scanResult = scanner.ScanAll(resolved.AssetFiles, ps5Swizzle);

        var mapping = FontMapping.FromScanResult(scanResult, resolved.GamePath);
        mapping.UnityVersion = version ?? "";

        string? tempRoot = null;

        try
        {
            Dictionary<int, string> generatedSdfDirs = new();
            if (!ttfOnly)
            {
                var paddings = mapping.Fonts.Values
                    .Where(entry => entry.Type == FontType.SDF)
                    .Select(entry => NormalizePadding(entry.AtlasPadding))
                    .Distinct()
                    .OrderBy(value => value)
                    .ToList();

                if (paddings.Count > 0)
                {
                    int[] unicodes;
                    try
                    {
                        unicodes = MakeSdfCommand.LoadCharset(MakeSdfCommand.DefaultCharsetArgument);
                    }
                    catch (FileNotFoundException ex)
                    {
                        AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
                        return;
                    }

                    if (unicodes.Length == 0)
                    {
                        AnsiConsole.MarkupLine("[red]Default charset is empty.[/]");
                        return;
                    }

                    tempRoot = Path.Combine(
                        Path.GetTempPath(),
                        "UnityFontReplacer_Oneshot",
                        Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempRoot);

                    var fontBaseName = Path.GetFileNameWithoutExtension(ttfPath);
                    foreach (var padding in paddings)
                    {
                        var paddingDir = Path.Combine(tempRoot, $"padding_{padding}");
                        Directory.CreateDirectory(paddingDir);

                        AnsiConsole.MarkupLine($"[cyan]Generating SDF: padding {padding}[/]");
                        var result = SdfGenerator.Generate(
                            ttfData,
                            unicodes,
                            atlasWidth: 4096,
                            atlasHeight: 4096,
                            padding: padding,
                            pointSize: 0,
                            rasterMode: false,
                            filterMode: TextureFilterMode.Bilinear);

                        try
                        {
                            SdfGenerator.SaveToFiles(result, paddingDir, fontBaseName);
                        }
                        finally
                        {
                            result.AtlasImage.Dispose();
                        }

                        generatedSdfDirs[padding] = paddingDir;
                    }
                }
            }

            int assignCount = 0;
            foreach (var entry in mapping.Fonts.Values)
            {
                if (sdfOnly && entry.Type == FontType.TTF)
                    continue;
                if (ttfOnly && entry.Type == FontType.SDF)
                    continue;

                if (entry.Type == FontType.TTF)
                {
                    entry.ReplaceTo = ttfPath;
                    assignCount++;
                    continue;
                }

                var padding = NormalizePadding(entry.AtlasPadding);
                if (!generatedSdfDirs.TryGetValue(padding, out var sdfDir))
                    continue;

                entry.ReplaceTo = sdfDir;
                assignCount++;
            }

            AnsiConsole.MarkupLine($"Replacement targets: [green]{assignCount}[/]");

            if (assignCount == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No fonts to replace[/]");
                return;
            }

            var replacer = new FontReplacer(ctx);
            replacer.BuildCabMappings(resolved.AssetFiles);

            var replaced = replacer.ReplaceFromMapping(mapping, outputDir);
            AnsiConsole.MarkupLine($"[green]Oneshot complete: {replaced} font(s) replaced[/]");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    private static int NormalizePadding(int padding)
    {
        return padding > 0 ? padding : DefaultPadding;
    }
}
