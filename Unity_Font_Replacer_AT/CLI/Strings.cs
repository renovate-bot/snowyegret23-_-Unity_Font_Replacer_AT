namespace UnityFontReplacer.CLI;

public static class Strings
{
    public static string Lang { get; } =
#if DEFAULT_UI_LANG_EN
        "en";
#else
        "ko";
#endif

    private static readonly Dictionary<string, Dictionary<string, string>> Table = new()
    {
        ["app_description"] = new()
        {
            ["ko"] = "Unity 폰트 교체 도구 (AssetsTools.NET)",
            ["en"] = "Unity Font Replacer (AssetsTools.NET)",
        },
        ["opt_gamepath"] = new()
        {
            ["ko"] = "Unity 게임 경로",
            ["en"] = "Unity game path",
        },
        ["cmd_parse"] = new()
        {
            ["ko"] = "게임 에셋을 스캔하여 폰트 매핑 JSON 생성",
            ["en"] = "Scan game assets and generate font mapping JSON",
        },
        ["cmd_list"] = new()
        {
            ["ko"] = "JSON 매핑 파일을 사용하여 폰트 교체",
            ["en"] = "Replace fonts using a JSON mapping file",
        },
        ["cmd_export"] = new()
        {
            ["ko"] = "TMP 폰트 에셋 추출 (JSON + PNG)",
            ["en"] = "Export TMP font assets (JSON + PNG)",
        },
        ["cmd_makesdf"] = new()
        {
            ["ko"] = "TTF에서 SDF 아틀라스 생성",
            ["en"] = "Generate SDF atlas from TTF",
        },
        ["scan_start"] = new()
        {
            ["ko"] = "폰트 스캔 시작...",
            ["en"] = "Starting font scan...",
        },
        ["scan_file"] = new()
        {
            ["ko"] = "스캔 중: {0}",
            ["en"] = "Scanning: {0}",
        },
        ["scan_found_ttf"] = new()
        {
            ["ko"] = "TTF 폰트 발견: {0}",
            ["en"] = "TTF font found: {0}",
        },
        ["scan_found_tmp"] = new()
        {
            ["ko"] = "TMP 폰트 발견: {0} ({1})",
            ["en"] = "TMP font found: {0} ({1})",
        },
        ["scan_complete"] = new()
        {
            ["ko"] = "스캔 완료: TTF {0}개, TMP {1}개 발견",
            ["en"] = "Scan complete: found {0} TTF, {1} TMP fonts",
        },
        ["err_gamepath_not_found"] = new()
        {
            ["ko"] = "게임 경로를 찾을 수 없습니다: {0}",
            ["en"] = "Game path not found: {0}",
        },
        ["err_no_data_folder"] = new()
        {
            ["ko"] = "Data 폴더를 찾을 수 없습니다",
            ["en"] = "Data folder not found",
        },
        ["json_saved"] = new()
        {
            ["ko"] = "JSON 저장 완료: {0}",
            ["en"] = "JSON saved: {0}",
        },
    };

    public static string Get(string key)
    {
        if (Table.TryGetValue(key, out var langMap) && langMap.TryGetValue(Lang, out var text))
            return text;
        return $"[{key}]";
    }

    public static string Get(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }
}
