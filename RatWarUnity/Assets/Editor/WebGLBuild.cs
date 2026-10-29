using System.Linq;
using UnityEditor;
using UnityEngine;

// 웹 빌드 (GitHub Pages 용)
//  - 실행: Unity.exe -batchmode -quit -projectPath RatWarUnity -executeMethod WebGLBuild.Build
//  - 출력: Builds/WebGL  (GitHub Pages 는 압축 헤더를 못 붙이므로 Gzip + 압축 해제 폴백 사용)
//  - 끝나면 활성 플랫폼을 Windows 로 되돌림
public static class WebGLBuild
{
    public static void Build()
    {
        var prevCompression = PlayerSettings.WebGL.compressionFormat;
        var prevFallback = PlayerSettings.WebGL.decompressionFallback;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[WebGLBuild] {report.summary.result} · {report.summary.totalSize / (1024 * 1024)} MB · {report.summary.totalTime}");

        PlayerSettings.WebGL.compressionFormat = prevCompression;
        PlayerSettings.WebGL.decompressionFallback = prevFallback;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
