using UnityEditor;
using UnityEngine;

// Headless play-mode drivers. Editor-only; never ships in a build.
//
//   RunMissionTest — enters play mode, waits for MissionTestHarness to finish the
//   twelve-mission battery, then exits with a non-zero code if anything failed, so
//   the whole thing works as a CI-style gate:
//
//     Unity -batchmode -projectPath <proj> \
//           -executeMethod PlayCapture.RunMissionTest -missiontest -logFile test.log
//
//   IMPORTANT: do NOT add -nographics. The real GLB cockpit's PFD/MFD render to
//   off-screen cameras, and Unity's null graphics device crashes in the dynamic batch
//   renderer when they do. -batchmode alone (with a graphics device) is correct and is
//   what the cockpit project's own verification workflow always used.
//
//   Do NOT pass -quit: the editor must stay alive long enough to run play mode.
//   Domain and scene reload are disabled so entering play mode is fast.
public static class PlayCapture
{
    static double t0;
    static bool entered;
    // The mission battery now flies the WHOLE BANK (42 missions x 300 s at 8x is about
    // 26 minutes of wall clock before overhead), not the original twelve. 1800 s used to
    // be generous and would now time out most of the way through.
    const double HardTimeoutS = 5400.0;

    /// <summary>Timed play-mode run for the COCKPIT/VISUAL verification tools
    /// (ScreenProbe -screens, YokeProbe -probe, YokeShot, FlightTest -flighttest).
    /// Enters play mode, lets the sim run for ~58 s, then exits. Kept from the
    /// cockpit project's toolchain — those probes are how the GLB cockpit, the yoke
    /// rigging and the PFD/MFD screen placement were verified in the first place,
    /// and they are still the way to re-verify them after a change.
    ///
    ///   Unity -batchmode -nographics -projectPath <p> \
    ///         -executeMethod PlayCapture.Run -screens -logFile screens.log</summary>
    public static void Run()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickTimed;
        EditorApplication.EnterPlaymode();
    }

    static void TickTimed()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }
        if (EditorApplication.timeSinceStartup - t0 > 58.0)
        {
            EditorApplication.update -= TickTimed;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }

    /// <summary>Renders a snapshot of every mission scene (MissionShots, -shots).</summary>
    public static void RunMissionShots()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickShots;
        EditorApplication.EnterPlaymode();
    }

    static void TickShots()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }
        bool done = MissionShots.Finished;
        bool timedOut = EditorApplication.timeSinceStartup - t0 > HardTimeoutS;
        if (!done && !timedOut) return;
        if (timedOut && !done) Debug.LogError("[SHOTS] HARD TIMEOUT");
        EditorApplication.update -= TickShots;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(done ? 0 : 1);
    }

    /// <summary>Runs the cockpit control tests (ControlTestHarness, -controltest).</summary>
    public static void RunControlTest()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickControlTest;
        EditorApplication.EnterPlaymode();
    }

    static void TickControlTest()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }
        bool done = ControlTestHarness.Finished;
        bool timedOut = EditorApplication.timeSinceStartup - t0 > 900.0;
        if (!done && !timedOut) return;
        int failures = ControlTestHarness.Failures;
        if (timedOut && !done) { Debug.LogError("[CTEST] HARD TIMEOUT"); failures = Mathf.Max(1, failures); }
        Debug.Log("[CTEST] exiting, failures=" + failures);
        EditorApplication.update -= TickControlTest;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>Runs the wind-model tests (WindTestHarness, -windtest).</summary>

    public static void RunPhysicsTest()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickPhysicsTest;
        EditorApplication.EnterPlaymode();
    }

    static void TickPhysicsTest()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }
        bool done = PhysicsTestHarness.Finished;
        bool timedOut = EditorApplication.timeSinceStartup - t0 > 900.0;
        if (!done && !timedOut) return;
        int failures = PhysicsTestHarness.Failures;
        if (timedOut && !done) { Debug.LogError("[PTEST] HARD TIMEOUT"); failures = Mathf.Max(1, failures); }
        Debug.Log("[PTEST] exiting, failures=" + failures);
        EditorApplication.update -= TickPhysicsTest;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    public static void RunWindTest()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickWindTest;
        EditorApplication.EnterPlaymode();
    }

    static void TickWindTest()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }
        bool done = WindTestHarness.Finished;
        bool timedOut = EditorApplication.timeSinceStartup - t0 > 900.0;
        if (!done && !timedOut) return;
        int failures = WindTestHarness.Failures;
        if (timedOut && !done) { Debug.LogError("[WTEST] HARD TIMEOUT"); failures = Mathf.Max(1, failures); }
        Debug.Log("[WTEST] exiting, failures=" + failures);
        EditorApplication.update -= TickWindTest;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>Renders the cockpit design-review shots (CockpitDesignShots,
    /// -designshots). CockpitDesignShots' own header documented this entry point, but it
    /// did not exist — so the documented command failed and the shots were only ever
    /// produced by the generic timed Run(), which exits on a stopwatch rather than when
    /// the renders are actually finished.</summary>
    public static void RunDesignShots()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickDesignShots;
        EditorApplication.EnterPlaymode();
    }

    static void TickDesignShots()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }
        bool done = CockpitDesignShots.Finished;
        bool timedOut = EditorApplication.timeSinceStartup - t0 > 900.0;
        if (!done && !timedOut) return;
        if (timedOut && !done) Debug.LogError("[DESIGNSHOTS] HARD TIMEOUT");
        EditorApplication.update -= TickDesignShots;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(done ? 0 : 1);
    }

    public static void RunMissionTest()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        entered = false;
        EditorApplication.update += TickMissionTest;
        EditorApplication.EnterPlaymode();
    }

    static void TickMissionTest()
    {
        if (!EditorApplication.isPlaying) return;
        if (!entered) { entered = true; t0 = EditorApplication.timeSinceStartup; }

        bool done = MissionTestHarness.Finished;
        bool timedOut = EditorApplication.timeSinceStartup - t0 > HardTimeoutS;
        if (!done && !timedOut) return;

        int failures = MissionTestHarness.Failures;
        if (timedOut && !done)
        {
            Debug.LogError("[MTEST] HARD TIMEOUT after " + HardTimeoutS + " s — harness never reported done");
            failures = failures > 0 ? failures : 1;
        }
        Debug.Log("[MTEST] exiting, failures=" + failures);

        EditorApplication.update -= TickMissionTest;
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
