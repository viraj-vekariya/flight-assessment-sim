// EventMarkers — the ONE canonical marker vocabulary for the experiment.
//
// Why a file of string constants: every marker written to events.csv, pushed to the
// LSL outlet, and asserted by the test harness comes from here. Nothing is spelled
// out ad hoc at a call site, so an EEG analysis script can rely on an exact, closed
// set of tags, and a typo can never silently produce an unmatched epoch label.
//
// EPOCHING CONTRACT (this is the part that matters for EEG)
//   * Every marker carries THREE clocks (see ExperimentLogger): mission time, a
//     monotonic host clock, and UTC wall clock — plus the LSL clock when the outlet
//     is live.
//   * TRIGGER_ARMED is when the SIMULATION injected the abnormality.
//     CUE_ONSET is when the pilot could FIRST PERCEIVE it.
//     They are different events and are logged separately, because for a gradual
//     failure (carburettor ice) they can be 30 s apart, and epoch zero for a
//     startle/ERP analysis must be CUE_ONSET, never TRIGGER_ARMED.
//   * PILOT_FIRST_RESPONSE is the first control or switch action after CUE_ONSET
//     that exceeds a deadband, so response latency is measurable without relying
//     on the participant pressing an "I noticed" key.

public static class EventMarkers
{
    // ---- session / block structure ----
    public const string SessionStart      = "SESSION_START";
    public const string SessionEnd        = "SESSION_END";
    public const string BaselineStart     = "BASELINE_START";
    public const string BaselineEnd       = "BASELINE_END";
    public const string BriefStart        = "BRIEF_START";
    public const string BriefEnd          = "BRIEF_END";

    // ---- mission lifecycle ----
    public const string MissionStart      = "MISSION_START";
    public const string MissionEnd        = "MISSION_END";
    public const string MissionSuccess    = "MISSION_SUCCESS";
    public const string MissionFailure    = "MISSION_FAILURE";
    public const string MissionAbort      = "MISSION_ABORT";
    public const string MissionRestart    = "MISSION_RESTART";
    public const string PhaseChange       = "PHASE_CHANGE";

    // ---- the load manipulation ----
    public const string TriggerArmed      = "TRIGGER_ARMED";       // sim injected it
    public const string CueOnset          = "CUE_ONSET";           // pilot could perceive it
    public const string WarningAppears    = "WARNING_APPEARS";
    public const string PilotFirstResponse= "PILOT_FIRST_RESPONSE";
    public const string FailureResolved   = "FAILURE_RESOLVED";
    public const string RecoveryStart     = "RECOVERY_START";

    // ---- procedural work ----
    public const string ChecklistStart    = "CHECKLIST_START";
    public const string ChecklistItem     = "CHECKLIST_ITEM";
    public const string ChecklistComplete = "CHECKLIST_COMPLETE";
    public const string ChecklistTimeout  = "CHECKLIST_TIMEOUT";

    // ---- communication / navigation ----
    public const string AtcMessage        = "ATC_MESSAGE";
    public const string AtcReadbackOk     = "ATC_READBACK_OK";
    public const string AtcReadbackMiss   = "ATC_READBACK_MISS";
    public const string TargetChange      = "TARGET_CHANGE";       // new alt / hdg
    public const string Waypoint          = "WAYPOINT";

    // ---- discrete probes (secondary task -> spare capacity / P300) ----
    public const string ProbeOnset        = "PROBE_ONSET";
    public const string ProbeHit          = "PROBE_HIT";
    public const string ProbeMiss         = "PROBE_MISS";
    public const string ProbeFalseAlarm   = "PROBE_FALSE_ALARM";

    // ---- decisions ----
    public const string DecisionPrompt    = "DECISION_PROMPT";
    public const string DecisionMade      = "DECISION_MADE";
    public const string DecisionExpired   = "DECISION_EXPIRED";

    // ---- aircraft state / environment ----
    public const string ConfigChange      = "CONFIGURATION_CHANGE"; // flaps / carb heat / selector...
    public const string WeatherOnset      = "WEATHER_ONSET";
    public const string WeatherEnd        = "WEATHER_END";
    public const string TrafficOnset      = "TRAFFIC_ONSET";
    public const string GoAroundCommanded = "GO_AROUND_COMMANDED";
    public const string GoAroundInitiated = "GO_AROUND_INITIATED";
    public const string Touchdown         = "TOUCHDOWN";
    public const string Crash             = "CRASH";
    public const string Stall             = "STALL";

    // ---- physical cockpit interaction ----
    // State TRANSITIONS only. Continuous control POSITION lives in the 50 Hz telemetry;
    // putting it here as well would bury the meaningful events under motor noise.
    public const string ControlGrab       = "CONTROL_GRAB";      // detail = control id
    public const string ControlRelease    = "CONTROL_RELEASE";   // detail = control id
    public const string FlapSelected      = "FLAP_SELECTED";     // detail = detent + label
    public const string TrimChanged       = "TRIM_CHANGED";      // debounced
    public const string ThrottleChanged   = "THROTTLE_CHANGED";  // debounced
    public const string BrakeApplied      = "BRAKE_APPLIED";
    public const string BrakeReleased     = "BRAKE_RELEASED";

    // ---- self-report ----
    public const string TlxStart          = "TLX_START";
    public const string TlxSubmit         = "TLX_SUBMIT";

    /// <summary>Every marker tag, for the test harness and for the analysis-side
    /// schema check. Keep in sync when adding a marker above.</summary>
    public static readonly string[] All =
    {
        SessionStart, SessionEnd, BaselineStart, BaselineEnd, BriefStart, BriefEnd,
        MissionStart, MissionEnd, MissionSuccess, MissionFailure, MissionAbort, MissionRestart, PhaseChange,
        TriggerArmed, CueOnset, WarningAppears, PilotFirstResponse, FailureResolved, RecoveryStart,
        ChecklistStart, ChecklistItem, ChecklistComplete, ChecklistTimeout,
        AtcMessage, AtcReadbackOk, AtcReadbackMiss, TargetChange, Waypoint,
        ProbeOnset, ProbeHit, ProbeMiss, ProbeFalseAlarm,
        DecisionPrompt, DecisionMade, DecisionExpired,
        ControlGrab, ControlRelease, FlapSelected, TrimChanged, ThrottleChanged,
        BrakeApplied, BrakeReleased,
        ConfigChange, WeatherOnset, WeatherEnd, TrafficOnset,
        GoAroundCommanded, GoAroundInitiated, Touchdown, Crash, Stall,
        TlxStart, TlxSubmit
    };
}
