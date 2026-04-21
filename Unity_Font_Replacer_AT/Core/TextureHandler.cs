using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace UnityFontReplacer.Core;

public static class TextureHandler
{
    public static bool ExportToPng(
        AssetsManager am, AssetsFileInstance inst,
        AssetFileInfo texInfo, string outputPath)
    {
        var baseField = am.GetBaseField(inst, texInfo);
        var texFile = TextureFile.ReadTextureFile(baseField);

        var picData = texFile.FillPictureData(inst);
        if (picData == null || picData.Length == 0)
            return false;

        return texFile.DecodeTextureImage(picData, outputPath, ImageExportType.Png);
    }

    /// <summary>
    /// Texture2D를 PNG 파일로 교체한다.
    /// 원본 텍스처 포맷을 유지하며, Alpha8일 경우 PNG의 알파 채널만 추출.
    /// </summary>
    public static void ReplaceFromPng(
        AssetsManager am, AssetsFileInstance inst,
        AssetFileInfo texInfo, string pngPath)
    {
        var baseField = am.GetBaseField(inst, texInfo);
        var texFile = TextureFile.ReadTextureFile(baseField);
        int originalFormat = texFile.m_TextureFormat;

        // placeholder 텍스처(128x128 이하) 스킵 — 크기를 대폭 변경하면 번들 구조가 깨질 수 있음
        if (texFile.m_Width <= 128 && texFile.m_Height <= 128)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[dim]Skipping small texture: {texFile.m_Name} ({texFile.m_Width}x{texFile.m_Height})[/]");
            return;
        }

        if (originalFormat == 1) // Alpha8
        {
            // PNG에서 알파 채널만 추출하여 raw Alpha8 바이트로 교체
            ReplaceAsAlpha8(texFile, baseField, texInfo, pngPath);
        }
        else
        {
            // 일반 포맷: EncodeTextureImage 사용
            texFile.EncodeTextureImage(pngPath, quality: 3);
            texFile.WriteTo(baseField);
            texInfo.SetNewData(baseField);
        }
    }

    /// <summary>
    /// Alpha8 포맷으로 텍스처를 교체한다.
    /// PNG의 알파 채널(또는 그레이스케일)을 추출하여 raw bytes로 설정.
    /// </summary>
    private static void ReplaceAsAlpha8(
        TextureFile texFile, AssetTypeValueField baseField,
        AssetFileInfo texInfo, string pngPath)
    {
        using var image = Image.Load<Rgba32>(pngPath);
        int w = image.Width;
        int h = image.Height;

        // 알파 채널 추출 → Alpha8 raw bytes
        // Unity 텍스처는 bottom-origin, PNG는 top-origin → 상하 반전
        var alpha8 = new byte[w * h];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(h - 1 - y);
                for (int x = 0; x < w; x++)
                {
                    alpha8[y * w + x] = row[x].A;
                }
            }
        });

        texFile.SetPictureData(alpha8, w, h);
        texFile.m_TextureFormat = 1; // Alpha8 유지
        texFile.WriteTo(baseField);
        texInfo.SetNewData(baseField);
    }

    public static void ReplaceFromRawBytes(
        AssetsManager am, AssetsFileInstance inst,
        AssetFileInfo texInfo, byte[] rawData, int width, int height)
    {
        var baseField = am.GetBaseField(inst, texInfo);
        var texFile = TextureFile.ReadTextureFile(baseField);

        texFile.SetPictureData(rawData, width, height);
        texFile.WriteTo(baseField);
        texInfo.SetNewData(baseField);
    }

    public static AssetFileInfo? FindTextureByPathId(AssetsFileInstance inst, long pathId)
    {
        return inst.file.GetAssetsOfType(AssetClassID.Texture2D)
            .FirstOrDefault(i => i.PathId == pathId);
    }

    public static (int width, int height, int format) ReadTextureInfo(
        AssetsManager am, AssetsFileInstance inst, AssetFileInfo texInfo)
    {
        var baseField = am.GetBaseField(inst, texInfo);
        var texFile = TextureFile.ReadTextureFile(baseField);
        return (texFile.m_Width, texFile.m_Height, texFile.m_TextureFormat);
    }
}
