using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityFontReplacer.Core;

public static class BundleProcessor
{
    /// <summary>
    /// 번들 내 수정된 에셋 파일들을 적용하고 번들을 저장한다.
    /// </summary>
    public static void SaveBundle(
        BundleFileInstance bunInst,
        List<(int dirIndex, AssetsFileInstance fileInst)> modifiedFiles,
        string? outputPath = null,
        bool recompress = false)
    {
        // 수정된 각 에셋 파일을 번들 디렉토리 엔트리에 적용
        var dirInfos = bunInst.file.BlockAndDirInfo.DirectoryInfos;
        foreach (var (dirIndex, fileInst) in modifiedFiles)
        {
            dirInfos[dirIndex].SetNewData(fileInst.file);
        }

        var targetPath = outputPath ?? bunInst.path;
        var isInPlace = string.Equals(
            Path.GetFullPath(targetPath),
            Path.GetFullPath(bunInst.path),
            StringComparison.OrdinalIgnoreCase);

        if (isInPlace)
        {
            // 먼저 임시 파일에 쓰기
            var tempPath = targetPath + ".tmp";
            try
            {
                WriteBundle(bunInst, tempPath, recompress);

                // 원본 파일 핸들 닫기 (rename 전에 반드시 필요)
                CloseBundleReaders(bunInst);

                var backupPath = targetPath + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Move(targetPath, backupPath);
                File.Move(tempPath, targetPath);
                File.Delete(backupPath);
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }
        else
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            WriteBundle(bunInst, targetPath, recompress);
        }
    }

    private static void WriteBundle(BundleFileInstance bunInst, string path, bool recompress)
    {
        using var writer = new AssetsFileWriter(path);

        if (recompress)
        {
            bunInst.file.Pack(writer, AssetBundleCompressionType.LZ4);
        }
        else
        {
            bunInst.file.Write(writer);
        }
    }

    /// <summary>
    /// 번들과 내부 에셋 파일의 모든 리더를 닫는다.
    /// in-place 저장 시 원본 파일 핸들 해제를 위해 필요.
    /// </summary>
    private static void CloseBundleReaders(BundleFileInstance bunInst)
    {
        // 내부 에셋 파일 리더 닫기
        foreach (var assetsInst in bunInst.loadedAssetsFiles)
        {
            try { assetsInst.file.Reader?.Close(); }
            catch { }
        }

        // 번들 데이터 리더 닫기
        try { bunInst.file.DataReader?.Close(); }
        catch { }

        // 번들 파일 리더 닫기
        try { bunInst.file.Reader?.Close(); }
        catch { }

        // BundleStream (원본 FileStream) 닫기
        try { bunInst.BundleStream?.Close(); }
        catch { }
    }
}
