using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityFontReplacer.CLI;
using UnityFontReplacer.Models;

namespace UnityFontReplacer.Core;

public class FontScanner
{
    private readonly AssetsContext _ctx;

    // 공유 리소스
    private string? _tpkPath;
    private string? _unityVersion;
    private IMonoBehaviourTemplateGenerator? _sharedMonoGen;

    public FontScanner(AssetsContext ctx)
    {
        _ctx = ctx;
    }

    public ScanResult ScanAll(List<string> assetFiles, bool detectPs5Swizzle = false, int maxWorkers = 1)
    {
        var result = new ScanResult();
        result.UnityVersion = _ctx.DetectUnityVersion();

        // 공유 리소스 한 번만 로드
        LoadSharedResources(result.UnityVersion);

        using var status = new StatusBar(maxWorkers);
        status.SetTotal(assetFiles.Count);
        status.Log(Strings.Get("scan_start"));

        int fileIdx = 0;

        foreach (var filePath in assetFiles)
        {
            fileIdx++;
            var fileName = Path.GetFileName(filePath);
            status.Update(fileIdx, fileName);

            try
            {
                if (ShouldSkipFile(filePath, fileName))
                    continue;

                var entries = ScanFileIsolated(filePath, fileName);
                result.Entries.AddRange(entries);

                foreach (var e in entries)
                {
                    var shortFile = TruncMiddle(e.File, 50);
                    if (e.Type == FontType.TTF)
                        status.Log($"TTF: {e.Name} ({shortFile} #{e.PathId})");
                    else
                    {
                        var schema = e.Schema == TmpSchemaVersion.New ? "new" : "old";
                        status.Log($"SDF: {e.Name} ({schema}, {shortFile} #{e.PathId})");
                    }
                }
            }
            catch (Exception ex)
            {
                // 파싱 불가 파일은 무시하되 디버깅용 로그
                if (fileName.Contains("font", StringComparison.OrdinalIgnoreCase))
                    status.Log($"[WARN] {fileName}: {ex.Message}");
            }
        }

        status.Clear();
        Console.WriteLine(Strings.Get("scan_complete", result.TtfCount, result.TmpCount));
        return result;
    }

    private void LoadSharedResources(string? unityVersion)
    {
        _unityVersion = unityVersion;
        _tpkPath = ClassDataDownloader.EnsureClassData();

        if (_ctx.ManagedPath != null && Directory.Exists(_ctx.ManagedPath))
            _sharedMonoGen = new MonoCecilTempGenerator(_ctx.ManagedPath);
    }

    /// <summary>
    /// 파일마다 새 AssetsManager 생성 → 공유 리소스 참조 → 스캔 → 완전 해제.
    /// 번들의 언팩 데이터가 남지 않도록 완전 격리.
    /// </summary>
    private List<FontEntry> ScanFileIsolated(string filePath, string displayName)
    {
        var entries = new List<FontEntry>();
        var am = new AssetsManager();
        am.UseTemplateFieldCache = true;
        am.UseQuickLookup = true;

        // ClassDB 로드 (tpk 파일 읽기는 가벼움, 메모리에 누적 안 됨)
        if (_tpkPath != null && _unityVersion != null)
        {
            am.LoadClassPackage(_tpkPath);
            am.LoadClassDatabaseFromPackage(_unityVersion);
        }
        if (_sharedMonoGen != null) am.MonoTempGenerator = _sharedMonoGen;

        try
        {
            if (IsBundleFile(filePath))
                ScanBundle(am, filePath, displayName, entries);
            else
                ScanAssetsFile(am, filePath, displayName, entries);
        }
        catch { }
        finally
        {
            // MonoGen은 공유 → null로 해제 방지. 나머지는 전부 해제.
            am.MonoTempGenerator = null;
            am.UnloadAll(true);
            GC.Collect(2, GCCollectionMode.Forced, false);
        }

        return entries;
    }

    private static void ScanBundle(AssetsManager am, string filePath, string displayName, List<FontEntry> entries)
    {
        // unpackIfPacked: false → LZ4는 스트리밍 읽기, 메모리에 전체를 풀지 않음
        var bundle = am.LoadBundleFile(filePath, unpackIfPacked: false);
        var dirInfos = bundle.file.BlockAndDirInfo.DirectoryInfos;

        // 번들 내 모든 직렬화 파일을 먼저 로드 (MonoScript 크로스 참조 해석용)
        var instances = new List<(int idx, AssetsFileInstance inst)>();
        for (int i = 0; i < dirInfos.Count; i++)
        {
            if (!dirInfos[i].IsSerialized) continue;
            try
            {
                var inst = am.LoadAssetsFileFromBundle(bundle, i);
                instances.Add((i, inst));
            }
            catch { }
        }

        bool verbose = displayName.Contains("font", StringComparison.OrdinalIgnoreCase);
        foreach (var (idx, inst) in instances)
        {
            try { ScanInstance(am, inst, $"{displayName}/{dirInfos[idx].Name}", entries, verbose); }
            catch (Exception ex) { if (verbose) Console.Error.WriteLine($"  [DIAG] ScanInstance error: {ex.Message}"); }
        }
    }

    private static void ScanAssetsFile(AssetsManager am, string filePath, string displayName, List<FontEntry> entries)
    {
        try
        {
            var inst = am.LoadAssetsFile(filePath, loadDeps: false);
            ScanInstance(am, inst, displayName, entries);
        }
        catch { }
    }

    private static void ScanInstance(AssetsManager am, AssetsFileInstance inst, string displayName, List<FontEntry> entries, bool verbose = false)
    {
        var file = inst.file;
        var assetsName = Path.GetFileName(inst.path);

        var fontInfos = file.GetAssetsOfType(AssetClassID.Font);
        var mbInfos = file.GetAssetsOfType(AssetClassID.MonoBehaviour);

        if (verbose)
            Console.Error.WriteLine($"  [DIAG] {displayName}: Font={fontInfos.Count}, MB={mbInfos.Count}");

        if (fontInfos.Count == 0 && mbInfos.Count == 0) return;

        foreach (var info in fontInfos)
        {
            try
            {
                var name = AssetHelper.GetAssetNameFast(inst.file, am.ClassDatabase, info);
                if (string.IsNullOrEmpty(name))
                {
                    var bf = am.GetBaseField(inst, info);
                    name = bf["m_Name"].AsString;
                }

                entries.Add(new FontEntry
                {
                    File = displayName,
                    AssetsName = assetsName,
                    PathId = info.PathId,
                    Type = FontType.TTF,
                    Name = name ?? $"Font_{info.PathId}",
                });
            }
            catch (Exception ex) { if (verbose) Console.Error.WriteLine($"  [DIAG] Font read error: {ex.Message}"); }
        }

        foreach (var info in mbInfos)
        {
            try { ScanMonoBehaviour(am, inst, info, displayName, assetsName, entries, verbose); }
            catch (Exception ex) { if (verbose) Console.Error.WriteLine($"  [DIAG] MB scan error #{info.PathId}: {ex.Message}"); }
        }
    }

    private static void ScanMonoBehaviour(
        AssetsManager am, AssetsFileInstance inst, AssetFileInfo info,
        string displayName, string assetsName, List<FontEntry> entries, bool verbose = false)
    {
        // 1차: MonoScript 클래스명으로 판별
        bool isTmp = false;
        try
        {
            var bf = am.GetBaseField(inst, info, AssetReadFlags.SkipMonoBehaviourFields);
            var scriptPPtr = bf["m_Script"];
            if (verbose)
                Console.Error.WriteLine($"  [DIAG] MB #{info.PathId}: m_Script FileId={scriptPPtr["m_FileID"].AsInt}, PathId={scriptPPtr["m_PathID"].AsLong}");

            var scriptExt = am.GetExtAsset(inst, scriptPPtr);
            if (scriptExt.baseField != null)
            {
                var className = scriptExt.baseField["m_ClassName"].AsString;
                if (verbose) Console.Error.WriteLine($"  [DIAG]   -> className={className}");
                if (className == "TMP_FontAsset")
                    isTmp = true;
                else
                    return;
            }
            else
            {
                if (verbose) Console.Error.WriteLine($"  [DIAG]   -> MonoScript not resolved (external?)");
            }
        }
        catch (Exception ex)
        {
            if (verbose) Console.Error.WriteLine($"  [DIAG]   -> MonoScript resolve error: {ex.Message}");
        }

        AssetTypeValueField fullField;
        try
        {
            fullField = am.GetBaseField(inst, info);
        }
        catch (Exception ex)
        {
            if (verbose) Console.Error.WriteLine($"  [DIAG]   -> GetBaseField failed: {ex.Message}");
            return;
        }

        var schemaInfo = TmpSchemaDetector.Inspect(
            fullField,
            inst.file.Metadata.UnityVersion);

        if (!isTmp && !schemaInfo.IsTmp)
        {
            if (verbose) Console.Error.WriteLine("  [DIAG]   -> field inspect says not TMP");
            return;
        }

        if (schemaInfo.GlyphCount <= 0)
        {
            if (verbose) Console.Error.WriteLine("  [DIAG]   -> glyph count is 0, skipped");
            return;
        }

        if (schemaInfo.AtlasFileId != 0 && schemaInfo.AtlasPathId == 0)
        {
            if (verbose) Console.Error.WriteLine("  [DIAG]   -> external atlas stub only, skipped");
            return;
        }

        var schema = schemaInfo.Version;
        if (schema == TmpSchemaVersion.Unknown)
            schema = TmpSchemaVersion.New;

        string fontName = fullField["m_Name"].IsDummy
            ? $"TMP_{info.PathId}"
            : fullField["m_Name"].AsString;

        if (verbose)
        {
            Console.Error.WriteLine(
                $"  [DIAG]   -> TMP confirmed, name={fontName}, schema={schema}, glyphs={schemaInfo.GlyphCount}, atlas={schemaInfo.AtlasFileId}:{schemaInfo.AtlasPathId}");
        }

        entries.Add(new FontEntry
        {
            File = displayName,
            AssetsName = assetsName,
            PathId = info.PathId,
            Type = FontType.SDF,
            Name = fontName,
            Schema = schema,
            GlyphCount = schemaInfo.GlyphCount,
            AtlasPadding = schemaInfo.AtlasPadding,
            AtlasPathId = schemaInfo.AtlasPathId,
        });
    }

    /// <summary>
    /// .assets 파일은 헤더만 읽어서 Font/MonoBehaviour TypeId 존재 여부를 경량 확인.
    /// 번들은 풀어야 하므로 스킵 불가 → false 반환.
    /// </summary>
    private static bool ShouldSkipFile(string filePath, string fileName)
    {
        // 번들은 여기서 스킵 판단 불가 (ScanBundle 내에서 TypeId 체크)
        if (IsBundleFile(filePath))
            return false;

        // .assets 파일: 헤더만 읽어서 TypeId 확인
        try
        {
            var file = new AssetsFile();
            using var reader = new AssetsFileReader(filePath);
            file.Read(reader);

            foreach (var info in file.Metadata.AssetInfos)
            {
                if (info.TypeId == (int)AssetClassID.Font ||
                    info.TypeId == (int)AssetClassID.MonoBehaviour)
                    return false; // 폰트 가능성 있음 → 스캔
            }
            return true; // Font/MB 없음 → 스킵
        }
        catch
        {
            return false; // 읽기 실패 → 안전하게 스캔
        }
    }

    public static bool IsBundleFile(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (fs.Length < 8) return false;
            Span<byte> header = stackalloc byte[8];
            fs.ReadExactly(header);
            return header.StartsWith("UnityFS"u8) ||
                   header.StartsWith("UnityWeb"u8[..7]) ||
                   header.StartsWith("UnityRaw"u8[..7]);
        }
        catch { return false; }
    }

    private static string TruncMiddle(string s, int maxLen)
    {
        if (s.Length <= maxLen) return s;
        int half = (maxLen - 2) / 2;
        return s[..half] + ".." + s[^half..];
    }
}
