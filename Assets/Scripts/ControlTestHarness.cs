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

        // And the GEOMETRY moved with it — not just the number.
        //
        // spec.visual is the ARM, which animates from IDENTITY; the rest rake lives on its
        // parent pivot. Both halves are checked, because the whole point of splitting them
        // is that neither may leak into the other:
        //
        //   arm.localRotation   must be exactly value x sweep about the animated axis
        //   pivot.localRotation must be the rest rake, and must NEVER change
        //
        // An arm that carried its own rake would be driven from 0 to +44 deg instead of
        // -22 to +22 — hard on one stop at idle, 22 deg past the other at full power. A
        // rake added to the animated value instead of held on the pivot is the +-22 -> -66
        // deg bug that laid an earlier build's handles flat across their own placards.
        if (c.spec.visual != null)
        {
            var arm = c.spec.visual;
            var pivot = arm.parent;
            Check("the animated visual hangs off a pivot", pivot != null && pivot.name == "LeverPivot",
                  "parent=" + (pivot == null ? "NONE" : pivot.name));

            float armDeg = Quaternion.Angle(Quaternion.identity, arm.localRotation);
            float wantDeg = Mathf.Abs(c.Smoothed * c.spec.visualTravel);
            Check("the arm is rotated by value x sweep", Mathf.Abs(armDeg - wantDeg) < 1.0f,
                  "arm=" + armDeg.ToString("F1") + " deg, expected " + wantDeg.ToString("F1") + " deg");

            if (pivot != null)
            {
                // The invariant is that the rake EXISTS and lives HERE — not that it happens
                // to equal half the sweep. Asserting a particular angle would freeze a design
                // choice into the test: the rake is set from what the pilot can see, and it
                // was moved from -22 deg to +20 deg for exactly that reason. What must never
                // be true is a rake of zero, which would mean it had been folded back into
                // the animated value.
                float rake = Quaternion.Angle(Quaternion.identity, pivot.localRotation);
                Check("the pivot carries a real rest rake, not the arm", rake > 1f,
                      "pivot rake=" + rake.ToString("F1") + " deg");
                pivotRakeAtPower = pivot.localRotation;
            }

            // At idle the arm must be back at IDENTITY — the rest pose, with the rake still
            // supplying the whole of the lever's angle.
            for (int i = 0; i < 10; i++) { ctl.SetThrottle(0f); yield return null; }
            yield return WaitSeconds(0.25f);
            Check("idle returns the arm to identity",
                  Quaternion.Angle(Quaternion.identity, arm.localRotation) < 1.0f,
                  "arm=" + Quaternion.Angle(Quaternion.identity, arm.localRotation).ToString("F1") + " deg from rest");
            if (pivot != null)
                Check("the pivot rake is the same at idle as at power",
                      Quaternion.Angle(pivotRakeAtPower, pivot.localRotation) < 0.01f,
                      "moved " + Quaternion.Angle(pivotRakeAtPower, pivot.localRotation).ToString("F3") + " deg");
        }
        ctl.ClearOverrides(); yield return WaitFrames(2);

        yield return ThrottleIsWhatTheMouseGrabs(c);
        yield return ThrottleSweepIsClear(c);
        yield return ThrottleTracksTheHand(c);
    }

    /// <summary>THE KNOB MUST GO WHERE THE HAND PUTS IT.
    ///
    /// PhysicalControl is position-based on purpose: hand displacement maps to control
    /// position, so a participant's control input does not depend on how long they held
    /// their hand somewhere. That promise is only kept if spec.travel is the distance the
    /// HAND moves, in world metres, along spec.axis.
    ///
    /// On an arc it is easy to gear to the wrong length. The knob's straight-line movement
    /// between the stops is the chord, 48.7 mm, but only its vertical component, 28.6 mm,
    /// lies along the axis the hand is measured on — and the model is not at unit scale, so
    /// neither number is a world distance. Gear to the chord and the ball trails the hand
    /// holding it by nearly half; nothing else in this battery would notice.</summary>
    IEnumerator ThrottleTracksTheHand(PhysicalControl c)
    {
        Section("THROTTLE GEARING  (the ball must keep up with the hand dragging it)");

        var grip = GripOf(c);
        if (grip == null) { Fail("throttle gearing", "no grip to follow"); yield break; }

        for (int i = 0; i < 10; i++) { ctl.SetThrottle(0f); yield return null; }
        yield return WaitSeconds(0.3f);
        ctl.ClearOverrides();

        Vector3 axis = c.transform.TransformDirection(c.spec.axis).normalized;
        // MEASURE IN THE CONTROL'S OWN FRAME. The aeroplane is on the ground and about to be
        // given most of its power, so it rolls, and a world-space before/after would be
        // measuring the take-off roll as well as the lever. The control's frame moves with
        // the aeroplane, so the difference is the lever and nothing else.
        Vector3 fromLocal = c.transform.InverseTransformPoint(grip.bounds.center);
        // Local metres are not world metres — spec.travel is a world distance, so the two
        // have to be compared in the same units. This is the factor between them.
        float localToWorld = c.transform.TransformVector(c.spec.axis.normalized).magnitude;

        // HOLD THE HAND STILL IN THE WORLD, relative to the point it took hold at.
        //
        // UpdateGrab measures the hand as a world delta from wherever the grab started, so
        // the target has to be offset from THAT point, once. Recomputing it from
        // c.transform.position every frame quietly subtracts the aeroplane's own movement
        // from the hand: this test opens the throttle to full on the ground, the aeroplane
        // starts its take-off roll, and the lever stalled at 0.85 with the drift looking
        // exactly like a gearing error. The brake is held for the same reason.
        Vector3 origin = c.transform.position;
        c.EndGrab();
        c.BeginGrab(origin);
        // Settle on Time.deltaTime, the clock the smoothing itself uses. Accumulating
        // unscaledDeltaTime here left the lever short after what looked like ten time
        // constants — the same trap TestFrameRateIndependence documents.
        float held = 0f, settle = Mathf.Max(0.4f, c.spec.smoothingTau * 12f);
        while (held < settle)
        {
            c.UpdateGrab(origin + axis * c.spec.travel);
            ctl.SetBrake(1f);
            held += Time.deltaTime;
            yield return null;
        }
        Vector3 toLocal = c.transform.InverseTransformPoint(grip.bounds.center);
        float moved = Vector3.Dot(toLocal - fromLocal, c.spec.axis.normalized) * localToWorld;
        float asked = c.spec.travel;
        c.EndGrab();

        report.AppendLine("   model lossyScale=" + c.transform.lossyScale.ToString("F3")
                          + "  local->world along the axis=" + localToWorld.ToString("F3")
                          + "  ball radius=" + (Mathf.Max(grip.bounds.extents.x,
                              Mathf.Max(grip.bounds.extents.y, grip.bounds.extents.z)) * 1000f).ToString("F1") + " mm");
        Check("one full travel of the hand reaches full power", c.Smoothed > 0.95f,
              "value=" + c.Smoothed.ToString("F3"));
        Check("the ball moved as far as the hand did",
              Mathf.Abs(moved - asked) < 0.006f,
              "hand moved " + (asked * 1000f).ToString("F1") + " mm, ball moved "
              + (moved * 1000f).ToString("F1") + " mm along the same axis");

        for (int i = 0; i < 10; i++) { ctl.SetThrottle(0f); ctl.SetBrake(1f); yield return null; }
        for (int i = 0; i < 8; i++) { ctl.SetBrake(0f); yield return null; }
        ctl.ClearOverrides(); yield return WaitFrames(2);
    }

    /// <summary>THE KNOB MUST SWEEP THROUGH FRESH AIR, AND THE GROUP MUST STAY OFF THE MFD.
    ///
    /// A lever on an arc can foul things a lever in a slot cannot: the knob leaves the
    /// casing's plane, so at the stops it can be driven into the quadrant cheeks, into the
    /// panel behind it, or up through the display above it. None of that shows up in any
    /// wiring test — the control would read, drive and animate perfectly while visibly
    /// passing through solid aeroplane.
    ///
    /// Measured against DRAWN TRIANGLES, not renderer bounds. A bounds test against a
    /// cockpit panel is a test against a box the size of the cabin, and would either fail
    /// constantly or, if loosened, pass constantly. It is also why the triangle count is
    /// reported: a geometry test that silently examined nothing would pass, and this
    /// project has already been burnt once by a probe that confidently measured the wrong
    /// thing.</summary>
    IEnumerator ThrottleSweepIsClear(PhysicalControl c)
    {
        Section("THROTTLE SWEEP  (the knob must clear the casing, the panel and the MFD)");

        var arm = c.spec.visual;
        if (arm == null) { Fail("throttle sweep", "the throttle has no animated visual"); yield break; }
        var pivot = arm.parent;

        // The KNOB is the arm's furthest child renderer from the pivot — found by geometry
        // rather than by name, so renaming a primitive cannot quietly disarm this.
        Renderer knob = null; float far = -1f;
        foreach (var r in arm.GetComponentsInChildren<Renderer>(true))
        {
            float d = Vector3.Distance(r.bounds.center, pivot != null ? pivot.position : arm.position);
            if (d > far) { far = d; knob = r; }
        }
        if (knob == null) { Fail("throttle sweep", "the arm has no renderers"); yield break; }
        float knobR = Mathf.Max(knob.bounds.extents.x, Mathf.Max(knob.bounds.extents.y, knob.bounds.extents.z));

        // Everything that is NOT the moving arm is an obstacle — including this control's
        // own casing, which is exactly the thing a lever is most likely to swing through.
        var obstacles = new System.Collections.Generic.List<MeshFilter>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.transform.IsChildOf(arm)) continue;
            // The EXTERIOR is not an obstacle. AircraftBuilder's fuselage is a placeholder
            // capsule on layer 10 that the cockpit camera culls and that the whole cabin
            // sits INSIDE — so "distance to it" is measured from within its own skin and
            // says nothing about whether the knob clips anything the pilot can see. Left in,
            // it reported the fuselage shell as the nearest geometry at full power and
            // buried the number that matters, which is the clearance to the casing.
            if (mf.gameObject.layer == AircraftBuilder.ExteriorLayer) continue;
            var rr = mf.GetComponent<Renderer>();
            if (rr == null || !rr.enabled) continue;
            obstacles.Add(mf);
        }

        Camera eyeCam = null;
        foreach (var k in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (k.targetTexture == null && k.name == "CockpitCamera") eyeCam = k;
        Check("the pilot's own camera was found, so visibility is judged from the seat",
              eyeCam != null, eyeCam == null ? "CockpitCamera MISSING" : "ok");

        Transform model = c.transform;
        while (model != null && model.name != "RealCockpitModel") model = model.parent;
        Check("the cockpit model was found, so the MFD bound can be checked in its frame",
              model != null, model == null ? "RealCockpitModel MISSING" : "ok");

        const float MfdLowerEdge = 0.4856f;    // model-local y, measured
        float worstTop = float.MinValue; string worstTopAt = "";

        foreach (float where in new[] { 0f, 0.5f, 1f })
        {
            for (int i = 0; i < 12; i++) { ctl.SetThrottle(where); yield return null; }
            yield return WaitSeconds(0.3f);

            string at = where < 0.25f ? "idle" : where > 0.75f ? "full power" : "mid-travel";
            Vector3 kc = knob.bounds.center;
            float best = float.MaxValue; string bestName = "nothing"; int tris = 0, meshes = 0, unreadable = 0;

            foreach (var mf in obstacles)
            {
                var rr = mf.GetComponent<Renderer>();
                // Prune on bounds first: only geometry that could possibly be within reach
                // of the knob is worth walking triangle by triangle.
                if ((rr.bounds.ClosestPoint(kc) - kc).magnitude > knobR + 0.05f) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.isReadable) { unreadable++; continue; }
                meshes++;
                var v = mesh.vertices; var idx = mesh.triangles;
                var xf = mf.transform;
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    Vector3 a = xf.TransformPoint(v[idx[i]]);
                    Vector3 b = xf.TransformPoint(v[idx[i + 1]]);
                    Vector3 d = xf.TransformPoint(v[idx[i + 2]]);
                    float dist = Vector3.Distance(kc, ClosestOnTriangle(kc, a, b, d));
                    tris++;
                    if (dist < best) { best = dist; bestName = mf.name + " (" + xf.parent?.name + ")"; }
                }
            }

            Check("the knob examined real geometry at " + at, tris > 0,
                  tris + " triangles across " + meshes + " meshes"
                  + (unreadable > 0 ? ", " + unreadable + " unreadable and SKIPPED" : ""));
            Check("the knob clears everything at " + at, best > knobR,
                  "nearest geometry is " + ((best - knobR) * 1000f).ToString("F1")
                  + " mm outside the ball (" + bestName + "); knob radius "
                  + (knobR * 1000f).ToString("F1") + " mm");

            // ── THE PILOT HAS TO BE ABLE TO SEE IT ──────────────────────────────
            //
            // A control that works, animates and can be grabbed is still broken if it sits
            // below the bottom edge of the frame. The first arc build put 76% of the ball
            // off-screen at idle and every other check in this battery passed.
            //
            // Measured on the REAL cockpit camera, which carries CockpitCamera.basePitch,
            // so this is the view the participant actually gets before they touch the
            // right mouse button to look around.
            if (eyeCam != null)
            {
                Vector3 kcv = knob.bounds.center;
                float r = knobR;
                bool inFrame = true; float lowest = 9f;
                foreach (var probe in new[] { kcv,
                                              kcv + eyeCam.transform.up * r,
                                              kcv - eyeCam.transform.up * r,
                                              kcv + eyeCam.transform.right * r,
                                              kcv - eyeCam.transform.right * r })
                {
                    Vector3 vp = eyeCam.WorldToViewportPoint(probe);
                    lowest = Mathf.Min(lowest, vp.y);
                    if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) inFrame = false;
                }
                Check("the whole knob is inside the pilot's default view at " + at, inFrame,
                      "lowest point of the ball is at viewport y=" + lowest.ToString("F3")
                      + " (0 is the bottom edge of the frame)");
            }

            // The whole group — casing and lever together — against the display above it.
            if (model != null)
            {
                // REAL VERTICES, not bounds corners. An axis-aligned box round a blade
                // raked 64 deg overstates its height by about 5 mm, and 5 mm is most of the
                // clearance being measured — a conservative test that fails on its own
                // padding is no more use than one that passes on slack.
                float top = float.MinValue;
                foreach (var mf in c.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = mf.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var xf = mf.transform;
                    foreach (var vtx in mesh.vertices)
                        top = Mathf.Max(top, model.InverseTransformPoint(xf.TransformPoint(vtx)).y);
                }
                if (top > worstTop) { worstTop = top; worstTopAt = at; }
                Check("the group stays below the MFD's lower edge at " + at, top < MfdLowerEdge,
                      "top of group y=" + top.ToString("F4") + " vs MFD lower edge "
                      + MfdLowerEdge.ToString("F4") + " ("
                      + ((MfdLowerEdge - top) * 1000f).ToString("F1") + " mm clear)");
            }
        }
        report.AppendLine("   highest point of the whole throttle group: y=" + worstTop.ToString("F4")
                          + " at " + worstTopAt + ", MFD lower edge y=" + MfdLowerEdge.ToString("F4"));

        ctl.ClearOverrides(); yield return WaitFrames(2);
    }

    /// <summary>The GRIP of a lever: the furthest child renderer of the animated visual
    /// from its pivot.
    ///
    /// Found by geometry, never by name, and never assumed to be spec.visual itself. On the
    /// quadrant lever spec.visual is the ARM, whose origin sits ON the pivot — so aiming at
    /// spec.visual.position aims at the hinge, 65 mm from the ball the pilot actually
    /// clicks. Doing exactly that made the reach test report a comfortable 40 mm when it was
    /// measuring a point no participant will ever aim at.</summary>
    static Renderer GripOf(PhysicalControl c)
    {
        var arm = c.spec.visual;
        if (arm == null) return null;
        Vector3 hinge = arm.parent != null ? arm.parent.position : arm.position;
        Renderer best = null; float far = -1f;
        foreach (var r in arm.GetComponentsInChildren<Renderer>(true))
        {
            float d = Vector3.Distance(r.bounds.center, hinge);
            if (d > far) { far = d; best = r; }
        }
        return best;
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
    /// Detection, 5.1.5. Used instead of a bounds test because the things the knob can
    /// foul are large flat panels, whose bounds say nothing useful about where they are.</summary>
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
        var grip = GripOf(c);
        if (grip == null) { Fail("throttle reach", "throttle has no grip to aim at"); yield break; }
        float gripR = Mathf.Max(grip.bounds.extents.x,
                      Mathf.Max(grip.bounds.extents.y, grip.bounds.extents.z));

        foreach (float where in new[] { 0f, 1f })
        {
            for (int i = 0; i < 10; i++) { ctl.SetThrottle(where); yield return null; }
            yield return WaitSeconds(0.25f);

            // Aim at the BALL the pilot can see and click — not at the arm's origin, which
            // is the hinge. See GripOf.
            Ray ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(grip.bounds.center));

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

            // Hitting the CENTRE of the ball is not enough — a participant aims anywhere on
            // it. The capture radius has to reach the ball's far edge, or clicks that
            // visibly land on the knob will do nothing.
            float toCentre = Vector3.Distance(ray.GetPoint(
                Vector3.Dot(c.transform.position - ray.origin, ray.direction)), c.transform.position);
            Check("the whole ball is inside the capture radius at " + at,
                  toCentre + gripR <= c.spec.captureRadius,
                  "centre is " + (toCentre * 1000f).ToString("F0") + " mm off the axis + ball radius "
                  + (gripR * 1000f).ToString("F0") + " mm = "
                  + ((toCentre + gripR) * 1000f).ToString("F0") + " mm needed, radius is "
                  + (c.spec.captureRadius * 1000f).ToString("F0") + " mm");
        }

        // ── AND THE OTHER WAY ROUND ──────────────────────────────────────────────
        //
        // The knob now stands 60 mm proud of the panel, which puts it NEARER the pilot
        // than the yoke hub. Nearest-along-the-ray therefore now favours the throttle,
        // and the ambiguity that had to be fixed in one direction can reappear in the
        // other: a bigger throttle sphere could start stealing clicks meant for the yoke.
        // Sizing one radius against the other is only safe if both directions are checked.
        var yoke = Find("yoke");
        if (yoke != null)
        {
            Ray ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(yoke.transform.position));
            PhysicalControl best = null; float bestT = float.MaxValue;
            var seen = new System.Text.StringBuilder();
            foreach (var k in rig.Controls)
            {
                if (k == null) continue;
                Vector3 toC = k.transform.position - ray.origin;
                float t = Vector3.Dot(toC, ray.direction);
                if (t < 0f) continue;
                float dist = Vector3.Distance(ray.GetPoint(t), k.transform.position);
                if (seen.Length > 0) seen.Append("; ");
                seen.Append(k.spec.id + " ray-dist=" + (dist * 1000f).ToString("F0")
                            + "mm radius=" + (k.spec.captureRadius * 1000f).ToString("F0")
                            + "mm range=" + (t * 1000f).ToString("F0") + "mm"
                            + (dist <= k.spec.captureRadius ? " ELIGIBLE" : ""));
                if (dist > k.spec.captureRadius) continue;
                if (t < bestT) { bestT = t; best = k; }
            }
            Check("aiming at the yoke still selects the yoke", best == yoke,
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

    /// <summary>The pivot's rake, sampled at power, so idle can be compared against it.
    /// A rake that moves is a rake that has leaked into the animated value.</summary>
    Quaternion pivotRakeAtPower = Quaternion.identity;

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
