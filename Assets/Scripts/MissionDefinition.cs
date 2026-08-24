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
    /// <summary>Baseline turbulence 0..1 present for the WHOLE mission (0 = calm).</summary>
    public float AmbientTurbulence = 0f;
    /// <summary>Crosswind component, m/s, positive = from the right.</summary>
    public float CrosswindMs = 0f;
    /// <summary>0 = CAVOK, 1 = minimum visibility used in the study.</summary>
    public float Visibility01 = 0f;

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
