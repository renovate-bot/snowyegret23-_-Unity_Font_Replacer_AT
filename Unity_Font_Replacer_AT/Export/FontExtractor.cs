using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Spectre.Console;
using UnityFontReplacer.CLI;
using UnityFontReplacer.Core;
using UnityFontReplacer.Models;

namespace UnityFontReplacer.Export;

/// <summary>
/// TMP 폰트 에셋을 추출하는 2-pass 엔진.
/// Pass 1: 모든 에셋 파일 스캔 → TMP_FontAsset 발견 → 아틀라스/머티리얼 참조 수집
/// Pass 2: 참조된 텍스처/머티리얼 에셋을 로드하여 PNG/JSON으로 내보내기
/// </summary>
public class FontExtractor
{
    private readonly AssetsContext _ctx;

    public FontExtractor(AssetsContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// TMP 폰트 에셋을 추출하여 outputDir에 저장한다.
    /// </summary>
    public int Extract(List<string> assetFiles, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        int exportCount = 0;

        // Pass 1: TMP_FontAsset 발견 + 참조 수집
        var discovered = new List<DiscoveredFont>();

        AnsiConsole.MarkupLine(Strings.Get("scan_start"));

        foreach (var filePath in assetFiles)
        {
            try
            {
                DiscoverInFile(filePath, discovered);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Scan warning ({Markup.Escape(Path.GetFileName(filePath))}): {Markup.Escape(ex.Message)}[/]");
            }
        }

        AnsiConsole.MarkupLine($"Discovered [green]{discovered.Count}[/] TMP font(s)");

        // Pass 2: 추출
        foreach (var font in discovered)
        {
            try
            {
                ExportFont(font, outputDir);
                exportCount++;
                AnsiConsole.MarkupLine($"[green]Exported: {Markup.Escape(font.Name)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Export failed ({Markup.Escape(font.Name)}): {Markup.Escape(ex.Message)}[/]");
            }
        }

        return exportCount;
    }

    private void DiscoverInFile(string filePath, List<DiscoveredFont> discovered)
    {
        if (FontScanner.IsBundleFile(filePath))
        {
            DiscoverInBundle(filePath, discovered);
            return;
        }

        try
        {
            var inst = _ctx.LoadAssetsFile(filePath);
            DiscoverInInstance(inst, filePath, discovered);
        }
        catch
        {
            // 파싱 불가 파일 건너뜀
        }
    }

    private void DiscoverInBundle(string filePath, List<DiscoveredFont> discovered)
    {
        try
        {
            var bundle = _ctx.LoadBundleFile(filePath);
            var dirInfos = bundle.file.BlockAndDirInfo.DirectoryInfos;

            for (int i = 0; i < dirInfos.Count; i++)
            {
                if (!dirInfos[i].IsSerialized) continue;
                try
                {
                    var inst = _ctx.LoadAssetsFileFromBundle(bundle, i);
                    DiscoverInInstance(inst, $"{filePath}/{dirInfos[i].Name}", discovered);
                }
                catch { }
            }
        }
        catch { }
    }

    private void DiscoverInInstance(AssetsFileInstance inst, string sourcePath, List<DiscoveredFont> discovered)
    {
        var mbInfos = inst.file.GetAssetsOfType(AssetClassID.MonoBehaviour);
        foreach (var info in mbInfos)
        {
            try
            {
                // MonoScript 확인
                var baseField = _ctx.Manager.GetBaseField(inst, info, AssetReadFlags.SkipMonoBehaviourFields);
                var scriptExt = _ctx.Manager.GetExtAsset(inst, baseField["m_Script"]);
                if (scriptExt.baseField == null) continue;

                var className = scriptExt.baseField["m_ClassName"].AsString;
                if (className != "TMP_FontAsset") continue;

                // 전체 필드 로드
                var fullField = _ctx.Manager.GetBaseField(inst, info);
                var tmpAsset = TmpFontHandler.ReadFromField(fullField);

                if (tmpAsset.GlyphCount == 0) continue;

                discovered.Add(new DiscoveredFont
                {
                    Name = tmpAsset.Name,
                    SourcePath = sourcePath,
                    FileInstance = inst,
                    MonoBehaviourInfo = info,
                    FontAsset = tmpAsset,
                });
            }
            catch { }
        }
    }

    private void ExportFont(DiscoveredFont font, string outputDir)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // 1. TMP 구조 JSON 내보내기
        var fullField = _ctx.Manager.GetBaseField(font.FileInstance, font.MonoBehaviourInfo);

        // MonoBehaviour dict를 직렬화 (간이 방식: 주요 필드만)
        var sdfJson = SerializeTmpAsset(font.FontAsset);
        var jsonPath = Path.Combine(outputDir, SanitizeFileName(font.Name) + ".json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(sdfJson, jsonOptions));

        // 2. 아틀라스 텍스처 PNG 내보내기
        if (font.FontAsset.AtlasTexturePathId != 0 && font.FontAsset.AtlasTextureFileId == 0)
        {
            // 같은 파일 내 참조
            var texInfo = TextureHandler.FindTextureByPathId(font.FileInstance, font.FontAsset.AtlasTexturePathId);
            if (texInfo != null)
            {
                var pngPath = Path.Combine(outputDir, SanitizeFileName(font.Name) + " Atlas.png");
                TextureHandler.ExportToPng(_ctx.Manager, font.FileInstance, texInfo, pngPath);
            }
        }
        else if (font.FontAsset.AtlasTexturePathId != 0)
        {
            // 외부 파일 참조 - GetExtAsset으로 해석
            try
            {
                var atlasRef = fullField[font.FontAsset.SchemaVersion == TmpSchemaVersion.New
                    ? "m_AtlasTextures" : "atlas"];

                AssetTypeValueField pptrField;
                if (!atlasRef.IsDummy && atlasRef.FieldName == "m_AtlasTextures")
                {
                    var arr = atlasRef["Array"];
                    pptrField = arr.Children.Count > 0 ? arr[0] : atlasRef;
                }
                else
                {
                    pptrField = atlasRef;
                }

                var ext = _ctx.Manager.GetExtAsset(font.FileInstance, pptrField);
                if (ext.info != null && ext.file != null)
                {
                    var pngPath = Path.Combine(outputDir, SanitizeFileName(font.Name) + " Atlas.png");
                    TextureHandler.ExportToPng(_ctx.Manager, ext.file, ext.info, pngPath);
                }
            }
            catch { }
        }

        // 3. 머티리얼 JSON 내보내기
        if (font.FontAsset.MaterialPathId != 0 && font.FontAsset.MaterialFileId == 0)
        {
            var matInfo = MaterialPatcher.FindMaterialByPathId(font.FileInstance, font.FontAsset.MaterialPathId);
            if (matInfo != null)
            {
                ExportMaterialJson(font.FileInstance, matInfo, outputDir, font.Name);
            }
        }
        else if (font.FontAsset.MaterialPathId != 0)
        {
            try
            {
                var matRef = fullField[font.FontAsset.SchemaVersion == TmpSchemaVersion.New
                    ? "m_Material" : "material"];
                if (matRef.IsDummy)
                    matRef = fullField["m_material"];

                var ext = _ctx.Manager.GetExtAsset(font.FileInstance, matRef);
                if (ext.info != null && ext.file != null)
                {
                    ExportMaterialJson(ext.file, ext.info, outputDir, font.Name);
                }
            }
            catch { }
        }
    }

    private void ExportMaterialJson(AssetsFileInstance inst, AssetFileInfo matInfo, string outputDir, string fontName)
    {
        var baseField = _ctx.Manager.GetBaseField(inst, matInfo);
        var floats = baseField["m_SavedProperties"]["m_Floats"]["Array"];
        if (floats.IsDummy) return;

        var props = new Dictionary<string, float>();
        foreach (var entry in floats.Children)
        {
            var name = entry["first"];
            if (!name.IsDummy)
                props[name.AsString] = entry["second"].AsFloat;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var matJson = new Dictionary<string, object>
        {
            ["m_SavedProperties"] = new Dictionary<string, object>
            {
                ["m_Floats"] = props.Select(kv => new object[] { kv.Key, kv.Value }).ToArray()
            }
        };

        var path = Path.Combine(outputDir, SanitizeFileName(fontName) + " Material.json");
        File.WriteAllText(path, JsonSerializer.Serialize(matJson, jsonOptions));
    }

    private static TmpFontAssetJson SerializeTmpAsset(TmpFontAsset asset)
    {
        var json = new TmpFontAssetJson
        {
            m_AtlasWidth = asset.AtlasWidth,
            m_AtlasHeight = asset.AtlasHeight,
            m_AtlasPadding = asset.AtlasPadding,
            m_AtlasRenderMode = asset.AtlasRenderMode,
            m_FaceInfo = new TmpFaceInfoJson
            {
                m_FamilyName = asset.FaceInfo.FamilyName,
                m_StyleName = asset.FaceInfo.StyleName,
                m_PointSize = asset.FaceInfo.PointSize,
                m_Scale = asset.FaceInfo.Scale,
                m_UnitsPerEM = asset.FaceInfo.UnitsPerEM,
                m_LineHeight = asset.FaceInfo.LineHeight,
                m_AscentLine = asset.FaceInfo.AscentLine,
                m_CapLine = asset.FaceInfo.CapLine,
                m_MeanLine = asset.FaceInfo.MeanLine,
                m_Baseline = asset.FaceInfo.Baseline,
                m_DescentLine = asset.FaceInfo.DescentLine,
                m_SuperscriptOffset = asset.FaceInfo.SuperscriptOffset,
                m_SubscriptOffset = asset.FaceInfo.SubscriptOffset,
                m_UnderlineOffset = asset.FaceInfo.UnderlineOffset,
                m_UnderlineThickness = asset.FaceInfo.UnderlineThickness,
                m_StrikethroughOffset = asset.FaceInfo.StrikethroughOffset,
                m_TabWidth = asset.FaceInfo.TabWidth,
            },
        };

        if (asset.GlyphTable != null)
        {
            json.m_GlyphTable = asset.GlyphTable.Select(g => new TmpGlyphJson
            {
                m_Index = g.Index,
                m_Metrics = new TmpMetricsJson
                {
                    m_Width = g.MetricsWidth,
                    m_Height = g.MetricsHeight,
                    m_HorizontalBearingX = g.HorizontalBearingX,
                    m_HorizontalBearingY = g.HorizontalBearingY,
                    m_HorizontalAdvance = g.HorizontalAdvance,
                },
                m_GlyphRect = new TmpGlyphRectJson
                {
                    m_X = g.RectX,
                    m_Y = g.RectY,
                    m_Width = g.RectWidth,
                    m_Height = g.RectHeight,
                },
                m_Scale = g.Scale,
                m_AtlasIndex = g.AtlasIndex,
            }).ToList();
        }

        if (asset.CharacterTable != null)
        {
            json.m_CharacterTable = asset.CharacterTable.Select(c => new TmpCharacterJson
            {
                m_ElementType = c.ElementType,
                m_Unicode = c.Unicode,
                m_GlyphIndex = c.GlyphIndex,
                m_Scale = c.Scale,
            }).ToList();
        }

        return json;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private class DiscoveredFont
    {
        public required string Name { get; init; }
        public required string SourcePath { get; init; }
        public required AssetsFileInstance FileInstance { get; init; }
        public required AssetFileInfo MonoBehaviourInfo { get; init; }
        public required TmpFontAsset FontAsset { get; init; }
    }
}
