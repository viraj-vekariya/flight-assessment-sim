// BuildTool — the laboratory build, from a command line rather than from remembered
// dialog settings.
//
// WHY A SCRIPT AND NOT THE BUILD DIALOG
//   A build produced by clicking through the Build Settings window is not reproducible:
//   the scenes, the target, the compression, the development flag and the output path
//   all live in whatever state someone left them in. For a build that a laboratory will
//   collect data with, "which settings was this built with?" has to be answerable from
//   the repository, so it lives here.
//
//   Run:
//     UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
//     "$UNITY" -batchmode -nographics -quit -projectPath "$PWD" \
//              -executeMethod BuildTool.BuildWindows -logFile build_win.log
//
// NOTE ON SCENES
//   This project builds its entire world, aircraft and cockpit FROM CODE at runtime —
//   there are no wired scenes, by deliberate architectural choice (see
//   FINAL_ARCHITECTURE.md). The build therefore ships whatever scene list the project
//   has, which may be empty, and `Bootstrap` spawns `GameManager` on load. That is why
//   there is no scene-management strategy to get wrong, and why this file does not try
//   to assemble one.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildTool
{
    const string ProductName = "FlightAssessmentSim";

    [MenuItem("Tools/Experiment/Build Windows (lab)")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", ProductName + ".exe");

    [MenuItem("Tools/Experiment/Build macOS (development)")]
    public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS", ProductName + ".app");

    static void Build(BuildTarget target, string label, string exeName)
    {
        // Fail LOUDLY and specifically if the platform module is missing. Unity cannot
        // cross-compile to a target whose module is not installed, and the default
        // failure is an opaque "Build failed" that costs an hour to diagnose.
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
        {
            string msg =
                "[BuildTool] " + label + " build support is NOT INSTALLED in this editor.\n" +
                "Install it once with:\n" +
                "  \"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub\" -- --headless \\\n" +
                "      install-modules --version " + Application.unityVersion +
                " --module " + (target == BuildTarget.StandaloneWindows64 ? "windows-mono" : "mac-mono") +
                " --childModules\n" +
                "See WINDOWS_DEPLOYMENT.md.";
            Debug.LogError(msg);
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
        }

        string root = Path.GetDirectoryName(Application.dataPath);
        string outDir = Path.Combine(root, "Builds", label);
        Directory.CreateDirectory(outDir);

        var opts = new BuildPlayerOptions
        {
            scenes = EnsureBootstrapScene(),
            locationPathName = Path.Combine(outDir, exeName),
            target = target,
            // NOT a development build: the profiler hooks and the debug overlay have no
            // place in a session that a participant is being recorded in, and the
            // development player's extra allocation shows up as frame-time jitter.
            options = BuildOptions.None,
        };

        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = "DefaultCompany";      // keep persistentDataPath stable
        PlayerSettings.runInBackground = true;              // never pause during EEG recording
        PlayerSettings.resizableWindow = false;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        // A fixed, known frame policy: vsync off and no target cap, so the frame rate is
        // whatever the machine can do rather than something that changes with a display.
        // The physics clock is fixed at 0.02 s regardless, which is what the telemetry
        // and event timing are locked to.
        QualitySettings.vSyncCount = 0;

        Debug.Log("[BuildTool] building " + label + " -> " + opts.locationPathName);
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;

        Debug.Log("[BuildTool] result=" + s.result +
                  " size=" + (s.totalSize / (1024 * 1024)) + " MB" +
                  " errors=" + s.totalErrors + " warnings=" + s.totalWarnings +
                  " time=" + s.totalTime);

        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildTool] BUILD FAILED");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // Unity emits a folder literally named "..._BurstDebugInformation_DoNotShip".
        // Take it at its word: it is several MB of symbols that a laboratory PC has no
        // use for, and leaving it in a folder someone is told to copy wholesale invites
        // it onto the lab machine forever.
        foreach (var d in Directory.GetDirectories(outDir))
            if (d.EndsWith("_DoNotShip"))
            {
                try { Directory.Delete(d, true); Debug.Log("[BuildTool] removed " + Path.GetFileName(d)); }
                catch (System.Exception e) { Debug.LogWarning("[BuildTool] could not remove " + d + ": " + e.Message); }
            }

        WriteBuildManifest(outDir, label, s);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>Return the build's scene list, creating the one scene it needs if the
    /// project has none.
    ///
    /// THIS PROJECT HAS NO SCENES ON PURPOSE. The terrain, aerodrome, aircraft and
    /// cockpit are all constructed from C# at runtime, and `Bootstrap` spawns
    /// `GameManager` via [RuntimeInitializeOnLoadMethod]. In the EDITOR that works with
    /// no scene at all — press Play and the world builds itself — which is why the
    /// project has run for months without one.
    ///
    /// A PLAYER cannot. Unity refuses to build an empty scene list: it falls back to
    /// "the current scene", which in batch mode is an unsaved untitled one, and fails
    /// with `Cannot build untitled scene.` — the actual error this project hit the first
    /// time anyone tried to build it. So the simulator had never been packaged into a
    /// player on ANY platform, and that was invisible because nothing had ever asked.
    ///
    /// The scene created here is deliberately EMPTY. It is a launch point, not a level:
    /// nothing is wired in it, so it cannot drift out of step with the code that builds
    /// the world, and the project keeps the single-launch-path property that makes
    /// "which scene was that recorded in?" an unaskable question.</summary>
    static string[] EnsureBootstrapScene()
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled) list.Add(s.path);
        if (list.Count > 0) return list.ToArray();

        const string dir = "Assets/Scenes";
        const string path = dir + "/Bootstrap.unity";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            Debug.Log("[BuildTool] created the empty bootstrap scene at " + path +
                      " (the world is built from code; this is only a launch point)");
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        list.Add(path);
        return list.ToArray();
    }

    /// <summary>Drop a manifest beside the build recording exactly what produced it.
    /// A data-collection build has to be identifiable months later, when the question is
    /// "was this session recorded with the version that had the wind model?" and the
    /// only artefact left is a folder on a lab PC.</summary>
    static void WriteBuildManifest(string outDir, string label, BuildSummary s)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"product\": \"" + ProductName + "\",");
        sb.AppendLine("  \"platform\": \"" + label + "\",");
        sb.AppendLine("  \"unity_version\": \"" + Application.unityVersion + "\",");
        sb.AppendLine("  \"built_utc\": \"" + System.DateTime.UtcNow.ToString("o") + "\",");
        sb.AppendLine("  \"size_mb\": " + (s.totalSize / (1024 * 1024)) + ",");
        sb.AppendLine("  \"bank_size\": " + MissionLibrary.All().Count + ",");
        sb.AppendLine("  \"cognitive_axis\": " + MissionLibrary.CognitiveAxis().Count + ",");
        sb.AppendLine("  \"psychomotor_axis\": " + MissionLibrary.PsychomotorAxis().Count + ",");
        sb.AppendLine("  \"variants_per_cell\": " + MissionLibrary.VariantCount + ",");
        sb.AppendLine("  \"telemetry_columns\": " + ExperimentLogger.TelemetryHeader().Split(',').Length + ",");
        sb.AppendLine("  \"telemetry_hz\": " + ExperimentLogger.TelemetryHz + ",");
        sb.AppendLine("  \"marker_tags\": " + EventMarkers.All.Length);
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(outDir, "build_manifest.json"), sb.ToString());
        Debug.Log("[BuildTool] wrote build_manifest.json");
    }
}
