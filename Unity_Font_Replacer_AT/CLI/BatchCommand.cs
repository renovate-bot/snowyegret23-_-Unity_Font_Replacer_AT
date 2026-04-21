using System.CommandLine;
using Spectre.Console;
using UnityFontReplacer.Core;
using UnityFontReplacer.Models;

namespace UnityFontReplacer.CLI;

public static class BatchCommand
{
    private sealed class BatchFontSpec
    {
        public required string SdfSource { get; init; }
        public string? TtfPath { get; init; }
    }

    public static Command Build(Option<string> gamePathOption)
    {
        var batchCommand = new Command("batch", "Batch replace all fonts with a builtin font");

        var fontOption = new Option<string>(
            aliases: ["--font", "-f"],
            description: "Builtin font name (mulmaru / nanumgothic) or path to font directory")
        { IsRequired = true };

        var ps5Option = new Option<bool>("--ps5-swizzle", "Handle PS5 texture swizzle");
        var outputOption = new Option<string?>("--output-only", "Write modified files to this directory instead of in-place");
        var sdfOnlyOption = new Option<bool>("--sdfonly", "Replace SDF fonts only");
        var ttfOnlyOption = new Option<bool>("--ttfonly", "Replace TTF fonts only");

        batchCommand.AddOption(gamePathOption);
        batchCommand.AddOption(fontOption);
        batchCommand.AddOption(ps5Option);
        batchCommand.AddOption(outputOption);
        batchCommand.AddOption(sdfOnlyOption);
        batchCommand.AddOption(ttfOnlyOption);

        batchCommand.SetHandler(async (gamePath, font, ps5, output, sdfOnly, ttfOnly) =>
        {
            await ExecuteAsync(gamePath, font, ps5, output, sdfOnly, ttfOnly);
        }, gamePathOption, fontOption, ps5Option, outputOption, sdfOnlyOption, ttfOnlyOption);

        return batchCommand;
    }

    public static async Task ExecuteAsync(
        string gamePath, string fontName, bool ps5Swizzle,
        string? outputDir, bool sdfOnly, bool ttfOnly)
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

        var fontSpec = ResolveBatchFontSpec(fontName);
        if (fontSpec == null)
        {
            AnsiConsole.MarkupLine($"[red]Font not found: {Markup.Escape(fontName)}[/]");
            return;
        }

        using var ctx = new AssetsContext(resolved.DataPath, resolved.ManagedPath);

        var version = ctx.DetectUnityVersion();
        if (version != null)
        {
            ctx.LoadClassDatabase(version);
            ctx.SetupMonoCecil();
        }

        // 1. 스캔
        var scanner = new FontScanner(ctx);
        var scanResult = scanner.ScanAll(resolved.AssetFiles, ps5Swizzle);

        // 2. 매핑 생성 - 모든 폰트를 해당 폰트로 교체
        var mapping = FontMapping.FromScanResult(scanResult, resolved.GamePath);
        mapping.UnityVersion = version ?? "";

        int assignCount = 0;
        foreach (var entry in mapping.Fonts.Values)
        {
            if (sdfOnly && entry.Type == FontType.TTF) continue;
            if (ttfOnly && entry.Type == FontType.SDF) continue;

            if (entry.Type == FontType.SDF)
            {
                entry.ReplaceTo = fontSpec.SdfSource;
                assignCount++;
                continue;
            }

            if (fontSpec.TtfPath == null)
                continue;

            entry.ReplaceTo = fontSpec.TtfPath;
            assignCount++;
        }

        AnsiConsole.MarkupLine($"Replacement targets: [green]{assignCount}[/]");

        if (assignCount == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No fonts to replace[/]");
            return;
        }

        // 3. CAB 매핑 수집 (크로스 번들 텍스처 교체용)
        var replacer = new FontReplacer(ctx);
        replacer.BuildCabMappings(resolved.AssetFiles);

        // 4. 교체 실행
        var replaced = replacer.ReplaceFromMapping(mapping, outputDir);

        AnsiConsole.MarkupLine($"[green]Batch complete: {replaced} font(s) replaced[/]");
    }

    private static BatchFontSpec? ResolveBatchFontSpec(string fontName)
    {
        if (TryGetBuiltinAlias(fontName, out var builtinAlias))
        {
            var ttfPath = ResolveBuiltinTtfPath(builtinAlias);
            return new BatchFontSpec
            {
                SdfSource = builtinAlias,
                TtfPath = ttfPath,
            };
        }

        if (Directory.Exists(fontName))
        {
            return new BatchFontSpec
            {
                SdfSource = fontName,
                TtfPath = Directory
                    .EnumerateFiles(fontName, "*.*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path =>
                        path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)),
            };
        }

        if (File.Exists(fontName))
        {
            if (fontName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                fontName.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            {
                return new BatchFontSpec
                {
                    SdfSource = Path.GetDirectoryName(fontName)!,
                    TtfPath = fontName,
                };
            }

            if (fontName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(fontName)!;
                var ttfPath = Directory
                    .EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path =>
                        path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
                return new BatchFontSpec
                {
                    SdfSource = fontName,
                    TtfPath = ttfPath,
                };
            }
        }

        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(exeDir, "KR_ASSETS", fontName),
            Path.Combine(exeDir, fontName),
            Path.Combine(Directory.GetCurrentDirectory(), "KR_ASSETS", fontName),
            Path.Combine(Directory.GetCurrentDirectory(), fontName),
        };

        foreach (var dir in candidates)
        {
            if (!Directory.Exists(dir))
                continue;

            var jsonPath = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (jsonPath == null)
                continue;

            var ttfPath = Directory
                .EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path =>
                    path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

            return new BatchFontSpec
            {
                SdfSource = dir,
                TtfPath = ttfPath,
            };
        }

        return null;
    }

    private static bool TryGetBuiltinAlias(string input, out string alias)
    {
        alias = "";
        var normalized = input.Trim();
        if (normalized.Equals("mulmaru", StringComparison.OrdinalIgnoreCase))
        {
            alias = "Mulmaru";
            return true;
        }

        if (normalized.Equals("nanumgothic", StringComparison.OrdinalIgnoreCase))
        {
            alias = "NanumGothic";
            return true;
        }

        return false;
    }

    private static string? ResolveBuiltinTtfPath(string alias)
    {
        foreach (var root in EnumerateKrAssetsRoots())
        {
            foreach (var ext in new[] { ".ttf", ".otf" })
            {
                var path = Path.Combine(root, alias + ext);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateKrAssetsRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[]
                 {
                     Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KR_ASSETS"),
                     Path.Combine(Directory.GetCurrentDirectory(), "KR_ASSETS"),
                 })
        {
            if (Directory.Exists(root) && seen.Add(root))
                yield return root;
        }
    }
}
