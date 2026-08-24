// MissionDefinition — the full experimental specification of ONE mission.
//
// This is the data structure PHASE 4 of the design asks for: everything a reader
// needs to reproduce the trial, plus the pre-registered predictions the experiment
// is meant to test. MissionLibrary.cs fills in twelve of these; nothing about a
// mission is hard-coded in a MonoBehaviour.
//
// It deliberately holds BOTH:
//   * the machine-readable trial definition (initial conditions, event schedule,
//     success criteria) that the simulator executes, and
//   * the human-readable analysis (workload rationale, expected errors, EEG
//     relevance) that goes into MISSION_DESIGN.md and metadata.json.
// Keeping them in one object is what stops the documentation drifting away from
// what the simulator actually does — the docs are GENERATED from this file.

using System.Collections.Generic;
using UnityEngine;

public enum FlightPhase { Preflight, Takeoff, Climb, Cruise, Descent, Approach, Landing, Circuit }

public class MissionDefinition
{
    // ---------------- identity ----------------
    public string Id = "";                 // L1..L4, M1..M4, H1..H4
    public string Name = "";
    public WorkloadClass Class = WorkloadClass.Low;
    public FlightPhase Phase = FlightPhase.Cruise;

    // ---------------- initial conditions ----------------
    public ScenarioStart Start = ScenarioStart.Airborne;
    public ScenarioGoal Goal = ScenarioGoal.HoldTargets;
    /// <summary>Start position in world metres. Ignored when Start == Runway.</summary>
    public Vector3 StartPos = new Vector3(0f, 700f, -200f);
    public float StartAltitudeM = 700f;
    public float StartAirspeedKmh = 190f;
    public float StartHeadingDeg = 0f;
    public float TargetAltitudeM = 700f;
    public float TargetHeadingDeg = 0f;
    public float AltToleranceM = 100f;
    public float HdgToleranceDeg = 12f;

    // ---------------- environment ----------------
    /// <summary>Baseline turbulence 0..1 present for the WHOLE mission (0 = calm).
    /// Turbulence is a ZERO-MEAN disturbance: it makes the aeroplane wobble but does
    /// not change where it goes. For a manipulation that changes the TASK, use wind.</summary>
    public float AmbientTurbulence = 0f;
    /// <summary>0 = CAVOK, 1 = minimum visibility used in the study.</summary>
    public float Visibility01 = 0f;

    // ---------------- wind (see WindModel.cs) ----------------
    // Specified meteorologically — direction the wind blows FROM, and the FREE-STREAM
    // speed at 300 m. The surface wind a pilot would be given follows from the
    // boundary-layer profile, so briefings and ATIS text are generated, never typed.
    // Author these with SetWind(crosswindMs, headwindMs) rather than by hand.
    /// <summary>Direction the wind blows FROM, degrees true. 0 with no wind.</summary>
    public float WindFromDeg = 0f;
    /// <summary>Free-stream steady wind speed at WindModel.RefHeightM, m/s. 0 = calm.</summary>
    public float WindSpeedMs = 0f;
    /// <summary>Peak gust excursion about the steady vector, m/s. A gusty crosswind is
    /// a materially harder task than a steady one of the same mean, because the
    /// correction cannot be set once and left.</summary>
    public float WindGustMs = 0f;
    /// <summary>Altitude (m) of a discrete shear layer, 0 = none. Used by the
    /// windshear-on-final missions.</summary>
    public float WindShearAltM = 0f;
    /// <summary>Wind-speed change across the shear layer, m/s (+ = stronger above).</summary>
    public float WindShearDeltaMs = 0f;
    /// <summary>Wind-direction change across the shear layer, degrees.</summary>
    public float WindShearDeltaDeg = 0f;

    /// <summary>Author the wind in the terms the workload argument is actually about:
    /// how much crosswind (positive = from the right) and how much headwind (negative
    /// = a tailwind) the pilot has on the runway. `runwayHeadingDeg` defaults to 010,
    /// the study's single runway.</summary>
    public MissionDefinition SetWind(float crosswindMs, float headwindMs, float gustMs = 0f,
                                     float runwayHeadingDeg = 0f)
    {
        WindFromDeg = WindModel.DirectionFor(runwayHeadingDeg, crosswindMs, headwindMs, out float spd);
        WindSpeedMs = spd;
        WindGustMs = gustMs;
        return this;
    }

    // ---------------- aircraft configuration & systems ----------------
    public float StartFlaps01 = 0f;
    public float StartFuelL = 180f;
    /// <summary>Systems that are already unserviceable at engine start (rare — most
    /// abnormalities are injected in flight by a SystemFailure event).</summary>
    public List<FailureKind> PreexistingFailures = new List<FailureKind>();

    // ---------------- the trial ----------------
    /// <summary>Nominal mission duration in seconds. HELD CONSTANT ACROSS CLASSES —
    /// see EXPERIMENT_PROTOCOL.md §"Duration control". A mission that can end early
    /// (a landing) still has this as its hard ceiling.</summary>
    public float DurationS = 300f;
    /// <summary>Seconds of quiet, un-manipulated flight at the head of the mission.
    /// This is the WITHIN-MISSION baseline: the EEG reference the load segment is
    /// contrasted against, recorded under identical visuals and identical manual
    /// control so the contrast is the manipulation and nothing else.</summary>
    public float InTaskBaselineS = 60f;
    public List<ScenarioEvent> Events = new List<ScenarioEvent>();
    public List<Waypoint> Waypoints = new List<Waypoint>();

    // ---------------- pilot-facing text ----------------
    public string Objective = "";
    public string Brief = "";

    // ---------------- experimental analysis (pre-registered) ----------------
    public WorkloadProfile Profile = new WorkloadProfile();
    public ExpectedTlx Expected = new ExpectedTlx();
    public string LoadRationale = "";        // WHY this class, in prose
    public string EegRelevance = "";         // what the EEG analysis should see & why
    public string ExpectedErrors = "";       // what a participant plausibly gets wrong
    /// <summary>When true, an arrival that the pilot survives under control counts
    /// as success even if it was not on the runway. Set for the forced-landing
    /// mission, where insisting on the runway would reward exactly the behaviour
    /// FAA-H-8083-3C warns against ("desire to save the airplane" / stretching the
    /// glide) — the correct airmanship answer there can be a field.</summary>
    public bool SurvivalIsSuccess = false;
    public string SuccessCriteria = "";
    public string FailureConditions = "";
    public string AviationBasis = "";        // the real-world source for the abnormality
    /// <summary>Anything the simulator cannot model faithfully for this mission.
    /// Printed into metadata.json so no analysis can quietly forget it.</summary>
    public string Approximations = "";
    /// <summary>Marker tags this mission MUST emit. The test harness asserts them.</summary>
    public string[] RequiredMarkers = new string[0];

    // ---------------- helpers ----------------
    public string ClassTag => Class.ToString().ToUpper();

    /// <summary>Crosswind on the study's runway, m/s, positive = from the right.
    /// Derived, never stored, so it can never disagree with the wind that is flown.</summary>
    public float CrosswindMs =>
        WindModel.Crosswind(WindFromDeg, WindSpeedMs, Aerodrome.RunwayHeadingDeg);
    /// <summary>Headwind on the study's runway, m/s. Negative = a tailwind.</summary>
    public float HeadwindMs =>
        WindModel.Headwind(WindFromDeg, WindSpeedMs, Aerodrome.RunwayHeadingDeg);
    /// <summary>ATIS-style wind for briefings and documentation, e.g. "310/14G20 kt".</summary>
    public string WindReport => WindModel.Report(WindFromDeg, WindSpeedMs, WindGustMs);

    /// <summary>Build the runnable Scenario the existing engine executes. All of the
    /// experiment metadata rides along on Scenario.Mission.</summary>
    public Scenario ToScenario()
    {
        var s = new Scenario
        {
            Mission = this,
            Id = Id,
            Title = Id + " — " + Name,
            Desc = Brief,
            Start = Start,
            Goal = Goal,
            Difficulty = Class == WorkloadClass.Low ? 0.15f : Class == WorkloadClass.Medium ? 0.5f : 0.85f,
            Duration = DurationS,
            TargetAltitude = TargetAltitudeM,
            TargetHeading = TargetHeadingDeg,
            AltTolerance = AltToleranceM,
            HdgTolerance = HdgToleranceDeg,
        };
        foreach (var e in Events) s.Events.Add(e);
        foreach (var w in Waypoints) s.Waypoints.Add(w);
        return s;
    }

    // ---- terse builders used by MissionLibrary so the mission table stays readable ----
    public static ScenarioEvent Msg(float t, string label) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Message, Label = label };

    public static ScenarioEvent Atc(float t, ScenarioEventType type, float value, string label, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = type, Value = value, Label = label, Jitter = jitter };

    public static ScenarioEvent Readback(float t, string label, float window, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Readback, Label = label,
                            RequiresResponse = true, ResponseWindow = window, Jitter = jitter };

    public static ScenarioEvent Wx(float t, float dur, float intensity, string label, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Weather, Duration = dur, Value = intensity,
                            Label = label, Jitter = jitter };

    public static ScenarioEvent Fail(float t, FailureKind kind, float severity, string label, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.SystemFailure, Failure = kind,
                            Severity = severity, Label = label, Jitter = jitter };

    public static ScenarioEvent List(float t, string checklistId, string label) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Checklist, ChecklistId = checklistId, Label = label };

    public static ScenarioEvent Probe(float t, string label, float window, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Probe, Label = label,
                            RequiresResponse = true, ResponseWindow = window, Jitter = jitter };

    public static ScenarioEvent Decide(float t, string label, float window, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Decision, Label = label,
                            RequiresResponse = true, ResponseWindow = window, Jitter = jitter };

    public static ScenarioEvent Cfg(float t, string label) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.ConfigCall, Label = label };

    /// <summary>A traffic call the pilot must acknowledge, with no aeroplane drawn.</summary>
    public static ScenarioEvent Trfc(float t, string label, float window, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Traffic, Label = label,
                            RequiresResponse = true, ResponseWindow = window, Jitter = jitter };

    /// <summary>Traffic the pilot can actually SEE: spawns a visible aircraft flying a
    /// scripted path. `respond` false means it is there to be looked at and reasoned
    /// about rather than acknowledged with a keypress.</summary>
    public static ScenarioEvent Traffic(float t, TrafficBehaviour behaviour, string label,
                                        bool respond = false, float window = 6f, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.Traffic, Label = label,
                            Traffic = behaviour, SpawnsTraffic = true,
                            RequiresResponse = respond, ResponseWindow = window, Jitter = jitter };

    /// <summary>ATC take-off clearance. Releasing the hold-short gate requires this
    /// (or a Readback whose text contains "CLEARED FOR TAKE").</summary>
    public static ScenarioEvent TakeoffClearance(float t, string label, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.ConfigCall,
                            Label = "CLEARED FOR TAKE-OFF RUNWAY 01 — " + label, Jitter = jitter };

    public static ScenarioEvent Around(float t, string label, float jitter = 0f) =>
        new ScenarioEvent { Time = t, Type = ScenarioEventType.GoAround, Label = label, Jitter = jitter };
}
