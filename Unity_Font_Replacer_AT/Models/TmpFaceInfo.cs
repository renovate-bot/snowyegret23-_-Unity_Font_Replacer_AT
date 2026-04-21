using System.Text.Json.Serialization;

namespace UnityFontReplacer.Models;

/// <summary>
/// TMP FaceInfo 통합 모델. 신 스키마(m_FaceInfo)와 구 스키마(m_fontInfo) 모두 이 모델로 변환된다.
/// </summary>
public class TmpFaceInfo
{
    public string FamilyName { get; set; } = "";
    public string StyleName { get; set; } = "";
    public int PointSize { get; set; }
    public float Scale { get; set; } = 1.0f;
    public int UnitsPerEM { get; set; }
    public float LineHeight { get; set; }
    public float AscentLine { get; set; }
    public float CapLine { get; set; }
    public float MeanLine { get; set; }
    public float Baseline { get; set; }
    public float DescentLine { get; set; }
    public float SuperscriptOffset { get; set; }
    public float SuperscriptSize { get; set; } = 0.5f;
    public float SubscriptOffset { get; set; }
    public float SubscriptSize { get; set; } = 0.5f;
    public float UnderlineOffset { get; set; }
    public float UnderlineThickness { get; set; }
    public float StrikethroughOffset { get; set; }
    public float StrikethroughThickness { get; set; }
    public float TabWidth { get; set; }

    // 구 스키마(m_fontInfo)에만 있는 필드
    public int Padding { get; set; }
    public int AtlasWidth { get; set; }
    public int AtlasHeight { get; set; }
}
