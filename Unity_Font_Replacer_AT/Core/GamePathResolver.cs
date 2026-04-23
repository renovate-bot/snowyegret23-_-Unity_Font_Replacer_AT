namespace UnityFontReplacer.Core;

public class ResolvedGamePath
{
    public required string GamePath { get; init; }
    public required string DataPath { get; init; }
    public string? ManagedPath { get; set; }
    public required List<string> AssetFiles { get; init; }
}

public static class GamePathResolver
{
    // 에셋으로 알려진 확장자
    private static readonly HashSet<string> IncludeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".assets", ".bundle", ".unity3d",
    };

    public static ResolvedGamePath? Resolve(string gamePath)
    {
        gamePath = gamePath.Trim().Trim('"');

        if (!Directory.Exists(gamePath))
            return null;

        var dataPath = FindDataFolder(gamePath);
        if (dataPath == null)
            return null;

        var managedPath = Path.Combine(dataPath, "Managed");
        if (!Directory.Exists(managedPath))
        {
            managedPath = null;
        }

        var assetFiles = CollectAssetFiles(dataPath);

        return new ResolvedGamePath
        {
            GamePath = gamePath,
            DataPath = dataPath,
            ManagedPath = managedPath,
            AssetFiles = assetFiles,
        };
    }

    private static string? FindDataFolder(string gamePath)
    {
        // 직접 _Data 패턴 검색
        var dirs = Directory.GetDirectories(gamePath, "*_Data", SearchOption.TopDirectoryOnly);
        if (dirs.Length > 0)
            return dirs[0];

        // StreamingAssets가 있는 Data 폴더 검색
        var dataDir = Path.Combine(gamePath, "Data");
        if (Directory.Exists(dataDir))
            return dataDir;

        // gamePath 자체가 Data 폴더일 수 있음
        if (File.Exists(Path.Combine(gamePath, "globalgamemanagers")) ||
            File.Exists(Path.Combine(gamePath, "globalgamemanagers.assets")))
            return gamePath;

        return null;
    }

    private static List<string> CollectAssetFiles(string dataPath)
    {
        var files = new List<string>();

        foreach (var file in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            var fileName = Path.GetFileName(file);

            // 1. 알려진 확장자
            if (IncludeExtensions.Contains(ext))
            {
                files.Add(file);
                continue;
            }

            // 2. globalgamemanagers
            if (fileName.StartsWith("globalgamemanagers", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(file);
                continue;
            }

            // 3. 확장자 없는 파일 (번들일 수 있음)
            if (string.IsNullOrEmpty(ext))
            {
                files.Add(file);
                continue;
            }

            // 4. 그 외: 시그니처로 에셋인지 확인 (번들 또는 직렬화 파일)
            if (FontScanner.IsBundleFile(file) || IsSerializedAssetsFile(file))
            {
                files.Add(file);
            }
        }

        return files;
    }

    /// <summary>
    /// 직렬화 에셋 파일 시그니처 확인.
    /// 헤더의 version 필드(오프셋 8, big-endian int32)가 유효 범위(6~100)인지 체크.
    /// </summary>
    private static bool IsSerializedAssetsFile(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (fs.Length < 20) return false;

            using var reader = new AssetsTools.NET.AssetsFileReader(fs);
            return AssetsTools.NET.AssetsFile.IsAssetsFile(reader, 0, fs.Length);
        }
        catch
        {
            return false;
        }
    }
}
