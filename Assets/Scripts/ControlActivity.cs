// ControlActivity — how much the pilot's HANDS were doing, as distinct from how much
// their MIND was doing.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHY THIS IS NOT OPTIONAL
// ═══════════════════════════════════════════════════════════════════════════════
// The cognitive axis works by holding manual demand constant within a phase row, so
// that an EEG difference between workload classes cannot be muscle activity. That is a
// DESIGN claim, and a design claim about the human is only as good as the measurement
// that checks it. A participant can be given a motor-matched pair of missions and still
// move very differently in them — because they are anxious, because they are
// over-controlling, or because the mission's events happened to arrive while their hand
// was on the yoke.
//
// The crosswind axis needs it even more directly: there, manual demand is deliberately
// the thing being manipulated, so "EEG went up" is uninterpretable without knowing how
// much the control activity went up alongside it.
//
// So control activity is measured on the same 50 Hz physics clock as everything else,
// and is written into performance.json for every trial. Three quantities, chosen
// because they answer three different questions:
//
//   RATE      — mean |d(input)/dt| summed over the three axes. "How fast were the
//               controls being moved?" This is the closest thing to a proxy for EMG
//               and movement artifact.
//   ACTIVITY  — the time integral of that, i.e. total control travel over the trial.
//               "How much movement was there in total?" Rate can be equal while total
//               differs, if one mission is busy in bursts and another is busy throughout.
//   VARIABILITY — the standard deviation of each input about its own trial mean.
//               "How steady were the controls held?" This is the one that separates a
//               pilot holding a large steady correction (a crosswind crab: high
//               deflection, LOW variability) from a pilot chasing the aeroplane
//               (over-control: similar deflection, HIGH variability). Those two are the
//               same on a rate measure and are not the same behaviour at all.
//
// A fourth quantity, the fraction of the trial with a hand physically on a cockpit
// control, comes free from the existing `control_held` telemetry column and is
// summarised here too.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHAT IT IS FOR, IN THE ANALYSIS
// ═══════════════════════════════════════════════════════════════════════════════
//   * As a MANIPULATION CHECK on the cognitive axis: within a phase row, the three
//     classes should NOT differ much on these. If they do, the motor matching failed
//     for that row and the row's EEG contrast must be reported with that caveat.
//   * As a COVARIATE on the psychomotor axis, where they are expected to differ and
//     the question is whether the EEG effect survives adjusting for them.
//   * As part of the VARIANT EXCHANGEABILITY evidence: two variants of one cell that
//     produce materially different control activity are not interchangeable, whatever
//     their workload profiles claim.

using UnityEngine;

public class ControlActivity
{
    // Running accumulators, advanced on the physics step.
    float prevPitch, prevRoll, prevYaw, prevThrottle;
    bool primed;

    double travelPitch, travelRoll, travelYaw, travelThrottle;   // integral of |d input|
    double sumPitch, sumRoll, sumYaw;                            // for the means
    double sumSqPitch, sumSqRoll, sumSqYaw;                      // for the SDs
    int n;
    float elapsed;
    int heldSamples;

    public void Reset()
    {
        primed = false;
        travelPitch = travelRoll = travelYaw = travelThrottle = 0.0;
        sumPitch = sumRoll = sumYaw = 0.0;
        sumSqPitch = sumSqRoll = sumSqYaw = 0.0;
        n = 0; elapsed = 0f; heldSamples = 0;
    }

    /// <summary>One physics-step sample. `held` is true when a cockpit control is
    /// physically in the pilot's hand this step.</summary>
    public void Sample(CessnaPhysics ac, float dt, bool held)
    {
        if (ac == null || dt <= 0f) return;
        float p = ac.pitchInput, r = ac.rollInput, y = ac.yawInput, t = ac.Throttle01;

        if (primed)
        {
            travelPitch += Mathf.Abs(p - prevPitch);
            travelRoll += Mathf.Abs(r - prevRoll);
            travelYaw += Mathf.Abs(y - prevYaw);
            travelThrottle += Mathf.Abs(t - prevThrottle);
        }
        prevPitch = p; prevRoll = r; prevYaw = y; prevThrottle = t;
        primed = true;

        sumPitch += p; sumRoll += r; sumYaw += y;
        sumSqPitch += (double)p * p; sumSqRoll += (double)r * r; sumSqYaw += (double)y * y;
        n++; elapsed += dt;
        if (held) heldSamples++;
    }

    static double Sd(double sum, double sumSq, int n)
    {
        if (n < 2) return 0.0;
        double mean = sum / n;
        double var = sumSq / n - mean * mean;
        return var > 0.0 ? System.Math.Sqrt(var) : 0.0;
    }

    /// <summary>Total control travel over the trial, in units of full deflection,
    /// summed over pitch, roll and yaw. Throttle is reported separately because it is a
    /// different muscle group and a different kind of action.</summary>
    public double TravelStick => travelPitch + travelRoll + travelYaw;
    public double TravelThrottle => travelThrottle;

    /// <summary>Mean rate of control movement, full-deflections per second.</summary>
    public double RateStick => elapsed > 0.01f ? TravelStick / elapsed : 0.0;

    /// <summary>Standard deviation of each input about its own trial mean. High values
    /// mean the controls were being CHASED; a large steady correction shows as a large
    /// mean with a small SD.</summary>
    public double SdPitch => Sd(sumPitch, sumSqPitch, n);
    public double SdRoll => Sd(sumRoll, sumSqRoll, n);
    public double SdYaw => Sd(sumYaw, sumSqYaw, n);
    public double SdStick => SdPitch + SdRoll + SdYaw;

    /// <summary>Mean deflection of each axis — the steady component.</summary>
    public double MeanPitch => n > 0 ? sumPitch / n : 0.0;
    public double MeanRoll => n > 0 ? sumRoll / n : 0.0;
    public double MeanYaw => n > 0 ? sumYaw / n : 0.0;

    /// <summary>Fraction of the trial with a hand on a physical cockpit control.</summary>
    public double HeldFraction => n > 0 ? (double)heldSamples / n : 0.0;

    public int Samples => n;
    public float ElapsedS => elapsed;

    /// <summary>Write the covariates into a metrics dictionary, using the same names the
    /// analysis and the variant-equivalence report expect.</summary>
    public void WriteInto(System.Collections.Generic.Dictionary<string, float> m)
    {
        if (m == null) return;
        m["ctrl_travel_stick"] = (float)TravelStick;
        m["ctrl_travel_throttle"] = (float)TravelThrottle;
        m["ctrl_rate_stick"] = (float)RateStick;
        m["ctrl_sd_pitch"] = (float)SdPitch;
        m["ctrl_sd_roll"] = (float)SdRoll;
        m["ctrl_sd_yaw"] = (float)SdYaw;
        m["ctrl_sd_stick"] = (float)SdStick;
        m["ctrl_mean_pitch"] = (float)MeanPitch;
        m["ctrl_mean_roll"] = (float)MeanRoll;
        m["ctrl_mean_yaw"] = (float)MeanYaw;
        m["ctrl_held_fraction"] = (float)HeldFraction;
        m["ctrl_samples"] = n;
    }
}
