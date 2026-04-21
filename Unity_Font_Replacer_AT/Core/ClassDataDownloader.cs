using System.IO.Compression;
using Spectre.Console;

namespace UnityFontReplacer.Core;

/// <summary>
/// classdata.tpk 자동 다운로드.
/// AssetRipper/Tpk nightly build에서 LZ4 압축 버전을 받는다.
/// </summary>
public static class ClassDataDownloader
{
    private const string DownloadUrl =
        "https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/lz4_file.zip";

    private const string TpkFileName = "classdata.tpk";

    /// <summary>
    /// classdata.tpk를 찾거나, 없으면 다운로드한다.
    /// </summary>
    public static string EnsureClassData()
    {
        // 1. 기존 파일 검색
        var existing = FindExisting();
        if (existing != null)
            return existing;

        // 2. 다운로드
        var targetDir = AppDomain.CurrentDomain.BaseDirectory;
        var targetPath = Path.Combine(targetDir, TpkFileName);

        AnsiConsole.MarkupLine("[yellow]classdata.tpk not found, downloading...[/]");

        try
        {
            DownloadTpk(targetPath);
            AnsiConsole.MarkupLine($"[green]Downloaded classdata.tpk → {Markup.Escape(targetPath)}[/]");
            return targetPath;
        }
        catch (Exception ex)
        {
            // exe 디렉토리에 쓸 수 없으면 CWD에 시도
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), TpkFileName);
            try
            {
                DownloadTpk(cwdPath);
                AnsiConsole.MarkupLine($"[green]Downloaded classdata.tpk → {Markup.Escape(cwdPath)}[/]");
                return cwdPath;
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Failed to download classdata.tpk: {ex.Message}\n" +
                    $"Download manually from: {DownloadUrl}");
            }
        }
    }

    private static string? FindExisting()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TpkFileName),
            Path.Combine(Directory.GetCurrentDirectory(), TpkFileName),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void DownloadTpk(string targetPath)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        // nightly.link는 zip으로 래핑된 artifact를 반환
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Downloading classdata.tpk...", ctx =>
            {
                var zipBytes = httpClient.GetByteArrayAsync(DownloadUrl).GetAwaiter().GetResult();

                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                // zip 안에서 .tpk 파일 찾기
                var tpkEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.EndsWith(".tpk", StringComparison.OrdinalIgnoreCase));

                if (tpkEntry == null)
                {
                    // tpk가 아니면 첫 번째 파일 사용
                    tpkEntry = archive.Entries.FirstOrDefault()
                        ?? throw new InvalidOperationException("Downloaded zip is empty");
                }

                using var entryStream = tpkEntry.Open();
                using var fileStream = File.Create(targetPath);
                entryStream.CopyTo(fileStream);
            });
    }
}
