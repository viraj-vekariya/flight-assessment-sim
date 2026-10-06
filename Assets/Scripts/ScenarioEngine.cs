// ScenarioEngine — runs one trial.
//
// Sets the aircraft and its systems up, schedules the (jittered) event list, fires
// the events, applies their effects, runs any checklist drill, captures reaction
// times and tracking error, emits the standardised event markers, streams telemetry,
// and restores everything cleanly on end/abort/restart.
//
// TWO MODES, ONE ENGINE
//   * EXPERIMENT scenarios (Scenario.Mission != null) come from MissionLibrary. They
//     get the full treatment: seeded jitter, in-task baseline segmentation, systems
//     failures, checklists, the structured ExperimentLogger output tree.
//   * LEGACY scenarios (Scenario.Mission == null) are the older ScenarioLibrary /
//     Campaign levels. They keep working exactly as before, writing the old
//     flat-CSV ScenarioLogger output. Nothing about the campaign was broken to add
//     the experiment.
//
// SCORING IS NOT THE POINT. The engine still computes the legacy score (the campaign
// needs it), but for an experiment mission the score is NEVER shown to the
// participant, because a visible score would bias how they fly the rest of the
// session. What the experiment consumes is the telemetry, the markers, the
// objective performance metrics and the participant's own NASA-TLX.

using System.Collections.Generic;
using UnityEngine;

public class ScenarioEngine : MonoBehaviour
{
    // ---- state read by the HUD / GameManager / test harness ---------------------
    public bool Active { get; private set; }
    public bool IsComplete { get; private set; }
    public ScenarioResult Result { get; private set; }
    public Scenario Current { get; private set; }
    public float Time01 { get; private set; }   // scenario elapsed seconds
    public string DataFile { get; private set; }

    public float CurTargetAlt { get; private set; }
    public float CurTargetHdg { get; private set; }
    public float AltError { get; private set; }
    public float HdgError { get; private set; }
    public bool WeatherActive { get; private set; }
    public bool FaultActive { get; private set; }

    public string Banner { get; private set; } = "";
    public float BannerUntil { get; private set; }
    public bool AlarmActive { get; private set; }
    public string AlarmLabel { get; private set; } = "";
    public bool HasWaypoint { get; private set; }
    public Vector3 WaypointPos { get; private set; }
    public string WaypointName { get; private set; } = "";
    public int WaypointIndex { get; private set; }
    public int WaypointTotal { get; private set; }

    // ---- experiment-only state ---------------------------------------------------
    /// <summary>"BASELINE" during the quiet head of the mission, "TASK" afterwards.
    /// Written into every telemetry row so the analysis can slice without guessing.</summary>
    public string Segment { get; private set; } = "TASK";
    public ChecklistRun Checklist { get; private set; }
    /// <summary>Taxi phase for the HUD: 0 taxi, 1 holding short, 2 cleared, 3 airborne.</summary>
    public int TaxiPhase => taxiPhase;
    /// <summary>True while the aircraft must stop at the hold-short line.</summary>
    public bool HoldingShort => Current != null && Current.Goal == ScenarioGoal.TaxiTakeoff && taxiPhase == 1;
    /// <summary>Slant range to the nearest active traffic, or -1 when there is none.</summary>
    public float NearestTrafficM
    {
        get
        {
            float best = -1f;
            if (ac == null) return -1f;
            foreach (var t in traffic)
            {
                float r = t != null ? t.RangeFrom(ac.transform.position) : -1f;
                if (r >= 0f && (best < 0f || r < best)) best = r;
            }
            return best;
        }
    }
    public MissionDefinition Mission => Current != null ? Current.Mission : null;
    public bool IsExperiment => Mission != null;
    public ExperimentLogger Experiment { get; private set; } = new ExperimentLogger();
    /// <summary>Objective performance metrics for the trial (also written to disk).</summary>
    public Dictionary<string, float> Metrics { get; private set; } = new Dictionary<string, float>();

    GameManager gm;
    CessnaPhysics ac;
    AircraftSystems sys;
    AircraftController ctl;
    Rigidbody rb;
    InstrumentGauge[] gauges;
    ScenarioLogger legacyLog = new ScenarioLogger();

    float telemetryAccum;
    int nextEvent;
    float altTol, hdgTol;
    System.Random rng;
    int trialSeed;

    // deviation accumulators
    float sampleT, inTolT, altErrInt, hdgErrInt, altErrSq, hdgErrSq;

    // response handling (one pending response at a time)
    bool respPending;
    float respShownAt, respWindow;
    string respLabel = "";
    ScenarioEventType respType;
    int alarmsTotal, alarmsHit, alarmsMissed, falseAlarms, rtCount;
    float rtSum;

    // control smoothness (input "jerk" per second)
    float prevPitch, prevRoll, prevYaw, jerkSum, jerkTime, jerkInstant;
    /// <summary>Control-activity covariates — the measurement that lets an EEG effect be
    /// separated from hand movement. See ControlActivity.cs.</summary>
    readonly ControlActivity ctrlActivity = new ControlActivity();

    // failure / cue tracking
    FailureKind armedKind = FailureKind.None;
    bool cueSeen, firstResponseSeen, resolvedSeen;
    float cueAt = -1f;
    float ctlAtCue;

    // effects
    float weatherUntil, weatherIntensity, savedFog, ambientTurb, baseSkySeverity;
    int faultGauge = -1;
    float faultUntil;
    float turbSeed;
    GameObject markersRoot;

    // navigation / landing / mission
    int wpIdx, wpReached;
    bool wasGrounded;
    float touchdownSink, touchdownX, touchdownDrift;
    // ---- TAKE-OFF EVALUATION (see brief section 30: "the participant should not
    // simply press a button and fly") ----
    /// <summary>Airspeed and distance down the runway at the moment the wheels leave —
    /// the two numbers that say whether the rotation was flown or merely arrived at.</summary>
    float rotateAirspeedKmh, rotateDistanceM, rollStartZ;
    /// <summary>Worst lateral and directional excursion during the ground roll only.
    /// Separate from the airborne centreline metric because keeping straight on the
    /// runway and tracking the extended centreline are different skills, and in a
    /// crosswind they fail in different ways.</summary>
    float rollMaxDevM, rollMaxHdgDevDeg;
    float airborneAtT, targetAltAtT, initialClimbRate;
    bool rollStarted;
    // ---- LANDING EVALUATION (brief section 29: more than "did the plane crash") ----
    /// <summary>Distance from the threshold to the touchdown point, along the runway.
    /// The single most informative landing number and the one that was not recorded.</summary>
    float touchdownDistanceM, touchdownSpeedKmh;
    /// <summary>Height at which the flare began (first sustained arrest of the sink
    /// rate) and how long it lasted. Late flare and no flare look identical in a
    /// sink-rate-only score.</summary>
    float flareStartAltM, flareStartT;
    float prevSinkMs;
    /// <summary>RMS deviation from a nominal 3-degree glidepath over the approach,
    /// sampled between 300 m and 30 m above the runway.</summary>
    float glideErrSq, glideT;
    bool excursion;
    /// <summary>Worst lateral excursion from the runway centreline while below 60 m, and
    /// the time-integral of |drift angle| over the same window. Both are meaningless
    /// without a wind model and both are primary metrics for the crosswind axis.</summary>
    float maxCenterlineDev, driftAbsInt, driftTime;
    bool landed;
    bool climbDone;
    int missionPhase;
    float landDeadline;
    bool goAroundCommanded, goAroundInitiated;
    float goAroundAt, lowAltAtGoAround;

    // taxi / take-off (TaxiTakeoff goal)
    int taxiPhase;              // 0 taxi, 1 held at hold-short, 2 cleared/rolling, 3 airborne
    bool lineUpCleared;         // ATC has cleared line-up + take-off
    bool holdShortReached, holdShortBusted, rotated;
    float taxiDistance;         // metres travelled on the ground (a taxi-quality measure)
    Vector3 lastGroundPos;

    // traffic
    readonly List<TrafficAircraft> traffic = new List<TrafficAircraft>();
    GameObject trafficRoot;

    public void Init(GameManager manager)
    {
        gm = manager;
        ac = manager.Aircraft;
        rb = ac.Body;
        ctl = ac.GetComponent<AircraftController>();
        sys = ac.GetComponent<AircraftSystems>();
        if (sys == null) sys = ac.gameObject.AddComponent<AircraftSystems>();
        if (ac.GetComponent<SystemsInput>() == null) ac.gameObject.AddComponent<SystemsInput>();
        savedFog = RenderSettings.fogDensity;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TRIAL START
    // ═══════════════════════════════════════════════════════════════════════════

    public void Begin(Scenario s) { Begin(s, 0); }

    /// <summary>`seedOffset` lets a repeat of the same mission draw a DIFFERENT
    /// jitter, so a participant cannot learn the event times by repetition, while
    /// the schedule stays reproducible from (session seed, mission, offset).</summary>
    public void Begin(Scenario s, int seedOffset)
    {
        RestoreAll();
        Current = s;
        Active = true; IsComplete = false; Result = null;
        Time01 = 0f; telemetryAccum = 0f; nextEvent = 0;

        CurTargetAlt = s.TargetAltitude; CurTargetHdg = s.TargetHeading;
        altTol = s.AltTolerance; hdgTol = s.HdgTolerance;
        sampleT = inTolT = altErrInt = hdgErrInt = altErrSq = hdgErrSq = 0f;
        respPending = false; alarmsTotal = alarmsHit = alarmsMissed = falseAlarms = rtCount = 0; rtSum = 0f;
        touchdownSink = touchdownX = touchdownDrift = 0f;
        maxCenterlineDev = driftAbsInt = driftTime = 0f;
        rotateAirspeedKmh = rotateDistanceM = rollStartZ = 0f;
        rollMaxDevM = rollMaxHdgDevDeg = 0f;
        airborneAtT = targetAltAtT = initialClimbRate = 0f;
        rollStarted = false;
        touchdownDistanceM = touchdownSpeedKmh = 0f;
        flareStartAltM = flareStartT = prevSinkMs = 0f;
        glideErrSq = glideT = 0f;
        excursion = false;
        prevPitch = prevRoll = prevYaw = jerkSum = jerkTime = jerkInstant = 0f;
        ctrlActivity.Reset();
        wpIdx = wpReached = 0; wasGrounded = false; landed = false; climbDone = false;
        missionPhase = 0; landDeadline = 0f;
        goAroundCommanded = goAroundInitiated = false; goAroundAt = -1f; lowAltAtGoAround = 0f;
        armedKind = FailureKind.None; cueSeen = firstResponseSeen = resolvedSeen = false; cueAt = -1f;
        Checklist = null;
        taxiPhase = 0; lineUpCleared = false; holdShortReached = false; holdShortBusted = false;
        rotated = false; taxiDistance = 0f;
        ClearTraffic();
        Metrics = new Dictionary<string, float>();

        // Deterministic per-trial RNG: session seed + mission id + repeat offset.
        trialSeed = ExperimentLogger.Seed ^ (s.Id != null ? s.Id.GetHashCode() : 0) ^ (seedOffset * 7919);
        rng = new System.Random(trialSeed);
        turbSeed = 5.1f + (float)rng.NextDouble() * 20f;

        ScheduleEvents(s);

        gauges = ac.GetComponentsInChildren<InstrumentGauge>();
        foreach (var g in gauges) g.enabled = true;

        // Systems: always start from a fully serviceable aeroplane, then apply any
        // pre-existing failures the mission asks for. This is what guarantees no
        // abnormality can leak from one trial into the next.
        if (sys != null)
        {
            sys.ResetAll();
            sys.OnCuePerceptible = OnFailureCue;
            sys.OnConfigChange = OnConfigChange;
        }
        SubscribeCockpitEvents();

        var m = s.Mission;
        Segment = (m != null && m.InTaskBaselineS > 0f) ? "BASELINE" : "TASK";
        ambientTurb = m != null ? m.AmbientTurbulence : 0f;

        // Sky follows the mission's specified conditions, so "clear day" looks like one
        // and "heavy rain, poor visibility" looks like that. Without this every mission
        // rendered under the same overcast HDRI and the weather manipulation was
        // invisible to the participant regardless of what the fog density said.
        ApplyMissionSky(m);
        ApplyMissionWind(m);

        SetupAircraft(s);
        // Clean configuration for every trial. Without this, flap and spoiler
        // selections carry over from the previous mission — see
        // AircraftController.ResetConfiguration for why that would corrupt the design.
        if (ctl != null) ctl.ResetConfiguration(m != null ? m.StartFlaps01 : 0f);
        else if (m != null) { ac.flaps = m.StartFlaps01; ac.spoiler = 0f; }

        // The PHYSICAL controls must reset too. Otherwise a lever the participant left
        // pulled in one mission is still visibly pulled in the next — and worse, a
        // position-based control writes its stale position back to the aircraft on the
        // first frame, silently re-applying the previous trial's configuration. Same
        // class of leak flaps had, one layer further out.
        CockpitControlRig.Instance?.ResetAll(m != null ? m.StartFlaps01 : 0f);
        if (m != null && sys != null)
            foreach (var f in m.PreexistingFailures) sys.Arm(f);

        SpawnWaypointMarkers(s);
        WaypointTotal = s.Waypoints.Count;

        // ---- logging ----
        if (IsExperiment)
        {
            Experiment.BeginTrial(m, s);
            DataFile = Experiment.TrialDir;
            Experiment.Mark(EventMarkers.MissionStart,
                            m.Id + "|" + m.ClassTag + "|seed=" + trialSeed, ac, m.Phase.ToString());
            if (Segment == "BASELINE")
                Experiment.Mark(EventMarkers.BaselineStart, "in_task|" + m.InTaskBaselineS.ToString("F0") + "s", ac);
        }
        else
        {
            legacyLog.Begin(s);
            DataFile = legacyLog.FilePath;
            legacyLog.Marker(0f, "SCENARIO_START", s.Id + " " + s.DiffLabel);
        }

        if (s.Start == ScenarioStart.Runway) VoiceCallouts.Instance?.SayATC("cleared_takeoff");
    }

    /// <summary>Resolve each event's actual firing time by applying its jitter from
    /// the seeded RNG, then sort. Uniform in [-J, +J]. Events with Jitter == 0 (pure
    /// briefing messages) are left exactly where they are.</summary>
    void ScheduleEvents(Scenario s)
    {
        foreach (var e in s.Events)
        {
            float j = e.Jitter <= 0f ? 0f : (float)(rng.NextDouble() * 2.0 - 1.0) * e.Jitter;
            e.ScheduledTime = Mathf.Max(0.5f, e.Time + j);
        }
        s.Events.Sort((a, b) => a.ScheduledTime.CompareTo(b.ScheduledTime));
    }

    void SetupAircraft(Scenario s)
    {
        var m = s.Mission;
        if (m != null && m.Goal == ScenarioGoal.TaxiTakeoff)
        {
            ac.ResetTo(Aerodrome.StandPos, Aerodrome.StandRot, false, 0f);
            ac.throttle = 0f;
            lastGroundPos = ac.transform.position;
            return;
        }
        if (m != null && m.Start == ScenarioStart.Airborne)
        {
            // Experiment missions state their own start position so approach geometry
            // is reproducible to the metre.
            ac.ResetTo(m.StartPos, Quaternion.Euler(0f, m.StartHeadingDeg, 0f), true, m.StartAirspeedKmh / 3.6f);
            ac.throttle = m.Goal == ScenarioGoal.Land ? 0.35f : 0.62f;
            return;
        }
        switch (s.Goal)
        {
            case ScenarioGoal.TaxiTakeoff:
                // Parked on the stand, engine running, brakes off, nose north.
                ac.ResetTo(Aerodrome.StandPos, Aerodrome.StandRot, false, 0f);
                ac.throttle = 0f;
                break;
            case ScenarioGoal.Land:
                ac.ResetTo(new Vector3(0f, 400f, -3000f), Quaternion.identity, true, 55f);
                ac.throttle = 0.35f;
                break;
            case ScenarioGoal.Mission:
            case ScenarioGoal.TakeoffClimb:
                ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
                break;
            default:
                ac.ResetTo(new Vector3(0f, s.TargetAltitude, -200f),
                           Quaternion.Euler(0f, s.TargetHeading, 0f), true, 50f);
                break;
        }
    }

    public void Restart()
    {
        if (Current == null) return;
        if (IsExperiment) Experiment.Mark(EventMarkers.MissionRestart, Current.Id, ac);
        int off = Mathf.Abs((int)(Time.realtimeSinceStartup * 13f)) % 997;   // fresh jitter draw
        Begin(Current, off);
    }

    public void Abort()
    {
        if (IsExperiment && Experiment.Open)
        {
            Experiment.Mark(EventMarkers.MissionAbort, Current != null ? Current.Id : "", ac);
            Experiment.Close();
        }
        legacyLog.Close();
        RestoreAll();
        Active = false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PER-FRAME
    // ═══════════════════════════════════════════════════════════════════════════

    public void Tick(float dt)
    {
        if (!Active || IsComplete) return;
        // NOTE: Time01 and telemetry sampling are advanced in FixedUpdate, NOT here.
        // See the comment on FixedUpdate for why.

        // Segment transition: the quiet in-task baseline ends.
        var m = Mission;
        if (m != null && Segment == "BASELINE" && Time01 >= m.InTaskBaselineS)
        {
            Segment = "TASK";
            Experiment.Mark(EventMarkers.BaselineEnd, "in_task", ac);
        }

        // Control jerk (needed by the telemetry row, so compute it BEFORE sampling).
        jerkInstant = (Mathf.Abs(ac.pitchInput - prevPitch) + Mathf.Abs(ac.rollInput - prevRoll) +
                       Mathf.Abs(ac.yawInput - prevYaw)) / Mathf.Max(dt, 1e-4f);
        jerkSum += Mathf.Abs(ac.pitchInput - prevPitch) + Mathf.Abs(ac.rollInput - prevRoll) + Mathf.Abs(ac.yawInput - prevYaw);
        jerkTime += dt;
        prevPitch = ac.pitchInput; prevRoll = ac.rollInput; prevYaw = ac.yawInput;

        var ev = Current.Events;
        while (nextEvent < ev.Count && ev[nextEvent].ScheduledTime <= Time01) { Fire(ev[nextEvent]); nextEvent++; }

        UpdateEffects();
        UpdateChecklist();
        HandleResponse();
        DetectFirstResponse();
        DetectResolution();
        UpdateDeviation(dt);
        UpdateGoal();
        CheckComplete();
    }

    void UpdateDeviation(float dt)
    {
        AltError = Mathf.Abs(ac.AltitudeM - CurTargetAlt);
        HdgError = Mathf.Abs(Mathf.DeltaAngle(ac.HeadingDeg, CurTargetHdg));
        // Tracking error is only meaningful when there is a flight target to track.
        // On a taxi/take-off mission that is true only once airborne — before rotation
        // the aeroplane is on the ground and "altitude error" is the height of the
        // assigned cruise level, which would score a perfect taxi as 0% in tolerance.
        bool tracking = Current.Goal == ScenarioGoal.HoldTargets
                     || Current.Goal == ScenarioGoal.TakeoffClimb
                     || (Current.Goal == ScenarioGoal.TaxiTakeoff && taxiPhase >= 3);
        if (tracking)
        {
            altErrInt += AltError * dt; hdgErrInt += HdgError * dt;
            altErrSq += AltError * AltError * dt; hdgErrSq += HdgError * HdgError * dt;
            sampleT += dt;
            if (AltError <= altTol && HdgError <= hdgTol) inTolT += dt;
        }

        // Runway-relative tracking, accumulated only in the last 60 m of height and
        // within the runway's own length, i.e. over the take-off roll, the flare and
        // the rollout. Outside that window a "centreline deviation" is just the
        // aircraft being somewhere else in the circuit and means nothing.
        float h = ac.AltitudeM - Aerodrome.RunwayElevationM;
        if (h < 60f && Mathf.Abs(ac.transform.position.z) < Aerodrome.RunwayHalfLength + 60f)
        {
            maxCenterlineDev = Mathf.Max(maxCenterlineDev, Mathf.Abs(ac.transform.position.x));
            // Only integrate drift while there is a real ground track to have drifted
            // from. Holding the brakes on a windy stand is not a drift error, and
            // including it made a crosswind take-off report a mean |drift| of 154
            // degrees, which is nonsense and would have been analysed as data.
            if (ac.GroundSpeedMs > 8f)
            {
                driftAbsInt += Mathf.Abs(ac.DriftAngleDeg) * dt;
                driftTime += dt;
            }
        }

        // ---- approach quality, for the landing missions ----
        if (Current != null && Current.Goal == ScenarioGoal.Land && !ac.Grounded)
        {
            Vector3 p = ac.transform.position;
            float agl = ac.AltitudeM - Aerodrome.RunwayElevationM;
            float toThr = Aerodrome.ThresholdZ - p.z;          // +ve = still short of it
            // GLIDEPATH ERROR. The nominal path is 3 degrees to the threshold; the error
            // is the angular difference between where the aeroplane is and where that
            // path would put it. Sampled between 30 m and 300 m so it covers the approach
            // and not the flare or the join.
            if (agl > 30f && agl < 300f && toThr > 200f)
            {
                float actualDeg = Mathf.Atan2(agl, toThr) * Mathf.Rad2Deg;
                float err = actualDeg - 3f;
                glideErrSq += err * err * dt; glideT += dt;
            }
            // FLARE. The first sustained arrest of the sink rate below 30 m. Recording
            // WHERE it began separates "flared late" from "did not flare", which a
            // touchdown sink rate alone cannot: both produce a firm arrival.
            float sink = -ac.VerticalSpeedMs;
            if (flareStartAltM <= 0f && agl < 30f && agl > 0.5f && prevSinkMs > 0.6f && sink < prevSinkMs - 0.15f)
            { flareStartAltM = agl; flareStartT = Time01; }
            prevSinkMs = Mathf.Lerp(prevSinkMs, sink, 0.15f);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════════

    void Fire(ScenarioEvent e)
    {
        switch (e.Type)
        {
            case ScenarioEventType.Message:
                Mark(EventMarkers.AtcMessage, e.Label);
                Show(e.Label); break;

            case ScenarioEventType.HeadingChange:
                VoiceCallouts.Instance?.SayATC(Mathf.DeltaAngle(ac.HeadingDeg, e.Value) >= 0f ? "turn_right" : "turn_left");
                CurTargetHdg = e.Value;
                Mark(EventMarkers.TargetChange, "heading=" + e.Value.ToString("F0"));
                Show("ATC: " + e.Label); break;

            case ScenarioEventType.AltitudeChange:
                VoiceCallouts.Instance?.SayATC(e.Value > CurTargetAlt ? "climb" : "descend");
                CurTargetAlt = e.Value;
                Mark(EventMarkers.TargetChange, "altitude=" + e.Value.ToString("F0"));
                Show("ATC: " + e.Label); break;

            case ScenarioEventType.Weather:
                WeatherActive = true; weatherUntil = Time01 + e.Duration;
                weatherIntensity = Mathf.Max(0.1f, e.Value);
                // Deepen the sky as well as the fog, so the onset is actually SEEN.
                WorldBuilder.SetSkyOvercast(Mathf.Max(baseSkySeverity, weatherIntensity));
                RenderSettings.fogDensity = Mathf.Lerp(0.00018f, 0.00055f, weatherIntensity);
                VoiceCallouts.Instance?.SayATC("weather_ahead");
                Mark(EventMarkers.WeatherOnset, "intensity=" + weatherIntensity.ToString("F2") + ";dur=" + e.Duration.ToString("F0"));
                Show(string.IsNullOrEmpty(e.Label) ? "WEATHER — turbulence & low vis" : e.Label); break;

            case ScenarioEventType.InstrumentFault:
                faultGauge = (e.Gauge >= 0 && gauges.Length > 0) ? Mathf.Clamp(e.Gauge, 0, gauges.Length - 1)
                            : (gauges.Length > 0 ? rng.Next(0, gauges.Length) : -1);
                if (faultGauge >= 0) gauges[faultGauge].enabled = false;
                FaultActive = true; faultUntil = Time01 + Mathf.Max(2f, e.Duration);
                VoiceCallouts.Instance?.SayATC("instrument_fault");
                Mark(EventMarkers.WarningAppears, "instrument_fault;gauge=" + faultGauge);
                if (e.RequiresResponse) BeginResponse(e); break;

            // ---- experiment event types ------------------------------------------
            case ScenarioEventType.SystemFailure:
                ArmFailure(e); break;

            case ScenarioEventType.Checklist:
                StartChecklist(e.ChecklistId, e.Label); break;

            case ScenarioEventType.Probe:
                Mark(EventMarkers.ProbeOnset, e.Label);
                VoiceCallouts.Instance?.SayATC("caution");
                BeginResponse(e); break;

            case ScenarioEventType.Traffic:
                if (e.SpawnsTraffic) SpawnTraffic(e.Traffic, e.Label);
                else Mark(EventMarkers.TrafficOnset, e.Label);
                VoiceCallouts.Instance?.SayATC("traffic");
                if (e.RequiresResponse) BeginResponse(e); else Show(e.Label);
                break;

            case ScenarioEventType.GoAround:
                goAroundCommanded = true; goAroundAt = Time01; lowAltAtGoAround = ac.AltitudeM;
                Mark(EventMarkers.GoAroundCommanded, e.Label);
                VoiceCallouts.Instance?.SayATC("caution");
                Show("GO AROUND — " + e.Label); break;

            case ScenarioEventType.ConfigCall:
                // A ConfigCall labelled as a take-off clearance releases the hold-short
                // gate. Everything else is an instruction banner.
                if (e.Label != null && e.Label.ToUpper().Contains("CLEARED FOR TAKE"))
                    ClearForTakeoff();
                Mark(EventMarkers.AtcMessage, "config_call|" + e.Label);
                Show(e.Label); break;

            case ScenarioEventType.Readback:
                Mark(EventMarkers.AtcMessage, "clearance|" + e.Label);
                VoiceCallouts.Instance?.SayATC("caution");
                BeginResponse(e); break;

            case ScenarioEventType.Decision:
                Mark(EventMarkers.DecisionPrompt, e.Label);
                VoiceCallouts.Instance?.SayATC("caution");
                BeginResponse(e); break;

            case ScenarioEventType.Alarm:
            case ScenarioEventType.Distraction:
                Mark(EventMarkers.WarningAppears, e.Label);
                VoiceCallouts.Instance?.SayATC(
                    !string.IsNullOrEmpty(e.Label) && e.Label.ToUpper().Contains("TRAFFIC") ? "traffic" : "caution");
                if (e.RequiresResponse) BeginResponse(e); else Show(e.Label);
                break;
        }
    }

    /// <summary>Baseline sky for the mission: clear unless the mission declares reduced
    /// visibility or schedules a weather event.</summary>
    void ApplyMissionSky(MissionDefinition m)
    {
        float sev = m != null ? m.Visibility01 : 0f;
        if (m != null && sev <= 0.01f)
            foreach (var e in m.Events)
                if (e.Type == ScenarioEventType.Weather) { sev = Mathf.Max(sev, e.Value * 0.6f); break; }

        if (sev <= 0.01f) WorldBuilder.SetSkyClear();
        else WorldBuilder.SetSkyOvercast(sev);
        baseSkySeverity = sev;
    }

    /// <summary>Configure the air mass for this trial. Seeded from the trial seed, so
    /// the gust series replays exactly; disabled outright when the mission is calm, so
    /// a calm mission is bit-identical to the pre-wind flight model.</summary>
    void ApplyMissionWind(MissionDefinition m)
    {
        if (m == null || (m.WindSpeedMs <= 0.01f && m.WindGustMs <= 0.01f)) { WindModel.Disable(); return; }
        WindModel.Configure(m.WindFromDeg, m.WindSpeedMs, m.WindGustMs, trialSeed,
                            m.WindShearAltM, m.WindShearDeltaMs, m.WindShearDeltaDeg,
                            Aerodrome.RunwayElevationM);
        WindModel.Clock = 0f;
    }

    void ArmFailure(ScenarioEvent e)
    {
        if (sys == null) return;
        armedKind = e.Failure;
        cueSeen = firstResponseSeen = resolvedSeen = false;
        sys.Arm(e.Failure, e.Severity);
        // TRIGGER_ARMED is when the SIMULATION injected it. CUE_ONSET (raised by
        // AircraftSystems) is when the pilot could first perceive it. For a gradual
        // failure these are minutes apart and only the second one is a valid epoch.
        Mark(EventMarkers.TriggerArmed, e.Failure + ";severity=" + e.Severity.ToString("F2"));
        // Abrupt failures also warrant a warning banner; gradual ones must NOT be
        // announced, or the diagnostic task the mission is built around disappears.
        bool gradual = e.Failure == FailureKind.CarbIce || e.Failure == FailureKind.AlternatorFailure ||
                       e.Failure == FailureKind.StaticBlocked;
        if (!gradual)
        {
            Mark(EventMarkers.WarningAppears, e.Label);
            Show("⚠  " + e.Label);
        }
    }

    /// <summary>Raised by AircraftSystems the moment the symptom becomes perceptible.</summary>
    void OnFailureCue(FailureKind k)
    {
        if (cueSeen) return;
        cueSeen = true; cueAt = Time01;
        ctlAtCue = Mathf.Abs(ac.pitchInput) + Mathf.Abs(ac.rollInput) + Mathf.Abs(ac.yawInput) + ac.Throttle01;
        Mark(EventMarkers.CueOnset, k.ToString());
    }

    void OnConfigChange(string what)
    {
        Mark(EventMarkers.ConfigChange, what);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PHYSICAL COCKPIT INTERACTION -> MARKERS
    // ═══════════════════════════════════════════════════════════════════════════
    // Discrete actions become one marker each. CONTINUOUS controls (trim, throttle) are
    // DEBOUNCED: a marker only when the value has moved a meaningful amount AND a
    // minimum interval has passed. Their exact position is already in the 50 Hz
    // telemetry, so the marker exists to say "the pilot adjusted this", not to
    // reconstruct the movement.
    const float TrimMarkerStep = 0.10f;      // 10% of full trim travel
    const float ThrottleMarkerStep = 0.15f;
    const float MarkerMinIntervalS = 1.0f;
    float lastTrimMarked, lastTrimMarkT = -99f;
    float lastThrMarked, lastThrMarkT = -99f;

    void SubscribeCockpitEvents()
    {
        lastTrimMarked = ac != null ? ac.trim : 0f; lastTrimMarkT = -99f;
        lastThrMarked = ac != null ? ac.Throttle01 : 0f; lastThrMarkT = -99f;

        CockpitEvents.OnGrab    = id => Mark(EventMarkers.ControlGrab, id);
        CockpitEvents.OnRelease = id => Mark(EventMarkers.ControlRelease, id);
        CockpitEvents.OnFlapSelected = (i, label) =>
            Mark(EventMarkers.FlapSelected, "detent=" + i + ";" + label);
        CockpitEvents.OnTrimChanged = v =>
        {
            if (Mathf.Abs(v - lastTrimMarked) < TrimMarkerStep && Time01 - lastTrimMarkT < 6f) return;
            if (Time01 - lastTrimMarkT < MarkerMinIntervalS) return;
            lastTrimMarked = v; lastTrimMarkT = Time01;
            Mark(EventMarkers.TrimChanged, "trim=" + v.ToString("F2"));
        };
        CockpitEvents.OnThrottleMoved = v =>
        {
            if (Mathf.Abs(v - lastThrMarked) < ThrottleMarkerStep) return;
            if (Time01 - lastThrMarkT < MarkerMinIntervalS) return;
            lastThrMarked = v; lastThrMarkT = Time01;
            Mark(EventMarkers.ThrottleChanged, "throttle=" + v.ToString("F2"));
        };
        CockpitEvents.OnBrakeStateChanged = (on, p) =>
            Mark(on ? EventMarkers.BrakeApplied : EventMarkers.BrakeReleased, "pressure=" + p.ToString("F2"));
    }

    /// <summary>First deliberate pilot action after the cue: a control deflection or
    /// throttle change beyond a deadband, or a systems switch. Gives an objective
    /// response latency without asking the participant to press an "I noticed" key.</summary>
    void DetectFirstResponse()
    {
        if (!cueSeen || firstResponseSeen) return;
        float now = Mathf.Abs(ac.pitchInput) + Mathf.Abs(ac.rollInput) + Mathf.Abs(ac.yawInput) + ac.Throttle01;
        if (Mathf.Abs(now - ctlAtCue) > 0.18f)
        {
            firstResponseSeen = true;
            Mark(EventMarkers.PilotFirstResponse, "latency_s=" + (Time01 - cueAt).ToString("F2"));
        }
    }

    void DetectResolution()
    {
        if (!cueSeen || resolvedSeen || armedKind == FailureKind.None || sys == null) return;
        if (sys.IsResolved(armedKind))
        {
            resolvedSeen = true;
            Mark(EventMarkers.FailureResolved, armedKind + ";t_from_cue_s=" + (Time01 - cueAt).ToString("F2"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHECKLISTS
    // ═══════════════════════════════════════════════════════════════════════════

    void StartChecklist(string id, string label)
    {
        var def = ChecklistLibrary.Get(id);
        if (def == null) { Debug.LogWarning("[Scenario] unknown checklist " + id); return; }
        Checklist = new ChecklistRun { Def = def, Index = 0, StartedAt = Time01, ItemShownAt = Time01 };
        Mark(EventMarkers.ChecklistStart, def.Id + "|" + def.Title);
        Show(string.IsNullOrEmpty(label) ? def.Title : label);
    }

    void UpdateChecklist()
    {
        if (Checklist == null || Checklist.Complete) return;
        var item = Checklist.Current;
        bool done = false;

        if (item.Kind == ChecklistItemKind.Do)
        {
            done = item.Condition != null && sys != null && item.Condition(sys, ac);
        }
        else
        {
            // CHECK items are acknowledged with SPACE — but only when no timed
            // response prompt is pending, so one keypress can never be counted as
            // both a probe response and a checklist acknowledgement.
            if (!respPending && ConsumeAck()) done = true;
        }

        float dt = Time01 - Checklist.ItemShownAt;
        if (!done && dt > item.TimeoutS)
        {
            Checklist.Timeouts++;
            Mark(EventMarkers.ChecklistTimeout, Checklist.Def.Id + "|" + item.Label);
            done = true;   // advance rather than deadlock the trial
        }

        if (done)
        {
            Mark(EventMarkers.ChecklistItem,
                 Checklist.Def.Id + "|" + Checklist.Index + "|" + item.Label + "|dt=" + dt.ToString("F2"));
            Checklist.Index++;
            Checklist.ItemShownAt = Time01;
            if (Checklist.Complete)
                Mark(EventMarkers.ChecklistComplete,
                     Checklist.Def.Id + "|total_s=" + (Time01 - Checklist.StartedAt).ToString("F1") +
                     "|timeouts=" + Checklist.Timeouts);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TIMED RESPONSES (probes, decisions, readbacks, traffic)
    // ═══════════════════════════════════════════════════════════════════════════

    void BeginResponse(ScenarioEvent e)
    {
        if (respPending) { alarmsMissed++; MarkMiss(); }
        respPending = true; respShownAt = Time01; respWindow = e.ResponseWindow;
        respLabel = e.Label; respType = e.Type;
        alarmsTotal++;
        AlarmActive = true; AlarmLabel = e.Label;
    }

    void HandleResponse()
    {
        bool ack = ConsumeAck();
        if (respPending)
        {
            if (ack)
            {
                float rt = Time01 - respShownAt;
                alarmsHit++; rtSum += rt; rtCount++;
                Mark(HitMarker(respType), respLabel + "|rt_ms=" + (rt * 1000f).ToString("F0"));
                // A read-back of a take-off clearance is what actually releases the
                // hold-short gate, so the pilot must have acknowledged it — not merely
                // heard it — before the runway may be entered.
                if (respType == ScenarioEventType.Readback && respLabel != null &&
                    respLabel.ToUpper().Contains("CLEARED FOR TAKE")) ClearForTakeoff();
                EndResponse();
            }
            else if (Time01 - respShownAt > respWindow)
            {
                alarmsMissed++; MarkMiss();
                EndResponse();
            }
        }
        else if (ack && Checklist == null)
        {
            // A keypress with nothing pending and no drill running is a false alarm.
            falseAlarms++;
            Mark(EventMarkers.ProbeFalseAlarm);
        }
    }

    string HitMarker(ScenarioEventType t) =>
        t == ScenarioEventType.Decision ? EventMarkers.DecisionMade
      : t == ScenarioEventType.Readback ? EventMarkers.AtcReadbackOk
      : EventMarkers.ProbeHit;

    void MarkMiss()
    {
        string tag = respType == ScenarioEventType.Decision ? EventMarkers.DecisionExpired
                   : respType == ScenarioEventType.Readback ? EventMarkers.AtcReadbackMiss
                   : EventMarkers.ProbeMiss;
        Mark(tag, respLabel);
    }

    void EndResponse() { respPending = false; AlarmActive = false; }

    // ---- external acknowledge channel -------------------------------------------
    // SPACE is the participant's acknowledge key, but a keypress cannot be injected
    // by a headless test (or by a VR controller, or by a future eye-tracker button).
    // ExternalAck() is the one supported way to deliver the same signal from code.
    // It is consumed exactly once, in the same place the keyboard is read, so a test
    // exercises the identical path a participant does.
    bool externalAck;
    public void ExternalAck() => externalAck = true;
    bool ConsumeAck()
    {
        bool a = externalAck || Input.GetKeyDown(KeyCode.Space);
        externalAck = false;
        return a;
    }

    /// <summary>Read-only view of the pending timed response, for the HUD and for
    /// the test harness (which must know whether an acknowledge is expected).</summary>
    public bool ResponsePending => respPending;
    public string ResponseLabel => respLabel;

    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECTS / GOALS / COMPLETION
    // ═══════════════════════════════════════════════════════════════════════════

    void UpdateEffects()
    {
        if (WeatherActive && Time01 >= weatherUntil)
        {
            WeatherActive = false;
            // Back to the mission's baseline conditions, not to whatever the previous
            // trial left behind.
            if (baseSkySeverity <= 0.01f) WorldBuilder.SetSkyClear();
            else WorldBuilder.SetSkyOvercast(baseSkySeverity);
            Mark(EventMarkers.WeatherEnd);
        }
        if (FaultActive && Time01 >= faultUntil)
        {
            if (faultGauge >= 0 && faultGauge < gauges.Length) gauges[faultGauge].enabled = true;
            FaultActive = false; faultGauge = -1;
        }
        if (Time01 > BannerUntil) Banner = "";

        // Go-around: detected as a real, flown manoeuvre (power up AND climbing),
        // not as a keypress, so the behavioural record reflects what the aeroplane did.
        if (goAroundCommanded && !goAroundInitiated &&
            ac.Throttle01 > 0.85f && ac.VerticalSpeedMs > 0.8f)
        {
            goAroundInitiated = true;
            Mark(EventMarkers.GoAroundInitiated, "latency_s=" + (Time01 - goAroundAt).ToString("F2"));
        }
    }

    void UpdateGoal()
    {
        if (ac.Grounded && !wasGrounded)
        {
            touchdownSink = -ac.VerticalSpeedMs;
            touchdownX = ac.transform.position.x;
            // Drift at the moment of touchdown: a correctly de-crabbed landing touches
            // down with the aeroplane pointing where it is going, i.e. drift near zero.
            // Landing crabbed side-loads the gear and shows up here and nowhere else.
            touchdownDrift = ac.DriftAngleDeg;
            touchdownSpeedKmh = ac.AirspeedKmh;
            touchdownDistanceM = ac.transform.position.z - Aerodrome.ThresholdZ;
            bool onRunway = Mathf.Abs(touchdownX) < 16f && Mathf.Abs(ac.transform.position.z) < 305f;
            if (onRunway && !ac.Crashed) { landed = true; }
            Mark(EventMarkers.Touchdown,
                 "sink=" + touchdownSink.ToString("F2") + ";x=" + touchdownX.ToString("F1") +
                 ";on_runway=" + (onRunway ? 1 : 0));
        }
        wasGrounded = ac.Grounded;

        // RUNWAY EXCURSION: on the ground, past the threshold, and off the paved
        // surface. Distinct from "crashed" — an aeroplane that rolls off the side into
        // the grass and stops has not crashed, and the difference matters for a landing
        // score that is supposed to be more than a survival flag.
        if (!excursion && ac.Grounded && landed)
        {
            Vector3 gp = ac.transform.position;
            if (Mathf.Abs(gp.x) > Aerodrome.RunwayHalfWidth + 2f ||
                gp.z > Aerodrome.RunwayHalfLength + 5f)
            {
                excursion = true;
                Mark(EventMarkers.PhaseChange, "runway_excursion|x=" + gp.x.ToString("F1") +
                     ";z=" + gp.z.ToString("F1"));
            }
        }

        if (Current.Goal == ScenarioGoal.Navigate) UpdateWaypoints();
        else if (Current.Goal == ScenarioGoal.TaxiTakeoff) UpdateTaxiTakeoff();
        else if (Current.Goal == ScenarioGoal.Mission) UpdateMission();
        else if (Current.Goal == ScenarioGoal.TakeoffClimb && !ac.Grounded && ac.AltitudeM >= Current.TargetAltitude)
        {
            if (!climbDone) Mark(EventMarkers.PhaseChange, "target_altitude_reached");
            climbDone = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TAXI -> HOLD SHORT -> LINE UP -> TAKE OFF
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // Four phases, each with its own marker so the EEG can be sliced by them:
    //   0 TAXI       follow the route to the hold-short line
    //   1 HOLD SHORT stopped, waiting for the line-up clearance
    //   2 CLEARED    line up and roll
    //   3 AIRBORNE   climb and hold the assigned altitude/heading
    //
    // The hold-short line is a hard rule, not decoration: crossing it before the
    // clearance is a RUNWAY INCURSION and is logged as one. That single binary is
    // the most meaningful behavioural error the taxi phase can produce, and it is
    // exactly the error the real-world procedure exists to prevent.
    void UpdateTaxiTakeoff()
    {
        // ground distance travelled — a cheap taxi-quality/efficiency measure
        if (ac.Grounded)
        {
            Vector3 p = ac.transform.position;
            taxiDistance += Vector3.Distance(new Vector3(p.x, 0f, p.z),
                                             new Vector3(lastGroundPos.x, 0f, lastGroundPos.z));
            lastGroundPos = p;
        }

        switch (taxiPhase)
        {
            case 0:   // taxiing to the hold short
            {
                UpdateWaypoints();
                Vector3 p = ac.transform.position;

                // Runway incursion: on the runway surface before being cleared.
                if (!holdShortBusted && !lineUpCleared && Aerodrome.OnRunway(p))
                {
                    holdShortBusted = true;
                    Mark(EventMarkers.MissionFailure, "runway_incursion|uncleared_entry");
                    Show("⚠  RUNWAY INCURSION — you were not cleared to enter");
                }

                // Reached the hold-short area: stop and wait.
                if (!holdShortReached && p.z >= Aerodrome.HoldShortZ - 25f && p.z <= Aerodrome.LinkZ + 5f)
                {
                    holdShortReached = true;
                    // The state machine is authoritative for taxi progress, so credit the
                    // hold-short checkpoint here. Otherwise the route counter stalls at
                    // ALPHA — the hold-short trigger zone is entered before the waypoint
                    // radius is — and a perfectly flown taxi reports 1/4 checkpoints.
                    if (wpIdx <= Aerodrome.HoldShortIndex)
                    { wpReached = Aerodrome.HoldShortIndex + 1; wpIdx = Aerodrome.HoldShortIndex + 1; }

                    if (!lineUpCleared)
                    {
                        taxiPhase = 1;
                        Mark(EventMarkers.PhaseChange, "hold_short_reached|taxi_m=" + taxiDistance.ToString("F0"));
                        Show("HOLDING SHORT RUNWAY 01 — wait for clearance");
                    }
                    else taxiPhase = 2;
                }
                break;
            }

            case 1:   // holding short, waiting for the clearance
                if (lineUpCleared)
                {
                    taxiPhase = 2;
                    Mark(EventMarkers.PhaseChange, "cleared_for_takeoff");
                    Show("CLEARED FOR TAKE-OFF RUNWAY 01");
                }
                break;

            case 2:   // lining up and rolling
            {
                Vector3 rp = ac.transform.position;
                // The roll proper starts once the aeroplane is on the runway and moving.
                if (!rollStarted && ac.Grounded && Aerodrome.OnRunway(rp) && ac.GroundSpeedMs > 3f)
                { rollStarted = true; rollStartZ = rp.z; }
                if (rollStarted && ac.Grounded)
                {
                    rollMaxDevM = Mathf.Max(rollMaxDevM, Mathf.Abs(rp.x));
                    rollMaxHdgDevDeg = Mathf.Max(rollMaxHdgDevDeg,
                        Mathf.Abs(Mathf.DeltaAngle(ac.HeadingDeg, Aerodrome.RunwayHeadingDeg)));
                }
                if (!ac.Grounded && ac.AltitudeM > 8f)
                {
                    taxiPhase = 3; rotated = true;
                    rotateAirspeedKmh = ac.AirspeedKmh;
                    rotateDistanceM = rollStarted ? Mathf.Abs(ac.transform.position.z - rollStartZ) : 0f;
                    airborneAtT = Time01;
                    wpReached = Current.Waypoints.Count;   // route completed by getting airborne
                    HasWaypoint = false;
                    Mark(EventMarkers.PhaseChange, "airborne|taxi_m=" + taxiDistance.ToString("F0") +
                         ";rotate_kmh=" + rotateAirspeedKmh.ToString("F0") +
                         ";roll_m=" + rotateDistanceM.ToString("F0"));
                }
                break;
            }

            case 3:   // airborne, climbing to the assigned level
                if (!climbDone && ac.AltitudeM >= Current.TargetAltitude - 20f)
                {
                    climbDone = true;
                    targetAltAtT = Time01;
                    if (targetAltAtT > airborneAtT + 1f)
                        initialClimbRate = (ac.AltitudeM - 8f) / (targetAltAtT - airborneAtT);
                    Mark(EventMarkers.PhaseChange, "target_altitude_reached;t_from_rotate_s=" +
                         (targetAltAtT - airborneAtT).ToString("F1"));
                }
                break;
        }
    }

    /// <summary>ATC clears the aircraft to line up and take off. Called by a
    /// ConfigCall/Readback event in the mission's schedule, so the clearance can be
    /// delayed (making the pilot wait at the hold short) or made conditional.</summary>
    public void ClearForTakeoff()
    {
        if (lineUpCleared) return;
        lineUpCleared = true;
        Mark(EventMarkers.AtcMessage, "cleared_for_takeoff");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TRAFFIC
    // ═══════════════════════════════════════════════════════════════════════════

    void SpawnTraffic(TrafficBehaviour b, string label)
    {
        if (trafficRoot == null)
        {
            trafficRoot = new GameObject("ScenarioTraffic");
            if (gm != null && gm.WorldRoot != null) trafficRoot.transform.SetParent(gm.WorldRoot.transform);
        }
        var t = TrafficAircraft.Spawn(trafficRoot.transform, b, "T" + (traffic.Count + 1));
        traffic.Add(t);
        Mark(EventMarkers.TrafficOnset, b + "|" + label);
    }

    void ClearTraffic()
    {
        foreach (var t in traffic) if (t != null) SimUtil.Destroy(t.gameObject);
        traffic.Clear();
        if (trafficRoot != null) { SimUtil.Destroy(trafficRoot); trafficRoot = null; }
    }

    /// <summary>True when traffic is physically sitting on the runway.</summary>
    public bool RunwayBlocked
    {
        get { foreach (var t in traffic) if (t != null && t.BlocksRunway) return true; return false; }
    }

    void UpdateWaypoints()
    {
        if (wpIdx >= Current.Waypoints.Count) { HasWaypoint = false; return; }
        var wp = Current.Waypoints[wpIdx];
        HasWaypoint = true; WaypointPos = wp.Pos; WaypointName = wp.Name; WaypointIndex = wpIdx;

        Vector3 p = ac.transform.position;
        float dist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(wp.Pos.x, wp.Pos.z));
        if (dist < wp.Radius)
        {
            wpReached++; wpIdx++;
            Mark(EventMarkers.Waypoint, wp.Name);
            VoiceCallouts.Instance?.SayATC("waypoint");
            Show("Waypoint " + wp.Name + " reached");
        }
    }

    void UpdateMission()
    {
        switch (missionPhase)
        {
            case 0:
                if (!ac.Grounded && ac.AltitudeM > 200f) { missionPhase = 1; Show("Navigate the waypoints"); }
                break;
            case 1:
                UpdateWaypoints();
                if (wpIdx >= Current.Waypoints.Count)
                {
                    missionPhase = 2;
                    landDeadline = Time01 + 180f;
                    VoiceCallouts.Instance?.SayATC("cleared_land");
                    Show("Waypoints complete — return and LAND on the main runway");
                }
                break;
            case 2:
                HasWaypoint = true;
                WaypointPos = new Vector3(0f, 0f, 0f);
                WaypointName = "RUNWAY";
                break;
        }
    }

    void CheckComplete()
    {
        switch (Current.Goal)
        {
            case ScenarioGoal.Navigate:
                // An EXPERIMENT navigation mission runs its full nominal duration, like
                // every other mission except a landing. Ending at the last waypoint broke
                // the design's central duration control without saying so: L3V3 finished
                // at 221 s, giving 161 s of task against the 240 s every other mission
                // gets, and the shortfall depended on how fast the participant flew —
                // so trial length would have varied with skill, which is exactly the kind
                // of thing that must not vary. The pilot simply holds the last leg.
                // The legacy (non-experiment) path keeps its old behaviour.
                if (IsExperiment) { if (Time01 >= Current.Duration) Complete(); }
                else if (wpIdx >= Current.Waypoints.Count || Time01 >= Current.Duration) Complete();
                break;
            case ScenarioGoal.Land:
                // An experiment landing mission must run its full nominal duration OR
                // end at touchdown — whichever comes first. Duration is the ceiling
                // that keeps trial lengths comparable across classes.
                if (landed || Time01 >= Current.Duration) Complete();
                break;
            case ScenarioGoal.TaxiTakeoff:
                // Always runs the full nominal duration, like every other mission —
                // the pilot keeps flying the assigned climb/level after getting airborne,
                // so trial length stays constant across all twelve.
                if (Time01 >= Current.Duration) Complete();
                break;
            case ScenarioGoal.Mission:
                if (missionPhase == 2 && landed) Complete();
                else if (missionPhase < 2 && Time01 >= Current.Duration) Complete();
                else if (missionPhase == 2 && Time01 >= landDeadline) Complete();
                break;
            case ScenarioGoal.TakeoffClimb:
                // Experiment climb missions keep flying after the altitude is reached
                // (they hold it), so only the clock ends them. Legacy ones end at the
                // altitude, as they always did.
                if (IsExperiment) { if (Time01 >= Current.Duration) Complete(); }
                else if (climbDone || Time01 >= Current.Duration) Complete();
                break;
            default:
                if (Time01 >= Current.Duration) Complete();
                break;
        }
    }

    // ---- physics-rate clock, telemetry, and turbulence ----
    //
    // THE MISSION CLOCK AND THE TELEMETRY BOTH RUN ON THE PHYSICS STEP, NOT THE FRAME.
    //
    // They used to be frame-driven, which quietly made the "50 Hz" telemetry rate a
    // LIE whenever the frame rate dropped: the sampler could only ever fire once per
    // frame, so at 30 fps it produced 30 Hz, and under the accelerated test harness it
    // collapsed to a couple of samples per simulated second. A physiological experiment
    // cannot have a sample rate that depends on how much scenery is on screen — the
    // rate would differ between missions, and the busiest (highest-workload) missions
    // would be sampled worst.
    //
    // Unity's default fixedDeltaTime is 0.02 s, i.e. exactly 50 Hz, so sampling once
    // per fixed step gives a true, frame-rate-independent 50 Hz. It also makes event
    // firing deterministic for a given seed regardless of the machine's frame rate,
    // which is what makes a trial reproducible.
    void FixedUpdate()
    {
        if (!Active || IsComplete || gm == null || gm.State != GameState.Flying) return;
        float fdt = Time.fixedDeltaTime;

        Time01 += fdt;
        // The gust series is sampled against the MISSION clock, not wall time, so it
        // replays identically from the seed. (See defect note in FINAL_TEST_REPORT.)
        WindModel.Clock = Time01;

        // Control activity is accumulated on EVERY physics step, not on the telemetry
        // tick: it is an integral of control MOVEMENT, and sampling it at the telemetry
        // rate would alias fast stick reversals into a smaller number. The telemetry rate
        // happens to equal the physics rate today, but this must not silently depend on that.
        ctrlActivity.Sample(ac, fdt, CockpitControlRig.Instance != null && CockpitControlRig.AnyGrabbed);

        telemetryAccum += fdt;
        float interval = IsExperiment ? 1f / ExperimentLogger.TelemetryHz : 0.1f;
        if (telemetryAccum >= interval - 1e-6f)
        {
            telemetryAccum -= interval;
            if (IsExperiment) { Experiment.SetMissionTime(Time01); Experiment.Sample(ac, sys, this, ctl, jerkInstant, Segment); }
            else legacyLog.Sample(ac, this, Time01);
        }

        if (rb == null) return;
        float k = (WeatherActive ? weatherIntensity : 0f) + ambientTurb;
        if (k <= 0.001f) return;

        // MISSION time, not UnityEngine.Time.time. Time.time is seconds since the
        // application started, so the turbulence phase depended on HOW LONG UNITY HAD
        // BEEN OPEN when the trial began — the sequence was seeded but not reproducible,
        // and two participants with the same seed got different turbulence. Time01 is
        // the physics-locked mission clock and always starts at 0.
        float t = Time01;
        float gy = Mathf.PerlinNoise(turbSeed, t * 0.22f) - 0.5f;
        float gx = Mathf.PerlinNoise(turbSeed + 17f, t * 0.30f) - 0.5f;
        float gz = Mathf.PerlinNoise(turbSeed + 41f, t * 0.26f) - 0.5f;
        float rRoll = Mathf.PerlinNoise(turbSeed + 63f, t * 0.35f) - 0.5f;
        float rPitch = Mathf.PerlinNoise(turbSeed + 85f, t * 0.40f) - 0.5f;

        rb.AddForce(new Vector3(gx * 0.5f, gy, gz * 0.5f) * (k * rb.mass * 2.2f), ForceMode.Force);
        rb.AddRelativeTorque(rPitch * k * 1400f, 0f, rRoll * k * 1400f, ForceMode.Force);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMPLETION
    // ═══════════════════════════════════════════════════════════════════════════

    void Complete()
    {
        if (IsComplete) return;
        Result = Score("COMPLETE");
        if (IsExperiment)
        {
            bool ok = EvaluateSuccess();
            Experiment.Mark(ok ? EventMarkers.MissionSuccess : EventMarkers.MissionFailure,
                            Current.Id + "|" + Headline("COMPLETE"), ac);
            Experiment.Mark(EventMarkers.MissionEnd, Current.Id, ac);
            Experiment.WriteOutcome(Metrics, ok ? "SUCCESS" : "INCOMPLETE");
            // NOT closed here — GameManager calls CloseTrial() the instant the trial
            // ends, which is now immediately after this. Kept as two steps because the
            // crash path below reaches the same place by a different route.
            Result.Passed = ok;
        }
        else
        {
            if (Result != null && Result.Passed) VoiceCallouts.Instance?.SayCockpit("mission_complete");
            legacyLog.WriteSummary(Result);
            legacyLog.Close();
        }
        RestoreAll();
        IsComplete = true;
    }

    public void OnCrash()
    {
        if (IsComplete) return;
        Result = Score("CRASHED");
        Result.Passed = false;
        Result.Outcome = "CRASHED";
        Result.Headline = "Crashed — " + (ac.CrashReason != "" ? ac.CrashReason : "aircraft destroyed");
        if (IsExperiment)
        {
            Experiment.Mark(EventMarkers.Crash, ac.CrashReason, ac);
            Experiment.Mark(EventMarkers.MissionFailure, Current.Id + "|crash", ac);
            Experiment.Mark(EventMarkers.MissionEnd, Current.Id, ac);
            Experiment.WriteOutcome(Metrics, "CRASHED");
            // Closed by GameManager.CloseTrial(), same as Complete().
        }
        else
        {
            legacyLog.Marker(Time01, "CRASH", ac.CrashReason);
            legacyLog.WriteSummary(Result);
            legacyLog.Close();
        }
        RestoreAll();
        IsComplete = true;
    }

    /// <summary>Objective success test for an experiment mission. Deliberately
    /// coarse: the experiment's dependent variables are continuous (tracking error,
    /// latencies, EEG), and this flag exists only for the session report and the
    /// automated test harness — never as a score shown to the participant.</summary>
    bool EvaluateSuccess()
    {
        var m = Mission;
        if (m == null) return true;
        if (Result != null && Result.Outcome == "CRASHED") return false;
        // Forced-landing missions: surviving under control IS the success criterion.
        if (m.SurvivalIsSuccess) return !ac.Crashed;
        switch (m.Goal)
        {
            case ScenarioGoal.Land:         return landed || goAroundInitiated;
            case ScenarioGoal.TaxiTakeoff:  return rotated && !holdShortBusted;
            case ScenarioGoal.TakeoffClimb: return climbDone;
            // NAVIGATE was missing, and its absence was not visible until the bank
            // gained a navigation mission: it fell through to `default`, which scores
            // TIME IN ALTITUDE/HEADING TOLERANCE — and UpdateDeviation does not even
            // accumulate a tracking window for a Navigate goal, so sampleT stayed 0,
            // the ratio evaluated 0 / 1, and a mission that reached every waypoint was
            // written to performance.json as MISSION_FAILURE. A participant's
            // successful navigation trial would have been labelled a failed one.
            case ScenarioGoal.Navigate:     return wpReached >= Current.Waypoints.Count;
            case ScenarioGoal.Mission:      return landed && wpReached >= Current.Waypoints.Count;
            default:
                // And guard the ratio itself: with no tracking window at all, 0/1 = 0 is
                // not "flew badly", it is "this goal does not track". Failing on it turns
                // a missing case into a silent wrong answer instead of a loud one.
                if (sampleT < 1f) return !ac.Crashed;
                return inTolT / sampleT > 0.5f;
        }
    }

    ScenarioResult Score(string outcome)
    {
        var r = new ScenarioResult { Outcome = outcome };

        float inTolPct = sampleT > 1f ? 100f * inTolT / sampleT : 0f;
        float meanRt = rtCount > 0 ? rtSum / rtCount * 1000f : 0f;
        float jerkPerSec = jerkSum / Mathf.Max(1f, jerkTime);
        bool hasEvents = alarmsTotal > 0;

        // Only report tracking metrics when tracking actually happened, so an
        // inapplicable 0% can never be mistaken for terrible performance.
        if (sampleT > 1f)
        {
            r.Metrics["time_in_tolerance_pct"] = inTolPct;
            r.Metrics["mean_alt_error_m"] = altErrInt / sampleT;
            r.Metrics["mean_hdg_error_deg"] = hdgErrInt / sampleT;
            r.Metrics["rms_alt_error_m"] = Mathf.Sqrt(altErrSq / sampleT);
            r.Metrics["rms_hdg_error_deg"] = Mathf.Sqrt(hdgErrSq / sampleT);
            r.Metrics["tracking_window_s"] = sampleT;
        }
        r.Metrics["control_jerk_per_s"] = jerkPerSec;
        r.Metrics["trial_duration_s"] = Time01;
        // The motor covariates. On the cognitive axis these are a MANIPULATION CHECK
        // (they should NOT differ much between classes inside a phase row); on the
        // crosswind axis they are the covariate an EEG effect must survive adjustment for.
        ctrlActivity.WriteInto(r.Metrics);
        if (hasEvents)
        {
            r.Metrics["probes_total"] = alarmsTotal; r.Metrics["probes_hit"] = alarmsHit;
            r.Metrics["probes_missed"] = alarmsMissed; r.Metrics["false_alarms"] = falseAlarms;
            r.Metrics["mean_rt_ms"] = meanRt;
        }
        if (cueAt >= 0f) r.Metrics["cue_onset_s"] = cueAt;
        if (Checklist != null)
        {
            r.Metrics["checklist_items_done"] = Checklist.Index;
            r.Metrics["checklist_timeouts"] = Checklist.Timeouts;
        }
        if (goAroundInitiated) r.Metrics["go_around_latency_s"] = 1f;
        if (Current.Waypoints.Count > 0) r.Metrics["waypoints_reached"] = wpReached;
        if (Current.Goal == ScenarioGoal.TaxiTakeoff)
        {
            r.Metrics["taxi_distance_m"] = taxiDistance;
            r.Metrics["runway_incursion"] = holdShortBusted ? 1 : 0;
            r.Metrics["became_airborne"] = rotated ? 1 : 0;
            r.Metrics["taxi_phase_reached"] = taxiPhase;
            // Take-off quality, not merely take-off occurrence.
            r.Metrics["rotate_airspeed_kmh"] = rotateAirspeedKmh;
            r.Metrics["rotate_distance_m"] = rotateDistanceM;
            r.Metrics["roll_max_centerline_dev_m"] = rollMaxDevM;
            r.Metrics["roll_max_heading_dev_deg"] = rollMaxHdgDevDeg;
            r.Metrics["initial_climb_rate_ms"] = initialClimbRate;
            r.Metrics["time_rotate_to_level_s"] = climbDone ? targetAltAtT - airborneAtT : -1f;
        }
        if (Current.Goal == ScenarioGoal.Land || Current.Goal == ScenarioGoal.Mission)
        {
            r.Metrics["landed"] = landed ? 1 : 0;
            r.Metrics["touchdown_sink_ms"] = touchdownSink;
            r.Metrics["centerline_offset_m"] = Mathf.Abs(touchdownX);
            // Crosswind-specific landing quality. Only meaningful now that there is a
            // wind to be blown off the centreline BY: before the wind model, centreline
            // deviation was a nearly free metric and drift at touchdown was always zero.
            r.Metrics["touchdown_drift_deg"] = touchdownDrift;
            r.Metrics["touchdown_bank_deg"] = ac.TouchdownBank;
            r.Metrics["max_centerline_dev_m"] = maxCenterlineDev;
            r.Metrics["mean_abs_drift_deg"] = driftTime > 0.5f ? driftAbsInt / driftTime : 0f;
            // Landing quality, not merely survival.
            r.Metrics["touchdown_distance_m"] = touchdownDistanceM;
            r.Metrics["touchdown_speed_kmh"] = touchdownSpeedKmh;
            r.Metrics["flare_start_alt_m"] = flareStartAltM;
            r.Metrics["flare_duration_s"] = flareStartT > 0f ? Mathf.Max(0f, Time01 - flareStartT) : -1f;
            r.Metrics["glidepath_rms_deg"] = glideT > 1f ? Mathf.Sqrt(glideErrSq / glideT) : -1f;
            r.Metrics["runway_excursion"] = excursion ? 1 : 0;
        }
        Metrics = r.Metrics;

        float crashFactor = outcome == "CRASHED" ? 0f : 1f;
        var raw = new Dictionary<string, float>
        {
            ["precision"] = ScoringRubric.Precision(inTolPct) * crashFactor,
            ["reaction"] = ScoringRubric.Reaction(meanRt),
            ["errors"] = ScoringRubric.Errors(alarmsMissed, falseAlarms),
            ["completion"] = (Current.Goal == ScenarioGoal.TakeoffClimb
                                ? (climbDone ? 100f : 0f)
                                : ScoringRubric.Completion(wpReached, Current.Waypoints.Count)) * crashFactor,
            ["smoothness"] = ScoringRubric.Smoothness(jerkPerSec),
            ["landing"] = ScoringRubric.Landing(ac.Landing),
        };

        var weights = (Current.Weights != null && Current.Weights.Count > 0)
            ? Current.Weights : ScoringRubric.Weights(Current.Goal, hasEvents);
        float wsum = 0f;
        foreach (var kv in weights) wsum += kv.Value;
        if (wsum <= 0f) wsum = 1f;

        float score = 0f;
        foreach (var kv in weights)
        {
            float wnorm = kv.Value / wsum;
            float rawVal = raw.TryGetValue(kv.Key, out float rv) ? rv : 0f;
            r.Breakdown.Add(new ScoreLine(kv.Key, rawVal, wnorm));
            score += rawVal * wnorm;
        }
        r.Score = Mathf.Clamp(score, 0f, 100f);
        r.Passed = outcome != "CRASHED" && r.Score >= 60f;
        r.SessionLabel = Current.Title;
        r.SessionCond = IsExperiment ? Mission.ClassTag : Current.DiffLabel;
        r.Headline = Headline(outcome);
        return r;
    }

    string Headline(string outcome)
    {
        if (outcome == "CRASHED") return "Crashed";
        switch (Current.Goal)
        {
            case ScenarioGoal.Navigate: return wpReached + "/" + Current.Waypoints.Count + " waypoints reached";
            case ScenarioGoal.Land:
                return landed ? ac.Landing + " landing — sink " + touchdownSink.ToString("F1") + " m/s"
                     : goAroundInitiated ? "Go-around flown" : "Did not land";
            case ScenarioGoal.Mission: return wpReached + "/" + Current.Waypoints.Count + " wpts, " + (landed ? ac.Landing + " landing" : "no landing");
            case ScenarioGoal.TakeoffClimb: return climbDone ? "Climbed to " + Current.TargetAltitude.ToString("F0") + " m" : "Did not reach target altitude";
            case ScenarioGoal.TaxiTakeoff:
                if (holdShortBusted) return "RUNWAY INCURSION — entered the runway uncleared";
                if (!rotated) return "Did not get airborne (taxi phase " + taxiPhase + ")";
                return "Airborne — taxi " + taxiDistance.ToString("F0") + " m, hold short respected" +
                       (sampleT > 1f ? ", in tolerance " + (100f * inTolT / sampleT).ToString("F0") + "% after rotation" : "");
            default:
                float inTolPct = 100f * inTolT / Mathf.Max(1f, sampleT);
                return "In tolerance " + inTolPct.ToString("F0") + "%" +
                       (alarmsTotal > 0 ? ", " + alarmsHit + "/" + alarmsTotal + " responses" : "");
        }
    }

    void RestoreAll()
    {
        respPending = false; AlarmActive = false; Banner = ""; HasWaypoint = false;
        WeatherActive = false; FaultActive = false; Checklist = null;
        // Never let one trial's weather or wind leak into the next.
        WindModel.Disable();
        WorldBuilder.SetSkyClear();
        baseSkySeverity = 0f;
        if (gauges != null) foreach (var g in gauges) if (g != null) g.enabled = true;
        if (markersRoot != null) { SimUtil.Destroy(markersRoot); markersRoot = null; }
        ClearTraffic();
        if (sys != null) { sys.OnCuePerceptible = null; sys.OnConfigChange = null; sys.ResetAll(); }
        // Drop cockpit subscriptions so a control moved between trials can never write
        // into a closed log.
        CockpitEvents.ClearAll();
    }

    void SpawnWaypointMarkers(Scenario s)
    {
        if (s.Waypoints.Count == 0) return;
        markersRoot = new GameObject("ScenarioMarkers");
        markersRoot.transform.SetParent(gm.WorldRoot.transform);

        // GROUND vs AIR markers. Airborne navigation waypoints get the tall magenta
        // beacon that can be seen from kilometres away. TAXI checkpoints must NOT:
        // a 250 m pink pylon standing on the taxiway dominates the entire forward view,
        // and on a mission whose whole point is that the pilot reads the SIGNAGE and
        // the markings, a giant unmissable beacon does the navigating for them —
        // removing the task and biasing where they look. Ground checkpoints therefore
        // get a low, flat, unobtrusive marker beside the taxiway instead.
        bool ground = s.Goal == ScenarioGoal.TaxiTakeoff;
        var col = ground ? new Color(0.25f, 0.85f, 1f) : new Color(1f, 0.2f, 0.85f);

        foreach (var wp in s.Waypoints)
        {
            if (ground)
            {
                // a small marker post just off the paved edge, not on the centreline
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "WP_" + wp.Name;
                post.transform.SetParent(markersRoot.transform);
                post.transform.position = new Vector3(wp.Pos.x - 14f, 0.6f, wp.Pos.z);
                post.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
                SimUtil.Destroy(post.GetComponent<Collider>());
                Paint(post, col);
            }
            else
            {
                var pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pylon.name = "WP_" + wp.Name;
                pylon.transform.SetParent(markersRoot.transform);
                pylon.transform.position = new Vector3(wp.Pos.x, 250f, wp.Pos.z);
                pylon.transform.localScale = new Vector3(6f, 250f, 6f);
                SimUtil.Destroy(pylon.GetComponent<Collider>());
                Paint(pylon, col);
            }
        }
    }

    static void Paint(GameObject g, Color c)
    {
        g.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = c };
    }

    /// <summary>Write a marker to whichever logger this trial is using.</summary>
    void Mark(string tag, string detail = "")
    {
        if (IsExperiment) Experiment.Mark(tag, detail, ac);
        else legacyLog.Marker(Time01, tag, detail);
    }

    /// <summary>Mark the start of the post-trial questionnaire. The questionnaire
    /// period is recorded but is NOT a task epoch — the analysis must exclude it,
    /// which it can only do if the boundary is timestamped.</summary>
    /// <summary>Close the trial's files. Called the moment the trial ends.
    ///
    /// This used to be MarkTlxSubmitAndClose, driven by the participant pressing SUBMIT
    /// on the post-trial NASA-TLX form. That form has been removed, so nothing else
    /// would ever close the trial — telemetry, events and metadata would be left open
    /// and the run would lose its last flush. Closing here is not optional tidying.</summary>
    public void CloseTrial()
    {
        if (IsExperiment && Experiment.Open) Experiment.Close();
    }

    void Show(string msg) { Banner = msg; BannerUntil = Time01 + 4.5f; }

    public string StatusText()
    {
        if (Current == null) return "";
        string head = Current.Title + (IsExperiment ? "  [" + Mission.ClassTag + "]" : "  [" + Current.DiffLabel + "]");
        string seg = IsExperiment && Segment == "BASELINE" ? "  · baseline" : "";
        switch (Current.Goal)
        {
            case ScenarioGoal.Navigate:
                return head + seg + "  —  waypoint " + Mathf.Min(wpIdx + 1, WaypointTotal) + "/" + WaypointTotal +
                       (HasWaypoint ? "  (" + WaypointName + ")" : "  done");
            case ScenarioGoal.Land:
                return head + seg + "  —  land on the runway   alt " + ac.AltitudeM.ToString("F0") + " m   sink " +
                       (-ac.VerticalSpeedMs).ToString("F1") + " m/s   t-" +
                       Mathf.Max(0f, Current.Duration - Time01).ToString("F0") + "s";
            case ScenarioGoal.Mission:
                string ph = missionPhase == 0 ? "take off & climb" : missionPhase == 1 ? "navigate" : "return & land";
                return head + seg + "  —  " + ph + "   wpt " + wpReached + "/" + WaypointTotal;
            default:
                float inTolPct = 100f * inTolT / Mathf.Max(1f, sampleT);
                return head + seg + "  —  hold " + CurTargetAlt.ToString("F0") + " m / " + CurTargetHdg.ToString("F0") +
                       "°   in-tol " + inTolPct.ToString("F0") + "%   t-" + Mathf.Max(0f, Current.Duration - Time01).ToString("F0") + "s";
        }
    }
}
