using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityFontReplacer.Models;

namespace UnityFontReplacer.Core;

/// <summary>
/// TMP Material의 float/color 속성을 교체 아틀라스와 교체 material 정보에 맞춰 보정한다.
/// 원본 Python 구현처럼 게임 머티리얼 스타일은 유지하고, atlas/padding 차이만 재보정하는 경로를 우선한다.
/// </summary>
public static class MaterialPatcher
{
    private static readonly HashSet<string> MaterialPaddingScaleKeys =
    [
        "_GradientScale",
        "_FaceDilate",
        "_OutlineWidth",
        "_OutlineSoftness",
        "_UnderlayDilate",
        "_UnderlaySoftness",
        "_UnderlayOffsetX",
        "_UnderlayOffsetY",
        "_GlowOffset",
        "_GlowInner",
        "_GlowOuter",
    ];

    private static readonly HashSet<string> MaterialStyleFloatKeys =
    [
        "_FaceDilate",
        "_OutlineWidth",
        "_OutlineSoftness",
        "_UnderlayDilate",
        "_UnderlaySoftness",
        "_UnderlayOffsetX",
        "_UnderlayOffsetY",
        "_GlowOffset",
        "_GlowInner",
        "_GlowOuter",
        "_ScaleRatioA",
        "_ScaleRatioB",
        "_ScaleRatioC",
    ];

    private static readonly HashSet<string> MaterialStylePaddingScaleKeys =
    [
        "_FaceDilate",
        "_OutlineWidth",
        "_OutlineSoftness",
        "_UnderlayDilate",
        "_UnderlaySoftness",
        "_UnderlayOffsetX",
        "_UnderlayOffsetY",
        "_GlowOffset",
        "_GlowInner",
        "_GlowOuter",
    ];

    private static readonly HashSet<string> MaterialStyleColorKeys =
    [
        "_FaceColor",
        "_OutlineColor",
        "_UnderlayColor",
        "_GlowColor",
    ];

    private static readonly HashSet<string> OutlineRatioKeys =
    [
        "_OutlineWidth",
        "_OutlineSoftness",
    ];

    public static float? ReadFloat(AssetTypeValueField materialField, string propertyName)
    {
        var floats = materialField["m_SavedProperties"]["m_Floats"]["Array"];
        if (floats.IsDummy) return null;

        foreach (var entry in floats.Children)
        {
            var first = entry["first"];
            if (!first.IsDummy && first.AsString == propertyName)
                return entry["second"].AsFloat;
        }

        return null;
    }

    public static bool SetFloat(AssetTypeValueField materialField, string propertyName, float value)
    {
        var floats = materialField["m_SavedProperties"]["m_Floats"]["Array"];
        if (floats.IsDummy) return false;

        foreach (var entry in floats.Children)
        {
            var first = entry["first"];
            if (!first.IsDummy && first.AsString == propertyName)
            {
                entry["second"].AsFloat = value;
                return true;
            }
        }

        return false;
    }

    public static void PatchForReplacement(
        AssetsManager am,
        AssetsFileInstance inst,
        AssetFileInfo matInfo,
        MaterialPatchPlan plan)
    {
        var baseField = am.GetBaseField(inst, matInfo);
        ApplyPlan(baseField, plan);
        matInfo.SetNewData(baseField);
    }

    public static AssetFileInfo? FindMaterialByPathId(AssetsFileInstance inst, long pathId)
    {
        return inst.file.GetAssetsOfType(AssetClassID.Material)
            .FirstOrDefault(i => i.PathId == pathId);
    }

    private static void ApplyPlan(AssetTypeValueField materialField, MaterialPatchPlan plan)
    {
        if (plan.UseGameMaterial)
            return;

        float replacementPadding = Math.Max(0, plan.ReplacementPadding);
        float targetPadding = plan.TargetPadding > 0 ? plan.TargetPadding : replacementPadding;
        float stylePaddingRatio = 1.0f;
        if (replacementPadding > 0 && targetPadding > 0)
            stylePaddingRatio = targetPadding / replacementPadding;
        if (stylePaddingRatio <= 0)
            stylePaddingRatio = 1.0f;

        var sourceFloats = plan.SourceMaterial?.FloatProperties ??
                           new Dictionary<string, float>(StringComparer.Ordinal);
        var sourceColors = plan.SourceMaterial?.ColorProperties ??
                           new Dictionary<string, MaterialColorValue>(StringComparer.Ordinal);

        var existingFloatMap = ReadFloatMap(materialField);
        float gradientScale = ResolveGradientScale(
            materialField,
            sourceFloats,
            replacementPadding,
            plan.ForceRaster);

        EnsureFloat(materialField, "_GradientScale", gradientScale);
        EnsureFloat(materialField, "_TextureWidth", plan.AtlasWidth);
        EnsureFloat(materialField, "_TextureHeight", plan.AtlasHeight);

        foreach (var (key, rawValue) in sourceFloats)
        {
            if (plan.PreserveGameStyle && MaterialStyleFloatKeys.Contains(key))
                continue;

            float value = rawValue;
            if (stylePaddingRatio != 1.0f && MaterialPaddingScaleKeys.Contains(key))
                value *= stylePaddingRatio;

            if (OutlineRatioKeys.Contains(key))
            {
                value = ApplyOutlineRatioFallback(
                    key,
                    value,
                    existingFloatMap,
                    gradientScale,
                    plan.OutlineRatio);
            }

            EnsureFloat(materialField, key, value);
        }

        if (plan.PreserveGameStyle)
        {
            foreach (var key in MaterialStyleFloatKeys)
            {
                if (!existingFloatMap.TryGetValue(key, out var existing))
                    continue;

                float value = existing;
                if (MaterialStylePaddingScaleKeys.Contains(key))
                    value *= stylePaddingRatio;
                if (OutlineRatioKeys.Contains(key))
                    value *= plan.OutlineRatio;
                EnsureFloat(materialField, key, value);
            }
        }

        foreach (var (key, color) in sourceColors)
        {
            if (plan.PreserveGameStyle && MaterialStyleColorKeys.Contains(key))
                continue;

            EnsureColor(materialField, key, color);
        }

        if (plan.ForceRaster)
        {
            EnsureFloat(materialField, "_OutlineWidth", 0f);
            EnsureFloat(materialField, "_OutlineSoftness", 0f);
            EnsureFloat(materialField, "_UnderlayOffsetX", 0f);
            EnsureFloat(materialField, "_UnderlayOffsetY", 0f);
            EnsureFloat(materialField, "_UnderlayDilate", 0f);
            EnsureFloat(materialField, "_UnderlaySoftness", 0f);
            EnsureFloat(materialField, "_UnderlayStrength", 0f);
            EnsureFloat(materialField, "_GlowOffset", 0f);
            EnsureFloat(materialField, "_GlowOuter", 0f);
            EnsureFloat(materialField, "_GlowInner", 0f);
            EnsureFloat(materialField, "_GlowPower", 0f);
        }

        var finalGradientScale = ReadFloat(materialField, "_GradientScale") ?? gradientScale;
        if (replacementPadding > 0 && finalGradientScale > 0)
            EnsureFloat(materialField, "_ScaleRatioA", replacementPadding / finalGradientScale);
    }

    private static float ResolveGradientScale(
        AssetTypeValueField materialField,
        Dictionary<string, float> sourceFloats,
        float replacementPadding,
        bool forceRaster)
    {
        if (forceRaster)
            return 1.0f;

        if (sourceFloats.TryGetValue("_GradientScale", out var sourceGradient) &&
            sourceGradient > 0)
        {
            return sourceGradient;
        }

        if (replacementPadding > 0)
            return replacementPadding + 1.0f;

        var existingGradient = ReadFloat(materialField, "_GradientScale");
        return existingGradient is > 0 ? existingGradient.Value : 1.0f;
    }

    private static float ApplyOutlineRatioFallback(
        string key,
        float value,
        Dictionary<string, float> existingFloatMap,
        float gradientScale,
        float outlineRatio)
    {
        if (outlineRatio <= 0)
            outlineRatio = 1.0f;

        float baseValue = value;
        if (Math.Abs(baseValue) <= 1e-9)
        {
            if (existingFloatMap.TryGetValue(key, out var existing) && Math.Abs(existing) > 1e-9)
            {
                baseValue = existing;
            }
            else if (key == "_OutlineWidth" && gradientScale > 0)
            {
                baseValue = 1.0f / gradientScale;
            }
        }

        return baseValue * outlineRatio;
    }

    private static Dictionary<string, float> ReadFloatMap(AssetTypeValueField materialField)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        var floats = materialField["m_SavedProperties"]["m_Floats"]["Array"];
        if (floats.IsDummy)
            return result;

        foreach (var entry in floats.Children)
        {
            var first = entry["first"];
            if (!first.IsDummy)
                result[first.AsString] = entry["second"].AsFloat;
        }

        return result;
    }

    private static void EnsureFloat(AssetTypeValueField materialField, string propertyName, float value)
    {
        if (SetFloat(materialField, propertyName, value))
            return;

        var floats = materialField["m_SavedProperties"]["m_Floats"]["Array"];
        if (floats.IsDummy || floats.Children.Count == 0)
            return;

        var elem = ValueBuilder.DefaultValueFieldFromArrayTemplate(floats);
        elem["first"].AsString = propertyName;
        elem["second"].AsFloat = value;
        floats.Children.Add(elem);
    }

    private static void EnsureColor(
        AssetTypeValueField materialField,
        string propertyName,
        MaterialColorValue value)
    {
        var colors = materialField["m_SavedProperties"]["m_Colors"]["Array"];
        if (colors.IsDummy)
            return;

        foreach (var entry in colors.Children)
        {
            var first = entry["first"];
            if (!first.IsDummy && first.AsString == propertyName)
            {
                WriteColor(entry["second"], value);
                return;
            }
        }

        if (colors.Children.Count == 0)
            return;

        var elem = ValueBuilder.DefaultValueFieldFromArrayTemplate(colors);
        elem["first"].AsString = propertyName;
        WriteColor(elem["second"], value);
        colors.Children.Add(elem);
    }

    private static void WriteColor(AssetTypeValueField field, MaterialColorValue value)
    {
        field["r"].AsFloat = value.R;
        field["g"].AsFloat = value.G;
        field["b"].AsFloat = value.B;
        field["a"].AsFloat = value.A;
    }
}
