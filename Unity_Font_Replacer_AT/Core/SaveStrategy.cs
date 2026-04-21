using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityFontReplacer.Core;

public static class SaveStrategy
{
    /// <summary>
    /// 수정된 .assets 파일을 저장한다.
    /// outputPath가 null이면 원본을 덮어쓴다 (임시파일 → rename).
    /// </summary>
    public static void SaveAssetsFile(AssetsFileInstance inst, string? outputPath = null)
    {
        var targetPath = outputPath ?? inst.path;
        var isInPlace = string.Equals(
            Path.GetFullPath(targetPath),
            Path.GetFullPath(inst.path),
            StringComparison.OrdinalIgnoreCase);

        if (isInPlace)
        {
            // 임시 파일에 쓰고 원본을 교체
            var tempPath = targetPath + ".tmp";
            try
            {
                using (var writer = new AssetsFileWriter(tempPath))
                {
                    inst.file.Write(writer);
                }

                CloseAssetsReaders(inst);

                // 원본 백업 후 교체
                var backupPath = targetPath + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Move(targetPath, backupPath);
                File.Move(tempPath, targetPath);
                File.Delete(backupPath);
            }
            catch
            {
                // 실패 시 임시 파일 정리
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }
        else
        {
            // 출력 디렉토리 확인
            var dir = Path.GetDirectoryName(targetPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var writer = new AssetsFileWriter(targetPath);
            inst.file.Write(writer);
        }
    }

    /// <summary>
    /// --output-only 경로를 해석하여 대상 파일 경로를 생성한다.
    /// 원본 파일 경로의 상대 구조를 출력 디렉토리에 재현한다.
    /// </summary>
    public static string ResolveOutputPath(string originalPath, string dataPath, string outputDir)
    {
        var relativePath = Path.GetRelativePath(dataPath, originalPath);
        return Path.Combine(outputDir, relativePath);
    }

    private static void CloseAssetsReaders(AssetsFileInstance inst)
    {
        try { inst.file.Reader?.Close(); }
        catch { }

        try { inst.AssetsStream?.Close(); }
        catch { }
    }
}
