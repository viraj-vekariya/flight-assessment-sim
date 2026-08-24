using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Headless flight verifier. ONLY runs under -batchmode (never in the shipped game), so it
/// cannot affect normal play. It confirms the sim booted straight into free flight, then flies
/// a scripted profile (ground accel -> rotate -> climb -> right turn -> level -> descent),
/// logging the flight model's own readouts every second to persistentDataPath/flighttest.csv.
/// It disables AircraftController and drives CessnaPhysics directly to test the flight model.
/// </summary>
public class FlightTest : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // Test harness only: runs under -batchmode OR when Unity is launched with -flighttest.
        // Never spawns in normal play, so it cannot affect the shipped game.
        // This harness DRIVES THE CONTROLS. It must never run alongside another one.
        // MissionTestHarness (-missiontest) also drives the controls, and the two
        // fought every frame: the mission battery's inputs were overwritten, so the
        // aeroplane flew FlightTest's scripted climb profile instead of the mission,
        // climbed away from every assigned altitude, and half the missions were
        // scored INCOMPLETE for reasons that had nothing to do with the missions.
        // Explicit opt-out wins over the implicit batchmode default.
        var args = System.Environment.GetCommandLineArgs();
        foreach (var a in args) if (a == "-missiontest" || a == "-probe" || a == "-screens" || a == "-shots" || a == "-controltest"
                             || a == "-designshots" || a == "-cockpitaudit") return;

        bool enabled = Application.isBatchMode;
        if (!enabled) foreach (var a in args) if (a == "-flighttest") { enabled = true; break; }
        if (!enabled) return;
        new GameObject("FlightTest").AddComponent<FlightTest>();
    }

    // NOTE: in this sim, NEGATIVE pitchInput = NOSE UP, positive = nose down.
    enum Phase { Roll, Rotate, Climb, Turn, Cruise, Descend, Done }

    CessnaPhysics phys;
    float t, nextLog, phaseT, cruiseHdg, lastPitch;
    Phase phase = Phase.Roll;
    string bootState = "?";
    bool wrote, liftedOff, crashSeen;
    float aThr, aPitch, aRoll;                     // smoothed (ramped) applied inputs
    readonly StringBuilder sb = new StringBuilder();

    Transform yokeVis;
    void Start() => sb.AppendLine("t,phase,thr,spd_kmh,alt,vs,hdg,aoa,pitch,roll,grounded,stall,rollIn,yokeZ,yokeLocalZ");

    void Update()
    {
        if (phys == null)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Aircraft == null) return;
            phys = gm.Aircraft;
            bootState = gm.State.ToString();
            var ctl = phys.GetComponent<AircraftController>();
            if (ctl != null) ctl.enabled = false;
            Debug.Log("[FLIGHTTEST] bootState=" + bootState);
        }

        // The yoke is built async (after the GLB loads), so find it lazily.
        if (yokeVis == null) { var yv = GameObject.Find("YokeVisual"); if (yv != null) { yokeVis = yv.transform; Debug.Log("[FLIGHTTEST] YokeVisual found at t=" + t.ToString("F1")); } }

        float dt = Time.deltaTime;
        t += dt; phaseT += dt;
        if (phys.AltitudeM > 3f) liftedOff = true;

        if (phys.Crashed) { if (!crashSeen) { crashSeen = true;
            Debug.Log($"[FLIGHTTEST] CRASH '{phys.CrashReason}' alt={phys.AltitudeM:F0} pos={phys.transform.position:F0} spd={phys.AirspeedKmh:F0} phase={phase}"); } }
        else crashSeen = false;

        // Attitude-hold autopilot: each phase targets a PITCH ATTITUDE + BANK ANGLE (like a
        // pilot who rotates then holds the climb, rather than holding a fixed stick input).
        // Phases advance on real aircraft state so a stray reset never desyncs the profile.
        float tThr = 1f, targetPitch = 0f, targetBank = 0f;
        switch (phase)
        {
            case Phase.Roll:
                if (phys.AirspeedKmh > 95f) { phase = Phase.Rotate; phaseT = 0f; }
                break;
            case Phase.Rotate: targetPitch = 8f;
                if (phys.AltitudeM > 4f) { phase = Phase.Climb; phaseT = 0f; }
                break;
            case Phase.Climb: targetPitch = 9f;
                if (phys.AltitudeM > 90f) { phase = Phase.Turn; phaseT = 0f; cruiseHdg = phys.HeadingDeg; }
                break;
            case Phase.Turn: targetPitch = 4f; targetBank = 20f;
                if (Mathf.Abs(Mathf.DeltaAngle(cruiseHdg, phys.HeadingDeg)) > 55f || phaseT > 12f) { phase = Phase.Cruise; phaseT = 0f; }
                break;
            case Phase.Cruise: tThr = 0.68f; targetPitch = 0f;
                if (phaseT > 8f) { phase = Phase.Descend; phaseT = 0f; }
                break;
            case Phase.Descend: tThr = 0.4f; targetPitch = -4f;
                if (phys.AltitudeM < 30f || phaseT > 15f) { phase = Phase.Done; }
                break;
        }

        // PD pitch controller (nose-up = NEGATIVE input); P roll controller for bank hold.
        float pitchRate = (phys.PitchDeg - lastPitch) / Mathf.Max(dt, 1e-4f); lastPitch = phys.PitchDeg;
        float pitchCmd = phase == Phase.Roll ? 0f
                       : Mathf.Clamp(-0.05f * (targetPitch - phys.PitchDeg) + 0.015f * pitchRate, -0.6f, 0.6f);
        float rollCmd  = Mathf.Clamp(0.03f * (targetBank - phys.RollDeg), -0.5f, 0.5f);

        aThr   = Mathf.MoveTowards(aThr,   tThr,     dt * 0.6f);
        aPitch = Mathf.MoveTowards(aPitch, pitchCmd, dt * 1.5f);
        aRoll  = Mathf.MoveTowards(aRoll,  rollCmd,  dt * 1.5f);
        phys.throttle = aThr; phys.pitchInput = aPitch; phys.rollInput = aRoll;

        if (t >= nextLog)
        {
            nextLog += 1f;
            float yz = yokeVis != null ? yokeVis.eulerAngles.z : -1f;
            float ylz = yokeVis != null ? yokeVis.localEulerAngles.z : -1f;
            sb.AppendLine($"{t:F1},{phase},{phys.Throttle01:F2},{phys.AirspeedKmh:F1},{phys.AltitudeM:F1},{phys.VerticalSpeedMs:F2},{phys.HeadingDeg:F0},{phys.AoADeg:F1},{phys.PitchDeg:F1},{phys.RollDeg:F1},{phys.Grounded},{phys.Stalled},{phys.rollInput:F2},{yz:F1},{ylz:F1}");
            // Write EVERY second so we always keep the latest data even if the harness cuts off.
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "flighttest.csv"),
                sb.ToString() + $"# liftedOff={liftedOff} phase={phase} bootState={bootState}\n");
        }

        if ((phase == Phase.Done || t > 56f) && !wrote)
        {
            wrote = true;
            Debug.Log("[FLIGHTTEST] done liftedOff=" + liftedOff + " finalPhase=" + phase);
        }
    }
}
