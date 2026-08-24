// XRSetup — creates and wires the XR loader configuration.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THIS IS THE STEP THE PREVIOUS VR ATTEMPT MISSED
// ═══════════════════════════════════════════════════════════════════════════════
// Installing the OpenXR package is not enough to make a project VR-capable. Unity needs
// an XRGeneralSettings asset per build target, holding an XRManagerSettings with a
// LOADER assigned, registered in EditorBuildSettings under the XR Management key. Without
// that chain, XRSettings.enabled is false at runtime, no HMD is ever acquired, and every
// VR script compiles and silently does nothing — which is exactly what happened before.
//
// Doing it from a script rather than by hand means the configuration is:
//   * reproducible on the Windows machine that will actually run the headset,
//   * checkable (Verify() reports the state of every link in the chain),
//   * and recorded in version control as code rather than as a binary asset nobody
//     can diff.
//
// PLATFORM NOTE. OpenXR runs on Windows and Android (Quest), NOT on macOS. Configuring
// it here on a Mac sets up the Standalone (Windows) and Android targets correctly; the
// Mac editor will still report "no XR loader active" and run desktop, which is right.
//
// Run:
//   Unity -batchmode -nographics -quit -projectPath <p> -executeMethod XRSetup.Configure
//   Unity -batchmode -nographics -quit -projectPath <p> -executeMethod XRSetup.Verify

using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEditor.XR.OpenXR.Features;

public static class XRSetup
{
    const string XRDir = "Assets/XR";
    const string SettingsAsset = XRDir + "/XRGeneralSettingsPerBuildTarget.asset";
    const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

    // The build targets the experiment can actually run VR on.
    //   Standalone = Windows PC driving a Quest over Link (the intended lab setup)
    //   Android    = Quest standalone (not the plan, but configured so it is possible)
    static readonly BuildTargetGroup[] Targets =
    { BuildTargetGroup.Standalone, BuildTargetGroup.Android };

    [MenuItem("Tools/Experiment/Configure XR (OpenXR)")]
    public static void Configure()
    {
        Directory.CreateDirectory(XRDir);

        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsAsset);
        if (perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, SettingsAsset);
            Debug.Log("[XRSetup] created " + SettingsAsset);
        }

        // Register it with the editor. THIS is the link that was missing before: without
        // it the asset exists but Unity never looks at it.
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                                                    out XRGeneralSettingsPerBuildTarget existing) ||
            existing != perTarget)
        {
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            Debug.Log("[XRSetup] registered XR settings under " + XRGeneralSettings.k_SettingsKey);
        }

        foreach (var group in Targets)
        {
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = perTarget.SettingsForBuildTarget(group);
            if (general == null) { Debug.LogError("[XRSetup] no XRGeneralSettings for " + group); continue; }

            if (general.Manager == null)
            {
                var mgr = ScriptableObject.CreateInstance<XRManagerSettings>();
                mgr.name = "XRManagerSettings_" + group;
                AssetDatabase.AddObjectToAsset(mgr, perTarget);
                general.Manager = mgr;
            }

            // Start XR when the app starts. The runtime still degrades to desktop when no
            // headset is present — VRRuntime reports which happened.
            general.InitManagerOnStart = true;

            bool ok = XRPackageMetadataStore.AssignLoader(general.Manager, OpenXRLoaderType, group);
            Debug.Log("[XRSetup] " + group + ": OpenXR loader " + (ok ? "assigned" : "ASSIGN FAILED"));

            ConfigureOpenXR(group);
        }

        EditorUtility.SetDirty(perTarget);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[XRSetup] done.");
        Verify();
    }

    /// <summary>Enable the interaction profiles the target hardware uses. Without a
    /// profile the runtime initialises but reports no controllers — a failure that looks
    /// like broken code rather than missing configuration.</summary>
    static void ConfigureOpenXR(BuildTargetGroup group)
    {
        FeatureHelpers.RefreshFeatures(group);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (settings == null) { Debug.LogWarning("[XRSetup] no OpenXRSettings for " + group); return; }

        // Quest 2 / Quest 3 Touch controllers. The Meta Quest feature group is what the
        // Link setup advertises; the Touch profile is what supplies grip/trigger/buttons.
        EnableFeature<OculusTouchControllerProfile>(settings, group, "Oculus Touch");
        // Harmless if the hardware differs: a profile the runtime does not support is
        // simply not selected. Enabling the generic profile as well means a non-Touch
        // headset still gets pose + trigger rather than nothing.
        EnableFeature<KHRSimpleControllerProfile>(settings, group, "KHR Simple Controller");
    }

    static void EnableFeature<T>(OpenXRSettings settings, BuildTargetGroup group, string label)
        where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
    {
        var f = settings.GetFeature<T>();
        if (f == null) { Debug.LogWarning("[XRSetup] " + group + ": feature not found - " + label); return; }
        f.enabled = true;
        EditorUtility.SetDirty(f);
        Debug.Log("[XRSetup] " + group + ": enabled " + label + " interaction profile");
    }

    /// <summary>Report the state of every link in the chain, so a VR failure can be
    /// diagnosed instead of guessed at.</summary>
    [MenuItem("Tools/Experiment/Verify XR configuration")]
    public static void Verify()
    {
        Debug.Log("──────── XR CONFIGURATION ────────");
        bool registered = EditorBuildSettings.TryGetConfigObject(
            XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
        Debug.Log("  settings asset registered : " + (registered && perTarget != null));

        if (!registered || perTarget == null)
        {
            Debug.LogError("  -> XR is NOT configured. Run XRSetup.Configure.");
            return;
        }

        foreach (var group in Targets)
        {
            var general = perTarget.SettingsForBuildTarget(group);
            string mgr = general?.Manager == null ? "NONE" : general.Manager.name;
            int loaders = general?.Manager?.activeLoaders?.Count ?? 0;
            string names = "";
            if (general?.Manager?.activeLoaders != null)
                foreach (var l in general.Manager.activeLoaders) names += (names == "" ? "" : ", ") + l.name;

            Debug.Log($"  {group,-12} manager={mgr}  initOnStart={general?.InitManagerOnStart}  " +
                      $"loaders={loaders} [{names}]");

            var s = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (s == null) { Debug.LogWarning($"  {group,-12} no OpenXRSettings"); continue; }
            var feats = s.GetFeatures();
            string on = "";
            foreach (var f in feats) if (f != null && f.enabled) on += (on == "" ? "" : ", ") + f.GetType().Name;
            Debug.Log($"  {group,-12} enabled OpenXR features: {(on == "" ? "NONE" : on)}");
        }

        Debug.Log("  NOTE: macOS cannot run OpenXR. On this machine the editor will report");
        Debug.Log("        'no XR loader active' and run desktop — that is expected and correct.");
        Debug.Log("        Verification on hardware happens on the Windows machine driving the Quest.");
        Debug.Log("──────────────────────────────────");
    }
}
