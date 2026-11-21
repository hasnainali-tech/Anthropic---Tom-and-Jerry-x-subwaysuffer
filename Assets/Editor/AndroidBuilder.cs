// Command-line Android build entry point.
// Invoked by build-android.bat via:  -executeMethod AndroidBuilder.Build
//
// Optional command-line args (after Unity's own args):
//   -format apk|aab        Output type (default: apk)
//   -outputPath <path>     Output file (default: Builds/Android/<product>.<ext>)
//   -development           Development build (debuggable, profiler)
//   -versionCode <int>     Override Android bundle version code
//   -versionName <string>  Override bundleVersion (e.g. 1.2.0)
//
// Release signing (optional) is read from environment variables so
// passwords never land in ProjectSettings:
//   ANDROID_KEYSTORE_PATH, ANDROID_KEYSTORE_PASS,
//   ANDROID_KEYALIAS_NAME, ANDROID_KEYALIAS_PASS

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuilder
{
    // In-Editor one-click build (same settings as the command line). Output: Builds/Android/<product>.apk
    [MenuItem("Tools/Cheese Escape/Build Android APK")]
    public static void BuildApkFromMenu()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Build();
        string dir = Path.GetFullPath(Path.Combine("Builds", "Android"));
        if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.apk").Length > 0)
            EditorUtility.RevealInFinder(Directory.GetFiles(dir, "*.apk")[0]);
    }

    const string ResultFile = "Builds/Android/build_result.txt";
    static readonly System.Text.StringBuilder errors = new System.Text.StringBuilder();

    static void CollectErrors(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            lock (errors) errors.AppendLine("[" + type + "] " + msg).AppendLine();
    }

    static void WriteResult(string text)
    {
        Directory.CreateDirectory("Builds/Android");
        File.WriteAllText(ResultFile, text);
    }

    public static void Build()
    {
        int exitCode = 1;
        errors.Clear();
        WriteResult("STARTED " + DateTime.Now + "\n");
        Application.logMessageReceivedThreaded += CollectErrors;
        string resultText = "";
        bool prevAab = EditorUserBuildSettings.buildAppBundle;
        bool prevCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        string prevKeystoreName = PlayerSettings.Android.keystoreName;
        string prevAliasName = PlayerSettings.Android.keyaliasName;

        try
        {
            string format = (GetArg("-format") ?? "apk").ToLowerInvariant();
            if (format != "apk" && format != "aab")
                throw new ArgumentException($"Unknown -format '{format}'. Use apk or aab.");

            bool aab = format == "aab";
            bool dev = HasArg("-development");

            string product = string.Join("_", PlayerSettings.productName.Split(Path.GetInvalidFileNameChars()))
                                   .Replace(' ', '_');
            string output = GetArg("-outputPath")
                            ?? Path.Combine("Builds", "Android", $"{product}{(dev ? "_dev" : "")}.{format}");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            string versionCode = GetArg("-versionCode");
            if (!string.IsNullOrEmpty(versionCode))
                PlayerSettings.Android.bundleVersionCode = int.Parse(versionCode);

            string versionName = GetArg("-versionName");
            if (!string.IsNullOrEmpty(versionName))
                PlayerSettings.bundleVersion = versionName;

            ConfigureSigning();

            EditorUserBuildSettings.buildAppBundle = aab;

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in Build Settings.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = dev
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };

            Debug.Log($"[AndroidBuilder] Building {format.ToUpper()} ({(dev ? "development" : "release")}) " +
                      $"-> {Path.GetFullPath(output)}");
            Debug.Log($"[AndroidBuilder] Package: {PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android)}, " +
                      $"version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuilder] SUCCESS: {summary.totalSize / (1024f * 1024f):0.0} MB " +
                          $"in {summary.totalTime:mm\\:ss}");
                exitCode = 0;
                resultText = $"SUCCESS {DateTime.Now}\n{Path.GetFullPath(output)}\n{summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime}\n";
            }
            else
            {
                Debug.LogError($"[AndroidBuilder] FAILED: {summary.result}, {summary.totalErrors} error(s).");
                resultText = $"FAILED {DateTime.Now}: {summary.result}, {summary.totalErrors} error(s)\n";
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                            resultText += "[step: " + step.name + "] " + m.content + "\n\n";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidBuilder] {e}");
        }
        finally
        {
            Application.logMessageReceivedThreaded -= CollectErrors;
            if (resultText == "") resultText = "FAILED " + DateTime.Now + " (exception)\n";
            lock (errors) WriteResult(resultText + "\n----- logged errors -----\n" + errors);
            // Don't leave CLI-only settings behind in ProjectSettings.
            EditorUserBuildSettings.buildAppBundle = prevAab;
            PlayerSettings.Android.useCustomKeystore = prevCustomKeystore;
            PlayerSettings.Android.keystoreName = prevKeystoreName;
            PlayerSettings.Android.keyaliasName = prevAliasName;
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(exitCode);
    }

    static void ConfigureSigning()
    {
        string keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
        if (string.IsNullOrEmpty(keystore))
        {
            Debug.Log("[AndroidBuilder] No ANDROID_KEYSTORE_PATH set - signing with the debug key.");
            PlayerSettings.Android.useCustomKeystore = false;
            return;
        }
        if (!File.Exists(keystore))
            throw new FileNotFoundException("Keystore not found", keystore);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystore;
        PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS") ?? "";
        PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME") ?? "";
        PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS") ?? "";
        Debug.Log($"[AndroidBuilder] Signing with keystore {keystore}, alias '{PlayerSettings.Android.keyaliasName}'.");
    }

    static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    static bool HasArg(string name) =>
        Environment.GetCommandLineArgs().Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}

// Starts a build automatically after a script reload if Builds/Android/BUILD_REQUEST exists
// (lets a build be kicked off without clicking menus). The file is deleted first, so it runs once.
[InitializeOnLoad]
static class AndroidAutoBuild
{
    const string Trigger = "Builds/Android/BUILD_REQUEST";
    static AndroidAutoBuild()
    {
        if (!File.Exists(Trigger) || Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Trigger)) return;
            File.Delete(Trigger);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            AndroidBuilder.BuildApkFromMenu();
        };
    }
}
