// 그림 들여오기 규칙: Resources/Art 아래 PNG · JPG 는 전부 스프라이트 (가운데 피벗, 100px = 1 유닛)
// 지형 무늬는 바닥에 반복해서 깔 수 있게 Repeat. 그림 크기는 코드가 rig.json · assets.json 의 w/h 로 맞춤.
using UnityEditor;
using UnityEngine;

public class ArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var ti = (TextureImporter)assetImporter;
        bool terrain = assetPath.Contains("/out/terrain/");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = !assetPath.Contains("/out/icons/") && !assetPath.Contains("/out/ui/");
        ti.wrapMode = terrain ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (assetPath.EndsWith("/out/sky_clouds.png")) ti.wrapMode = TextureWrapMode.Mirror; // 시야 안개 연무 무늬로도 씀 (거울 반복 = 이음매 없음)
        ti.filterMode = FilterMode.Bilinear;
        ti.maxTextureSize = 2048;
        ti.textureCompression = terrain ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        ti.isReadable = terrain; // 바닥 굽기(GroundBaker)가 픽셀을 읽음
        ti.sRGBTexture = true;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect; // 판때기 · 리그 파츠는 사각형이 정렬 · 변형에 안전
        s.spriteAlignment = (int)SpriteAlignment.Center;
        ti.SetTextureSettings(s);
        // UI 키트: 웹 CSS border-image 조각 값 그대로 9칸 늘이기. PPU 400 → 테두리가 화면에서 1/4 굵기
        if (assetPath.Contains("/out/ui/"))
        {
            ti.spritePixelsPerUnit = 400;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            float b = n == "panel" ? 64 : n == "panel_sq" ? 60 : n == "btn_red" ? 38 : n.StartsWith("btn") ? 34 : n == "slot" ? 50 : n == "bar" ? 24 : 0;
            ti.spriteBorder = n == "header" ? new Vector4(60, 40, 60, 40) : new Vector4(b, b, b, b);
        }
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/audio/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.Streaming; // 배경음은 길어서 스트리밍
        ai.defaultSampleSettings = s;
    }
}
