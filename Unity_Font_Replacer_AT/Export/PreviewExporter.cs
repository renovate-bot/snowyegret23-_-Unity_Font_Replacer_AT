using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UnityFontReplacer.Core;
using UnityFontReplacer.Models;

namespace UnityFontReplacer.Export;

/// <summary>
/// 아틀라스 PNG + 개별 글리프 크롭 PNG 내보내기.
/// </summary>
public static class PreviewExporter
{
    /// <summary>
    /// 아틀라스 텍스처를 PNG로 내보낸다.
    /// </summary>
    public static bool ExportAtlas(
        AssetsManager am, AssetsFileInstance inst,
        long atlasPathId, string outputPath)
    {
        var texInfo = TextureHandler.FindTextureByPathId(inst, atlasPathId);
        if (texInfo == null) return false;

        return TextureHandler.ExportToPng(am, inst, texInfo, outputPath);
    }

    /// <summary>
    /// 아틀라스에서 개별 글리프를 크롭하여 PNG로 내보낸다.
    /// </summary>
    public static int ExportGlyphCrops(
        string atlasPngPath, TmpFontAsset fontAsset, string outputDir)
    {
        if (!File.Exists(atlasPngPath)) return 0;

        Directory.CreateDirectory(outputDir);
        int count = 0;

        using var atlas = Image.Load<Rgba32>(atlasPngPath);
        int atlasH = atlas.Height;

        var glyphs = fontAsset.SchemaVersion == TmpSchemaVersion.New
            ? fontAsset.GlyphTable?.Select(g => (g.RectX, g.RectY, g.RectWidth, g.RectHeight,
                fontAsset.CharacterTable?.FirstOrDefault(c => c.GlyphIndex == g.Index)?.Unicode ?? g.Index))
            : fontAsset.GlyphInfoList?.Select(g => ((int)g.X, atlasH - (int)g.Y - (int)g.Height,
                (int)g.Width, (int)g.Height, g.Id));

        if (glyphs == null) return 0;

        foreach (var (rx, ry, rw, rh, unicode) in glyphs)
        {
            if (rw <= 0 || rh <= 0) continue;

            // bottom-origin → top-origin 변환 (이미지 좌표계)
            int imgY = atlasH - ry - rh;
            if (imgY < 0 || rx < 0) continue;

            int cropW = Math.Min(rw, atlas.Width - rx);
            int cropH = Math.Min(rh, atlasH - imgY);
            if (cropW <= 0 || cropH <= 0) continue;

            try
            {
                using var crop = atlas.Clone(ctx =>
                    ctx.Crop(new Rectangle(rx, imgY, cropW, cropH)));

                var glyphPath = Path.Combine(outputDir, $"{unicode}.png");
                crop.SaveAsPng(glyphPath);
                count++;
            }
            catch
            {
                // 개별 크롭 실패 무시
            }
        }

        return count;
    }
}
