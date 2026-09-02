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

        // ── MINIMAL COCKPIT (3 Sep 2026) ─────────────────────────────────────────
        // The cockpit now contains ONE physical control, the yoke, plus the two glass
        // displays. Every other control was removed on request. The tests below check
        // the two properties that actually matter after that change: that the yoke still
        // works, and that removing the OBJECTS did not remove the INPUTS.
        yield return TestYoke();
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


    /// <summary>THE COCKPIT CONTAINS EXACTLY ONE PHYSICAL CONTROL.
    ///
    /// Asserted rather than assumed, because "I removed the controls" is easy to believe
    /// and easy to get half-right — a builder left in the list, or a piece of furniture
    /// still drawn with nothing on it, would both pass unnoticed otherwise.</summary>
    IEnumerator TestOnlyYokeIsPhysical()
    {
        Section("MINIMAL COCKPIT  (the yoke is the only physical control)");

        int n = 0;
        var names = new System.Text.StringBuilder();
        foreach (var c in rig.Controls)
        {
            if (c == null) continue;
            n++;
            if (names.Length > 0) names.Append(", ");
            names.Append(c.spec.id);
        }
        Check("exactly one physical control", n == 1, n + " built: " + names);
        Check("and it is the yoke", HasControl("yoke"), "yoke=" + (HasControl("yoke") ? "present" : "MISSING"));

        foreach (string gone in new[] { "throttle", "flaps", "brake", "trim", "carb_heat",
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
