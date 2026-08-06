#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Creates a portrait Android test APK from the enabled Build Settings scenes.
/// A request file lets an already-open Unity Editor perform the build safely.
/// </summary>
[InitializeOnLoad]
public static class AndroidBuildAutomation
{
    private const string RequestRelativePath = "Builds/Android/build-request.txt";
    private const string ResultRelativePath = "Builds/Android/build-result.txt";
    private const string ApkRelativePath = "Builds/Android/CocaSorting-mobile.apk";

    private static bool buildScheduled;

    static AndroidBuildAutomation()
    {
        EditorApplication.delayCall += TryRunRequestedBuild;
    }

    [MenuItem("Build/Coca Sorting/Build Android Test APK")]
    public static void BuildAndroidTestApk()
    {
        RunBuild("manual-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
    }

    private static void TryRunRequestedBuild()
    {
        if (buildScheduled || BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling)
        {
            return;
        }

        string requestPath = GetProjectPath(RequestRelativePath);
        if (!File.Exists(requestPath))
        {
            return;
        }

        string requestId = File.ReadAllText(requestPath).Trim();
        if (string.IsNullOrEmpty(requestId) || HasCompletedRequest(requestId))
        {
            return;
        }

        buildScheduled = true;
        EditorApplication.delayCall += () => RunBuild(requestId);
    }

    private static void RunBuild(string requestId)
    {
        string resultPath = GetProjectPath(ResultRelativePath);
        string apkPath = GetProjectPath(ApkRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(apkPath));

        bool originalCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        bool originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;

        try
        {
            ConfigureMobilePlayerSettings();

            // The configured release keystore path belongs to another machine.
            // A Development APK uses Unity's standard debug signing instead.
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android))
            {
                throw new InvalidOperationException("Unity could not switch to the Android build target.");
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes exist in Build Settings.");
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            long apkSizeBytes = File.Exists(apkPath)
                ? new FileInfo(apkPath).Length
                : 0;
            string result =
                $"RequestId: {requestId}{Environment.NewLine}" +
                $"Result: {summary.result}{Environment.NewLine}" +
                $"Errors: {summary.totalErrors}{Environment.NewLine}" +
                $"Warnings: {summary.totalWarnings}{Environment.NewLine}" +
                $"ApkSizeBytes: {apkSizeBytes}{Environment.NewLine}" +
                $"BuildReportSizeBytes: {summary.totalSize}{Environment.NewLine}" +
                $"Duration: {summary.totalTime}{Environment.NewLine}" +
                $"Output: {apkPath}{Environment.NewLine}";
            File.WriteAllText(resultPath, result);

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android build finished with {summary.result} and {summary.totalErrors} error(s).");
            }

            Debug.Log($"Android test APK built successfully: {apkPath}");
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                resultPath,
                $"RequestId: {requestId}{Environment.NewLine}" +
                $"Result: Failed{Environment.NewLine}" +
                $"Exception: {exception}{Environment.NewLine}");
            Debug.LogException(exception);
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = originalCustomKeystore;
            EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
            buildScheduled = false;
        }
    }

    private static void ConfigureMobilePlayerSettings()
    {
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        // This project's Swappy package requires a shared C++ runtime while
        // Unity's generated IL2CPP project uses the static runtime. Disable the
        // optional integration so Gradle can configure the native project.
        PlayerSettings.Android.optimizedFramePacing = false;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
    }

    private static bool HasCompletedRequest(string requestId)
    {
        string resultPath = GetProjectPath(ResultRelativePath);
        return File.Exists(resultPath) &&
               File.ReadAllText(resultPath).StartsWith(
                   "RequestId: " + requestId,
                   StringComparison.Ordinal);
    }

    private static string GetProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
#endif
