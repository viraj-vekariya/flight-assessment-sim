using System.Collections.Generic;
using UnityEngine;

public class LevelResult
{
    public bool Passed;
    public float Score;                 // 0..100
    public string Headline = "";
    public Dictionary<string, float> Metrics = new Dictionary<string, float>();
}

/// <summary>
/// Configures and scores the flight modes:
///   FreeFlight  - sandbox, no scoring.
///   L0 Baseline - start airborne trimmed, hold straight & level 60 s.
///   L1 Takeoff  - start on runway, take off and climb to target altitude.
///   L2 LevelHold- start airborne, hold assigned altitude & heading 90 s.
/// Tracks deviation from target and produces a LevelResult on completion.
/// </summary>
public class LevelManager
{
    public FlightMode Mode { get; private set; }
    public bool Measured => Mode != FlightMode.FreeFlight;
    public bool IsComplete { get; private set; }
    public LevelResult Result { get; private set; }

    public float TargetAltitude { get; private set; }
    public float TargetHeading { get; private set; }
    public float Elapsed { get; private set; }
    public float Duration { get; private set; }

    public float AltTolerance { get; private set; }
    public float HdgTolerance { get; private set; }

    CessnaPhysics phys;
    RunwayInfo runway;

    // accumulators
    float altErrInt, hdgErrInt, timeInTol, sampleTime;
    float rotationTime = -1f, reachTime = -1f, maxAlt;

    public void Init(CessnaPhysics aircraft, RunwayInfo rw)
    {
        phys = aircraft;
        runway = rw;
    }

    public void StartMode(FlightMode mode)
    {
        Mode = mode;
        IsComplete = false;
        Result = null;
        Elapsed = 0f;
        altErrInt = hdgErrInt = timeInTol = sampleTime = 0f;
        rotationTime = reachTime = -1f;
        maxAlt = 0f;
        TargetAltitude = 0f; TargetHeading = 0f;
        AltTolerance = 100f; HdgTolerance = 10f;
        Duration = 0f;

        switch (mode)
        {
            case FlightMode.FreeFlight:
            case FlightMode.L1_Takeoff:
                phys.ResetTo(runway.Start, runway.Rot, false, 0f);   // on the runway
                if (mode == FlightMode.L1_Takeoff)
                {
                    TargetAltitude = 300f; TargetHeading = 0f;
                    Duration = 120f; AltTolerance = 0f;
                }
                break;

            case FlightMode.L0_Baseline:
                phys.ResetTo(new Vector3(0f, 500f, -200f), Quaternion.identity, true, 50f);
                TargetAltitude = 500f; TargetHeading = 0f;
                Duration = 60f; AltTolerance = 50f; HdgTolerance = 5f;
                break;

            case FlightMode.L2_LevelHold:
                phys.ResetTo(new Vector3(-200f, 500f, 0f), Quaternion.Euler(0f, 90f, 0f), true, 50f);
                TargetAltitude = 500f; TargetHeading = 90f;
                Duration = 90f; AltTolerance = 100f; HdgTolerance = 10f;
                break;
        }
    }

    public void Tick(float dt)
    {
        if (!Measured || IsComplete) return;
        Elapsed += dt;
        maxAlt = Mathf.Max(maxAlt, phys.AltitudeM);

        float aErr = AltError;
        float hErr = HeadingError;
        altErrInt += aErr * dt;
        hdgErrInt += hErr * dt;
        sampleTime += dt;
        if (aErr <= AltTolerance && hErr <= HdgTolerance) timeInTol += dt;

        if (Mode == FlightMode.L1_Takeoff)
        {
            if (rotationTime < 0f && !phys.Grounded && phys.AltitudeM > runway.Start.y + 4f)
                rotationTime = Elapsed;
            if (reachTime < 0f && phys.AltitudeM >= TargetAltitude)
                reachTime = Elapsed;
            if (reachTime > 0f) Complete();
            else if (Elapsed >= Duration) Complete();
        }
        else // L0, L2 timed holds
        {
            if (Elapsed >= Duration) Complete();
        }
    }

    void Complete()
    {
        IsComplete = true;
        float t = Mathf.Max(1f, sampleTime);
        var r = new LevelResult();

        switch (Mode)
        {
            case FlightMode.L1_Takeoff:
                r.Passed = reachTime > 0f;
                r.Score = r.Passed ? Mathf.Clamp(100f - reachTime, 0f, 100f) : 0f;
                r.Headline = r.Passed ? "Airborne and climbed to target" : "Did not reach target altitude";
                r.Metrics["rotation_time_s"] = Mathf.Max(0f, rotationTime);
                r.Metrics["time_to_target_s"] = Mathf.Max(0f, reachTime);
                r.Metrics["max_altitude_m"] = maxAlt;
                break;

            default: // L0, L2
                float meanAlt = altErrInt / t;
                float meanHdg = hdgErrInt / t;
                float inTolPct = 100f * timeInTol / t;
                r.Score = inTolPct;
                r.Passed = inTolPct >= 70f;
                r.Headline = r.Passed ? "Target held well" : "Drifted off target too much";
                r.Metrics["mean_alt_error_m"] = meanAlt;
                r.Metrics["mean_hdg_error_deg"] = meanHdg;
                r.Metrics["time_in_tolerance_pct"] = inTolPct;
                break;
        }
        Result = r;
    }

    public float AltError => phys != null ? Mathf.Abs(phys.AltitudeM - TargetAltitude) : 0f;
    public float HeadingError => phys != null ? Mathf.Abs(Mathf.DeltaAngle(phys.HeadingDeg, TargetHeading)) : 0f;
    public float TimeRemaining => Mathf.Max(0f, Duration - Elapsed);

    public string StatusText()
    {
        if (!Measured) return "FREE FLIGHT  —  fly anywhere.  R = back to runway";
        switch (Mode)
        {
            case FlightMode.L1_Takeoff:
                return $"L1 TAKEOFF  —  climb to {TargetAltitude:F0} m   (alt {phys.AltitudeM:F0} m)";
            case FlightMode.L0_Baseline:
                return $"L0 BASELINE  —  hold {TargetAltitude:F0} m / {TargetHeading:F0}°   " +
                       $"in-tol {(100f * timeInTol / Mathf.Max(1f, sampleTime)):F0}%   t-{TimeRemaining:F0}s";
            case FlightMode.L2_LevelHold:
                return $"L2 LEVEL HOLD  —  {TargetAltitude:F0} m (±{AltTolerance:F0}) / {TargetHeading:F0}° (±{HdgTolerance:F0})   " +
                       $"in-tol {(100f * timeInTol / Mathf.Max(1f, sampleTime)):F0}%   t-{TimeRemaining:F0}s";
        }
        return "";
    }
}
