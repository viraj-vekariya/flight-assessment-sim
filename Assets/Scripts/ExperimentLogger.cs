// ExperimentLogger — the data backbone of the cognitive-load experiment.
//
// ═══════════════════════════════════════════════════════════════════════════════
// OUTPUT LAYOUT  (persistentDataPath/FlightSimData/experiment/)
// ═══════════════════════════════════════════════════════════════════════════════
//   <PID>/<SESSION>/
//       session.json                     participant, order, seeds, build info
//       <TRIAL>_<MISSION>/
//           telemetry.csv                50 Hz aircraft + systems + control state
//           events.csv                   every marker, three clocks each
//           nasa_tlx.json                the participant's actual self-report
//           metadata.json                the mission spec + the pre-registration
//           eeg/                         where the EEG recording is dropped
//               sync.json                clock offsets for alignment
//               README.txt               what to put here and how it lines up
//
//   TRIAL is the ordinal position in the session (T01, T02, ...) and MISSION is the
//   mission id (L1..H4). Both are in the folder name so an analysis script can
//   recover presentation ORDER — which it must, to model learning and fatigue —
//   without parsing anything.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE CLOCK CONTRACT  (this is what makes EEG alignment possible)
// ═══════════════════════════════════════════════════════════════════════════════
//   Every row of telemetry.csv and every row of events.csv carries FOUR time fields:
//       t_mission  seconds since MISSION_START            (analysis-friendly)
//       t_host     monotonic host clock, seconds          (never jumps; the master)
//       t_unix     UTC unix seconds with millisecond res  (cross-machine)
//       t_lsl      LabStreamingLayer local_clock()        (-1 when LSL is absent)
//
//   t_host is the master. It is Time.realtimeSinceStartupAsDouble, which is
//   monotonic, is NOT affected by Time.timeScale, and does NOT drift with frame
//   rate — unlike accumulating Time.deltaTime, which is what the old logger did and
//   which accumulates error over a 300 s trial.
//
//   ALIGNMENT RECIPE (also written into every eeg/README.txt):
//     * If the EEG amplifier is on LSL and this sim is pushing markers, both
//       streams carry t_lsl; align directly on it. Nothing else is needed.
//     * If the EEG is recorded separately, record ANY shared physical event
//       (a TTL, a clap, a button press) and use the t_unix column of the nearest
//       marker to compute a single constant offset. sync.json stores the
//       host↔unix↔lsl offsets measured at MISSION_START so that offset can be
//       recovered even if the analysis happens months later.
//
// NO FABRICATION: this class never writes an EEG sample or a workload score it did
// not receive. The eeg/ folder is created empty, with instructions.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class ExperimentLogger
{
    public const float TelemetryHz = 50f;

    public string TrialDir { get; private set; } = "";
    public string TelemetryPath { get; private set; } = "";
    public string EventsPath { get; private set; } = "";
    public bool Open { get; private set; }

    StreamWriter telem, events;
    double t0Host, t0Unix, t0Lsl;
    float missionT;
    int eventSeq;
    MissionDefinition mission;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    /// <summary>UTF-8 with NO byte-order mark. .NET's Encoding.UTF8 emits a BOM,
    /// which survives into every CSV and JSON file and then breaks plain
    /// `json.load()` in Python and shifts the first column name in naive CSV
    /// readers. Every analysis script would have to know to pass utf-8-sig. Writing
    /// clean UTF-8 removes a whole class of silent downstream bugs.</summary>
    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    // ---- session-level state (shared by every trial in the session) -------------
    static string sessionDir = "";
    static int trialOrdinal;
    public static string SessionId { get; private set; } = "";
    public static int Seed { get; private set; }

    /// <summary>Monotonic host clock. The master time base for everything.</summary>
    public static double HostNow => Time.realtimeSinceStartupAsDouble;
    /// <summary>UTC unix seconds, millisecond resolution.</summary>
    public static double UnixNow =>
        (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

    public static string ExperimentRoot =>
        Path.Combine(Application.persistentDataPath, "FlightSimData", "experiment");

    // ═══════════════════════════════════════════════════════════════════════════
    // SESSION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Open a session folder and write session.json. Called once, when the
    /// participant is confirmed. `order` is the counterbalanced mission order.</summary>
    public static void BeginSession(List<string> order, int seed)
    {
        Seed = seed;
        trialOrdinal = 0;
        string pid = Sanitize(ParticipantManager.FilePrefix);
        SessionId = "S" + ParticipantManager.Session.ToString("00") + "_" + ParticipantManager.ISONow();
        sessionDir = Path.Combine(ExperimentRoot, pid, SessionId);
        Directory.CreateDirectory(sessionDir);

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"participant_id\": {J(pid)},");
        sb.AppendLine($"  \"session_id\": {J(SessionId)},");
        sb.AppendLine($"  \"session_number\": {ParticipantManager.Session},");
        sb.AppendLine($"  \"started_utc\": {J(System.DateTime.UtcNow.ToString("o"))},");
        sb.AppendLine($"  \"random_seed\": {seed},");
        sb.AppendLine($"  \"telemetry_hz\": {TelemetryHz.ToString("F0", CI)},");
        sb.AppendLine($"  \"unity_version\": {J(Application.unityVersion)},");
        sb.AppendLine($"  \"sim_build\": {J(Application.version)},");
        sb.AppendLine($"  \"platform\": {J(Application.platform.ToString())},");
        sb.AppendLine($"  \"lsl_available\": {(LSLSync.Available ? "true" : "false")},");
        // INPUT MODALITY. VR and desktop sessions must never be pooled: the interface is
        // a between-subject factor, not noise. Recorded per session so that can be
        // enforced in analysis rather than remembered.
        sb.AppendLine($"  \"input_modality\": {J(VRRuntime.Modality.ToString().ToLower())},");
        sb.AppendLine($"  \"vr_status\": {J(VRRuntime.StatusLine)},");
        sb.Append("  \"mission_order\": [");
        for (int i = 0; i < order.Count; i++) sb.Append((i > 0 ? ", " : "") + J(order[i]));
        sb.AppendLine("],");
        sb.AppendLine("  \"note\": \"No personally identifying information is stored. participant_id is a study code assigned by the experimenter.\"");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(sessionDir, "session.json"), sb.ToString(), Utf8NoBom);
        Debug.Log("[Experiment] session -> " + sessionDir);
    }

    public static bool SessionOpen => !string.IsNullOrEmpty(sessionDir);

    /// <summary>Fallback so a trial flown without a formal session still lands
    /// somewhere sane instead of throwing.</summary>
    static void EnsureSession()
    {
        if (!SessionOpen) BeginSession(new List<string>(), 0);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TRIAL
    // ═══════════════════════════════════════════════════════════════════════════

    public void BeginTrial(MissionDefinition m, Scenario s)
    {
        EnsureSession();
        Close();
        mission = m;
        trialOrdinal++;
        string tag = "T" + trialOrdinal.ToString("00") + "_" + Sanitize(m != null ? m.Id : (s != null ? s.Id : "UNKNOWN"));
        TrialDir = Path.Combine(sessionDir, tag);
        Directory.CreateDirectory(TrialDir);
        Directory.CreateDirectory(Path.Combine(TrialDir, "eeg"));

        t0Host = HostNow; t0Unix = UnixNow; t0Lsl = LSLSync.Clock();
        missionT = 0f; eventSeq = 0;

        TelemetryPath = Path.Combine(TrialDir, "telemetry.csv");
        EventsPath = Path.Combine(TrialDir, "events.csv");
        telem = new StreamWriter(TelemetryPath, false, Utf8NoBom);
        events = new StreamWriter(EventsPath, false, Utf8NoBom);

        telem.WriteLine(TelemetryHeader());
        events.WriteLine("seq,t_mission,t_host,t_unix,t_lsl,marker,detail,mission_phase," +
                         "altitude_m,airspeed_kmh,heading_deg");

        WriteSyncJson();
        WriteEegReadme();
        if (m != null) WriteMetadata(m, s);
        Open = true;
    }

    public void Close()
    {
        if (!Open) return;
        telem.Flush(); telem.Close(); telem = null;
        events.Flush(); events.Close(); events = null;
        Open = false;
    }

    /// <summary>Mission time, driven by the engine so it matches event scheduling.</summary>
    public void SetMissionTime(float t) => missionT = t;

    // ═══════════════════════════════════════════════════════════════════════════
    // TELEMETRY
    // ═══════════════════════════════════════════════════════════════════════════

    public static string TelemetryHeader() =>
        // time
        "t_mission,t_host,t_unix,t_lsl," +
        // aircraft (TRUE state, from the flight model)
        "pos_x,pos_y,pos_z,altitude_m,airspeed_kmh,vspeed_ms,heading_deg,pitch_deg,roll_deg,yaw_rate_dps,aoa_deg," +
        // aircraft (INDICATED state, what the pilot's instruments show — can differ)
        "ind_airspeed_kmh,ind_altitude_m,ind_vspeed_ms," +
        // controls
        "in_pitch,in_roll,in_yaw,throttle,flaps_actual,flaps_selected,spoiler,brake," +
        // trim + who is flying. `elevator_cmd` is stick+trim combined: it separates
        // "how much are they HOLDING" from "how much are they MOVING", which is the
        // distinction that makes trim a workload measure rather than just a control.
        "trim,elevator_cmd,brake_pressure," +
        // Control ownership, so a later analysis can tell hand-flown segments from
        // keyboard ones without inferring it. 1 = a cockpit control is driving the axis.
        "ovr_pitch,ovr_roll,ovr_yaw,ovr_throttle,ovr_trim,ovr_brake,control_held," +
        // control activity (for the motor-artifact confound check)
        "ctrl_jerk," +
        // navigation / task error
        "target_alt_m,target_hdg_deg,alt_err_m,hdg_err_deg,dist_to_wpt_m,cross_track_m," +
        // traffic + ground phase (added with the taxi/take-off missions)
        "nearest_traffic_m,taxi_phase," +
        // flags
        "grounded,stalled,stall_warn,weather_active,gauge_fault," +
        // mission context
        "mission_phase,segment," +
        // systems
        AircraftSystems.TelemetryHeader;

    public void Sample(CessnaPhysics ac, AircraftSystems sys, ScenarioEngine eng,
                       AircraftController ctl, float jerk, string segment)
    {
        if (!Open || ac == null) return;
        var sb = new StringBuilder(512);
        Vector3 p = ac.transform.position;

        A(sb, missionT, 3); A(sb, HostNow - t0Host, 4); A(sb, UnixNow, 3);
        A(sb, LSLSync.Available ? LSLSync.Clock() : -1.0, 4);

        A(sb, p.x, 2); A(sb, p.y, 2); A(sb, p.z, 2);
        A(sb, ac.AltitudeM, 2); A(sb, ac.AirspeedKmh, 2); A(sb, ac.VerticalSpeedMs, 3);
        A(sb, ac.HeadingDeg, 2); A(sb, ac.PitchDeg, 2); A(sb, ac.RollDeg, 2);
        A(sb, ac.YawRateDps, 2); A(sb, ac.AoADeg, 2);

        if (sys != null) { A(sb, sys.IndicatedAirspeedKmh, 2); A(sb, sys.IndicatedAltitudeM, 2); A(sb, sys.IndicatedVSpeedMs, 3); }
        else { A(sb, ac.AirspeedKmh, 2); A(sb, ac.AltitudeM, 2); A(sb, ac.VerticalSpeedMs, 3); }

        A(sb, ac.pitchInput, 4); A(sb, ac.rollInput, 4); A(sb, ac.yawInput, 4);
        A(sb, ac.Throttle01, 4); A(sb, ac.Flaps01, 3);
        A(sb, ctl != null ? ctl.FlapsSelected : ac.Flaps01, 3);
        A(sb, ac.Spoiler01, 3); sb.Append(ac.braking ? "1," : "0,");

        A(sb, ac.trim, 4); A(sb, ac.ElevatorCmd, 4); A(sb, ac.brakeInput01, 3);
        sb.Append(ctl != null && ctl.PitchOverridden ? "1," : "0,");
        sb.Append(ctl != null && ctl.RollOverridden ? "1," : "0,");
        sb.Append(ctl != null && ctl.YawOverridden ? "1," : "0,");
        sb.Append(ctl != null && ctl.ThrottleOverridden ? "1," : "0,");
        sb.Append(ctl != null && ctl.TrimOverridden ? "1," : "0,");
        sb.Append(ctl != null && ctl.BrakeOverridden ? "1," : "0,");
        sb.Append(HeldControlId()).Append(',');

        A(sb, jerk, 4);

        if (eng != null)
        {
            A(sb, eng.CurTargetAlt, 1); A(sb, eng.CurTargetHdg, 1);
            A(sb, eng.AltError, 2); A(sb, eng.HdgError, 2);
            float d = -1f, xt = 0f;
            if (eng.HasWaypoint)
            {
                Vector3 w = eng.WaypointPos;
                d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(w.x, w.z));
            }
            // Cross-track against the runway centreline (x = 0, runway faces +Z),
            // which is the meaningful lateral error for every approach mission.
            xt = p.x;
            A(sb, d, 2); A(sb, xt, 2);
            A(sb, eng.NearestTrafficM, 1);
            sb.Append(eng.TaxiPhase).Append(',');
        }
        else { sb.Append("0,0,0,0,-1,0,-1,0,"); }

        sb.Append(ac.Grounded ? "1," : "0,");
        sb.Append(ac.Stalled ? "1," : "0,");
        sb.Append(ac.StallWarning ? "1," : "0,");
        sb.Append(eng != null && eng.WeatherActive ? "1," : "0,");
        sb.Append(eng != null && eng.FaultActive ? "1," : "0,");

        sb.Append(mission != null ? mission.Phase.ToString() : "None").Append(',');
        sb.Append(segment).Append(',');

        sb.Append(sys != null ? sys.TelemetryFields() : EmptySystems());
        telem.WriteLine(sb.ToString());
    }

    /// <summary>Which physical control is in the pilot's hand right now, or "none".
    /// One column rather than one per control: only one control per hand is grabbable,
    /// and a string is far easier to read in an analysis than nine flags.</summary>
    static string HeldControlId()
    {
        var rig = CockpitControlRig.Instance;
        if (rig == null) return "none";
        foreach (var c in rig.Controls) if (c != null && c.Grabbed) return c.spec.id;
        return "none";
    }

    static string EmptySystems()
    {
        int n = AircraftSystems.TelemetryHeader.Split(',').Length;
        return new string(',', n - 1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Write one marker with all four clocks and the aircraft state at that
    /// instant, and push the same tag to LSL so the EEG stream sees it live.</summary>
    public void Mark(string marker, string detail = "", CessnaPhysics ac = null, string phase = "")
    {
        if (!Open) return;
        eventSeq++;
        double lsl = LSLSync.Available ? LSLSync.Clock() : -1.0;
        var sb = new StringBuilder(160);
        sb.Append(eventSeq).Append(',');
        A(sb, missionT, 3); A(sb, HostNow - t0Host, 4); A(sb, UnixNow, 3); A(sb, lsl, 4);
        sb.Append(marker).Append(',');
        sb.Append(string.IsNullOrEmpty(detail) ? "" : detail.Replace(",", ";").Replace("\n", " ")).Append(',');
        sb.Append(string.IsNullOrEmpty(phase) ? (mission != null ? mission.Phase.ToString() : "") : phase).Append(',');
        if (ac != null) { A(sb, ac.AltitudeM, 1); A(sb, ac.AirspeedKmh, 1); A(sb, ac.HeadingDeg, 1); }
        else sb.Append(",,");
        events.WriteLine(sb.ToString());
        events.Flush();   // markers are cheap and precious — never lose one to a crash

        LSLSync.Marker(string.IsNullOrEmpty(detail) ? marker : marker + "|" + detail);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SIDECAR FILES
    // ═══════════════════════════════════════════════════════════════════════════

    void WriteSyncJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"description\": \"Clock offsets captured at trial start. t_host is the master monotonic clock; add these offsets to convert.\",");
        sb.AppendLine($"  \"trial_start_host_s\": {t0Host.ToString("F6", CI)},");
        sb.AppendLine($"  \"trial_start_unix_s\": {t0Unix.ToString("F6", CI)},");
        sb.AppendLine($"  \"trial_start_lsl_s\": {t0Lsl.ToString("F6", CI)},");
        sb.AppendLine($"  \"trial_start_utc_iso\": {J(System.DateTime.UtcNow.ToString("o"))},");
        sb.AppendLine($"  \"lsl_available\": {(LSLSync.Available ? "true" : "false")},");
        sb.AppendLine("  \"host_to_unix_offset_s\": " + (t0Unix - t0Host).ToString("F6", CI) + ",");
        sb.AppendLine("  \"host_to_lsl_offset_s\": " + (LSLSync.Available ? (t0Lsl - t0Host).ToString("F6", CI) : "null"));
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(TrialDir, "eeg", "sync.json"), sb.ToString(), Utf8NoBom);
    }

    void WriteEegReadme()
    {
        string txt =
"EEG DATA FOR THIS TRIAL\n" +
"=======================\n\n" +
"This folder is intentionally EMPTY except for sync.json. The simulator does not\n" +
"record, synthesise or estimate EEG. Put the real recording here.\n\n" +
"WHAT TO DROP IN\n" +
"  eeg_raw.<ext>     the recording exactly as the amplifier software saved it\n" +
"                    (.xdf from LabRecorder, .bdf/.edf, .fif, .vhdr+.eeg+.vmrk ...)\n" +
"  eeg_notes.txt     montage, sampling rate, reference, impedances, anything odd\n\n" +
"HOW IT LINES UP WITH ../events.csv\n" +
"  Case 1 - LSL. If sync.json says lsl_available true, the sim pushed every marker\n" +
"  in events.csv to the 'FlightSimMarkers' LSL outlet with the SAME t_lsl value that\n" +
"  is in the t_lsl column. If you recorded with LabRecorder, the marker stream is\n" +
"  already inside the .xdf next to the EEG; align on it directly, no offset needed.\n\n" +
"  Case 2 - separate recorder. Use ONE shared physical event (TTL, button, clap) to\n" +
"  find a single constant offset, then apply it to the t_unix column. sync.json holds\n" +
"  the host/unix/LSL offsets measured at trial start so this is recoverable later.\n\n" +
"WHICH MARKER IS EPOCH ZERO\n" +
"  For anything event-related use CUE_ONSET, not TRIGGER_ARMED. TRIGGER_ARMED is when\n" +
"  the simulation injected the abnormality; CUE_ONSET is when the pilot could first\n" +
"  perceive it. For a gradual failure (carburettor ice) they can be 20-40 s apart, and\n" +
"  averaging on TRIGGER_ARMED would smear the response away entirely.\n\n" +
"BASELINE\n" +
"  The session-level resting baselines are in ../../baseline_*/ . The 60 s at the head\n" +
"  of THIS trial (segment column == 'BASELINE' in telemetry.csv) is the within-mission\n" +
"  baseline: same visuals, same manual control, no manipulation.\n";
        File.WriteAllText(Path.Combine(TrialDir, "eeg", "README.txt"), txt, Utf8NoBom);
    }

    void WriteMetadata(MissionDefinition m, Scenario s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"participant_id\": {J(Sanitize(ParticipantManager.FilePrefix))},");
        sb.AppendLine($"  \"session_id\": {J(SessionId)},");
        sb.AppendLine($"  \"trial_ordinal\": {trialOrdinal},");
        sb.AppendLine($"  \"mission_id\": {J(m.Id)},");
        sb.AppendLine($"  \"mission_name\": {J(m.Name)},");
        sb.AppendLine($"  \"condition\": {J(m.ClassTag)},");
        sb.AppendLine($"  \"flight_phase\": {J(m.Phase.ToString())},");
        sb.AppendLine($"  \"random_seed\": {Seed},");
        sb.AppendLine($"  \"input_modality\": {J(VRRuntime.Modality.ToString().ToLower())},");
        sb.AppendLine($"  \"started_utc\": {J(System.DateTime.UtcNow.ToString("o"))},");
        sb.AppendLine("  \"initial_conditions\": {");
        sb.AppendLine($"    \"start\": {J(m.Start.ToString())},");
        sb.AppendLine($"    \"goal\": {J(m.Goal.ToString())},");
        sb.AppendLine($"    \"altitude_m\": {m.StartAltitudeM.ToString("F0", CI)},");
        sb.AppendLine($"    \"airspeed_kmh\": {m.StartAirspeedKmh.ToString("F0", CI)},");
        sb.AppendLine($"    \"heading_deg\": {m.StartHeadingDeg.ToString("F0", CI)},");
        sb.AppendLine($"    \"target_altitude_m\": {m.TargetAltitudeM.ToString("F0", CI)},");
        sb.AppendLine($"    \"target_heading_deg\": {m.TargetHeadingDeg.ToString("F0", CI)},");
        sb.AppendLine($"    \"alt_tolerance_m\": {m.AltToleranceM.ToString("F0", CI)},");
        sb.AppendLine($"    \"hdg_tolerance_deg\": {m.HdgToleranceDeg.ToString("F0", CI)},");
        sb.AppendLine($"    \"flaps\": {m.StartFlaps01.ToString("F2", CI)},");
        sb.AppendLine($"    \"fuel_l\": {m.StartFuelL.ToString("F0", CI)},");
        sb.AppendLine($"    \"ambient_turbulence\": {m.AmbientTurbulence.ToString("F2", CI)},");
        sb.AppendLine($"    \"visibility_01\": {m.Visibility01.ToString("F2", CI)}");
        sb.AppendLine("  },");
        sb.AppendLine($"  \"duration_s\": {m.DurationS.ToString("F0", CI)},");
        sb.AppendLine($"  \"in_task_baseline_s\": {m.InTaskBaselineS.ToString("F0", CI)},");

        // The actual, jittered schedule this trial will run — so the trial is
        // reproducible even without re-deriving it from the seed.
        sb.AppendLine("  \"event_schedule\": [");
        if (s != null)
            for (int i = 0; i < s.Events.Count; i++)
            {
                var e = s.Events[i];
                sb.Append("    {\"t_nominal\": " + e.Time.ToString("F1", CI) +
                          ", \"t_scheduled\": " + e.ScheduledTime.ToString("F1", CI) +
                          ", \"jitter_s\": " + e.Jitter.ToString("F1", CI) +
                          ", \"type\": " + J(e.Type.ToString()) +
                          ", \"failure\": " + J(e.Failure.ToString()) +
                          ", \"label\": " + J(e.Label) + "}");
                sb.AppendLine(i < s.Events.Count - 1 ? "," : "");
            }
        sb.AppendLine("  ],");

        sb.AppendLine("  \"workload_profile\": {");
        var d = m.Profile.AsDictionary();
        int k = 0;
        foreach (var kv in d) { sb.AppendLine($"    {J(kv.Key)}: {kv.Value}" + (++k < d.Count ? "," : "")); }
        sb.AppendLine("  },");
        sb.AppendLine($"  \"predicted_load_index\": {m.Profile.PLI.ToString("F1", CI)},");
        sb.AppendLine("  \"expected_nasa_tlx\": {");
        sb.AppendLine($"    \"mental\": {m.Expected.Mental}, \"physical\": {m.Expected.Physical},");
        sb.AppendLine($"    \"temporal\": {m.Expected.Temporal}, \"performance\": {m.Expected.Performance},");
        sb.AppendLine($"    \"effort\": {m.Expected.Effort}, \"frustration\": {m.Expected.Frustration},");
        sb.AppendLine($"    \"rtlx\": {m.Expected.RTLX.ToString("F1", CI)}");
        sb.AppendLine("  },");
        sb.AppendLine("  \"note_on_expected_tlx\": \"PRE-REGISTERED PREDICTION, recorded before data collection. It is never shown to the participant and must never be substituted for a measured score.\",");
        sb.AppendLine($"  \"load_rationale\": {J(m.LoadRationale)},");
        sb.AppendLine($"  \"eeg_relevance\": {J(m.EegRelevance)},");
        sb.AppendLine($"  \"expected_errors\": {J(m.ExpectedErrors)},");
        sb.AppendLine($"  \"success_criteria\": {J(m.SuccessCriteria)},");
        sb.AppendLine($"  \"failure_conditions\": {J(m.FailureConditions)},");
        sb.AppendLine($"  \"aviation_basis\": {J(m.AviationBasis)},");
        sb.AppendLine($"  \"approximations\": {J(m.Approximations)}");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(TrialDir, "metadata.json"), sb.ToString(), Utf8NoBom);
    }

    /// <summary>Write the participant's ACTUAL NASA-TLX response for this trial.
    /// Called only from the questionnaire submit path. Never synthesised.</summary>
    public void WriteTlx(WorkloadRating r, float missionElapsed)
    {
        if (string.IsNullOrEmpty(TrialDir)) return;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"participant_id\": {J(Sanitize(ParticipantManager.FilePrefix))},");
        sb.AppendLine($"  \"session_id\": {J(SessionId)},");
        sb.AppendLine($"  \"trial_ordinal\": {trialOrdinal},");
        sb.AppendLine($"  \"mission_id\": {J(mission != null ? mission.Id : "")},");
        sb.AppendLine($"  \"condition\": {J(mission != null ? mission.ClassTag : "")},");
        sb.AppendLine($"  \"submitted_utc\": {J(System.DateTime.UtcNow.ToString("o"))},");
        sb.AppendLine($"  \"trial_duration_s\": {missionElapsed.ToString("F1", CI)},");
        sb.AppendLine("  \"instrument\": \"NASA-TLX, raw/unweighted (RTLX). 21-point scale, 0-100 in steps of 5, per the NASA TLX manual.\",");
        sb.AppendLine("  \"nasa_tlx\": {");
        sb.AppendLine($"    \"mental_demand\": {r.MentalDemand.ToString("F0", CI)},");
        sb.AppendLine($"    \"physical_demand\": {r.PhysicalDemand.ToString("F0", CI)},");
        sb.AppendLine($"    \"temporal_demand\": {r.TemporalDemand.ToString("F0", CI)},");
        sb.AppendLine($"    \"performance\": {r.Performance.ToString("F0", CI)},");
        sb.AppendLine($"    \"effort\": {r.Effort.ToString("F0", CI)},");
        sb.AppendLine($"    \"frustration\": {r.Frustration.ToString("F0", CI)},");
        sb.AppendLine($"    \"rtlx\": {r.RTLX.ToString("F2", CI)}");
        sb.AppendLine("  },");
        sb.AppendLine("  \"note_performance_anchor\": \"Performance is anchored GOOD=0 .. POOR=100, so a HIGHER value means the participant judged their own performance WORSE. It is averaged in that direction, per raw-TLX convention.\",");
        sb.AppendLine($"  \"bedford\": {r.Bedford}");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(TrialDir, "nasa_tlx.json"), sb.ToString(), Utf8NoBom);
    }

    /// <summary>Append the trial's objective performance summary to metadata's sibling.</summary>
    public void WriteOutcome(Dictionary<string, float> metrics, string outcome)
    {
        if (string.IsNullOrEmpty(TrialDir)) return;
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"outcome\": {J(outcome)},");
        sb.AppendLine($"  \"mission_id\": {J(mission != null ? mission.Id : "")},");
        sb.AppendLine("  \"metrics\": {");
        int i = 0;
        foreach (var kv in metrics)
            sb.AppendLine($"    {J(kv.Key)}: {kv.Value.ToString("F3", CI)}" + (++i < metrics.Count ? "," : ""));
        sb.AppendLine("  }");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(TrialDir, "performance.json"), sb.ToString(), Utf8NoBom);
    }

    // ---- helpers ---------------------------------------------------------------
    static void A(StringBuilder sb, double v, int dp) { sb.Append(v.ToString("F" + dp, CI)); sb.Append(','); }
    static void A(StringBuilder sb, float v, int dp) { sb.Append(v.ToString("F" + dp, CI)); sb.Append(','); }
    static string J(string s) => s == null ? "null"
        : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "") + "\"";
    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "UNKN";
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        return sb.ToString();
    }
}
