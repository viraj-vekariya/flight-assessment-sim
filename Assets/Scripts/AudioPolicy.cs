// AudioPolicy — one place that decides what the simulator is allowed to make a noise
// about, and records the answer.
//
// ═══════════════════════════════════════════════════════════════════════════════
// TWO PROBLEMS, ONE SWITCH
// ═══════════════════════════════════════════════════════════════════════════════
//
// THE DEVELOPMENT PROBLEM. Every headless battery run, every probe, every screenshot
// pass opens the simulator and starts a wind loop, a ground-roll rumble, a stall horn
// and a stream of ATC callouts. Across a forty-minute battery that is maddening, and the
// previous remedy was a hard-coded `engine.volume = 0f;  // (per request)` buried in
// AircraftAudio.Update.
//
// THE EXPERIMENTAL PROBLEM, which is the serious one. That hack would have shipped.
// A muted engine is not a neutral choice: engine sound is a WORKLOAD-RELEVANT CUE.
// A partial power loss is heard before it is seen, and M2V2 (carburettor icing) and
// H2V2 (partial power loss) both depend on the pilot noticing a change in how the engine
// is running. Muting it silently removes the primary cue from two HIGH/MEDIUM missions
// and leaves their workload rationale describing a task the participant is not being
// given.
//
// Worse, it would have varied without being recorded. If some participants hear the
// stall horn and others do not, that is an uncontrolled variable sitting in the middle
// of the manipulation — and nothing in the data would say which was which.
//
// So audio becomes a DECLARED CONDITION: chosen explicitly, applied in one place, and
// written into session.json and every trial's metadata.json.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE PROFILES
// ═══════════════════════════════════════════════════════════════════════════════
//   SILENT     nothing at all. The default for headless tool runs.
//   DEV        warnings and callouts only — you can tell what the simulator is doing
//              without the continuous loops. The default in the editor.
//   EXPERIMENT everything, at calibrated levels. The default in a built player, and
//              the ONLY profile a recorded session may use.
//
// Command line: -audio:silent | -audio:dev | -audio:experiment
// A recorded trial started under anything other than EXPERIMENT is marked as such in its
// metadata, so it can never be quietly pooled with properly-run trials.

using UnityEngine;

public enum AudioProfile { Silent, Dev, Experiment }

public static class AudioPolicy
{
    public static AudioProfile Profile { get; private set; } = AudioProfile.Experiment;
    static bool resolved;

    /// <summary>Continuous engine sound. A workload-relevant cue: rough running and
    /// partial power loss are HEARD before they are seen.</summary>
    public static float EngineGain => Profile == AudioProfile.Experiment ? 1f : 0f;
    /// <summary>Airflow noise. Ambience, and a weak airspeed cue.</summary>
    public static float WindGain => Profile == AudioProfile.Experiment ? 1f : 0f;
    /// <summary>Ground-roll rumble. Ambience, and a weak groundspeed cue on the roll.</summary>
    public static float GroundGain => Profile == AudioProfile.Experiment ? 1f : 0f;
    /// <summary>Stall horn, master caution, impact. These are ALERTS: they carry
    /// information the pilot is meant to act on, and reaction time to them is data.
    /// Kept in DEV so a developer can hear that a warning fired.</summary>
    public static float WarningGain => Profile == AudioProfile.Silent ? 0f : 1f;
    /// <summary>ATC and GPWS speech. Task instructions — never merely decorative.</summary>
    public static float CalloutGain => Profile == AudioProfile.Silent ? 0f : 1f;

    /// <summary>True when this profile is fit to record a trial under.</summary>
    public static bool ValidForExperiment => Profile == AudioProfile.Experiment;

    public static string Describe() =>
        Profile.ToString().ToLowerInvariant() +
        (ValidForExperiment ? "" : "  (NOT VALID FOR A RECORDED SESSION)");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Resolve()
    {
        if (resolved) return;
        resolved = true;

        // Explicit flag wins over everything.
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            if (a == "-audio:silent") { Profile = AudioProfile.Silent; Announce(); return; }
            if (a == "-audio:dev") { Profile = AudioProfile.Dev; Announce(); return; }
            if (a == "-audio:experiment") { Profile = AudioProfile.Experiment; Announce(); return; }
        }

        // Otherwise: a headless tool run is silent, the editor is quiet-but-informative,
        // and a built player is the real thing.
        if (Application.isBatchMode) Profile = AudioProfile.Silent;
        else if (Application.isEditor) Profile = AudioProfile.Dev;
        else Profile = AudioProfile.Experiment;
        Announce();
    }

    static void Announce() => Debug.Log("[AudioPolicy] profile = " + Describe());

    /// <summary>Force a profile — used by the operator menu so a session can be switched
    /// to EXPERIMENT without relaunching.</summary>
    public static void Set(AudioProfile p)
    {
        resolved = true;
        Profile = p;
        Announce();
    }
}
