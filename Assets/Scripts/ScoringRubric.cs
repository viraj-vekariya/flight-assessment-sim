using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single, transparent scoring rubric for every scenario. Each component maps
/// a logged metric to a 0..100 sub-score; default per-goal weights say which
/// components apply and how much. Everything here is tunable data (named constants)
/// — no hidden magic numbers — and every input is already in the CSV log, so a
/// score is reproducible from the data (sets up Phase-4 EEG correlation).
/// </summary>
public static class ScoringRubric
{
    // ---- component sub-scores (0..100) ----
    public static float Precision(float timeInTolPct) => Mathf.Clamp(timeInTolPct, 0f, 100f);

    // 300 ms -> 100, 1500 ms -> 0 (no responses required -> full marks)
    public static float Reaction(float meanRtMs) =>
        meanRtMs <= 0f ? 100f : Mathf.Clamp(100f - (meanRtMs - 300f) / 12f, 0f, 100f);

    // each miss / false-alarm costs 15 points
    public static float Errors(int misses, int falseAlarms) =>
        Mathf.Clamp(100f - (misses + falseAlarms) * 15f, 0f, 100f);

    public static float Completion(int reached, int total) =>
        total <= 0 ? 100f : 100f * reached / total;

    // control smoothness: low input "jerk" per second scores high (0.5/s -> ~80)
    public static float Smoothness(float jerkPerSec) => Mathf.Clamp(100f - jerkPerSec * 40f, 0f, 100f);

    public static float Landing(LandingTier t)
    {
        switch (t)
        {
            case LandingTier.Perfect: return 100f;
            case LandingTier.Good: return 85f;
            case LandingTier.Acceptable: return 65f;
            default: return 0f;   // hard landing / destroyed / ditched / no landing
        }
    }

    // ---- default component weights per goal (tunable) ----
    public static Dictionary<string, float> Weights(ScenarioGoal goal, bool hasEvents)
    {
        var w = new Dictionary<string, float>();
        switch (goal)
        {
            case ScenarioGoal.Navigate:
                w["completion"] = 0.70f; w["smoothness"] = 0.30f; break;
            case ScenarioGoal.Land:
                w["landing"] = 0.70f; w["smoothness"] = 0.30f; break;
            case ScenarioGoal.Mission:
                w["completion"] = 0.40f; w["landing"] = 0.40f; w["smoothness"] = 0.20f; break;
            case ScenarioGoal.TakeoffClimb:
                w["completion"] = 0.60f; w["smoothness"] = 0.40f; break;
            default: // HoldTargets / TakeoffClimb
                if (hasEvents) { w["precision"] = 0.45f; w["reaction"] = 0.25f; w["errors"] = 0.15f; w["smoothness"] = 0.15f; }
                else { w["precision"] = 0.70f; w["smoothness"] = 0.30f; }
                break;
        }
        return w;
    }
}
