// Data model for the scenario engine. A "level" is a Scenario: a start condition,
// a goal, a difficulty, target alt/heading + tolerances, an ordered list of timed
// events, and (for navigation) waypoints. New levels are DATA (see ScenarioLibrary),
// not new code — the engine interprets these.

using System.Collections.Generic;
using UnityEngine;

public enum ScenarioStart { Runway, Airborne }

public enum ScenarioGoal
{
    HoldTargets,    // hold the (possibly changing) target alt/heading for the duration
    Navigate,       // fly through a sequence of waypoints
    Land,           // fly the approach and land on the runway
    TakeoffClimb,   // take off and climb to the target altitude
    Mission,        // takeoff -> waypoints -> land
    TaxiTakeoff     // parking stand -> taxi route -> hold short -> line up -> take off -> climb
}

public enum ScenarioEventType
{
    Message,          // a banner/instruction
    HeadingChange,    // ATC: new target heading (Value)
    AltitudeChange,   // ATC: new target altitude (Value)
    Alarm,            // warning that must be acknowledged (SPACE) -> reaction time
    InstrumentFault,  // a gauge freezes for Duration; acknowledge (SPACE)
    Weather,          // turbulence + low-visibility for Duration, intensity = Value
    Distraction,      // a peripheral cue to respond to (SPACE) -> reaction time
    Decision,         // a timed choice: respond (SPACE) within ResponseWindow

    // ---- added for the 12-mission cognitive-load experiment -------------------
    SystemFailure,    // arm a real abnormality on AircraftSystems (Failure/Severity)
    Checklist,        // present a checklist/drill (ChecklistId) -> procedural load
    Probe,            // secondary-task probe (spare capacity / P300); RequiresResponse
    Traffic,          // conflicting traffic call -> visual search + decision
    GoAround,         // ATC/own-ship go-around command
    ConfigCall,       // "flaps 10", "carb heat on" -> a configuration action is due
    Readback          // ATC clearance the pilot must read back correctly (WM load)
}

public class ScenarioEvent
{
    public float Time;                  // seconds from scenario start
    public ScenarioEventType Type;
    public float Duration = 0f;         // faults / weather
    public float Value = 0f;            // heading / altitude / intensity
    public int Gauge = -1;              // gauge index for InstrumentFault (-1 = random)
    public string Label = "";
    public bool RequiresResponse = false;
    public float ResponseWindow = 3f;   // seconds allowed to respond

    // ---- experiment extensions -------------------------------------------------
    /// <summary>Abnormality to arm on AircraftSystems (SystemFailure events).</summary>
    public FailureKind Failure = FailureKind.None;
    /// <summary>0..1 how bad the abnormality is.</summary>
    public float Severity = 1f;
    /// <summary>Which checklist/drill to present (Checklist events).</summary>
    public string ChecklistId = "";
    /// <summary>Seconds of uniform random jitter applied to Time at trial start.
    /// Non-zero on every load-inducing event so a participant who repeats a mission
    /// cannot learn WHEN the failure arrives — the single biggest threat to a
    /// startle/surprise manipulation. Drawn from the trial's seeded RNG, so the
    /// schedule is still exactly reproducible from the logged seed.</summary>
    public float Jitter = 0f;
    /// <summary>Resolved firing time after jitter (filled in by the engine).</summary>
    public float ScheduledTime;
    /// <summary>What the traffic does, for Traffic events that spawn a visible aircraft.
    /// Ignored when SpawnsTraffic is false (a radio call with no aeroplane).</summary>
    public TrafficBehaviour Traffic = TrafficBehaviour.CrossingRunway;
    public bool SpawnsTraffic = false;
}

public class Waypoint
{
    public Vector3 Pos;
    public float Radius = 150f;
    public string Name = "WPT";

    public Waypoint(Vector3 pos, string name, float radius = 150f)
    {
        Pos = pos; Name = name; Radius = radius;
    }
}

public class Scenario
{
    /// <summary>The experiment mission this Scenario was built from (null for the
    /// legacy free-flight / campaign scenarios). Carries the workload class, the
    /// pre-registered predictions and the success criteria.</summary>
    public MissionDefinition Mission;

    public string Id = "", Title = "", Desc = "";
    public ScenarioStart Start = ScenarioStart.Airborne;
    public ScenarioGoal Goal = ScenarioGoal.HoldTargets;
    public float Difficulty = 0.5f;     // 0 = easy .. 1 = hard
    public int CampaignStage = 0;       // >0 if this scenario is a campaign level (for progress)

    public float Duration = 90f;
    public float TargetAltitude = 500f, TargetHeading = 90f;
    public float AltTolerance = 100f, HdgTolerance = 10f;

    public List<ScenarioEvent> Events = new List<ScenarioEvent>();
    public List<Waypoint> Waypoints = new List<Waypoint>();

    // Optional per-level scoring-weight override (component name -> weight). Empty
    // = use the default weights for this goal (see ScoringRubric.Weights).
    public Dictionary<string, float> Weights = new Dictionary<string, float>();

    public string DiffLabel =>
        Difficulty < 0.34f ? "EASY" : Difficulty < 0.67f ? "MEDIUM" : "HARD";
}

/// <summary>One transparent scoring component: a 0..100 raw sub-score and its
/// (normalized) weight. Contribution = Raw * Weight; the total score is the sum.</summary>
public class ScoreLine
{
    public string Name;
    public float Raw;       // 0..100
    public float Weight;    // normalized, components sum to 1
    public ScoreLine(string name, float raw, float weight) { Name = name; Raw = raw; Weight = weight; }
    public float Contribution => Raw * Weight;
}

public class ScenarioResult
{
    public bool Passed;
    public float Score;                 // 0..100 (sum of component contributions)
    public string Headline = "";
    public string Outcome = "COMPLETE"; // COMPLETE / CRASHED / FAILED
    public Dictionary<string, float> Metrics = new Dictionary<string, float>();
    public List<ScoreLine> Breakdown = new List<ScoreLine>();   // transparent score components
    public string SessionLabel = "", SessionCond = "";          // for the session report
}
