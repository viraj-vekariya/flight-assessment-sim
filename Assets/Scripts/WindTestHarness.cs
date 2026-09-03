// WindTestHarness — automated verification of the wind model and its effect on the
// aeroplane.
//
// WHY A SEPARATE BATTERY
//   The mission battery flies missions and the control battery moves levers; neither
//   can tell you whether a 6 m/s crosswind produces 6 m/s of crosswind. Wind is the
//   one subsystem where the quantity matters as much as the behaviour: a mission that
//   claims "12 kt crosswind" in its brief and delivers 4 kt is not a weaker
//   manipulation, it is a FALSE RECORD, and nothing downstream would catch it.
//
//   So this battery asserts NUMBERS, against closed-form predictions computed here in
//   the test rather than read back out of the model:
//     * the geometry of a meteorological wind direction
//     * the surface/free-stream relationship through the shear profile
//     * the round trip from "6 m/s from the right" to a direction and back
//     * that a headwind subtracts from groundspeed by exactly its own size
//     * that a crosswind produces the drift angle trigonometry predicts
//     * that the nose actually weathervanes on the ground, in the right direction
//     * that zero wind is EXACTLY zero, bit for bit
//     * that no wind survives a reset
//
//   Run:
//     Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunWindTest \
//           -windtest -logFile wind.log
//   (no -nographics: the GLB cockpit's displays render to off-screen cameras)
//
// WHAT IT CANNOT TEST
//   Whether the resulting task FEELS like a crosswind landing to a pilot. That needs
//   a person. This tests that the physics is the physics it claims to be.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class WindTestHarness : MonoBehaviour
{
    public static bool Finished { get; private set; }
    public static int Failures { get; private set; }

    readonly StringBuilder report = new StringBuilder();
    readonly List<string> problems = new List<string>();
    CessnaPhysics ac;
    AircraftController ctl;
    int checks;

    const float RwyHdg = Aerodrome.RunwayHeadingDeg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-windtest") { {
                    // One driver at a time — see SimDriver. A second driver does not
                    // crash, it quietly changes every number the battery reports.
                    if (!SimDriver.Claim("WindTestHarness")) return;
                    new GameObject("WindTestHarness").AddComponent<WindTestHarness>();
                } return; }
    }

    IEnumerator Start()
    {
        report.AppendLine("WIND MODEL TEST");
        report.AppendLine("===============");
        report.AppendLine("generated " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("unity " + Application.unityVersion);
        report.AppendLine();

        // ---- Part 1: pure model arithmetic. No aeroplane needed, so run it first;
        // if the geometry is wrong there is no point flying anything.
        PartOne();

        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Fail("boot", "no GameManager"); Finish(); yield break; }
            yield return null;
        }
        var gm = GameManager.Instance;
        ac = gm.Aircraft; ctl = ac.GetComponent<AircraftController>();
        if (!ParticipantManager.IsSet) ParticipantManager.SetID("WINDTEST");
        gm.SetParticipantReady();
        // Wait for the GLB cockpit to finish loading before flying anything. Until it
        // does, the legacy code-cockpit's CockpitInteraction is still enabled, and the
        // cockpit rig that owns the physical controls does not exist yet — so a test
        // run earlier than this measures a different aeroplane from the one the
        // participant flies. (ControlTestHarness waits the same 14 s, for the same reason.)
        yield return new WaitForSecondsRealtime(14f);
        ControlCheckMode.Enter();
        yield return new WaitForSecondsRealtime(0.5f);
        report.AppendLine("cockpit rig  : " +
            (CockpitControlRig.Instance != null ? CockpitControlRig.Instance.Controls.Length + " controls" : "NOT BUILT"));
        report.AppendLine();

        yield return TestOverrideSanity();
        yield return TestZeroWindIdentity();
        yield return TestHeadwindGroundspeed();
        yield return TestCrosswindDrift();
        yield return TestShearInFlight();
        yield return TestGroundWeathervane();
        yield return TestRudderHoldsCentreline();
        yield return TestResetLeavesNoWind();

        Finish();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PART 1 — the arithmetic
    // ═══════════════════════════════════════════════════════════════════════════

    void PartOne()
    {
        report.AppendLine("-- model arithmetic --");

        WindModel.Disable();
        Near("zero wind is exactly zero (x)", WindModel.Sample(new Vector3(0f, 500f, 0f)).x, 0f, 0f);
        Near("zero wind is exactly zero (z)", WindModel.Sample(new Vector3(0f, 500f, 0f)).z, 0f, 0f);
        True("Enabled false when calm", !WindModel.Enabled);

        // Geometry. "From 090" is an easterly: the air moves TOWARDS the west, i.e. -x.
        WindModel.Configure(90f, 10f, 0f, 1234, groundElevationM: 0f);
        Vector3 w = WindModel.Sample(new Vector3(0f, WindModel.RefHeightM, 0f));
        Near("from 090 blows towards -x", w.x, -10f, 0.05f);
        Near("from 090 has no north component", w.z, 0f, 0.05f);

        WindModel.Configure(0f, 10f, 0f, 1234, groundElevationM: 0f);
        w = WindModel.Sample(new Vector3(0f, WindModel.RefHeightM, 0f));
        Near("from 360 blows towards -z", w.z, -10f, 0.05f);

        // Shear profile: weaker near the ground, by the power law and nothing else.
        WindModel.Configure(270f, 12f, 0f, 99, groundElevationM: 0f);
        float atRef = WindModel.Sample(new Vector3(0f, WindModel.RefHeightM, 0f)).magnitude;
        float at10 = WindModel.Sample(new Vector3(0f, 10f, 0f)).magnitude;
        Near("free-stream speed at reference height", atRef, 12f, 0.05f);
        Near("surface speed follows the power law", at10,
             12f * Mathf.Pow(10f / WindModel.RefHeightM, WindModel.ShearExponent), 0.05f);
        True("wind is weaker near the ground", at10 < atRef - 1f);

        // The authoring round trip. This is the one that protects the mission briefs:
        // a mission says "6 m/s crosswind from the right, 4 m/s headwind" and the
        // aeroplane must actually get that.
        foreach (var (cx, hw) in new[] { (6f, 4f), (-6f, 4f), (8f, -2f), (3f, 9f) })
        {
            float dir = WindModel.DirectionFor(RwyHdg, cx, hw, out float spd);
            WindModel.Configure(dir, spd, 0f, 7, groundElevationM: 0f);
            Near($"round trip crosswind ({cx:F0},{hw:F0})", WindModel.CrosswindOn(RwyHdg), cx, 0.05f);
            Near($"round trip headwind  ({cx:F0},{hw:F0})", WindModel.HeadwindOn(RwyHdg), hw, 0.05f);
        }

        // Determinism: the gust is a function of (seed, clock) and nothing else.
        WindModel.Configure(270f, 8f, 4f, 4242, groundElevationM: 0f);
        WindModel.Clock = 37.5f;
        Vector3 a1 = WindModel.Sample(new Vector3(100f, 400f, 200f));
        WindModel.Configure(270f, 8f, 4f, 4242, groundElevationM: 0f);
        WindModel.Clock = 37.5f;
        Vector3 a2 = WindModel.Sample(new Vector3(100f, 400f, 200f));
        Near("same seed + same clock => same gust (x)", a1.x, a2.x, 1e-6f);
        Near("same seed + same clock => same gust (z)", a1.z, a2.z, 1e-6f);

        WindModel.Configure(270f, 8f, 4f, 9999, groundElevationM: 0f);
        WindModel.Clock = 37.5f;
        Vector3 a3 = WindModel.Sample(new Vector3(100f, 400f, 200f));
        True("a different seed gives a different gust", (a3 - a1).magnitude > 0.05f);

        // The gust must actually move: a "gust" that is constant is a steady wind.
        WindModel.Configure(270f, 8f, 5f, 4242, groundElevationM: 0f);
        float minS = 999f, maxS = -999f;
        for (float t = 0f; t < 300f; t += 0.5f)
        {
            WindModel.Clock = t;
            float s = WindModel.Sample(new Vector3(0f, 400f, 0f)).magnitude;
            minS = Mathf.Min(minS, s); maxS = Mathf.Max(maxS, s);
        }
        True("gust varies over a 300 s trial (spread " + (maxS - minS).ToString("F2") + " m/s)", maxS - minS > 1.5f);

        WindModel.Disable();
        report.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PART 2 — the aeroplane in the wind
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Park the aeroplane in level flight at a known state and let it settle.</summary>
    IEnumerator Fly(float altM, float hdgDeg, float speedMs, float seconds, float throttle = 0.62f)
    {
        ac.ResetTo(new Vector3(0f, altM, -1500f), Quaternion.Euler(0f, hdgDeg, 0f), true, speedMs);
        ctl?.ResetConfiguration(0f);
        float t = 0f;
        while (t < seconds)
        {
            // Drive through the controller's override API rather than writing the
            // physics fields directly: the controller re-reads the keyboard every
            // frame, so a direct write would be overwritten before FixedUpdate saw it.
            if (ctl != null) { ctl.SetPitch(0f); ctl.SetRoll(0f); ctl.SetYaw(0f); ctl.SetThrottle(throttle); }
            else { ac.throttle = throttle; ac.pitchInput = ac.rollInput = ac.yawInput = 0f; }
            t += Time.deltaTime; yield return null;
        }
    }

    /// <summary>Before measuring anything, prove the harness can actually fly the
    /// aeroplane. Every number below is meaningless if a commanded control input never
    /// reaches the flight model — and that failure mode is silent.</summary>
    IEnumerator TestOverrideSanity()
    {
        report.AppendLine("-- harness can command the aircraft --");
        var gm = GameManager.Instance;
        ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        for (int i = 0; i < 6; i++)
        {
            ctl.SetYaw(-1f); ctl.SetPitch(0.4f); ctl.SetRoll(-0.3f); ctl.SetThrottle(0.8f);
            yield return null;
        }
        report.AppendLine($"   after SetYaw(-1): yawInput={ac.yawInput:F3} ovr={ctl.YawOverridden}");
        report.AppendLine($"   after SetPitch(0.4): pitchInput={ac.pitchInput:F3} ovr={ctl.PitchOverridden}");
        report.AppendLine($"   after SetRoll(-0.3): rollInput={ac.rollInput:F3} ovr={ctl.RollOverridden}");
        report.AppendLine($"   after SetThrottle(0.8): throttle={ac.throttle:F3} ovr={ctl.ThrottleOverridden}");
        Near("commanded rudder reaches the flight model", ac.yawInput, -1f, 0.02f);
        Near("commanded elevator reaches the flight model", ac.pitchInput, 0.4f, 0.02f);
        Near("commanded aileron reaches the flight model", ac.rollInput, -0.3f, 0.02f);
        Near("commanded throttle reaches the flight model", ac.throttle, 0.8f, 0.02f);
        ctl.ResetConfiguration(0f);
        report.AppendLine();
    }

    IEnumerator TestZeroWindIdentity()
    {
        report.AppendLine("-- zero wind: airspeed == groundspeed --");
        WindModel.Disable();
        yield return Fly(600f, 0f, 50f, 3f);
        Near("groundspeed equals airspeed in calm air", ac.GroundSpeedMs, ac.AirspeedMs, 0.01f);
        Near("no drift in calm air", ac.DriftAngleDeg, 0f, 0.5f);
        Near("no wind vector in calm air", ac.WindVel.magnitude, 0f, 0f);
        report.AppendLine();
    }

    IEnumerator TestHeadwindGroundspeed()
    {
        report.AppendLine("-- headwind subtracts from groundspeed --");
        // A wind FROM the north (000) is a headwind for an aeroplane heading north.
        WindModel.Configure(0f, 12f, 0f, 11, groundElevationM: 0f);
        yield return Fly(600f, 0f, 50f, 6f);
        float windHere = WindModel.Sample(ac.transform.position).magnitude;
        Near("groundspeed = airspeed - headwind", ac.GroundSpeedMs, ac.AirspeedMs - windHere, 1.2f);
        True("groundspeed is lower than airspeed into a headwind", ac.GroundSpeedMs < ac.AirspeedMs - 6f);
        Near("a pure headwind causes no drift", ac.DriftAngleDeg, 0f, 1.5f);

        // And the reverse: a tailwind adds.
        WindModel.Configure(180f, 12f, 0f, 11, groundElevationM: 0f);
        yield return Fly(600f, 0f, 50f, 6f);
        True("groundspeed is higher than airspeed with a tailwind", ac.GroundSpeedMs > ac.AirspeedMs + 6f);
        report.AppendLine();
    }

    IEnumerator TestCrosswindDrift()
    {
        report.AppendLine("-- crosswind produces the predicted drift --");
        // Wind from 270 (the west) with the aeroplane heading north (000) is a
        // crosswind from the LEFT, so the aircraft is pushed to the EAST (+x) and its
        // track lies to the RIGHT of the nose => POSITIVE drift angle.
        WindModel.Configure(270f, 10f, 0f, 21, groundElevationM: 0f);
        yield return Fly(600f, 0f, 50f, 25f);
        float wv = WindModel.Sample(ac.transform.position).magnitude;
        float predicted = Mathf.Atan2(wv, ac.AirspeedMs) * Mathf.Rad2Deg;
        report.AppendLine($"   airspeed {ac.AirspeedMs:F1} m/s, wind {wv:F1} m/s, " +
                          $"drift {ac.DriftAngleDeg:F2}° (predicted {predicted:F2}°)");
        True("crosswind from the left drifts the track right", ac.DriftAngleDeg > 3f);
        Near("drift angle matches atan(Vw/Vac)", ac.DriftAngleDeg, predicted, 3.5f);

        WindModel.Configure(90f, 10f, 0f, 21, groundElevationM: 0f);
        yield return Fly(600f, 0f, 50f, 25f);
        True("crosswind from the right drifts the track left", ac.DriftAngleDeg < -3f);
        report.AppendLine();
    }

    IEnumerator TestShearInFlight()
    {
        report.AppendLine("-- shear: the wind the aeroplane is in changes with height --");
        WindModel.Configure(270f, 14f, 0f, 31, groundElevationM: 0f);
        yield return Fly(900f, 0f, 50f, 4f);
        float high = ac.WindVel.magnitude;
        yield return Fly(60f, 0f, 50f, 4f);
        float low = ac.WindVel.magnitude;
        report.AppendLine($"   wind at 900 m = {high:F2} m/s, at 60 m = {low:F2} m/s");
        True("the wind is weaker low down", low < high - 1.5f);

        // The discrete shear layer used by the windshear missions.
        WindModel.Configure(270f, 6f, 0f, 32, shearAltM: 200f, shearDeltaMs: 8f, groundElevationM: 0f);
        float below = WindModel.Sample(new Vector3(0f, 120f, 0f)).magnitude;
        float above = WindModel.Sample(new Vector3(0f, 300f, 0f)).magnitude;
        report.AppendLine($"   shear layer at 200 m: below {below:F2} m/s, above {above:F2} m/s");
        True("the discrete shear layer changes the wind across it", above > below + 5f);
        report.AppendLine();
    }

    IEnumerator TestGroundWeathervane()
    {
        report.AppendLine("-- ground: the nose weathervanes into the crosswind --");
        var gm = GameManager.Instance;

        // Roll down the runway with a crosswind from the RIGHT and no rudder. The nose
        // must swing RIGHT, into the wind. This is the effect whose absence made every
        // crosswind take-off in the old build track perfectly straight.
        WindModel.Configure(90f, 12f, 0f, 41, groundElevationM: Aerodrome.RunwayElevationM);
        yield return Roll(6f, rudder: 0f);
        float dRight = Mathf.DeltaAngle(0f, ac.HeadingDeg);
        report.AppendLine($"   crosswind from the right, no rudder: heading change {dRight:+0.00;-0.00}°");
        True("nose swings right into a right crosswind", dRight > 1.0f);

        WindModel.Configure(270f, 12f, 0f, 41, groundElevationM: Aerodrome.RunwayElevationM);
        yield return Roll(6f, rudder: 0f);
        float dLeft = Mathf.DeltaAngle(0f, ac.HeadingDeg);
        report.AppendLine($"   crosswind from the left,  no rudder: heading change {dLeft:+0.00;-0.00}°");
        True("nose swings left into a left crosswind", dLeft < -1.0f);

        WindModel.Disable();
        yield return Roll(6f, rudder: 0f);
        float dCalm = Mathf.DeltaAngle(0f, ac.HeadingDeg);
        report.AppendLine($"   calm, no rudder:                     heading change {dCalm:+0.00;-0.00}°");
        Near("the roll tracks straight in calm air", dCalm, 0f, 1.0f);
        report.AppendLine();
    }

    IEnumerator TestRudderHoldsCentreline()
    {
        report.AppendLine("-- rudder authority against the crosswind --");
        // The earlier version of this test held FULL rudder from a standing start,
        // which simply steers the aeroplane off the runway on the nosewheel and
        // measures nothing. What a crosswind take-off actually asks is: CAN the
        // centreline be held? So fly a proportional heading-hold on the rudder — a
        // crude but honest stand-in for a pilot — and measure the lateral deviation.
        //
        // The C172's demonstrated crosswind is 15 kt (7.7 m/s). At that value the
        // centreline must be holdable within the runway half-width; at a grossly
        // out-of-limits value it must not be. If rudder held any crosswind, the limit
        // would be a number in a brief with no consequence, and a crosswind mission
        // would not be a workload manipulation at all.
        float dLimit = 0f, dGross = 0f;

        WindModel.Configure(90f, 7.7f / WindModel.SurfaceSpeed(1f), 0f, 51,
                            groundElevationM: Aerodrome.RunwayElevationM);
        yield return RollHoldingCentreline(9f, r => dLimit = Mathf.Max(dLimit, r));
        report.AppendLine($"   15 kt crosswind, rudder holding heading: max lateral deviation {dLimit:F2} m");
        True($"the centreline is holdable at the demonstrated crosswind (< {Aerodrome.RunwayHalfWidth:F0} m)",
             dLimit < Aerodrome.RunwayHalfWidth);

        WindModel.Configure(90f, 20f / WindModel.SurfaceSpeed(1f), 0f, 52,
                            groundElevationM: Aerodrome.RunwayElevationM);
        yield return RollHoldingCentreline(9f, r => dGross = Mathf.Max(dGross, r));
        report.AppendLine($"   39 kt crosswind, rudder holding heading: max lateral deviation {dGross:F2} m");
        report.AppendLine($"   ratio {(dLimit > 0.01f ? dGross / dLimit : 0f):F1}x  (runway half-width {Aerodrome.RunwayHalfWidth:F0} m)");
        // A RATIO, NOT AN ABSOLUTE MARGIN.
        //
        // This asserted `dGross > dLimit + 2` — two metres more deviation — which is a
        // number implicitly calibrated to one particular value of the fin's yaw stiffness.
        // Changing that stiffness to the figure a real 172 actually has moved both
        // deviations down proportionally (0.70/2.80 became 0.50/1.96) and the absolute
        // margin failed while the RELATIONSHIP the test exists to check was unchanged:
        // 4.0x before, 3.9x after. A scale-free criterion states the intent and does not
        // have to be re-tuned every time an aerodynamic coefficient is corrected.
        True("a grossly out-of-limits crosswind costs materially more deviation",
             dLimit > 0.01f && dGross > dLimit * 2.5f);

        // HONEST LIMITATION, recorded rather than asserted away. The comment above this
        // test says a grossly out-of-limits crosswind "must not" be holdable. It never
        // tested that, and the aeroplane does not behave that way: 39 kt of crosswind still
        // holds the centreline to within about 2 m of a 15 m half-width, under this fin
        // value and under the previous one. The GROUND crosswind model is more forgiving
        // than the real aeroplane. It does not affect the airborne crab behaviour the
        // crosswind missions actually measure, but it is a real gap and it is written down.
        if (dGross < Aerodrome.RunwayHalfWidth * 0.5f)
            report.AppendLine($"   NOTE: a 39 kt crosswind is still holdable to {dGross:F1} m. "
                            + "The ground crosswind model is more forgiving than a real 172.");
        report.AppendLine();
    }

    /// <summary>Take-off roll with a proportional rudder heading-hold, reporting the
    /// running lateral deviation from the runway centreline.</summary>
    IEnumerator RollHoldingCentreline(float seconds, System.Action<float> onDeviation)
    {
        var gm = GameManager.Instance;
        ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        ctl?.ResetConfiguration(0f);
        float t = 0f;
        while (t < seconds)
        {
            if (ctl != null)
            {
                float hdgErr = Mathf.DeltaAngle(RwyHdg, ac.HeadingDeg);
                float xErr = ac.transform.position.x;              // runway centreline is x = 0
                // Steer on heading, biased by lateral offset — what a pilot does.
                float cmd = Mathf.Clamp(-(hdgErr * 0.12f + xErr * 0.05f), -1f, 1f);
                ctl.SetThrottle(1f); ctl.SetPitch(0f); ctl.SetRoll(0f);
                ctl.SetYaw(cmd); ctl.SetBrake(0f);
            }
            t += Time.deltaTime;
            yield return null;
            if (ac.Grounded) onDeviation(Mathf.Abs(ac.transform.position.x));
        }
        if (ctl != null) { ctl.SetYaw(0f); ctl.SetThrottle(0f); }
    }

    IEnumerator TestResetLeavesNoWind()
    {
        report.AppendLine("-- reset --");
        WindModel.Configure(270f, 15f, 6f, 61, groundElevationM: 0f);
        True("wind is on before the reset", WindModel.Enabled);
        WindModel.Disable();
        True("wind is off after Disable()", !WindModel.Enabled);
        Near("Sample() is zero after Disable()", WindModel.Sample(new Vector3(0f, 500f, 0f)).magnitude, 0f, 0f);
        yield return Fly(600f, 0f, 50f, 2f);
        Near("the aeroplane sees no wind after a reset", ac.WindVel.magnitude, 0f, 0f);
        report.AppendLine();
    }

    /// <summary>Accelerate down the runway from the threshold for `seconds`, holding a
    /// fixed rudder input, and leave the aeroplane wherever it ends up.</summary>
    IEnumerator Roll(float seconds, float rudder)
    {
        var gm = GameManager.Instance;
        // Use the world's own runway spawn rather than a hand-computed one: it is the
        // point the rest of the project already trusts to be clear of geometry and at
        // the right height above the surface.
        ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        ctl?.ResetConfiguration(0f);
        float t = 0f, yawSeen = 0f, spdSeen = 0f, thrSeen = 0f; int yawN = 0, groundedN = 0, ovrN = 0;
        while (t < seconds)
        {
            if (ctl != null)
            {
                ctl.SetThrottle(1f); ctl.SetPitch(0f); ctl.SetRoll(0f);
                ctl.SetYaw(rudder);       // held every frame; ownership expires otherwise
                ctl.SetBrake(0f);
            }
            else { ac.throttle = 1f; ac.pitchInput = ac.rollInput = 0f; ac.yawInput = rudder; }
            t += Time.deltaTime;
            yield return null;
            yawSeen += Mathf.Abs(ac.yawInput); yawN++;
            if (ac.Grounded) groundedN++;
            if (ctl != null && ctl.YawOverridden) ovrN++;
            thrSeen += ac.throttle;
            spdSeen = Mathf.Max(spdSeen, ac.GroundSpeedMs);
        }
        report.AppendLine($"      [roll diag] mean|yawInput|={(yawN > 0 ? yawSeen / yawN : 0f):F3} " +
                          $"yawOvr {(yawN > 0 ? 100f * ovrN / yawN : 0f):F0}% " +
                          $"meanThr {(yawN > 0 ? thrSeen / yawN : 0f):F2} " +
                          $"state {(GameManager.Instance != null ? GameManager.Instance.State.ToString() : "?")} " +
                          $"ctl {(ctl == null ? "NULL" : "ok")} frames {yawN} " +
                          $"grounded {(yawN > 0 ? 100f * groundedN / yawN : 0f):F0}% peak gs {spdSeen:F1} m/s " +
                          $"crashed={(ac.Crashed ? ac.CrashReason : "no")}");
        // A crash mid-roll invalidates the measurement: CessnaPhysics zeroes throttle
        // and every control input once Crashed is set, so the aeroplane would coast and
        // the rudder would read as having no effect. Say so rather than reporting a
        // number that means nothing.
        if (ac.Crashed) Fail("roll", "aircraft crashed during the take-off roll: " + ac.CrashReason);
        if (ctl != null) { ctl.SetYaw(0f); ctl.SetThrottle(0f); }
        else { ac.yawInput = 0f; ac.throttle = 0f; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ASSERTIONS + REPORT
    // ═══════════════════════════════════════════════════════════════════════════

    void True(string what, bool ok)
    {
        checks++;
        report.AppendLine((ok ? "   ok   " : "   FAIL ") + what);
        if (!ok) { problems.Add(what); Failures++; }
    }

    void Near(string what, float got, float want, float tol)
    {
        checks++;
        bool ok = Mathf.Abs(got - want) <= tol;
        report.AppendLine((ok ? "   ok   " : "   FAIL ") + what +
                          $"  (got {got:F4}, want {want:F4} ±{tol:F4})");
        if (!ok) { problems.Add($"{what}: got {got:F4}, want {want:F4} ±{tol:F4}"); Failures++; }
    }

    void Fail(string where, string why)
    {
        problems.Add(where + ": " + why); Failures++;
        report.AppendLine("   FAIL " + where + " — " + why);
    }

    void Finish()
    {
        WindModel.Disable();
        report.AppendLine();
        report.AppendLine("checks   : " + checks);
        report.AppendLine("problems : " + problems.Count);
        foreach (var p in problems) report.AppendLine("  - " + p);

        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "wind_test_report.txt");
        try { File.WriteAllText(path, report.ToString()); } catch { }
        Debug.Log("[WTEST]\n" + report);
        Debug.Log("[WTEST] wrote " + path);
        Finished = true;
    }
}
