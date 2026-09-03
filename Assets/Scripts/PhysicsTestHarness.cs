// PhysicsTestHarness — does the aeroplane FLY like a Cessna 172?
//
// WHY THIS EXISTS
//   Every other battery in this project checks plumbing: that a control is wired to a
//   variable, that a mission completes, that the wind model returns what its closed form
//   says. None of them ever asked the only question a flight simulator finally rests on —
//   whether the aircraft's motion is right. A cockpit and an environment sit on top of the
//   flight model, and if that model is wrong they are decoration on a wrong answer.
//
// HOW IT JUDGES
//   Against published Cessna 172S figures and against closed-form flight mechanics, not
//   against "feels about right":
//
//     clean stall (Vs1)        48 KCAS = 24.7 m/s
//     full-flap stall (Vs0)    40 KCAS = 20.6 m/s
//     best rate of climb       ~3.7 m/s (730 fpm) at sea level, Vy ~74 KIAS = 38 m/s
//     roll rate, full aileron  40-60 deg/s at cruise
//     coordinated turn rate    omega = g * tan(bank) / V     (exact, from the force balance)
//
//   The turn-rate check is the important one and is pure physics: in a level turn the
//   horizontal component of lift supplies the centripetal force, so the turn rate follows
//   from bank and speed alone. An aeroplane that banks but does not turn at that rate is
//   not flying, whatever else it does.
//
//   Run:  Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunPhysicsTest \
//              -physicstest -logFile phys.log
//   No -nographics (the displays render to off-screen cameras) and no -quit.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class PhysicsTestHarness : MonoBehaviour
{
    public static bool Finished { get; private set; }
    public static int Failures { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-physicstest")
            {
                if (!SimDriver.Claim("PhysicsTestHarness")) return;
                new GameObject("PhysicsTestHarness").AddComponent<PhysicsTestHarness>();
                return;
            }
    }

    CessnaPhysics ac;
    AircraftController ctl;
    readonly StringBuilder report = new StringBuilder();
    int checks;

    const float G = 9.81f;

    // ── held inputs, re-asserted every physics step ────────────────────────────────
    float hPitch, hRoll, hYaw, hThrottle;
    bool holding;

    // ASSERTED IN Update, NOT FixedUpdate.
    //
    // AircraftController's override model expires after GraceFrames = 1 RENDERED frames.
    // In batch mode the frame rate is unbounded while physics ticks at 50 Hz, so there can
    // be many Update calls between two FixedUpdates — an override set in FixedUpdate goes
    // stale before the controller next reads it, and every input silently reads zero. That
    // is exactly what the first run of this battery measured: a whole report of 0.0 that
    // looked like an aeroplane with no aerodynamics.
    void Update()
    {
        if (!holding || ctl == null) return;
        ctl.SetPitch(hPitch);
        ctl.SetRoll(hRoll);
        ctl.SetYaw(hYaw);
        ctl.SetThrottle(hThrottle);
    }

    IEnumerator Start()
    {
        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Debug.LogError("[PTEST] no GameManager"); Done(); yield break; }
            yield return null;
        }
        var gm = GameManager.Instance;
        if (!ParticipantManager.IsSet) ParticipantManager.SetID("PHYS");
        gm.SetParticipantReady();
        yield return new WaitForSecondsRealtime(6f);

        ac = gm.Aircraft;
        ctl = ac.GetComponent<AircraftController>();
        if (ac == null || ctl == null) { Debug.LogError("[PTEST] no aircraft"); Done(); yield break; }

        // THE CONTROLLER ONLY RUNS IN THE FLYING STATE. Outside it, AircraftController
        // zeroes pitch, roll, yaw and brake every frame and returns — so a battery that
        // never enters that state measures an aeroplane nobody is flying. Control check is
        // the right way in: it is a flying state that records nothing.
        ControlCheckMode.Enter();
        yield return new WaitForSecondsRealtime(0.5f);

        // No wind: every number below is about the aeroplane, not about the air mass.
        WindModel.Disable();

        Head("AEROPLANE — the constants the rest of the report is judged against");
        report.AppendLine(string.Format("   mass {0:0} kg   wing {1:0.0} m2   maxThrust {2:0} N",
                                        ac.mass, ac.wingArea, ac.maxThrust));
        report.AppendLine(string.Format("   cl0 {0:0.00}  clAlpha {1:0.00}/rad  stall {2:0.0} deg  cd0 {3:0.000}  k {4:0.000}",
                                        ac.cl0, ac.clAlpha, ac.stallAngleDeg, ac.cd0, ac.inducedK));

        // Closed-form stall speeds from the model's OWN coefficients. If the sim does not
        // match these, the aerodynamics disagree with themselves.
        float clMaxClean = ac.cl0 + ac.clAlpha * (ac.stallAngleDeg * Mathf.Deg2Rad);
        float clMaxFlap  = ac.cl0 + ac.flapsLiftBonus + ac.clAlpha * (ac.stallAngleDeg * Mathf.Deg2Rad);
        float vsClean = Mathf.Sqrt(2f * ac.mass * G / (ac.airDensity * ac.wingArea * clMaxClean));
        float vsFlap  = Mathf.Sqrt(2f * ac.mass * G / (ac.airDensity * ac.wingArea * clMaxFlap));
        report.AppendLine(string.Format("   predicted Vs clean {0:0.0} m/s ({1:0} kt)   full flap {2:0.0} m/s ({3:0} kt)",
                                        vsClean, vsClean * 1.944f, vsFlap, vsFlap * 1.944f));
        Check("clean stall speed matches a real 172 (48 kt +-15%)",
              Mathf.Abs(vsClean * 1.944f - 48f) <= 7.2f, vsClean * 1.944f, "kt");
        Check("full-flap stall speed matches a real 172 (40 kt +-15%)",
              Mathf.Abs(vsFlap * 1.944f - 40f) <= 6f, vsFlap * 1.944f, "kt");

        yield return TestBankMakesItTurn();
        yield return TestTurnRateAgainstTheory();
        yield return TestRollRate();
        yield return TestControlAuthorityVsSpeed();
        yield return TestSideslipDecays();
        yield return TestClimb();
        yield return TestStallAndRecovery();
        yield return TestGroundAndAuthorityOnset();
        yield return TestFinTermIsNotTheProblem();
        yield return TestTurnCoordinationAB();
        yield return TestRollPowerSweep();
        yield return TestDihedralEffect();
        yield return TestLateralStability();
        yield return TestTopSpeed();

        Write();
        Done();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // THE REPORTED COMPLAINT: bank the wings and the aeroplane must go sideways.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Roll into a bank, hold it, and check the aeroplane actually changes
    /// heading AND that its ground track curves the right way.
    ///
    /// Two separate things, because they fail separately: the nose can swing while the
    /// flight path stays straight (a skid, which looks like a turn and is not one), and
    /// the path can curve while the heading lags (a slip). A turn is both.</summary>
    IEnumerator TestBankMakesItTurn()
    {
        Head("BANK -> TURN   (roll in, hold, does it go round?)");

        foreach (float sign in new float[] { 1f, -1f })
        {
            yield return Airborne(52f);
            // Roll in, then hold the bank with a small held aileron against roll damping.
            yield return HoldFor(0f, sign * 0.65f, 0f, 0.75f, 2.2f);

            float hdg0 = ac.HeadingDeg;
            Vector3 p0 = ac.transform.position;
            Vector3 fwd0 = ac.transform.forward; fwd0.y = 0f; fwd0.Normalize();

            // Neutral-ish aileron: the bank holds itself once established.
            yield return HoldFor(-0.10f, sign * 0.05f, 0f, 0.75f, 6f);

            float bank = ac.RollDeg;
            float dHdg = Mathf.DeltaAngle(hdg0, ac.HeadingDeg);
            Vector3 d = ac.transform.position - p0; d.y = 0f;
            // Lateral displacement relative to the heading it started on: how far it went
            // "side to side" rather than straight on.
            // Vector3.Cross(up, forward) is the aircraft's RIGHT in Unity's left-handed
            // frame. An earlier version negated it, which reported every correct right turn
            // as curving left.
            float lateral = Vector3.Dot(d, Vector3.Cross(Vector3.up, fwd0).normalized);

            string dir = sign > 0f ? "RIGHT" : "LEFT";
            report.AppendLine(string.Format(
                "   {0} bank: held {1,6:0.0} deg -> heading changed {2,7:0.0} deg in 6 s, track moved {3,7:0.0} m sideways",
                dir, bank, dHdg, lateral));

            Check(dir + " bank actually banks", Mathf.Abs(bank) > 12f, Mathf.Abs(bank), "deg");
            Check(dir + " bank changes heading", Mathf.Abs(dHdg) > 8f, Mathf.Abs(dHdg), "deg in 6 s");
            Check(dir + " bank turns the RIGHT WAY", Mathf.Sign(dHdg) == sign, dHdg, "deg");
            Check(dir + " bank moves the flight path sideways",
                  Mathf.Abs(lateral) > 15f, Mathf.Abs(lateral), "m");
            Check(dir + " path curves the same way the nose goes",
                  Mathf.Sign(lateral) == sign, lateral, "m");
        }
    }

    /// <summary>The turn rate must match g*tan(bank)/V. This is not a style preference:
    /// it is what the force balance in a level turn requires, and it is the single number
    /// that says whether the lift vector is really doing the turning.</summary>
    IEnumerator TestTurnRateAgainstTheory()
    {
        Head("TURN RATE vs THEORY   (omega = g * tan(bank) / V)");

        foreach (float target in new float[] { 20f, 30f, 45f })
        {
            yield return Airborne(52f);
            // Roll in proportionally until the target bank is reached.
            float t = 0f;
            while (Mathf.Abs(ac.RollDeg) < target - 1f && t < 8f)
            {
                float err = (target - Mathf.Abs(ac.RollDeg)) / 30f;
                SetHold(-0.05f, Mathf.Clamp(err, 0.05f, 0.7f), 0f, 0.8f);
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // Hold it and measure, with a light back-pressure so it stays roughly level.
            float hdg0 = ac.HeadingDeg, alt0 = ac.AltitudeM;
            float vSum = 0f, bSum = 0f; int n = 0;
            float measured = 0f, dur = 5f; t = 0f;
            while (t < dur)
            {
                float hold = Mathf.Clamp((target - Mathf.Abs(ac.RollDeg)) / 40f, -0.35f, 0.35f);
                SetHold(-0.16f, Mathf.Sign(1f) * hold, 0f, 0.8f);
                vSum += ac.AirspeedMs; bSum += Mathf.Abs(ac.RollDeg); n++;
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            measured = Mathf.Abs(Mathf.DeltaAngle(hdg0, ac.HeadingDeg)) / dur;

            float vAvg = vSum / Mathf.Max(1, n);
            float bAvg = bSum / Mathf.Max(1, n);
            float ideal = G * Mathf.Tan(bAvg * Mathf.Deg2Rad) / Mathf.Max(1f, vAvg) * Mathf.Rad2Deg;
            float ratio = ideal > 0.01f ? measured / ideal : 0f;

            report.AppendLine(string.Format(
                "   asked {0,4:0} deg: flew {1,5:0.0} deg at {2,5:0.0} m/s -> {3,5:0.0} deg/s measured, {4,5:0.0} ideal  (ratio {5:0.00}), alt drift {6,6:0.0} m",
                target, bAvg, vAvg, measured, ideal, ratio, ac.AltitudeM - alt0));

            // Half to double the ideal rate. Wide on purpose: this is a hand-flown
            // manoeuvre with real damping and a real speed change, not a closed form. What
            // it catches is an aeroplane that banks and barely turns, or one that spins
            // round far faster than lift could pull it.
            Check(string.Format("turn rate at {0:0} deg is within 2x of theory", target),
                  ratio > 0.5f && ratio < 2.0f, ratio, "x ideal");
        }
    }

    IEnumerator TestRollRate()
    {
        Head("ROLL RATE   (full aileron at cruise; a 172 rolls 40-60 deg/s)");
        yield return Airborne(52f);

        // Let the roll rate settle against damping, then measure it.
        yield return HoldFor(-0.05f, 1f, 0f, 0.8f, 1.0f);
        float r0 = ac.RollDeg; float t = 0f; const float Dur = 0.8f;
        float peak = 0f;
        while (t < Dur)
        {
            SetHold(-0.05f, 1f, 0f, 0.8f);
            peak = Mathf.Max(peak, Mathf.Abs(ac.GetComponent<Rigidbody>().angularVelocity.z * Mathf.Rad2Deg));
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        float rate = Mathf.Abs(Mathf.DeltaAngle(r0, ac.RollDeg)) / Dur;
        report.AppendLine(string.Format("   full aileron -> {0:0.0} deg/s sustained, {1:0.0} deg/s peak   (a 172 rolls 45-60)",
                                        rate, peak));
        Check("roll rate is in a light-aircraft band (15-120 deg/s)", rate > 15f && rate < 120f, rate, "deg/s");
    }

    /// <summary>How responsive the controls are at each speed. Not pass/fail on its own —
    /// it is the table that answers "the yoke feels dead" with a number, and it shows the
    /// speed below which the aeroplane genuinely cannot be manoeuvred.</summary>
    IEnumerator TestControlAuthorityVsSpeed()
    {
        Head("CONTROL AUTHORITY vs SPEED   (roll response to full aileron, 1 s)");
        report.AppendLine("   speed m/s |  bank after 1 s of full aileron");
        foreach (float v in new float[] { 15f, 25f, 35f, 45f, 60f })
        {
            yield return Airborne(v);
            float r0 = ac.RollDeg;
            yield return HoldFor(-0.05f, 1f, 0f, 0.6f, 1.0f);
            float dRoll = Mathf.Abs(Mathf.DeltaAngle(r0, ac.RollDeg));
            report.AppendLine(string.Format("   {0,9:0} | {1,6:0.0} deg", v, dRoll));
            if (Mathf.Approximately(v, 45f))
                Check("controls are responsive at approach speed (45 m/s)", dRoll > 8f, dRoll, "deg in 1 s");
        }
    }

    IEnumerator TestSideslipDecays()
    {
        Head("DIRECTIONAL STABILITY   (kick the rudder, release, sideslip must wash out)");
        yield return Airborne(50f);
        yield return HoldFor(-0.05f, 0f, 1f, 0.8f, 1.5f);
        float slipPeak = Mathf.Abs(ac.SideslipDeg);
        yield return HoldFor(-0.05f, 0f, 0f, 0.8f, 4f);
        float slipEnd = Mathf.Abs(ac.SideslipDeg);
        report.AppendLine(string.Format("   sideslip {0:0.0} deg with full rudder -> {1:0.0} deg 4 s after release",
                                        slipPeak, slipEnd));
        Check("full rudder produces a real sideslip", slipPeak > 2f, slipPeak, "deg");
        Check("sideslip washes out when released", slipEnd < slipPeak * 0.6f + 1f, slipEnd, "deg");
    }

    IEnumerator TestClimb()
    {
        Head("CLIMB   (full power, nose up ~7 deg; a 172 does about 3.7 m/s at sea level)");
        yield return Airborne(38f);

        // Hold a PITCH ATTITUDE rather than chase a speed. A speed loop in a machine that
        // is already descending drives the nose the wrong way and measures a dive.
        // NEGATIVE pitchInput is nose up in this model (asserted by the control battery).
        float t = 0f, vsSum = 0f; int n = 0; float nextTrace = 0f;
        while (t < 12f)
        {
            float err = (7f - ac.PitchDeg) / 10f;          // want +7 deg nose up
            SetHold(Mathf.Clamp(-err, -0.6f, 0.6f), -ac.RollDeg / 60f, 0f, 1f);
            if (t >= nextTrace) { Trace("t=" + t.ToString("0")); nextTrace += 3f; }
            if (t > 7f) { vsSum += ac.VerticalSpeedMs; n++; }
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        float vs = vsSum / Mathf.Max(1, n);
        report.AppendLine(string.Format("   climb {0:0.00} m/s ({1:0} fpm) at {2:0.0} m/s, pitch {3:0.0} deg",
                                        vs, vs * 196.85f, ac.AirspeedMs, ac.PitchDeg));
        Check("climbs at full power (1-8 m/s)", vs > 1f && vs < 8f, vs, "m/s");
    }

    IEnumerator TestStallAndRecovery()
    {
        Head("STALL   (power off, decelerate gently to the break, then hands off)");
        yield return Airborne(40f);

        // A GENTLE 1g deceleration, not a pull-up. Yanking the nose up stalls the wing at a
        // much higher speed (an accelerated stall) and would be measured as the wrong stall
        // speed. Hold the aeroplane level and let the speed bleed off at idle.
        float t = 0f, stallSpeed = -1f, nextTrace = 0f;
        while (t < 30f && stallSpeed < 0f)
        {
            float vsErr = (0f - ac.VerticalSpeedMs) / 6f;         // hold level
            SetHold(Mathf.Clamp(-vsErr, -0.7f, 0.4f), -ac.RollDeg / 60f, 0f, 0.02f);
            if (t >= nextTrace) { Trace("t=" + t.ToString("0")); nextTrace += 4f; }
            if (ac.Stalled) stallSpeed = ac.AirspeedMs;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        float clMax = ac.cl0 + ac.clAlpha * (ac.stallAngleDeg * Mathf.Deg2Rad);
        float vsTheory = Mathf.Sqrt(2f * ac.mass * G / (ac.airDensity * ac.wingArea * clMax));
        if (stallSpeed < 0f)
        {
            report.AppendLine("   never stalled within 30 s of a level power-off deceleration");
            Check("the aeroplane can be stalled", false, 0f, "");
        }
        else
        {
            report.AppendLine(string.Format("   stalled at {0:0.0} m/s ({1:0} kt); 1g theory {2:0.0} m/s ({3:0} kt)",
                                            stallSpeed, stallSpeed * 1.944f, vsTheory, vsTheory * 1.944f));
            Check("stall happens near the 1g theoretical speed (within 30%)",
                  Mathf.Abs(stallSpeed - vsTheory) / vsTheory < 0.30f, stallSpeed, "m/s");
        }

        // HANDS OFF. A stable aeroplane lowers its nose and flies again with no input.
        report.AppendLine("   hands off from here:");
        float r = 0f; nextTrace = 0f;
        while (r < 10f)
        {
            SetHold(0f, 0f, 0f, 0.6f);
            if (r >= nextTrace) { Trace("t+" + r.ToString("0")); nextTrace += 2.5f; }
            r += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        Check("recovers from the stall hands-off", !ac.Stalled, ac.AoADeg, "deg AoA");
    }


    /// <summary>WHY THE YOKE FEELS DEAD ON THE RUNWAY.
    ///
    /// Control authority is scaled by speed squared, so a parked aeroplane has none — the
    /// ailerons deflect and nothing happens, which is exactly right and exactly what it
    /// feels like when something is broken. This puts a number on it: the speed at which
    /// the controls start to bite, and the ground roll to that speed.</summary>
    IEnumerator TestGroundAndAuthorityOnset()
    {
        Head("ON THE GROUND   (why the yoke does nothing until it is moving)");
        report.AppendLine(string.Format("   controlRefSpeed = {0:0} m/s; authority = (v/vref)^2, so:", ac.controlRefSpeed));
        foreach (float v in new float[] { 0f, 5f, 10f, 15f, 20f, 25f, 30f, 38f })
        {
            float a = Mathf.Clamp01((v * v) / (ac.controlRefSpeed * ac.controlRefSpeed));
            report.AppendLine(string.Format("      {0,4:0} m/s ({1,3:0} kt) -> {2,5:0}% of full control power",
                                            v, v * 1.944f, a * 100f));
        }

        // Take off for real and see when it flies.
        var gm = GameManager.Instance;
        holding = false;
        if (ctl != null) ctl.ClearOverrides();
        ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        yield return new WaitForFixedUpdate();

        float t = 0f, rotateSpeed = -1f, liftoffSpeed = -1f;
        float startZ = ac.transform.position.z;
        while (t < 40f && liftoffSpeed < 0f)
        {
            // Full power; ease the nose up once through 25 m/s, as a pilot would.
            float pitch = ac.AirspeedMs > 25f ? -0.35f : 0f;
            SetHold(pitch, 0f, 0f, 1f);
            if (rotateSpeed < 0f && ac.AirspeedMs > 25f) rotateSpeed = ac.AirspeedMs;
            if (!ac.Grounded && ac.AltitudeM > gm.Runway.Start.y + 2f) liftoffSpeed = ac.AirspeedMs;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        float roll = Mathf.Abs(ac.transform.position.z - startZ);
        report.AppendLine(string.Format("   take-off: rotated at {0:0.0} m/s, airborne at {1:0.0} m/s ({2:0} kt) after {3:0} m of ground roll",
                                        rotateSpeed, liftoffSpeed, liftoffSpeed * 1.944f, roll));
        Check("gets airborne on its own", liftoffSpeed > 0f, liftoffSpeed, "m/s");
        // A 172 lifts off around 55 kt and uses roughly 300 m of ground roll.
        Check("lift-off speed is sane (40-75 kt)",
              liftoffSpeed * 1.944f > 40f && liftoffSpeed * 1.944f < 75f, liftoffSpeed * 1.944f, "kt");
        Check("ground roll is sane (100-700 m)", roll > 100f && roll < 700f, roll, "m");
    }

    /// <summary>IS THE FIN TERM THE THING THAT CHANGED?
    ///
    /// The current flight model is Phase 4's plus a wind layer (identically zero when no
    /// wind is set), elevator trim, an analog brake, and one new aerodynamic term: a
    /// vertical-fin yaw moment proportional to sideslip. With no wind and no trim, that fin
    /// term is the ONLY thing that can make the aeroplane behave differently from Phase 4.
    ///
    /// So it is measured directly, both ways, rather than argued about.</summary>
    IEnumerator TestFinTermIsNotTheProblem()
    {
        Head("FIN TERM A/B   (the only aerodynamic change since Phase 4)");
        float saved = ac.finVolume;
        foreach (float fin in new float[] { 0f, saved })
        {
            ac.finVolume = fin;
            yield return Airborne(52f);
            yield return HoldFor(0f, 0.65f, 0f, 0.75f, 2.2f);
            float hdg0 = ac.HeadingDeg;
            Vector3 p0 = ac.transform.position;
            Vector3 f0 = ac.transform.forward; f0.y = 0f; f0.Normalize();
            yield return HoldFor(-0.10f, 0.05f, 0f, 0.75f, 6f);
            Vector3 d = ac.transform.position - p0; d.y = 0f;
            float lateral = Vector3.Dot(d, Vector3.Cross(Vector3.up, f0).normalized);
            report.AppendLine(string.Format(
                "   finVolume {0,4:0}{1}: bank {2,5:0.0} deg -> {3,5:0.0} deg of heading, {4,6:0.0} m sideways, sideslip {5,5:0.0} deg",
                fin, fin == 0f ? " (Phase 4)" : " (current)", ac.RollDeg,
                Mathf.DeltaAngle(hdg0, ac.HeadingDeg), lateral, ac.SideslipDeg));
        }
        ac.finVolume = saved;
        report.AppendLine("   If those two rows are close, the turn behaviour is Phase 4's.");
    }


    /// <summary>Is the synthetic "turn coordination" yaw still needed?
    ///
    /// `turnCoordination` adds a yaw moment proportional to sin(bank). It is a shortcut for
    /// something the real aeroplane gets for free: the lift vector curves the flight path,
    /// and the fin then weathervanes the nose onto it. With a proper fin term present the
    /// shortcut may be double-counting — so it is measured with and without.</summary>
    IEnumerator TestTurnCoordinationAB()
    {
        Head("TURN COORDINATION A/B   (is the synthetic yaw still earning its place?)");
        float saved = ac.turnCoordination;
        foreach (float tc in new float[] { 0f, saved })
        {
            ac.turnCoordination = tc;
            // A HELD 30 deg bank with back-pressure. Letting the bank run to 58 deg with no
            // back-pressure, as an earlier version did, puts the aeroplane in a descending
            // spiral where the turn rate is genuinely low - and compares two runs of a
            // manoeuvre neither of them was flying.
            yield return Airborne(52f);
            float g2 = 0f;
            while (Mathf.Abs(ac.RollDeg) < 29f && g2 < 8f)
            { SetHold(-0.05f, 0.5f, 0f, 0.8f); g2 += Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); }

            float hdg0 = ac.HeadingDeg;
            float bSum = 0f, vSum = 0f, sSum = 0f; int n = 0; float t = 0f;
            while (t < 5f)
            {
                float hold = Mathf.Clamp((30f - Mathf.Abs(ac.RollDeg)) / 40f, -0.35f, 0.35f);
                SetHold(-0.16f, hold, 0f, 0.8f);
                bSum += Mathf.Abs(ac.RollDeg); vSum += ac.AirspeedMs; sSum += ac.SideslipDeg; n++;
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            float bAvg = bSum / n, vAvg = vSum / n, sAvg = sSum / n;
            float measured = Mathf.Abs(Mathf.DeltaAngle(hdg0, ac.HeadingDeg)) / 5f;
            float ideal = G * Mathf.Tan(bAvg * Mathf.Deg2Rad) / Mathf.Max(1f, vAvg) * Mathf.Rad2Deg;
            report.AppendLine(string.Format(
                "   turnCoordination {0:0.000}{1}: bank {2,5:0.0}  turn {3,5:0.0} deg/s  ideal {4,5:0.0}  ratio {5:0.00}  mean sideslip {6,6:0.0} deg",
                tc, tc == 0f ? " (off)  " : " (on)   ", bAvg, measured, ideal,
                ideal > 0.01f ? measured / ideal : 0f, sAvg));
        }
        ac.turnCoordination = saved;
    }

    /// <summary>Find the roll power that gives a real light aeroplane's roll rate.
    ///
    /// The standard measure is the HELIX ANGLE pb/2V at full aileron, which for a general
    /// aviation aeroplane is about 0.07. That is speed-independent, which is why it is the
    /// figure of merit rather than a raw deg/s. b = 11.0 m for a 172.</summary>
    IEnumerator TestRollPowerSweep()
    {
        Head("ROLL POWER SWEEP   (target helix angle pb/2V = 0.07 at full aileron)");
        const float Span = 11.0f;
        float saved = ac.rollPower;
        var rb = ac.GetComponent<Rigidbody>();

        foreach (float rp in new float[] { 16000f, 20000f, 24000f })
        {
            ac.rollPower = rp;
            // Measure from a bank of -35 deg, rolling THROUGH level, so the sample window
            // sits around wings-level. Rolling up from level instead lets the bank grow
            // past 60 deg inside the window, where gravity, sideslip and the weathervane
            // all change the answer — which is why an earlier version of this sweep
            // reported 8 deg/s at a roll power that elsewhere measured 27.
            yield return Airborne(52f);
            float guard = 0f;
            while (ac.RollDeg > -35f && guard < 6f)
            { SetHold(-0.05f, -1f, 0f, 0.8f); guard += Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); }

            float pSum = 0f; int n = 0; float t = 0f;
            while (t < 1.2f)
            {
                SetHold(-0.05f, 1f, 0f, 0.8f);
                // Body-axis roll rate, sampled only while the bank is modest.
                if (Mathf.Abs(ac.RollDeg) < 40f)
                { pSum += Mathf.Abs(rb.angularVelocity.z) * Mathf.Rad2Deg; n++; }
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            float p = n > 0 ? pSum / n : 0f;
            float helix = (p * Mathf.Deg2Rad) * Span / (2f * Mathf.Max(1f, ac.AirspeedMs));
            report.AppendLine(string.Format("   rollPower {0,6:0} -> {1,5:0.0} deg/s at {2,5:0.0} m/s   helix pb/2V = {3:0.000}{4}",
                                            rp, p, ac.AirspeedMs, helix,
                                            Mathf.Abs(helix - 0.07f) < 0.015f ? "   <-- target" : ""));
        }
        ac.rollPower = saved;
        report.AppendLine("   (0.07 is the general-aviation target; below ~0.05 the aeroplane feels sluggish.)");
    }


    /// <summary>DIHEDRAL EFFECT: a sideslip must roll the aeroplane away from the slip.
    ///
    /// This is the coupling that makes a slip a slip. Rudder alone, with the stick central,
    /// should produce a bank away from the applied rudder — and if it does not, the pilot
    /// never has to hold opposite aileron, a crosswind landing needs no wing-low technique,
    /// and the aeroplane has neither of its two lateral modes.</summary>
    IEnumerator TestDihedralEffect()
    {
        Head("DIHEDRAL EFFECT   (rudder alone must roll it away from the slip)");
        float saved = ac.dihedralVolume;
        foreach (float dv in new float[] { 0f, saved })
        {
            ac.dihedralVolume = dv;
            yield return Airborne(50f);
            float r0 = ac.RollDeg;
            // Right rudder -> slip to the LEFT relative to the airflow -> roll RIGHT.
            yield return HoldFor(-0.05f, 0f, 1f, 0.8f, 3.5f);
            float dRoll = ac.RollDeg - r0;
            report.AppendLine(string.Format(
                "   dihedralVolume {0,5:0.0}{1}: 3.5 s of full right rudder -> bank {2,6:0.0} deg, sideslip {3,6:0.0} deg",
                dv, dv == 0f ? " (none)   " : " (current)", dRoll, ac.SideslipDeg));
            if (dv != 0f)
            {
                Check("rudder alone produces a bank", Mathf.Abs(dRoll) > 3f, Mathf.Abs(dRoll), "deg");
                Check("the bank is AWAY from the slip (right rudder -> right bank)",
                      Mathf.Sign(dRoll) == -Mathf.Sign(ac.SideslipDeg) || Mathf.Abs(ac.SideslipDeg) < 0.5f,
                      dRoll, "deg");
            }
        }
        ac.dihedralVolume = saved;
    }

    /// <summary>Hands off, wings level, the aeroplane must not diverge.
    ///
    /// Adding roll-from-sideslip couples the lateral axes, and a badly balanced pair of
    /// coefficients gives either a rapid spiral or a Dutch roll that never damps. This
    /// leaves it completely alone for 25 s and checks it is still flying.</summary>
    IEnumerator TestLateralStability()
    {
        Head("LATERAL STABILITY   (hands off for 25 s after a disturbance)");
        yield return Airborne(50f);
        yield return HoldFor(-0.05f, 0.35f, 0f, 0.75f, 1.0f);   // a nudge into bank

        float maxBank = 0f, maxSlip = 0f, t = 0f, nextTrace = 0f;
        while (t < 25f)
        {
            SetHold(-0.05f, 0f, 0f, 0.75f);                      // hands off in roll and yaw
            maxBank = Mathf.Max(maxBank, Mathf.Abs(ac.RollDeg));
            maxSlip = Mathf.Max(maxSlip, Mathf.Abs(ac.SideslipDeg));
            if (t >= nextTrace) { Trace("t=" + t.ToString("0")); nextTrace += 5f; }
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        report.AppendLine(string.Format("   after 25 s hands off: bank {0:0.0} deg (peak {1:0.0}), sideslip {2:0.0} deg (peak {3:0.0})",
                                        ac.RollDeg, maxBank, ac.SideslipDeg, maxSlip));
        Check("does not diverge in roll (bank stays under 80 deg)", maxBank < 80f, maxBank, "deg");
        Check("does not diverge in yaw (sideslip stays under 25 deg)", maxSlip < 25f, maxSlip, "deg");
        Check("still flying, not stalled or crashed", !ac.Crashed && !ac.Stalled, ac.AirspeedMs, "m/s");
    }


    /// <summary>Level top speed at full power.
    ///
    /// Reported rather than asserted, because it exposes a known simplification: thrust in
    /// this model is CONSTANT with airspeed (ThrustN = maxThrust * throttle). A real
    /// propeller's thrust falls roughly as power/speed, so a real 172 runs out of thrust
    /// near 124 kt. With constant thrust the aeroplane keeps accelerating until drag alone
    /// catches up, which happens well beyond anything a 172 can do.
    ///
    /// It is not fixed here: a thrust model changes take-off roll, climb rate and cruise
    /// speed in every mission, which would invalidate the timing of the whole verified
    /// bank. It is measured, written down, and left as a decision.</summary>
    IEnumerator TestTopSpeed()
    {
        Head("LEVEL TOP SPEED   (a C172S does about 124 kt / 64 m/s)");
        yield return Airborne(55f);
        float t = 0f, last = 0f;
        while (t < 45f)
        {
            // Hold level with pitch, full power, and let it run out to its own limit.
            float vsErr = (0f - ac.VerticalSpeedMs) / 5f;
            SetHold(Mathf.Clamp(-vsErr, -0.5f, 0.5f), -ac.RollDeg / 60f, 0f, 1f);
            last = ac.AirspeedMs;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        report.AppendLine(string.Format("   settles at {0:0.0} m/s ({1:0} kt) in level flight at full power", last, last * 1.944f));
        report.AppendLine(string.Format("   thrust model: CONSTANT {0:0} N at any speed (a real propeller lapses with speed)", ac.maxThrust));
        if (last * 1.944f > 145f)
            report.AppendLine("   NOTE: this is well above a 172's 124 kt. Constant thrust is the cause. "
                            + "Fixing it changes take-off, climb and cruise in every mission.");
    }

    // ── helpers ────────────────────────────────────────────────────────────────────

    void SetHold(float pitch, float roll, float yaw, float thr)
    { hPitch = pitch; hRoll = roll; hYaw = yaw; hThrottle = thr; holding = true; }

    IEnumerator HoldFor(float pitch, float roll, float yaw, float thr, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            SetHold(pitch, roll, yaw, thr);
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>Put the aeroplane in level flight at a given speed, high enough that the
    /// ground cannot interfere with a manoeuvre.</summary>
    IEnumerator Airborne(float speed)
    {
        holding = false;
        if (ctl != null) ctl.ClearOverrides();
        var gm = GameManager.Instance;
        Vector3 pos = new Vector3(0f, 1200f, 0f);
        ac.ResetTo(pos, Quaternion.identity, true, speed);
        yield return new WaitForFixedUpdate();
        // Settle briefly so the first frame's transients are not measured.
        yield return HoldFor(-0.05f, 0f, 0f, 0.75f, 1.2f);
    }


    /// <summary>Sample the handful of numbers that explain any manoeuvre. Without this a
    /// failing test says "it descended" and leaves you guessing whether the nose was down,
    /// the engine was not making power, or the wing was stalled.</summary>
    void Trace(string tag)
    {
        var sys = ac.GetComponent<AircraftSystems>();
        report.AppendLine(string.Format(
            "      {0,-6} spd {1,5:0.0}  vs {2,6:0.00}  pitch {3,6:0.0}  bank {4,6:0.0}  AoA {5,5:0.0}  thrust {6,6:0}N  power {7,4:0.00}  thr {8,4:0.00}  stalled {9}",
            tag, ac.AirspeedMs, ac.VerticalSpeedMs, ac.PitchDeg, ac.RollDeg, ac.AoADeg,
            ac.ThrustN, sys != null ? sys.EnginePower01 : 1f, ac.throttle, ac.Stalled));
    }

    // ── reporting ──────────────────────────────────────────────────────────────────

    void Head(string s) { report.AppendLine(); report.AppendLine("== " + s); }

    void Check(string name, bool ok, float value, string unit)
    {
        checks++;
        if (!ok) Failures++;
        string line = string.Format("   {0}  {1}   [{2:0.00} {3}]", ok ? "OK  " : "FAIL", name, value, unit);
        report.AppendLine(line);
        if (ok) Debug.Log("[PTEST] " + line.Trim());
        else Debug.LogError("[PTEST] " + line.Trim());
    }

    void Write()
    {
        report.AppendLine();
        report.AppendLine("checks   : " + checks);
        report.AppendLine("problems : " + Failures);
        string body = "PHYSICS TEST REPORT — does it fly like a Cessna 172?\n"
                    + "Generated " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" + report;
        foreach (string p in new[] {
            Path.Combine(Application.persistentDataPath, "physics_test_report.txt"),
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "physics_test_report.txt") })
        { try { File.WriteAllText(p, body); } catch { } }
        Debug.Log("[PTEST] done. checks=" + checks + " problems=" + Failures);
    }

    void Done()
    {
        holding = false;
        if (ctl != null) ctl.ClearOverrides();
        ControlCheckMode.Exit();
        SimDriver.Release("PhysicsTestHarness");
        Finished = true;
    }
}
