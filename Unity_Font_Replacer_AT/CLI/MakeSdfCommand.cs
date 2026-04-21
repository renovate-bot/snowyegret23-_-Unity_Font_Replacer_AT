using Spectre.Console;
using UnityFontReplacer.SDF;

namespace UnityFontReplacer.CLI;

public static class MakeSdfCommand
{
    public static async Task ExecuteAsync(
        string ttf, string atlasSize, int pointSize,
        int padding, string charset, string renderMode)
    {
        await Task.CompletedTask;

        // TTF 파일 로드
        var ttfPath = ResolveTtfPath(ttf);
        if (ttfPath == null)
        {
            AnsiConsole.MarkupLine($"[red]TTF not found: {Markup.Escape(ttf)}[/]");
            return;
        }

        var ttfData = File.ReadAllBytes(ttfPath);
        var fontName = Path.GetFileNameWithoutExtension(ttfPath);

        // 아틀라스 크기 파싱
        var (aw, ah) = ParseAtlasSize(atlasSize);

        // 캐릭터셋 로드
        var unicodes = LoadCharset(charset);
        if (unicodes.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No characters to process. Using default ASCII range.[/]");
            unicodes = Enumerable.Range(32, 95).ToArray(); // ASCII 32-126
        }

        bool rasterMode = renderMode.Equals("raster", StringComparison.OrdinalIgnoreCase);

        AnsiConsole.MarkupLine($"Font: [green]{Markup.Escape(fontName)}[/]");
        AnsiConsole.MarkupLine($"Atlas: [green]{aw}x{ah}[/], Padding: [green]{padding}[/]");
        AnsiConsole.MarkupLine($"Characters: [green]{unicodes.Length}[/], Mode: [green]{(rasterMode ? "Raster" : "SDF")}[/]");

        try
        {
            var result = SdfGenerator.Generate(ttfData, unicodes, aw, ah, padding, pointSize, rasterMode);

            var outputDir = Directory.GetCurrentDirectory();
            SdfGenerator.SaveToFiles(result, outputDir, fontName);

            result.AtlasImage.Dispose();

            AnsiConsole.MarkupLine($"[green]SDF generation complete![/]");
            AnsiConsole.MarkupLine($"  Point size: [cyan]{result.FontAsset.FaceInfo.PointSize}[/]");
            AnsiConsole.MarkupLine($"  Glyphs: [cyan]{result.FontAsset.GlyphCount}[/]");
            AnsiConsole.MarkupLine($"  Output: [cyan]{Markup.Escape(outputDir)}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]SDF generation failed: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static string? ResolveTtfPath(string ttf)
    {
        if (File.Exists(ttf)) return ttf;

        var inCwd = Path.Combine(Directory.GetCurrentDirectory(), ttf);
        if (File.Exists(inCwd)) return inCwd;

        var inExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ttf);
        if (File.Exists(inExe)) return inExe;

        return null;
    }

    private static (int width, int height) ParseAtlasSize(string s)
    {
        var parts = s.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out int w) &&
            int.TryParse(parts[1].Trim(), out int h))
            return (w, h);

        return (4096, 4096);
    }

    private static int[] LoadCharset(string charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
            return [];

        // 파일인지 확인
        if (File.Exists(charset))
        {
            var text = File.ReadAllText(charset);
            return TextToUnicodes(text);
        }

        // CWD에서 찾기
        var inCwd = Path.Combine(Directory.GetCurrentDirectory(), charset);
        if (File.Exists(inCwd))
        {
            var text = File.ReadAllText(inCwd);
            return TextToUnicodes(text);
        }

        // 리터럴 문자열로 취급
        return TextToUnicodes(charset);
    }

    private static int[] TextToUnicodes(string text)
    {
        var set = new HashSet<int>();
        for (int i = 0; i < text.Length; i++)
        {
            int cp;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                cp = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }
            else
            {
                cp = text[i];
            }

            // NUL과 서로게이트 제외
            if (cp > 0 && (cp < 0xD800 || cp > 0xDFFF))
                set.Add(cp);
        }

        return set.Order().ToArray();
    }
}
