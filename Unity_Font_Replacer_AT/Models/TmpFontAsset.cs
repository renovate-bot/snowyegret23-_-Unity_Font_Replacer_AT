namespace UnityFontReplacer.Models;

/// <summary>
/// TMP_FontAsset의 핵심 필드를 담는 통합 POCO 모델.
/// AssetTypeValueField에서 읽어와 조작한 뒤, 다시 ValueField에 쓸 때 사용.
/// </summary>
public class TmpFontAsset
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public TmpSchemaVersion SchemaVersion { get; set; }

    // Face info
    public TmpFaceInfo FaceInfo { get; set; } = new();

    // 신 스키마 글리프/캐릭터 테이블
    public List<TmpGlyphNew>? GlyphTable { get; set; }
    public List<TmpCharacterNew>? CharacterTable { get; set; }

    // 구 스키마 글리프 리스트
    public List<TmpGlyphOld>? GlyphInfoList { get; set; }

    // 아틀라스 참조
    public int AtlasTextureFileId { get; set; }
    public long AtlasTexturePathId { get; set; }

    // 머티리얼 참조
    public int MaterialFileId { get; set; }
    public long MaterialPathId { get; set; }

    // 아틀라스 파라미터
    public int AtlasWidth { get; set; }
    public int AtlasHeight { get; set; }
    public int AtlasPadding { get; set; }
    public int AtlasRenderMode { get; set; }

    public int GlyphCount => SchemaVersion == TmpSchemaVersion.New
        ? (GlyphTable?.Count ?? 0)
        : (GlyphInfoList?.Count ?? 0);
}
