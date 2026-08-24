// MissionShots — renders a snapshot of every mission scene.
//
// Dev/documentation tool. Runs ONLY with -shots; never ships and never touches
// experiment data. It flies each of the twelve missions far enough to reach a chosen
// "showcase moment" — the point where that mission's distinctive feature is on screen —
// and renders two views:
//
//     <id>_cockpit.png    the pilot's eye, through the real GLB cockpit
//     <id>_external.png    a chase view of the aeroplane in the scene
//
// The showcase moment matters. Capturing every mission at t = 0 would produce twelve
// near-identical pictures of a Cessna panel, which says nothing about what the missions
// actually are. Each capture time below is chosen so that the thing that DEFINES the
// mission is visible: the aeroplane holding short at a lit hold-short line, the traffic
// sitting across the runway, the door-open annunciator, the approach in cloud.
//
//   Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunMissionShots \
//         -shots -logFile shots.log
//
// NOTE: no -nographics. The cockpit's PFD/MFD render to off-screen cameras and Unity's
// null graphics device crashes on them — the same constraint as the mission battery.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MissionShots : MonoBehaviour
{
    public static bool Finished { get; private set; }

    const float TimeScale = 10f;
    const int W = 1600, H = 1000;

    /// <summary>Mission id -> the mission time at which its defining feature is on
    /// screen. Derived from each mission's event schedule in MissionLibrary.</summary>
    static readonly Dictionary<string, float> ShowcaseAt = new Dictionary<string, float>
    {
        // taxi row — caught on the taxiway with the signage, and at the hold short
        { "L1",  48f },   // taxiing alpha, direction signs in view
        { "M1",  50f },   // traffic crossing the runway ahead
        { "H1",  92f },   // holding short, traffic sitting ON the runway, heavy rain
        // climb row
        { "L2", 110f },   // established in the assigned climb
        { "M2", 135f },   // after the cabin door pops open
        { "H2", 165f },   // low volts, drill running
        // cruise row
        { "L3", 150f },   // straight and level, calm
        { "M3", 160f },   // mid re-clearance sequence
        { "H3", 175f },   // in cloud, instruments disagreeing
        // approach row
        { "L4", 215f },   // short final, runway ahead
        { "M4", 205f },   // approach in weather after the side-step
        { "H4", 150f },   // gliding after the engine failure
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-shots") { {
                    // One driver at a time — see SimDriver. A second driver does not
                    // crash, it quietly changes every number the battery reports.
                    if (!SimDriver.Claim("MissionShots")) return;
                    new GameObject("MissionShots").AddComponent<MissionShots>();
                } return; }
    }

    GameManager gm;
    CessnaPhysics ac;
    AircraftController ctl;
    ScenarioEngine eng;
    Camera shotCam, extCam;
    string dir;
    float lastPitch;

    IEnumerator Start()
    {
        Time.timeScale = TimeScale;
        Time.maximumDeltaTime = 0.5f;

        // wait for the world, the aircraft and the GLB cockpit to exist
        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Debug.Log("[SHOTS] no GameManager"); yield break; }
            yield return null;
        }
        gm = GameManager.Instance;
        ac = gm.Aircraft;
        ctl = ac.GetComponent<AircraftController>();
        eng = gm.ScenarioRunner;
        if (ctl != null) ctl.enabled = false;                     // this file drives the controls
        foreach (var ft in FindObjectsByType<FlightTest>(FindObjectsSortMode.None)) Destroy(ft.gameObject);

        if (!ParticipantManager.IsSet) ParticipantManager.SetID("SHOTS");
        gm.SetParticipantReady();

        dir = Path.Combine(Application.persistentDataPath, "shots");
        Directory.CreateDirectory(dir);
        Debug.Log("[SHOTS] writing to " + dir);

        // give the runtime-loaded cockpit GLB time to finish importing
        yield return new WaitForSecondsRealtime(12f);

        BuildCameras();

        int n = 0;
        // -onlymission:ID renders ONE mission. The bank is now 42, and re-rendering all
        // of them to look at a single display is a very slow way to check one thing.
        string only = null;
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("-onlymission:")) only = a.Substring("-onlymission:".Length);
        foreach (var m in MissionLibrary.All())
        {
            if (!string.IsNullOrEmpty(only) && m.Id != only) continue;
            n++;
            yield return Capture(m, n);
        }

        Time.timeScale = 1f;
        Debug.Log("[SHOTS] done — " + n + " missions, files in " + dir);
        Finished = true;
    }

    void BuildCameras()
    {
        var cockCam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None),
                                        c => c.name == "CockpitCamera");
        // Cockpit capture camera: rides the real cockpit camera, renders everything.
        var g = new GameObject("ShotCam");
        shotCam = g.AddComponent<Camera>();
        shotCam.enabled = false;
        if (cockCam != null)
        {
            // COPY the real cockpit camera's settings, do not invent them. Using
            // cullingMask = ~0 rendered the aircraft's EXTERIOR fuselage and propeller,
            // which the cockpit camera deliberately culls — so the snapshot showed a
            // huge pale cowling filling the lower third of a view the pilot never sees.
            shotCam.CopyFrom(cockCam);
            shotCam.enabled = false;
            g.transform.SetParent(cockCam.transform, false);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
        }
        else
        {
            shotCam.cullingMask = ~0;
            shotCam.nearClipPlane = 0.01f;
            shotCam.fieldOfView = 55f;
        }

        // External chase camera: excludes the cockpit interior layer so the panel does
        // not float in front of the aeroplane.
        var e = new GameObject("ShotCamExt");
        extCam = e.AddComponent<Camera>();
        extCam.cullingMask = ~(1 << CockpitBuilder.CockpitLayer);
        extCam.nearClipPlane = 0.3f;
        extCam.farClipPlane = 12000f;
        extCam.fieldOfView = 42f;
        extCam.enabled = false;
    }

    IEnumerator Capture(MissionDefinition m, int index)
    {
        Debug.Log("[SHOTS] " + m.Id + "  " + m.Name);
        gm.StartSingleMission(m.Id);
        ac.flaps = m.StartFlaps01; ac.spoiler = 0f; ac.braking = false; ac.brakeInput01 = 0f; ac.trim = 0f; ac.yawInput = 0f;
        lastPitch = ac.PitchDeg;

        float want = ShowcaseAt.TryGetValue(m.Id, out float w) ? w : 120f;
        float guard = Time.realtimeSinceStartup + 180f;

        while (gm.State == GameState.Flying && eng.Active && eng.Time01 < want)
        {
            if (ctl != null && ctl.enabled) ctl.enabled = false;
            Fly();
            AckIfNeeded();
            if (Time.realtimeSinceStartup > guard) break;
            yield return null;
        }

        // settle a couple of frames so the displays and any banner are drawn
        for (int i = 0; i < 3; i++) yield return null;

        string stem = index.ToString("00") + "_" + m.Id + "_" + m.ClassTag;
        yield return Shot(shotCam, stem + "_cockpit.png");
        PlaceExternal();
        yield return Shot(extCam, stem + "_external.png");

        gm.BackToMenu();
        yield return new WaitForSecondsRealtime(0.5f);
    }

    void PlaceExternal()
    {
        // Behind, above and slightly left — a three-quarter view that shows the
        // aeroplane's attitude and enough of the scene around it to read the phase.
        Vector3 p = ac.transform.position;
        Vector3 back = -ac.transform.forward, up = Vector3.up, right = ac.transform.right;
        bool onGround = ac.Grounded;
        float dist = onGround ? 34f : 42f;
        float rise = onGround ? 11f : 14f;
        extCam.transform.position = p + back * dist + up * rise - right * 12f;
        extCam.transform.LookAt(p + up * 1.5f, Vector3.up);
    }

    IEnumerator Shot(Camera cam, string file)
    {
        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Destroy(tex); Destroy(rt);
        Debug.Log("[SHOTS]   wrote " + file);
        yield return null;
    }

    // ── a compact pilot, good enough to hold the aeroplane where the picture wants it ──
    void Fly()
    {
        var m = eng.Mission;
        if (m == null) return;
        float dt = Time.deltaTime;

        if (m.Goal == ScenarioGoal.TaxiTakeoff && eng.TaxiPhase < 3) { Taxi(dt); return; }

        bool landing = m.Goal == ScenarioGoal.Land;
        bool engineOut = ac.GetComponent<AircraftSystems>() != null &&
                         ac.GetComponent<AircraftSystems>().Engine == EngineState.Failed;
        Vector3 p = ac.transform.position;
        float wantAlt, wantHdg, wantSpeed;

        if (landing)
        {
            float toGo = Mathf.Max(60f, -p.z - 150f);
            bool cleared = eng.Time01 >= MissionLibrary.BaselineEndT || m.Id == "H4";
            wantAlt = cleared ? Mathf.Min(m.StartAltitudeM, toGo * 0.055f) : m.StartAltitudeM;
            wantHdg = Mathf.Atan2(-p.x, Mathf.Max(60f, -p.z)) * Mathf.Rad2Deg;
            wantSpeed = ac.AltitudeM > 120f ? 145f : 128f;
        }
        else { wantAlt = eng.CurTargetAlt; wantHdg = eng.CurTargetHdg; wantSpeed = 180f; }
        if (engineOut) wantSpeed = 130f;

        float spdErr = ac.AirspeedKmh - wantSpeed;
        float thr = engineOut ? 0f : Mathf.Clamp01(ac.throttle - spdErr * 0.004f * dt * 60f);
        if (!engineOut) thr = Mathf.Clamp01(thr - (ac.AltitudeM - wantAlt) * 0.0004f);
        ac.throttle = Mathf.MoveTowards(ac.throttle, thr, dt * 0.7f);

        float altErr = ac.AltitudeM - wantAlt;
        float targetPitch = Mathf.Clamp(-altErr * 0.035f, -4.5f, 7f);
        if (landing && ac.AltitudeM < 22f) targetPitch = 3f;
        if (ac.AirspeedKmh > wantSpeed + 45f && altErr <= 0f) targetPitch = Mathf.Max(targetPitch, 4f);
        if (ac.AirspeedKmh < 105f || ac.StallWarning) targetPitch = Mathf.Min(targetPitch, -1.5f);

        float rate = (ac.PitchDeg - lastPitch) / Mathf.Max(dt, 1e-4f);
        lastPitch = ac.PitchDeg;
        ac.pitchInput = Mathf.Clamp(-0.045f * (targetPitch - ac.PitchDeg) + 0.012f * rate, -0.6f, 0.6f);

        Vector3 up = ac.transform.up, right = ac.transform.right;
        float trueBank = Mathf.Atan2(-right.y, up.y) * Mathf.Rad2Deg;
        float targetBank = Mathf.Clamp(Mathf.DeltaAngle(ac.HeadingDeg, wantHdg) * 1.0f, -22f, 22f);
        if (Mathf.Abs(trueBank) > 35f || up.y < 0.2f)
        { targetBank = 0f; ac.pitchInput = Mathf.Clamp(ac.pitchInput, -0.05f, 0.6f); }
        ac.rollInput = Mathf.Clamp(0.035f * (targetBank - trueBank), -0.7f, 0.7f);
        ac.yawInput = 0f;
    }

    void Taxi(float dt)
    {
        Vector3 p = ac.transform.position;
        Vector3 target = eng.HasWaypoint ? eng.WaypointPos : Aerodrome.LineUpPos;
        if (eng.TaxiPhase >= 2) target = new Vector3(0f, 0f, 400f);
        Vector3 d = target - p; d.y = 0f;
        float hdgErr = Mathf.DeltaAngle(ac.HeadingDeg, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        ac.yawInput = Mathf.Clamp(hdgErr * 0.05f, -1f, 1f);
        ac.rollInput = 0f; ac.pitchInput = 0f;

        if (eng.TaxiPhase == 1) { ac.throttle = 0f; ac.braking = ac.AirspeedKmh > 1f; ac.brakeInput01 = ac.braking ? 1f : 0f; return; }
        if (eng.TaxiPhase >= 2)
        {
            ac.braking = false; ac.brakeInput01 = 0f; ac.throttle = 1f;
            ac.pitchInput = ac.AirspeedKmh < 100f ? 0f : -0.32f;
            return;
        }
        float distToHold = Mathf.Abs(Aerodrome.HoldShortZ - p.z);
        float wantKmh = (!eng.HoldingShort && distToHold < 60f && p.z < Aerodrome.HoldShortZ) ? 8f : 18f;
        float err = ac.AirspeedKmh - wantKmh;
        ac.throttle = Mathf.Clamp01(ac.throttle - err * 0.004f);
        ac.braking = err > 6f; ac.brakeInput01 = ac.braking ? 0.8f : 0f;
    }

    void AckIfNeeded()
    {
        // Keep prompts and drills moving so the scene reaches its showcase moment.
        if (eng.ResponsePending) { eng.ExternalAck(); return; }
        var cl = eng.Checklist;
        if (cl == null || cl.Complete) return;
        var item = cl.Current;
        var sys = ac.GetComponent<AircraftSystems>();
        if (item == null || sys == null) return;
        if (item.Kind == ChecklistItemKind.Check) { eng.ExternalAck(); return; }
        string L = item.Label;
        if (L.Contains("[H]")) sys.SetCarbHeat(true);
        else if (L.Contains("[J]")) sys.SetSelector(FuelSelector.Left);
        else if (L.Contains("[K]")) sys.SetLoadShed(true);
        else if (L.Contains("[L]")) sys.SetAlternateStatic(true);
        else if (L.Contains("[F]")) ac.flaps = Mathf.MoveTowards(ac.flaps, Mathf.Min(0.5f, ac.flapAuthority01), Time.deltaTime * 0.5f);
    }
}
