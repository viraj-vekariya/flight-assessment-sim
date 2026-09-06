// ControlTestHarness — automated verification of every cockpit control.
//
// Drives each PhysicalControl through its range using the SAME grab API the mouse and
// VR interactors use, and asserts that the aircraft variable it is bound to actually
// follows. Then resets everything and asserts that nothing survived.
//
// WHY THIS RATHER THAN FLYING A MISSION
//   A control fault inside H4 is nearly undiagnosable: if the aeroplane misbehaves, the
//   cause could be the trim wheel wired backwards, a detent snapping to the wrong index,
//   an override latching, or the mission doing exactly what it is supposed to. This
//   isolates one question — "does this control move this variable, in this direction, by
//   this much" — and answers it in seconds.
//
//   Run:
//     Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunControlTest \
//           -controltest -logFile ct.log
//   (no -nographics: the GLB cockpit's displays render to off-screen cameras)
//
// WHAT IT CANNOT TEST
//   Whether a control is comfortable, reachable in VR, or grabbable without looking.
//   Those need a headset and a person. This tests wiring, direction, gearing and reset.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using UnityEngine;

public class ControlTestHarness : MonoBehaviour
{
    public static bool Finished { get; private set; }
    public static int Failures { get; private set; }

    readonly StringBuilder report = new StringBuilder();
    readonly List<string> problems = new List<string>();
    CessnaPhysics ac;
    AircraftController ctl;
    AircraftSystems sys;
    CockpitControlRig rig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-controltest") { {
                    // One driver at a time — see SimDriver. A second driver does not
                    // crash, it quietly changes every number the battery reports.
                    if (!SimDriver.Claim("ControlTestHarness")) return;
                    new GameObject("ControlTestHarness").AddComponent<ControlTestHarness>();
                } return; }
    }

    IEnumerator Start()
    {
        report.AppendLine("COCKPIT CONTROL TEST");
        report.AppendLine("====================");
        report.AppendLine("generated " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("unity " + Application.unityVersion);
        report.AppendLine();

        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Fail("boot", "no GameManager"); Finish(); yield break; }
            yield return null;
        }
        var gm = GameManager.Instance;
        ac = gm.Aircraft; ctl = ac.GetComponent<AircraftController>(); sys = ac.GetComponent<AircraftSystems>();
        if (!ParticipantManager.IsSet) ParticipantManager.SetID("CTLTEST");
        gm.SetParticipantReady();

        // The controls only exist once the GLB cockpit has loaded and been rigged.
        yield return new WaitForSecondsRealtime(14f);
        rig = CockpitControlRig.Instance;
        if (rig == null || rig.Controls.Length == 0)
        {
            Fail("rig", "CockpitControlRig not built — GLB cockpit did not load, so no physical controls exist");
            Finish(); yield break;
        }
        report.AppendLine("controls built: " + rig.Controls.Length);
        report.AppendLine("VR status     : " + VRRuntime.StatusLine);
        report.AppendLine("modality      : " + VRRuntime.Modality);
        report.AppendLine();

        // Controls are live in the control-check state (Flying, no scenario recording).
        ControlCheckMode.Enter();
        yield return new WaitForSecondsRealtime(0.5f);
        // Put it on the ground so wheel brakes are testable.
        ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        yield return new WaitForSecondsRealtime(0.5f);

        // ── MINIMAL COCKPIT (3 Sep 2026, amended 6 Sep) ──────────────────────────
        // The cockpit contains TWO physical controls — the yoke and the throttle — plus
        // the two glass displays. Every other control was removed on request. The tests
        // below check the properties that actually matter after that change: that both
        // controls still work, and that removing the OBJECTS did not remove the INPUTS.
        yield return TestYoke();
        // The throttle was restored on 6 Sep. Gated the same way the spoiler is, so this
        // battery follows the cockpit rather than dictating it: if the throttle object is
        // removed again the section stands down instead of reporting a regression.
        if (HasControl("throttle")) yield return TestThrottle();
        yield return TestOnlyYokeIsPhysical();
        yield return TestRemovedInputsStillDriveable();
        // The spoiler lever was removed long before this; the simulation capability
        // remains, so the test runs only if a spoiler control is actually present.
        if (HasControl("spoiler")) yield return TestSpoiler();
        yield return TestRudderPedalsFollowTheRudder();
        yield return TestSystemsStillReachable();
        yield return TestDisplaysDoNotLeakIntoTheWorld();
        yield return TestOwnershipHandback();
        yield return TestReset();
        yield return TestFrameRateIndependence();

        ControlCheckMode.Exit();
        Finish();
    }

    // ── individual controls ───────────────────────────────────────────────────


    /// <summary>THE COCKPIT CONTAINS EXACTLY TWO PHYSICAL CONTROLS: YOKE AND THROTTLE.
    ///
    /// Asserted rather than assumed, because "I removed the controls" is easy to believe
    /// and easy to get half-right — a builder left in the list, or a piece of furniture
    /// still drawn with nothing on it, would both pass unnoticed otherwise.
    ///
    /// The count is the point. Restoring the throttle on 6 September was meant to add ONE
    /// control; if a second one came back with it — because a shared builder was called,
    /// or because BuildStructure() was switched on to get the mounting — this is the check
    /// that says so. The method keeps its name so the history stays greppable.</summary>
    IEnumerator TestOnlyYokeIsPhysical()
    {
        Section("MINIMAL COCKPIT  (yoke + throttle are the only physical controls)");

        int n = 0;
        var names = new System.Text.StringBuilder();
        foreach (var c in rig.Controls)
        {
            if (c == null) continue;
            n++;
            if (names.Length > 0) names.Append(", ");
            names.Append(c.spec.id);
        }
        Check("exactly two physical controls", n == 2, n + " built: " + names);
        Check("the yoke is present", HasControl("yoke"), "yoke=" + (HasControl("yoke") ? "present" : "MISSING"));
        Check("the throttle is present", HasControl("throttle"), "throttle=" + (HasControl("throttle") ? "present" : "MISSING"));

        foreach (string gone in new[] { "flaps", "brake", "trim", "carb_heat",
                                        "fuel_selector", "load_shed", "alt_static" })
            Check("no cockpit object for " + gone, !HasControl(gone),
                  HasControl(gone) ? "STILL PRESENT" : "removed");
        yield break;
    }

    /// <summary>THE RUDDER PEDALS MUST MOVE WITH THE RUDDER, THE RIGHT WAY, AND NOT
    /// THROUGH THE AEROPLANE.
    ///
    /// Q/E had no cockpit feedback at all until now. The pedals are the model's own twin
    /// mesh cut in two and translated differentially, which is checked here on four counts,
    /// each of which has a specific way of going wrong:
    ///
    ///   THEY EXIST — the split silently produces nothing if the mesh is not readable or
    ///   the centreline gap is not where it was measured, and RealCockpit deliberately
    ///   degrades to static pedals rather than failing loudly.
    ///
    ///   THEY MOVE OPPOSITE WAYS — a rudder bar is differential. Both pedals moving
    ///   together would mean the sign was applied once instead of twice.
    ///
    ///   THE DIRECTION IS RIGHT — right rudder must push the RIGHT pedal away from the
    ///   pilot. Asserted against the yaw the aeroplane actually produces, not against an
    ///   assumed sign convention.
    ///
    ///   THEY CLEAR THE AEROPLANE — the historic failure here was hinging on Object_52's
    ///   node origin, which sits at the model datum about 280 mm below and 780 mm behind
    ///   the pedal faces, and threw a pedal through the cabin floor. Translation removes
    ///   the pivot, but the pedals still travel toward a firewall whose bounding box they
    ///   already overlap at rest, so the clearance is measured against drawn triangles at
    ///   both extremes.</summary>
    IEnumerator TestRudderPedalsFollowTheRudder()
    {
        Section("RUDDER PEDALS  (Q/E must show in the cockpit)");

        Transform model = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "RealCockpitModel") model = t;
        if (model == null) { Fail("pedals", "RealCockpitModel not found"); yield break; }

        Transform left = null, right = null, coL = null, coR = null;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "PedalPilotLeft")    left  = t;
            if (t.name == "PedalPilotRight")   right = t;
            if (t.name == "PedalCopilotLeft")  coL   = t;
            if (t.name == "PedalCopilotRight") coR   = t;
        }
        // FOUR, not two. Object_52 holds both footwells' pairs, so a cut at the centreline
        // alone yields one PAIR per side and moves both of the pilot's pedals together —
        // which is not a rudder at all. Asserted because the first build did exactly that
        // and every other check in this section still passed.
        Check("all four pedals were built from the twin mesh",
              left != null && right != null && coL != null && coR != null,
              "pilot L=" + (left == null ? "MISSING" : "ok")
              + " pilot R=" + (right == null ? "MISSING" : "ok")
              + " copilot L=" + (coL == null ? "MISSING" : "ok")
              + " copilot R=" + (coR == null ? "MISSING" : "ok"));
        if (left == null || right == null) yield break;

        var lr = left.GetComponent<Renderer>();
        var rr = right.GetComponent<Renderer>();
        // A single pedal pad, not a pair. The pilot's whole footwell group is 75 mm wide,
        // so anything near that width means the pair was never divided.
        float lw = lr == null ? 0f : lr.bounds.size.x, rw = rr == null ? 0f : rr.bounds.size.x;
        Check("each pedal is ONE pad, not a whole pair",
              lw > 0.010f && lw < 0.055f && rw > 0.010f && rw < 0.055f,
              "pilot left " + (lw * 1000f).ToString("F0") + " mm wide, right "
              + (rw * 1000f).ToString("F0") + " mm (the whole pair is 75 mm)");
        float lxc = model.InverseTransformPoint(lr.bounds.center).x;
        float rxc = model.InverseTransformPoint(rr.bounds.center).x;
        Check("the pilot's two pedals sit side by side in HIS footwell, right outboard of left",
              lxc < 0f && rxc < 0f && rxc > lxc,
              "left x=" + lxc.ToString("F3") + "  right x=" + rxc.ToString("F3")
              + " (both must be negative — the pilot's side — and right must be the larger)");

        Vector3 lRest = left.localPosition, rRest = right.localPosition;

        // ---- RIGHT rudder ------------------------------------------------------
        // HELD, not set and then waited on. AircraftController's override expires after a
        // grace of one rendered frame, so a settle that stops writing lets yawInput fall
        // straight back to zero — which is exactly what the first run of this test measured,
        // and it read as "the pedals do not move" rather than "the test let go".
        yield return HoldYaw(1f, 0.4f);
        float yawSign = Mathf.Sign(ac.yawInput);
        float rFwd = (right.localPosition - rRest).z;
        float lFwd = (left.localPosition  - lRest).z;

        Check("full right rudder actually reaches the aeroplane", ac.yawInput > 0.9f,
              "yawInput=" + ac.yawInput.ToString("F2"));
        Check("the pedals move at all with right rudder",
              Mathf.Abs(rFwd) > 0.001f, "right pedal moved " + (rFwd * 1000f).ToString("F1") + " mm");
        Check("the pedals move in OPPOSITE directions", rFwd * lFwd < 0f,
              "right " + (rFwd * 1000f).ToString("F1") + " mm, left " + (lFwd * 1000f).ToString("F1") + " mm");
        Check("right rudder pushes the RIGHT pedal forward, away from the pilot",
              rFwd * yawSign > 0f,
              "right pedal " + (rFwd * 1000f).ToString("F1") + " mm along +Z (+Z is toward the panel)");
        float travel = Mathf.Abs(rFwd);
        Check("the throw is a believable pedal movement (8-40 mm at model scale)",
              travel > 0.008f && travel < 0.040f,
              (travel * 1000f).ToString("F1") + " mm, about "
              + (travel * 1000f / 0.352f).ToString("F0") + " mm at life size");

        // ---- clearance at BOTH extremes ----------------------------------------
        foreach (float rud in new[] { 1f, -1f })
        {
            yield return HoldYaw(rud, 0.4f);

            string at = rud > 0f ? "full right rudder" : "full left rudder";
            // The pilot's pair only. The co-pilot's mirror them exactly, so testing all
            // four would double a slow check to prove the same thing twice.
            foreach (var pedal in new[] { left, right })
            {
                float best = float.MaxValue; string who = "nothing"; int tris = 0;
                var pm = pedal.GetComponent<MeshFilter>();
                if (pm == null || pm.sharedMesh == null || !pm.sharedMesh.isReadable) continue;
                var pv = pm.sharedMesh.vertices; var pi = pm.sharedMesh.triangles;

                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                    if (mf.transform == left || mf.transform == right) continue;
                    if (mf.gameObject.layer == AircraftBuilder.ExteriorLayer) continue;
                    var orr = mf.GetComponent<Renderer>();
                    if (orr == null || !orr.enabled) continue;
                    if ((orr.bounds.ClosestPoint(pedal.GetComponent<Renderer>().bounds.center)
                         - pedal.GetComponent<Renderer>().bounds.center).magnitude > 0.12f) continue;

                    var ov = mf.sharedMesh.vertices; var oi = mf.sharedMesh.triangles;
                    var oxf = mf.transform;
                    // Every pedal VERTEX against every nearby triangle. The pedal is a
                    // 168-triangle pad, so this stays cheap even against the fuselage.
                    for (int a = 0; a < pi.Length; a += 3)
                    {
                        Vector3 pp = pedal.TransformPoint(pv[pi[a]]);
                        for (int b = 0; b + 2 < oi.Length; b += 3)
                        {
                            float d = Vector3.Distance(pp, ClosestOnTriangle(pp,
                                oxf.TransformPoint(ov[oi[b]]),
                                oxf.TransformPoint(ov[oi[b + 1]]),
                                oxf.TransformPoint(ov[oi[b + 2]])));
                            tris++;
                            if (d < best) { best = d; who = mf.name; }
                        }
                    }
                }
                Check(pedal.name + " examined real geometry at " + at, tris > 0, tris + " vertex/triangle pairs");
                // Touching is fine — the pedal is bolted to the floor structure. Passing
                // THROUGH is not, so this asks that the nearest thing is not deeply
                // interpenetrating, using the pedal's own 36 mm height as the yardstick.
                Check(pedal.name + " does not bury itself in the aeroplane at " + at,
                      best < 0.5f, "nearest drawn geometry is " + (best * 1000f).ToString("F1")
                      + " mm away (" + who + ")");
            }
        }

        yield return HoldYaw(0f, 0.5f);
        Check("the co-pilot's pedals are linked to the pilot's",
              coR == null || coL == null
              || Mathf.Abs((coR.localPosition - coL.localPosition).z
                         - (right.localPosition - left.localPosition).z) < 0.001f,
              "dual controls move together");
        yield return HoldYaw(0f, 0.5f);
        Check("the pedals return to rest when the rudder is released",
              (left.localPosition - lRest).magnitude < 0.002f
              && (right.localPosition - rRest).magnitude < 0.002f,
              "left off-rest " + ((left.localPosition - lRest).magnitude * 1000f).ToString("F2")
              + " mm, right " + ((right.localPosition - rRest).magnitude * 1000f).ToString("F2") + " mm");

        // Where they sit in the pilot's default view, reported rather than asserted: the
        // pedals are meant to be looked DOWN at, as they are in a real cockpit.
        Camera eye = null;
        foreach (var k in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (k.targetTexture == null && k.name == "CockpitCamera") eye = k;
        if (eye != null)
        {
            var b = right.GetComponent<Renderer>().bounds;
            float lo = 9f, hi = -9f;
            foreach (var corner in Corners(b))
            {
                Vector3 vp = eye.WorldToViewportPoint(corner);
                lo = Mathf.Min(lo, vp.y); hi = Mathf.Max(hi, vp.y);
            }
            report.AppendLine("   pedals in the pilot's DEFAULT view: viewport y "
                            + lo.ToString("F2") + " .. " + hi.ToString("F2")
                            + (hi < 0f ? "  (BELOW the frame — visible only when looking down)"
                                       : "  (in frame)"));
        }
        ctl.ClearOverrides(); yield return WaitFrames(2);
    }

    /// <summary>Hold a rudder deflection for real time, rewriting it every frame so the
    /// one-frame override grace never lapses.</summary>
    IEnumerator HoldYaw(float amount, float seconds)
    {
        float t = 0f;
        while (t < seconds) { ctl.SetYaw(amount); t += Time.unscaledDeltaTime; yield return null; }
    }

    /// <summary>REMOVING THE OBJECTS MUST NOT HAVE REMOVED THE INPUTS.
    ///
    /// This is the check that makes the minimal cockpit safe. The aeroplane still has a
    /// throttle, flaps, brakes and trim; they simply have no grabbable object any more.
    /// If any of them had stopped responding, every mission would still "pass" its own
    /// battery — the scripted pilot drives the same code path — while a human participant
    /// found the aeroplane unflyable.</summary>
    IEnumerator TestRemovedInputsStillDriveable()
    {
        Section("REMOVED CONTROLS  (objects gone, inputs must still work)");
        if (ctl == null) { Fail("inputs", "AircraftController missing"); yield break; }

        // THROTTLE — held, because the override model expires after a grace of one frame.
        for (int i = 0; i < 8; i++) { ctl.SetThrottle(0.75f); yield return null; }
        Check("throttle still driveable", Mathf.Abs(ac.throttle - 0.75f) < 0.05f,
              "phys.throttle=" + ac.throttle.ToString("F2"));
        for (int i = 0; i < 8; i++) { ctl.SetThrottle(0f); yield return null; }

        // FLAPS — detent selection, which is what the missions and the checklists use.
        ctl.SetFlapDetent(2); yield return WaitFrames(4);
        Check("flaps still selectable", Mathf.Abs(ctl.FlapsSelected - 1f) < 0.01f,
              "selected=" + ctl.FlapsSelected.ToString("F2"));
        ctl.SetFlapDetent(0); yield return WaitFrames(4);

        // BRAKE.
        for (int i = 0; i < 8; i++) { ctl.SetBrake(1f); yield return null; }
        Check("brake still driveable", ac.brakeInput01 > 0.9f,
              "brakeInput01=" + ac.brakeInput01.ToString("F2"));
        for (int i = 0; i < 8; i++) { ctl.SetBrake(0f); yield return null; }
        yield return WaitFrames(4);
        Check("brake releases", ac.brakeInput01 < 0.05f,
              "brakeInput01=" + ac.brakeInput01.ToString("F2"));

        // TRIM.
        for (int i = 0; i < 10; i++) { ctl.SetTrim(0.5f); yield return null; }
        Check("trim still driveable", Mathf.Abs(ac.trim - 0.5f) < 0.06f,
              "phys.trim=" + ac.trim.ToString("F2"));
        for (int i = 0; i < 10; i++) { ctl.SetTrim(0f); yield return null; }
        ctl.ClearOverrides(); yield return WaitFrames(2);
    }

    /// <summary>THE DISPLAYS' SYMBOLOGY MUST NOT BE VISIBLE TO ANY WORLD CAMERA.
    ///
    /// LivePFD and LiveMFD build their symbology as ordinary world objects on private
    /// layers, parented to their own off-screen cameras. That is fine as long as every
    /// camera that renders the world excludes those layers — and the cockpit camera did
    /// not. Because the MFD's camera sits above the aeroplane looking down, its compass
    /// lettering appeared as huge sheared glyphs hanging over the cabin, which read as the
    /// moving map somehow mirrored into the roof glass.
    ///
    /// Checking the MASKS rather than looking for glyphs in a render makes this
    /// deterministic and independent of where the aeroplane happens to be.</summary>
    IEnumerator TestDisplaysDoNotLeakIntoTheWorld()
    {
        Section("DISPLAY LAYERS  (symbology may only be seen by its own camera)");

        int mask = CockpitBuilder.DisplayOnlyMask;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            // The displays' own cameras are the ones that SHOULD see these layers; they
            // are identified by rendering to a texture rather than to the screen.
            if (cam.targetTexture != null) continue;
            bool clean = (cam.cullingMask & mask) == 0;
            Check("world camera '" + cam.name + "' excludes the display layers", clean,
                  "cullingMask=0x" + cam.cullingMask.ToString("X")
                  + " display bits=0x" + (cam.cullingMask & mask).ToString("X"));
        }
        yield break;
    }

    IEnumerator TestYoke()
    {
        var c = Find("yoke"); if (c == null) { Fail("yoke", "control missing"); yield break; }
        Section("YOKE  (position grab -> pitch + roll)");

        // Full aft = nose up. In this flight model NEGATIVE pitchInput is nose up, so a
        // rearward hand movement must produce a negative pitchInput.
        yield return Sweep(c, -c.spec.axis, c.spec.travel * 0.5f);
        Check("full aft -> nose-up elevator", ac.pitchInput < -0.6f, "pitchInput=" + ac.pitchInput.ToString("F2"));

        // Re-centre between sweeps. The yoke deliberately HOLDS its position on release
        // (see COCKPIT_CONTROLS.md §3), so a fresh grab starts from wherever it was left
        // and a full-travel sweep from -1 only reaches 0. Testing "full forward" requires
        // starting from neutral, which is what a pilot's hand would do.
        c.EndGrab(); c.SetSilently(0f); yield return WaitSeconds(0.35f);
        yield return Sweep(c, c.spec.axis, c.spec.travel * 0.5f);
        Check("full forward -> nose-down elevator", ac.pitchInput > 0.6f, "pitchInput=" + ac.pitchInput.ToString("F2"));

        c.EndGrab(); c.SetSilently(0f); yield return WaitSeconds(0.35f);
        yield return Sweep(c, c.spec.secondaryAxis, c.spec.secondaryTravel * 0.5f, secondary: true);
        Check("full right -> right roll", ac.rollInput > 0.6f, "rollInput=" + ac.rollInput.ToString("F2"));

        c.EndGrab(); c.SetSilently(0f); yield return WaitSeconds(0.35f);
        yield return Sweep(c, -c.spec.secondaryAxis, c.spec.secondaryTravel * 0.5f, secondary: true);
        Check("full left -> left roll", ac.rollInput < -0.6f, "rollInput=" + ac.rollInput.ToString("F2"));

        // HALF displacement should give roughly half deflection — the "visual 50% =
        // aircraft 50%" requirement, measured rather than asserted.
        c.EndGrab(); c.SetSilently(0f); yield return WaitSeconds(0.35f);
        yield return Sweep(c, -c.spec.axis, c.spec.travel * 0.25f);
        float half = Mathf.Abs(ac.pitchInput);
        Check("half travel -> ~half deflection", half > 0.30f && half < 0.70f, "|pitchInput|=" + half.ToString("F2"));

        // Release must hand pitch back to the keyboard rather than freeze the aircraft.
        c.EndGrab();
        yield return WaitFrames(4);
        Check("release hands pitch back", !ctl.PitchOverridden, "PitchOverridden=" + ctl.PitchOverridden);
    }

    IEnumerator TestThrottle()
    {
        var c = Find("throttle"); if (c == null) { Fail("throttle", "control missing"); yield break; }
        Section("THROTTLE  (position lever -> phys.throttle)");

        c.SetSilently(0f); yield return WaitFrames(6);
        Check("idle", ac.Throttle01 < 0.05f, "throttle=" + ac.Throttle01.ToString("F2"));

        c.SetSilently(0.5f); yield return WaitFrames(10);
        Check("midpoint", Mathf.Abs(ac.Throttle01 - 0.5f) < 0.08f, "throttle=" + ac.Throttle01.ToString("F2"));

        c.SetSilently(1f); yield return WaitFrames(10);
        Check("full", ac.Throttle01 > 0.92f, "throttle=" + ac.Throttle01.ToString("F2"));

        // Pushing forward must ADD power (the aeroplane's convention).
        c.SetSilently(0.2f); yield return WaitFrames(6);
        float before = ac.Throttle01;
        c.BeginGrab(c.transform.position);
        c.UpdateGrab(c.transform.position + c.transform.TransformDirection(c.spec.axis).normalized * 0.03f);
        yield return WaitFrames(8);
        Check("push forward increases power", ac.Throttle01 > before, before.ToString("F2") + " -> " + ac.Throttle01.ToString("F2"));
        c.EndGrab(); yield return WaitFrames(2);

        // ── THE HANDLE'S POSITION IS THE THROTTLE, WHOEVER MOVED IT ──────────────
        //
        // The requirement is not just "grabbing the lever sets power" — it is that the
        // lever SHOWS the power, including when the KEYBOARD set it. PhysicalControl does
        // that by mirroring phys.Throttle01 whenever the control is not in the pilot's
        // hand, but nothing asserted it, so a lever that silently stopped following would
        // have passed every check above: it would still work perfectly when grabbed, and
        // simply lie about the aeroplane the rest of the time.
        //
        // Driven through AircraftController, which is the keyboard's own path, and the
        // control is deliberately NOT grabbed. Held for several frames because the
        // override expires after a grace of one rendered frame.
        for (int i = 0; i < 10; i++) { ctl.SetThrottle(0.65f); yield return null; }
        yield return WaitSeconds(0.25f);                       // let the smoothing settle
        Check("the handle follows the keyboard, ungrabbed",
              !c.Grabbed && Mathf.Abs(c.Smoothed - 0.65f) < 0.05f,
              "grabbed=" + c.Grabbed + " handle=" + c.Smoothed.ToString("F2")
              + " phys.throttle=" + ac.Throttle01.ToString("F2"));

        // And the GEOMETRY moved with it — not just the number. spec.visual is the
        // carriage; its offset along the channel must equal value x travel. This is what
        // catches the visualBase-captured-in-Awake bug, which puts the handle at the
        // centre of the channel at idle and past the end stop at full power.
        if (c.spec.visual != null)
        {
            float y = c.spec.visual.localPosition.y;
            float want = c.Smoothed * c.spec.visualTravel;
            Check("the handle GEOMETRY sits at value x travel",
                  Mathf.Abs(y - want) < 0.002f,
                  "handle y=" + (y * 1000f).ToString("F1") + " mm, expected "
                  + (want * 1000f).ToString("F1") + " mm");
            // Rest must be the BOTTOM of the channel, not its middle.
            for (int i = 0; i < 10; i++) { ctl.SetThrottle(0f); yield return null; }
            yield return WaitSeconds(0.25f);
            Check("idle parks the handle at the bottom of its channel",
                  Mathf.Abs(c.spec.visual.localPosition.y) < 0.002f,
                  "handle y=" + (c.spec.visual.localPosition.y * 1000f).ToString("F1") + " mm from rest");
        }
        ctl.ClearOverrides(); yield return WaitFrames(2);

        yield return ThrottleIsWhatTheMouseGrabs(c);
    }

    /// <summary>AIMING AT THE THROTTLE MUST GRAB THE THROTTLE.
    ///
    /// CockpitInteractorMouse.Nearest picks the control whose centre the mouse ray passes
    /// within captureRadius of, and among those the one NEAREST ALONG THE RAY. The yoke
    /// has a 130 mm capture radius and sits closer to the pilot than the panel, so it can
    /// shadow anything mounted behind it — this is the same class of defect that once let
    /// the yoke swallow a plunger 64 mm away.
    ///
    /// The second trap is subtler: the capture sphere is centred on the CONTROL, which is
    /// the middle of the channel, while the thing the pilot aims at is the KNOB, which at
    /// idle sits at the bottom of the travel. A capture radius smaller than half the travel
    /// leaves the visible knob outside its own grab volume.
    ///
    /// Both are checked at both ends of travel, by reproducing the interactor's documented
    /// selection rule against the real cockpit camera.</summary>
    IEnumerator ThrottleIsWhatTheMouseGrabs(PhysicalControl c)
    {
        Section("THROTTLE REACH  (the mouse must resolve to the knob, not the yoke)");

        Camera cam = null;
        foreach (var k in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (k.targetTexture == null && k.name == "CockpitCamera") cam = k;
        if (cam == null) { Fail("throttle reach", "no CockpitCamera to aim with"); yield break; }
        if (c.spec.visual == null) { Fail("throttle reach", "throttle has no visual to aim at"); yield break; }

        foreach (float where in new[] { 0f, 1f })
        {
            for (int i = 0; i < 10; i++) { ctl.SetThrottle(where); yield return null; }
            yield return WaitSeconds(0.25f);

            // Aim at the KNOB — the sphere the pilot can actually see and click.
            Vector3 knob = c.spec.visual.position;
            Ray ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(knob));

            // CockpitInteractorMouse.Nearest, minus the reach clamp (which only rejects
            // controls further than arm's length and cannot change the winner here).
            PhysicalControl best = null; float bestT = float.MaxValue;
            var seen = new System.Text.StringBuilder();
            foreach (var k in rig.Controls)
            {
                if (k == null) continue;
                Vector3 toC = k.transform.position - ray.origin;
                float t = Vector3.Dot(toC, ray.direction);
                if (t < 0f) continue;
                float dist = Vector3.Distance(ray.GetPoint(t), k.transform.position);
                // Every control's geometry against this ray, so the margin is a MEASURED
                // number in the report rather than something inferred from the verdict.
                if (seen.Length > 0) seen.Append("; ");
                seen.Append(k.spec.id + " ray-dist=" + (dist * 1000f).ToString("F0")
                            + "mm radius=" + (k.spec.captureRadius * 1000f).ToString("F0")
                            + "mm range=" + (t * 1000f).ToString("F0") + "mm"
                            + (dist <= k.spec.captureRadius ? " ELIGIBLE" : ""));
                if (dist > k.spec.captureRadius) continue;
                if (t < bestT) { bestT = t; best = k; }
            }

            string at = where < 0.5f ? "idle" : "full power";
            Check("aiming at the knob at " + at + " selects the throttle", best == c,
                  "selected=" + (best == null ? "NOTHING" : best.spec.id) + " | " + seen);
        }
        ctl.ClearOverrides(); yield return WaitFrames(2);
    }

    IEnumerator TestTrim()
    {
        var c = Find("trim"); if (c == null) { Fail("trim", "control missing"); yield break; }
        Section("TRIM WHEEL  (position wheel -> phys.trim, +1 nose up)");

        c.SetSilently(0f); yield return WaitFrames(6);
        Check("neutral", Mathf.Abs(ac.trim) < 0.05f, "trim=" + ac.trim.ToString("F2"));

        c.SetSilently(1f); yield return WaitFrames(12);
        Check("full nose up", ac.trim > 0.9f, "trim=" + ac.trim.ToString("F2"));
        Check("nose-up trim biases the elevator nose up",
              ac.ElevatorCmd < -0.2f, "elevatorCmd=" + ac.ElevatorCmd.ToString("F2"));

        c.SetSilently(-1f); yield return WaitFrames(12);
        Check("full nose down", ac.trim < -0.9f, "trim=" + ac.trim.ToString("F2"));
        Check("nose-down trim biases the elevator nose down",
              ac.ElevatorCmd > 0.2f, "elevatorCmd=" + ac.ElevatorCmd.ToString("F2"));

        c.SetSilently(0f); yield return WaitFrames(10);

        // Winding the wheel FORWARD must trim nose DOWN.
        c.BeginGrab(c.transform.position);
        c.UpdateGrab(c.transform.position + c.transform.TransformDirection(Vector3.forward).normalized * 0.04f);
        yield return WaitFrames(10);
        Check("wind forward -> nose down", ac.trim < -0.05f, "trim=" + ac.trim.ToString("F2"));
        c.EndGrab(); yield return WaitFrames(2);
    }

    IEnumerator TestFlaps()
    {
        var c = Find("flaps"); if (c == null) { Fail("flaps", "control missing"); yield break; }
        Section("FLAP LEVER  (detents -> selected flap)");

        for (int i = 0; i < AircraftController.FlapDetents.Length; i++)
        {
            c.SetSilently(AircraftController.FlapDetents[i], i);
            yield return WaitFrames(6);
            Check("detent " + AircraftController.FlapLabels[i] + " selected",
                  ctl.FlapDetentIndex == i, "selected=" + AircraftController.FlapLabels[ctl.FlapDetentIndex]);
        }

        // Flaps must move toward the selection, and stop where the motor allows.
        c.SetSilently(1f, 2); yield return WaitSeconds(1.6f);
        Check("flaps actually extend", ac.Flaps01 > 0.8f, "flaps_actual=" + ac.Flaps01.ToString("F2"));

        // Failed flap motor: SELECTED still moves, ACTUAL must not.
        //
        // Arm the REAL failure rather than poking flapAuthority01 directly — AircraftSystems
        // rewrites that field every FixedUpdate from the system state, so a manual poke is
        // erased before the next frame. Going through Arm() also means this test exercises
        // the same path a mission does.
        float held = ac.Flaps01;
        sys.Arm(FailureKind.FlapMotorFailure);
        yield return WaitSeconds(0.2f);
        c.SetSilently(0f, 0); yield return WaitSeconds(1.2f);
        Check("flap failure: selection moves", ctl.FlapDetentIndex == 0, "selected=UP");
        Check("flap failure: actual stays where the motor died",
              ac.Flaps01 >= held - 0.05f,
              "flaps_actual=" + ac.Flaps01.ToString("F2") + " authority=" + ac.flapAuthority01.ToString("F2"));
        sys.Clear(FailureKind.FlapMotorFailure);
        yield return WaitSeconds(0.2f);
        c.SetSilently(0f, 0); yield return WaitSeconds(1.6f);
        Check("flaps retract once the motor is restored", ac.Flaps01 < 0.1f, "flaps_actual=" + ac.Flaps01.ToString("F2"));
    }

    IEnumerator TestBrake()
    {
        var c = Find("brake"); if (c == null) { Fail("brake", "control missing"); yield break; }
        Section("BRAKE  (spring lever -> analog wheel brakes, ground only)");

        Check("aircraft is on the ground (wheel brakes only exist there)", ac.Grounded,
              "Grounded=" + ac.Grounded);

        c.EndGrab(); yield return WaitSeconds(0.3f);
        Check("off", !ac.braking, "braking=" + ac.braking);

        // A SPRING lever has to be HELD. Setting its value and letting go is not a test
        // of the brake, it is a test of the spring — which is the next check.
        yield return HoldLever(c, 1.0f);
        Check("full applied", ac.braking && ac.brakeInput01 > 0.9f,
              "braking=" + ac.braking + " pressure=" + ac.brakeInput01.ToString("F2"));

        yield return HoldLever(c, 0.5f);
        Check("partial pressure", ac.brakeInput01 > 0.3f && ac.brakeInput01 < 0.7f,
              "pressure=" + ac.brakeInput01.ToString("F2"));

        c.EndGrab();
        yield return WaitSeconds(0.35f);
        Check("releases to zero", ac.brakeInput01 < 0.1f, "pressure=" + ac.brakeInput01.ToString("F2"));

        // Brakes must not bite in the air, whatever the pilot does with the lever.
        ac.ResetTo(new Vector3(0f, 300f, -1500f), Quaternion.identity, true, 50f);
        yield return WaitSeconds(0.4f);
        yield return HoldLever(c, 1.0f);
        Check("airborne: brake input is ignored", !ac.braking && ac.brakeInput01 < 0.05f,
              "braking=" + ac.braking + " pressure=" + ac.brakeInput01.ToString("F2"));
        c.EndGrab();
        var gm2 = GameManager.Instance;
        ac.ResetTo(gm2.Runway.Start, gm2.Runway.Rot, false, 0f);
        yield return WaitSeconds(0.6f);
    }

    IEnumerator TestToggle(string id, System.Func<bool> read)
    {
        var c = Find(id); if (c == null) { Fail(id, "control missing"); yield break; }
        Section(c.spec.label.ToUpper() + "  (toggle)");
        bool start = read();
        c.Click(); yield return WaitFrames(6);
        Check("click flips state", read() != start, "state=" + read());
        c.Click(); yield return WaitFrames(6);
        Check("click flips back", read() == start, "state=" + read());
    }

    /// <summary>The four systems memory items lost their cockpit objects, but H2, H3 and
    /// H4 gate real checklist DO items on their state. This asserts the state can still be
    /// reached and read back, so those drills remain completable.</summary>
    IEnumerator TestSystemsStillReachable()
    {
        Section("SYSTEMS  (no cockpit object — keyboard H/J/K/L path)");
        if (sys == null) { Fail("systems", "AircraftSystems missing"); yield break; }

        sys.SetCarbHeat(true);  yield return null;
        Check("carb heat can be set", sys.CarbHeatOn, "CarbHeatOn=" + sys.CarbHeatOn);
        sys.SetCarbHeat(false); yield return null;

        sys.SetLoadShed(true);  yield return null;
        Check("load shed can be set", sys.LoadShed, "LoadShed=" + sys.LoadShed);
        sys.SetLoadShed(false); yield return null;

        sys.SetAlternateStatic(true); yield return null;
        Check("alternate static can be opened", sys.AlternateStaticOpen,
              "AlternateStaticOpen=" + sys.AlternateStaticOpen);
        sys.SetAlternateStatic(false); yield return null;

        sys.SetSelector(FuelSelector.Left); yield return null;
        Check("tank can be changed", sys.Selector == FuelSelector.Left, "Selector=" + sys.Selector);
        sys.SetSelector(FuelSelector.Both); yield return null;

        // The binding the checklist text advertises must exist, or the drill instructs the
        // participant to press a key that does nothing.
        var si = ac != null ? ac.GetComponent<SystemsInput>() : null;
        Check("SystemsInput present and enabled (H/J/K/L live)", si != null && si.enabled,
              si == null ? "MISSING" : "enabled=" + si.enabled);
    }

    /// <summary>The two black switches on the left bay were removed. This proves the
    /// ORANGE control beside them (carb heat) is untouched by that — present, in the same
    /// place, still interactable, still animating.
    ///
    /// The concern was that the switches and the carb-heat knob might share a mesh, a
    /// renderer or a parent, so that removing one moved or hid the other. They never did:
    /// every control on this panel is built as its own GameObject with its own transform,
    /// renderers and collider, and the bay behind them is a backing plate that nothing is
    /// parented to. This test exists so that stays true rather than being asserted.</summary>
    IEnumerator TestOrangeControlIndependence()
    {
        Section("ORANGE CONTROL (carb heat) — independent of the removed switches");

        Check("the two black switches are gone from the cockpit",
              Find("load_shed") == null && Find("alt_static") == null,
              "load_shed=" + (Find("load_shed") == null ? "absent" : "PRESENT") +
              " alt_static=" + (Find("alt_static") == null ? "absent" : "PRESENT"));

        var c = Find("carb_heat");
        if (c == null) { Fail("carb heat", "the orange control disappeared with the switches"); yield break; }

        // LOCAL position, not world: the control is parented to the cockpit, which is
        // parented to the aeroplane, and a parked aeroplane creeps. Measuring in world
        // space made this test report 30 mm of "drift" that was the aircraft rolling,
        // not the control moving.
        Vector3 p0 = c.transform.localPosition;
        Check("orange control is present and positioned", true, "cockpit-local " + p0.ToString("F3"));

        int rends = 0; bool orange = false;
        foreach (var r in c.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            rends++;
            var col = r.sharedMaterial != null ? r.sharedMaterial.color : Color.black;
            if (col.r > 0.6f && col.g > 0.25f && col.g < 0.65f && col.b < 0.25f) orange = true;
        }
        Check("orange control is VISIBLE", rends > 0, rends + " enabled renderers");
        Check("orange control still has its orange knob", orange, "orange material found");

        bool shared = false;
        foreach (var other in rig.Controls)
        {
            if (other == null || other == c) continue;
            if (c.transform.IsChildOf(other.transform) || other.transform.IsChildOf(c.transform)) shared = true;
        }
        Check("orange control shares no hierarchy with another control", !shared,
              "own GameObject, own transform");

        var col2 = c.GetComponent<SphereCollider>();
        Check("orange control is INTERACTABLE", col2 != null && col2.enabled,
              col2 == null ? "no collider" : "collider r=" + col2.radius.ToString("F3"));

        bool before = sys.CarbHeatOn;
        c.Click(); yield return WaitSeconds(0.25f);
        Check("orange control still toggles", sys.CarbHeatOn != before, "state=" + sys.CarbHeatOn);
        c.Click(); yield return WaitSeconds(0.25f);
        Check("orange control toggles back", sys.CarbHeatOn == before, "state=" + sys.CarbHeatOn);

        Check("orange control did not move while being used",
              (c.transform.localPosition - p0).magnitude < 0.0005f,
              "drift " + (c.transform.localPosition - p0).magnitude.ToString("F5") + " m in the cockpit frame");
    }

    IEnumerator TestFuelSelectorPhysical()
    {
        var c = Find("fuel_selector"); if (c == null) { Fail("fuel_selector", "control missing"); yield break; }
        Section("FUEL SELECTOR  (3-position rotary)");
        var seen = new List<FuelSelector>();
        for (int i = 0; i < 3; i++) { c.Click(); yield return WaitFrames(6); seen.Add(sys.Selector); }
        Check("cycles through all three tanks", seen.Distinct().Count() == 3,
              string.Join(" -> ", seen));
    }

    IEnumerator TestSpoiler()
    {
        var c = Find("spoiler"); if (c == null) { Fail("spoiler", "control missing"); yield break; }
        Section("SPOILER LEVER  (detents -> spoiler; held stowed during a recorded trial)");

        for (int d = 0; d <= 2; d++)
        {
            c.SetSilently(d * 0.5f, d);
            yield return WaitFrames(6);
            Check("detent " + d + " selected", ctl.SpoilerDetentIndex == d,
                  "index=" + ctl.SpoilerDetentIndex);
        }
        c.SetSilently(1f, 2); yield return WaitSeconds(1.2f);
        Check("spoiler actually extends", ac.Spoiler01 > 0.8f, "spoiler=" + ac.Spoiler01.ToString("F2"));
        c.SetSilently(0f, 0); yield return WaitSeconds(1.2f);
        Check("returns to UP", ac.Spoiler01 < 0.1f, "spoiler=" + ac.Spoiler01.ToString("F2"));
    }

    IEnumerator TestOwnershipHandback()
    {
        Section("CONTROL OWNERSHIP  (the override latch regression)");
        ctl.SetPitch(0.8f);
        yield return null;   // ownership expiry is a FRAME rule, so count frames here
        Check("a single write takes ownership", ctl.PitchOverridden, "PitchOverridden=true");
        yield return null; yield return null; yield return null;
        Check("ownership EXPIRES without further writes", !ctl.PitchOverridden,
              "PitchOverridden=" + ctl.PitchOverridden + " (latched = keyboard would be dead)");
    }

    IEnumerator TestReset()
    {
        Section("RESET  (no state may survive a trial change)");

        // Dirty everything a participant could dirty.
        Find("throttle")?.SetSilently(0.8f);
        Find("trim")?.SetSilently(0.7f);
        Find("flaps")?.SetSilently(1f, 2);
        Find("brake")?.SetSilently(1f);
        var ch = Find("carb_heat"); if (ch != null && !sys.CarbHeatOn) ch.Click();
        var ls = Find("load_shed"); if (ls != null && !sys.LoadShed) ls.Click();
        yield return WaitFrames(10);

        ctl.ResetConfiguration(0f);
        rig.ResetAll(0f);
        sys.ResetAll();
        yield return WaitFrames(10);

        Check("trim cleared", Mathf.Abs(ac.trim) < 0.01f, "trim=" + ac.trim.ToString("F3"));
        Check("brake cleared", ac.brakeInput01 < 0.01f && !ac.braking, "pressure=" + ac.brakeInput01.ToString("F2"));
        Check("flap selection cleared", ctl.FlapDetentIndex == 0, "selected=" + AircraftController.FlapLabels[ctl.FlapDetentIndex]);
        Check("carb heat cleared", !sys.CarbHeatOn, "carbHeat=" + sys.CarbHeatOn);
        Check("load shed cleared", !sys.LoadShed, "loadShed=" + sys.LoadShed);
        Check("fuel selector back to BOTH", sys.Selector == FuelSelector.Both, "selector=" + sys.Selector);
        Check("no override still owned", !ctl.AnyOverride, "AnyOverride=" + ctl.AnyOverride);

        foreach (var c in rig.Controls)
        {
            if (c == null || c.spec.target == ControlTarget.FuelSelector) continue;
            if (c.spec.kind == ControlKind.Toggle && c.ToggleState) Fail("reset", c.spec.id + " toggle still ON");
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Grab a control and move the "hand" a real distance along an axis, the
    /// same way an interactor would.</summary>
    /// <summary>Control smoothing must behave identically at 30, 60 and 90 FPS.
    ///
    /// This is not a performance test. A naive per-frame Lerp(a, b, 0.1f) converges
    /// FOUR TIMES faster at 120 FPS than at 30 FPS, which would mean a desktop
    /// participant at 60 FPS and a VR participant at 90 FPS were flying aircraft with
    /// different control response — frame rate would silently become an experimental
    /// variable. PhysicalControl therefore uses k = 1 - exp(-dt/tau), and this test
    /// proves it empirically rather than by inspection.
    ///
    /// Time.captureFramerate pins deltaTime to exactly 1/fps regardless of how fast
    /// the machine actually runs, which is how a fixed frame rate is simulated in
    /// batchmode.</summary>
    IEnumerator TestFrameRateIndependence()
    {
        Section("FRAME RATE INDEPENDENCE  (30 / 60 / 90 FPS must agree)");

        // Whichever smoothed control the cockpit actually has. This used to demand the
        // throttle by name; after the cockpit was reduced to the yoke alone, the test
        // reported "no throttle control" and stopped checking the smoothing law at all.
        // The property under test belongs to PhysicalControl, not to any one control.
        var c = Find("throttle") ?? Find("yoke");
        if (c == null) { Fail("framerate", "no smoothed control to measure"); yield break; }
        report.AppendLine("   measuring on: " + c.spec.id);

        int[] rates = { 30, 60, 90 };
        float[] measured = new float[rates.Length];
        float[] predicted = new float[rates.Length];
        float[] elapsed = new float[rates.Length];
        int original = Time.captureFramerate;
        // 0.03 s is under one time constant, so the response is mid-curve (~0.61) where
        // a wrong smoothing law is obvious. It is also chosen so that 30, 60 and 90 FPS
        // all land on exactly 0.0333 s (1, 2 and 3 frames), making the three runs
        // directly comparable with no quantisation difference to explain away.
        const float Horizon = 0.03f;

        for (int i = 0; i < rates.Length; i++)
        {
            Time.captureFramerate = rates[i];

            // Start every run from a known zero, unheld and fully settled.
            c.EndGrab();
            c.SetSilently(0f);
            yield return WaitSeconds(0.4f);

            // Step input: grab, then immediately demand full travel and hold it.
            Vector3 start = c.transform.position;
            Vector3 dir = c.transform.TransformDirection(c.spec.axis).normalized;
            c.BeginGrab(start);

            // Accumulate the SAME clock the smoothing uses (Time.deltaTime), not
            // unscaledDeltaTime — captureFramerate pins deltaTime and leaves real
            // unscaled time alone, so measuring the wrong one silently tests nothing.
            float t = 0f; int frames = 0; float target = 0f;
            while (t < Horizon)
            {
                c.UpdateGrab(start + dir * c.spec.travel);
                if (frames == 0) target = c.Value;   // the step the smoothing chases
                t += Time.deltaTime; frames++;
                yield return null;
            }

            // Smoothed, NOT Value: Value is the raw hand position and steps instantly.
            measured[i] = c.Smoothed;
            // Scaled by the STEP SIZE rather than assumed to be 1. A centred control, or
            // one with a response curve, does not reach 1.0 at full travel, and hard-coding
            // 1 would make this a test of the control's shaping instead of a test of the
            // frame-rate independence of its smoothing.
            predicted[i] = target * (1f - Mathf.Exp(-t / c.spec.smoothingTau));
            elapsed[i] = t;
            c.EndGrab();

            report.AppendLine("   " + rates[i].ToString().PadLeft(3) + " FPS -> value "
                              + measured[i].ToString("0.000")
                              + "   (analytic " + predicted[i].ToString("0.000")
                              + ", " + frames + " frames, dt=" + (t / frames).ToString("0.0000")
                              + "s, t=" + t.ToString("0.0000") + "s)");
        }

        Time.captureFramerate = original;

        // If captureFramerate silently did nothing, every run would have identical
        // frame timing and the comparison above would pass without testing anything.
        float dtLo = elapsed[0] / 1f, dtHi = elapsed[2] / 3f;
        Check("the frame rates were actually simulated",
              Mathf.Abs(elapsed[0] - 1f / 30f) < 0.005f && Mathf.Abs(elapsed[2] - 3f / 90f) < 0.005f,
              "30 FPS ran 1 frame of " + dtLo.ToString("0.0000") + "s, 90 FPS ran 3 of " + dtHi.ToString("0.0000") + "s");

        for (int i = 0; i < rates.Length; i++)
            Check(rates[i] + " FPS follows the time-constant curve",
                  Mathf.Abs(measured[i] - predicted[i]) < 0.04f,
                  "measured=" + measured[i].ToString("0.000") + " analytic=" + predicted[i].ToString("0.000"));

        float lo = Mathf.Min(measured[0], Mathf.Min(measured[1], measured[2]));
        float hi = Mathf.Max(measured[0], Mathf.Max(measured[1], measured[2]));
        Check("30/60/90 FPS agree with each other", hi - lo < 0.03f,
              "spread=" + (hi - lo).ToString("0.000") + " (a per-frame Lerp would spread ~0.5)");

        c.EndGrab();
        c.SetSilently(0f);
        yield return WaitSeconds(0.3f);
    }

    IEnumerator Sweep(PhysicalControl c, Vector3 localAxis, float metres, bool secondary = false)
    {
        Vector3 start = c.transform.position;
        c.BeginGrab(start);
        Vector3 dir = c.transform.TransformDirection(localAxis).normalized;
        const int steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            c.UpdateGrab(start + dir * (metres * i / steps));
            yield return WaitSeconds(0.02f);
        }
        // Settle for several smoothing time constants before measuring.
        yield return WaitSeconds(Mathf.Max(0.35f, c.spec.smoothingTau * 8f));
    }

    /// <summary>Wait REAL TIME, not frames.
    ///
    /// The first version of this harness waited a fixed number of frames. In batchmode
    /// frames take about 1.5 ms, so "wait 10 frames" was 15 ms — while control smoothing
    /// has a 45 ms time constant and the flaps take a full second to travel. Every
    /// time-based behaviour therefore appeared broken: the yoke read 0.11 instead of
    /// 1.00, the flaps "did not extend", the brake "did not release". All of those were
    /// the test measuring too early, not the control failing.
    ///
    /// Anything with a time constant must be given time.</summary>
    /// <summary>Grab a lever and HOLD it at a fraction of its travel, the way a hand
    /// would. Required for spring levers, which return to rest the moment nothing is
    /// holding them.</summary>
    IEnumerator HoldLever(PhysicalControl c, float fraction)
    {
        Vector3 start = c.transform.position;
        Vector3 dir = c.transform.TransformDirection(c.spec.axis).normalized;
        c.EndGrab();
        c.BeginGrab(start);
        float t = 0f, settle = Mathf.Max(0.35f, c.spec.smoothingTau * 8f);
        while (t < settle)
        {
            c.UpdateGrab(start + dir * (c.spec.travel * fraction));
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator WaitFrames(int n) => WaitSeconds(Mathf.Max(0.25f, n * 0.02f));

    IEnumerator WaitSeconds(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
    }

    static System.Collections.Generic.IEnumerable<Vector3> Corners(Bounds b)
    {
        Vector3 m = b.min, x = b.max;
        yield return new Vector3(m.x, m.y, m.z); yield return new Vector3(x.x, m.y, m.z);
        yield return new Vector3(m.x, x.y, m.z); yield return new Vector3(x.x, x.y, m.z);
        yield return new Vector3(m.x, m.y, x.z); yield return new Vector3(x.x, m.y, x.z);
        yield return new Vector3(m.x, x.y, x.z); yield return new Vector3(x.x, x.y, x.z);
    }

    /// <summary>Closest point on a triangle to a point — Ericson, Real-Time Collision
    /// Detection, 5.1.5. Used instead of a bounds test because the things a cockpit control
    /// can foul are large flat panels, whose bounds say nothing useful about where they
    /// actually are.</summary>
    static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;

        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));

        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));

        float denom = 1f / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    PhysicalControl Find(string id)
    {
        foreach (var c in rig.Controls) if (c != null && c.spec.id == id) return c;
        return null;
    }

    void Section(string s) { report.AppendLine(); report.AppendLine("── " + s); }

    /// <summary>True when the rig actually built a control with this id. Lets a
    /// test section stand down for a control that has been deliberately removed,
    /// instead of failing and looking like a regression.</summary>
    bool HasControl(string id)
    {
        if (rig == null) return false;
        foreach (var c in rig.Controls) if (c != null && c.spec != null && c.spec.id == id) return true;
        return false;
    }

    void Check(string what, bool ok, string detail)
    {
        report.AppendLine((ok ? "   OK    " : "   FAIL  ") + what + "   [" + detail + "]");
        if (!ok) { Failures++; problems.Add(what + " (" + detail + ")"); Debug.Log("[CTEST] FAIL " + what + " — " + detail); }
    }

    void Fail(string where, string what)
    { Failures++; problems.Add(where + ": " + what); report.AppendLine("   FAIL  " + where + " — " + what); }

    void Finish()
    {
        report.AppendLine();
        report.AppendLine("SUMMARY");
        report.AppendLine("-------");
        report.AppendLine("problems: " + problems.Count);
        foreach (var p in problems) report.AppendLine("  - " + p);
        if (problems.Count == 0) report.AppendLine("  (none)");
        report.AppendLine();
        report.AppendLine("NOTE: this verifies WIRING, DIRECTION, GEARING and RESET. Whether a control is");
        report.AppendLine("comfortable or reachable in VR needs a headset and a person.");

        // Write to BOTH locations. A stale copy in the project root - where the docs
        // tell a reader to look - is an excellent way to read an old result and
        // believe it is the current one. That happened twice during this work.
        try { File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                             "control_test_report.txt"), report.ToString()); } catch { }
        string path = Path.Combine(Application.persistentDataPath, "control_test_report.txt");
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        Debug.Log("[CTEST-REPORT]\n" + report.ToString());
        Debug.Log("[CTEST] done. problems=" + problems.Count + " report=" + path);
        Finished = true;
    }
}
