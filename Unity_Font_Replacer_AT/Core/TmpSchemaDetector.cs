using AssetsTools.NET;
using UnityFontReplacer.Models;

namespace UnityFontReplacer.Core;

public sealed class TmpSchemaInfo
{
    public required TmpSchemaVersion Version { get; init; }
    public required bool IsTmp { get; init; }
    public required int GlyphCount { get; init; }
    public required int AtlasPadding { get; init; }
    public required int AtlasFileId { get; init; }
    public required long AtlasPathId { get; init; }
}

public static class TmpSchemaDetector
{
    private static readonly Version TmpOldOnlyLast = new(2018, 3, 14);
    private static readonly Version TmpNewSchemaFirst = new(2018, 4, 2);

    public static TmpSchemaVersion Detect(
        AssetTypeValueField baseField,
        string? unityVersion = null)
    {
        return Inspect(baseField, unityVersion).Version;
    }

    public static TmpSchemaInfo Inspect(
        AssetTypeValueField baseField,
        string? unityVersion = null)
    {
        int newGlyphCount = CountArrayChildren(baseField["m_GlyphTable"]);
        int oldGlyphCount = CountArrayChildren(baseField["m_glyphInfoList"]);

        bool hasNewGlyphs = newGlyphCount > 0;
        bool hasOldGlyphs = oldGlyphCount > 0;
        bool hasNewFace = !baseField["m_FaceInfo"].IsDummy;
        bool hasOldFace = !baseField["m_fontInfo"].IsDummy;
        bool hasNewChars = !baseField["m_CharacterTable"].IsDummy;

        var newAtlasAny = FirstAtlasRef(baseField["m_AtlasTextures"]);
        var oldAtlasAny = ReadPPtr(baseField["atlas"]);

        var version = ResolveVersion(
            newGlyphCount,
            oldGlyphCount,
            hasNewFace,
            hasOldFace,
            newAtlasAny.Exists,
            oldAtlasAny.Exists,
            hasNewChars,
            unityVersion);

        int glyphCount;
        int atlasPadding;
        PPtrInfo atlasRef;
        int newPadding = ReadInt(baseField, "m_AtlasPadding");
        int oldPadding = ReadInt(baseField["m_fontInfo"], "Padding");

        if (version == TmpSchemaVersion.Old)
        {
            glyphCount = oldGlyphCount > 0 ? oldGlyphCount : newGlyphCount;
            atlasPadding = oldPadding > 0 ? oldPadding : newPadding;
            atlasRef = BestAtlasRef(newAtlasAny, oldAtlasAny, preferNew: false);
        }
        else
        {
            glyphCount = newGlyphCount > 0 ? newGlyphCount : oldGlyphCount;
            atlasPadding = newPadding > 0 ? newPadding : oldPadding;
            atlasRef = BestAtlasRef(newAtlasAny, oldAtlasAny, preferNew: true);
        }

        bool isTmp = hasNewGlyphs || hasOldGlyphs || hasNewFace || hasOldFace ||
                     newAtlasAny.Exists || oldAtlasAny.Exists;

        return new TmpSchemaInfo
        {
            Version = isTmp ? version : TmpSchemaVersion.Unknown,
            IsTmp = isTmp,
            GlyphCount = glyphCount,
            AtlasPadding = atlasPadding,
            AtlasFileId = atlasRef.FileId,
            AtlasPathId = atlasRef.PathId,
        };
    }

    private static TmpSchemaVersion ResolveVersion(
        int newGlyphCount,
        int oldGlyphCount,
        bool hasNewFace,
        bool hasOldFace,
        bool hasNewAtlas,
        bool hasOldAtlas,
        bool hasNewChars,
        string? unityVersion)
    {
        if (newGlyphCount > 0 && oldGlyphCount == 0)
            return TmpSchemaVersion.New;
        if (oldGlyphCount > 0 && newGlyphCount == 0)
            return TmpSchemaVersion.Old;
        if (newGlyphCount != oldGlyphCount)
            return newGlyphCount > oldGlyphCount
                ? TmpSchemaVersion.New
                : TmpSchemaVersion.Old;

        if (hasNewFace != hasOldFace)
            return hasNewFace ? TmpSchemaVersion.New : TmpSchemaVersion.Old;
        if (hasNewAtlas != hasOldAtlas)
            return hasNewAtlas ? TmpSchemaVersion.New : TmpSchemaVersion.Old;

        var hint = GetVersionHint(unityVersion);
        if (hint != TmpSchemaVersion.Unknown)
            return hint;

        if (hasNewFace || hasNewAtlas || hasNewChars)
            return TmpSchemaVersion.New;
        if (hasOldFace || hasOldAtlas)
            return TmpSchemaVersion.Old;

        return TmpSchemaVersion.New;
    }

    private static TmpSchemaVersion GetVersionHint(string? unityVersion)
    {
        if (string.IsNullOrWhiteSpace(unityVersion))
            return TmpSchemaVersion.Unknown;

        var parts = unityVersion
            .Split(['.', 'f', 'p', 'a', 'b'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => int.TryParse(token, out var value) ? value : -1)
            .Where(value => value >= 0)
            .Take(3)
            .ToArray();

        if (parts.Length < 3)
            return TmpSchemaVersion.Unknown;

        var version = new Version(parts[0], parts[1], parts[2]);
        if (version <= TmpOldOnlyLast)
            return TmpSchemaVersion.Old;
        if (version >= TmpNewSchemaFirst)
            return TmpSchemaVersion.New;
        return TmpSchemaVersion.Unknown;
    }

    private static int CountArrayChildren(AssetTypeValueField field)
    {
        if (field.IsDummy)
            return 0;

        var arrayField = field["Array"];
        if (!arrayField.IsDummy)
            return arrayField.Children.Count;

        return field.Children.Count;
    }

    private static PPtrInfo BestAtlasRef(
        PPtrInfo newRef,
        PPtrInfo oldRef,
        bool preferNew)
    {
        if (preferNew)
        {
            if (newRef.Exists && newRef.PathId > 0)
                return newRef;
            if (oldRef.Exists && oldRef.PathId > 0)
                return oldRef;
            if (newRef.Exists)
                return newRef;
            return oldRef;
        }

        if (oldRef.Exists && oldRef.PathId > 0)
            return oldRef;
        if (newRef.Exists && newRef.PathId > 0)
            return newRef;
        if (oldRef.Exists)
            return oldRef;
        return newRef;
    }

    private static PPtrInfo FirstAtlasRef(AssetTypeValueField field)
    {
        if (field.IsDummy)
            return default;

        var arrayField = field["Array"];
        if (!arrayField.IsDummy)
        {
            foreach (var child in arrayField.Children)
            {
                var pptr = ReadPPtr(child);
                if (pptr.Exists)
                    return pptr;
            }
        }

        return ReadPPtr(field);
    }

    private static PPtrInfo ReadPPtr(AssetTypeValueField field)
    {
        if (field.IsDummy)
            return default;

        var fileIdField = field["m_FileID"];
        var pathIdField = field["m_PathID"];
        if (fileIdField.IsDummy || pathIdField.IsDummy)
            return default;

        return new PPtrInfo
        {
            Exists = true,
            FileId = fileIdField.AsInt,
            PathId = pathIdField.AsLong,
        };
    }

    private static int ReadInt(AssetTypeValueField parent, string name)
    {
        if (parent.IsDummy)
            return 0;

        var field = parent[name];
        return field.IsDummy ? 0 : field.AsInt;
    }

    private readonly struct PPtrInfo
    {
        public bool Exists { get; init; }
        public int FileId { get; init; }
        public long PathId { get; init; }
    }
}
