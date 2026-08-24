using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// DocGen — generates MISSION_DESIGN.md and the final mission table straight from
// MissionLibrary.cs, so the documentation cannot drift away from what the simulator
// actually runs. Re-run it after ANY change to the mission set:
//
//   Unity -batchmode -nographics -quit -projectPath <proj> \
//         -executeMethod DocGen.Generate -logFile docgen.log
//
// or from the editor: Tools -> Experiment -> Regenerate mission docs.
public static class DocGen
{
    /// <summary>Do nothing, successfully. Reaching this method at all is the proof:
    /// Unity only resolves an -executeMethod target after every script has compiled, so a
    /// run that prints COMPILE OK could not have had a compile error. Cheaper and far less
    /// ambiguous than grepping a log for "error CS" and trusting the absence of a match.</summary>
    public static void CompileOnly()
    {
        UnityEngine.Debug.Log("[COMPILE] COMPILE OK");
        UnityEditor.EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Experiment/Regenerate mission docs")]
    public static void Generate()
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("# FINAL_MISSION_DESIGN — the mission bank");
        sb.AppendLine();
        sb.AppendLine("> **This file is GENERATED from `Assets/Scripts/MissionLibrary.cs` by");
        sb.AppendLine("> `Assets/Editor/DocGen.cs`. Do not edit it by hand — edit the mission set");
        sb.AppendLine("> and regenerate, so the document and the simulator can never disagree.**");
        sb.AppendLine();
        sb.AppendLine("Generated: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine();
        sb.AppendLine("Every mission is **" + MissionLibrary.StandardDurationS.ToString("F0", ci) +
                      " s** long with a **" + MissionLibrary.StandardBaselineS.ToString("F0", ci) +
                      " s in-task baseline** at its head. Duration is held constant across all three");
        sb.AppendLine("workload classes on purpose — see `FINAL_EXPERIMENT_PROTOCOL.md`.");
        sb.AppendLine();
        sb.AppendLine("## The bank, and the session");
        sb.AppendLine();
        sb.AppendLine("The bank holds **" + MissionLibrary.All().Count + " missions** on **two experimental axes**:");
        sb.AppendLine();
        sb.AppendLine("* **Cognitive axis — " + MissionLibrary.CognitiveAxis().Count + " missions.** 4 flight phases x 3 workload");
        sb.AppendLine("  classes x " + MissionLibrary.VariantCount + " interchangeable variants. Manual demand is MATCHED within each");
        sb.AppendLine("  phase row, which is what licenses reading a class difference as cognitive rather");
        sb.AppendLine("  than muscular.");
        sb.AppendLine("* **Psychomotor-integrated axis — " + MissionLibrary.PsychomotorAxis().Count + " missions.** Crosswind take-offs and");
        sb.AppendLine("  landings at graded crosswind levels. Deliberately NOT on the Low/Medium/High");
        sb.AppendLine("  scale: crosswind raises manual demand by construction, so putting it there would");
        sb.AppendLine("  break the matching the cognitive axis depends on. Analysed separately, with the");
        sb.AppendLine("  control-activity covariates, and its cognitive component isolated as a discrete");
        sb.AppendLine("  continue-or-abandon decision against a stated crosswind limit.");
        sb.AppendLine();
        sb.AppendLine("**A participant flies TWELVE**, not " + MissionLibrary.All().Count + ": one variant index per phase row,");
        sb.AppendLine("rotated by Latin square. " + MissionLibrary.All().Count + " x " + MissionLibrary.StandardDurationS.ToString("F0", ci) + " s is over three hours of flying inside one");
        sb.AppendLine("EEG session, and fatigue would dominate every contrast the study exists to measure.");
        sb.AppendLine("Within a phase row all three classes share one variant, so the Low-Medium-High");
        sb.AppendLine("contrast is always variant-matched; across rows the participant meets different");
        sb.AppendLine("variants, so variant is not perfectly nested in participant. See `MISSION_BANK_DESIGN.md`.");
        sb.AppendLine();

        // ---- summary table ----
        sb.AppendLine("## Final mission table");
        sb.AppendLine();
        sb.AppendLine("| ID | Axis | Phase | Class | v | Mission | Cognitive mechanism | Main abnormality | Time pressure | Multitasking | Decision complexity | PLI |");
        sb.AppendLine("|----|------|-------|-------|---|---------|---------------------|------------------|---------------|--------------|---------------------|-----|");
        foreach (var m in MissionLibrary.All())
        {
            sb.AppendLine("| " + m.Id + " | " + (m.Axis == LoadAxis.Cognitive ? "cog" : "psy") + " | " +
                          m.Phase + " | " + m.ClassTag + " | " + m.Variant + " | " + m.Name + " | " +
                          (string.IsNullOrEmpty(m.Mechanism) ? Mechanism(m) : m.Mechanism) + " | " + Abnormality(m) + " | " +
                          Bar(m.Profile.TemporalDemand) + " | " +
                          Bar(m.Profile.AttentionSwitching) + " | " +
                          Bar(m.Profile.DecisionComplexity) + " | " +
                          m.Profile.PLI.ToString("F0", ci) + " |");
        }
        sb.AppendLine();
        sb.AppendLine("`PLI` = Predicted Load Index (0-100) from the weighted demand model in");
        sb.AppendLine("`COGNITIVE_LOAD_MODEL.md`. It is a **prediction the experiment tests**, not a result.");
        sb.AppendLine();

        // ---- the crossed design grid ----
        sb.AppendLine("## The design grid — phase fully crossed with workload class");
        sb.AppendLine();
        sb.AppendLine("Every class appears exactly once in every phase, so a class effect can never be");
        sb.AppendLine("a phase effect, and every mission has a phase-matched LOW baseline.");
        sb.AppendLine();
        sb.AppendLine("| Flight phase | v | LOW | MEDIUM | HIGH | manual demand (L/M/H) |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (FlightPhase ph in MissionLibrary.Rows)
            for (int v = 1; v <= MissionLibrary.VariantCount; v++)
            {
                string row = "| " + (v == 1 ? "**" + ph.ToString() + "**" : "") + " | " + v + " ";
                string man = "| ";
                foreach (WorkloadClass wc in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
                {
                    var mm = MissionLibrary.Cell(ph, wc, v);
                    row += "| " + (mm == null ? "—" : mm.Id + " " + mm.Name + " (PLI " + mm.Profile.PLI.ToString("F0", ci) + ")") + " ";
                    man += (wc == WorkloadClass.Low ? "" : "/") + (mm == null ? "—" : mm.Profile.ManualControl.ToString());
                }
                sb.AppendLine(row + man + " |");
            }
        sb.AppendLine();
        sb.AppendLine("Manual demand is matched WITHIN each phase row (spread <= 1 on a 0-4 scale), which");
        sb.AppendLine("is where the class contrast is made — so a difference inside a row cannot be muscle");
        sb.AppendLine("activity rather than cognitive load.");
        sb.AppendLine();

        // ---- within-cell exchangeability ----
        sb.AppendLine("## Variant exchangeability (design check)");
        sb.AppendLine();
        sb.AppendLine("The variants of a cell are supposed to be interchangeable realisations of it, so");
        sb.AppendLine("their PREDICTED loads must agree. A variant scoring well above its siblings is not");
        sb.AppendLine("a second version of that class — it is drifting toward the next one, and since");
        sb.AppendLine("variant is assigned by participant, a participant's effective class would then");
        sb.AppendLine("depend on which variant they were given. Tolerance: **8 points on the 0-100 PLI**,");
        sb.AppendLine("enforced by the mission battery.");
        sb.AppendLine();
        sb.AppendLine("| Cell | " + JoinVariantHeaders() + " | spread | within tolerance |");
        sb.AppendLine("|---|" + RepeatCol(MissionLibrary.VariantCount + 2) + "|");
        foreach (FlightPhase ph in MissionLibrary.Rows)
            foreach (WorkloadClass wc in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
            {
                float lo = 1e9f, hi = -1e9f;
                string cells = "";
                for (int v = 1; v <= MissionLibrary.VariantCount; v++)
                {
                    var mm = MissionLibrary.Cell(ph, wc, v);
                    if (mm == null) { cells += "| — "; continue; }
                    float pv = mm.Profile.PLI;
                    lo = Mathf.Min(lo, pv); hi = Mathf.Max(hi, pv);
                    cells += "| " + mm.Id + " " + pv.ToString("F1", ci) + " ";
                }
                float sp = hi - lo;
                sb.AppendLine("| " + ph + "/" + wc.ToString().ToUpper() + " " + cells +
                              "| " + sp.ToString("F1", ci) + " | " + (sp <= 8f ? "yes" : "**NO**") + " |");
            }
        sb.AppendLine();

        // ---- the second axis ----
        sb.AppendLine("## The psychomotor-integrated axis (crosswind)");
        sb.AppendLine();
        sb.AppendLine("| ID | Mission | Crosswind | Headwind | Gust | Surface wind | Manual demand | PLI |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var m in MissionLibrary.PsychomotorAxis())
            sb.AppendLine("| " + m.Id + " | " + m.Name + " | " + m.CrosswindMs.ToString("F1", ci) + " m/s | " +
                          m.HeadwindMs.ToString("F1", ci) + " m/s | " + m.WindGustMs.ToString("F1", ci) + " m/s | " +
                          m.WindReport + " | " + m.Profile.ManualControl + " | " + m.Profile.PLI.ToString("F0", ci) + " |");
        sb.AppendLine();
        sb.AppendLine("These PLI values are **not comparable with the cognitive axis's**: they come from");
        sb.AppendLine("the same weighted model, but the model deliberately weights ManualControl LOW, and");
        sb.AppendLine("ManualControl is precisely what these missions manipulate. A finding from this axis");
        sb.AppendLine("is stated as *increased integrated psychomotor/cognitive demand*, never as");
        sb.AppendLine("*crosswind increased cognitive workload*.");
        sb.AppendLine();

        
        // ---- class separation check ----
        sb.AppendLine("## Class separation (design check)");
        sb.AppendLine();
        sb.AppendLine("| Class | n | PLI min | PLI max | PLI mean | manual-control range |");
        sb.AppendLine("|-------|---|---------|---------|----------|----------------------|");
        foreach (WorkloadClass c in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
        {
            var list = MissionLibrary.ForClass(c);   // cognitive axis only, by default
            float mn = 999f, mx = -1f, sum = 0f; int manMin = 9, manMax = -1;
            foreach (var m in list)
            {
                float p = m.Profile.PLI;
                mn = Mathf.Min(mn, p); mx = Mathf.Max(mx, p); sum += p;
                manMin = Mathf.Min(manMin, m.Profile.ManualControl);
                manMax = Mathf.Max(manMax, m.Profile.ManualControl);
            }
            sb.AppendLine("| " + c.ToString().ToUpper() + " | " + list.Count + " | " +
                          mn.ToString("F1", ci) + " | " + mx.ToString("F1", ci) + " | " +
                          (sum / Mathf.Max(1, list.Count)).ToString("F1", ci) + " | " +
                          manMin + "-" + manMax + " |");
        }
        sb.AppendLine();
        sb.AppendLine("The classes must not overlap on PLI, and the manual-control ranges should stay");
        sb.AppendLine("close together — a large manual-control gap between classes would mean an EEG");
        sb.AppendLine("difference could be muscle activity rather than cognitive load.");
        sb.AppendLine();

        // ---- full specs ----
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Full mission specifications");
        foreach (var m in MissionLibrary.All()) AppendMission(sb, m, ci);

        // ---- checklists ----
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Drills used by the missions");
        sb.AppendLine();
        foreach (var d in ChecklistLibrary.All())
        {
            sb.AppendLine("### " + d.Title + "  (`" + d.Id + "`)");
            sb.AppendLine();
            foreach (var it in d.Items)
                sb.AppendLine("- **" + it.Kind.ToString().ToUpper() + "** — " + it.Label +
                              "  *(timeout " + it.TimeoutS.ToString("F0", ci) + " s)*");
            sb.AppendLine();
        }
        sb.AppendLine("`DO` items complete only when the aircraft/systems state actually satisfies them;");
        sb.AppendLine("`CHECK` items complete on the participant's acknowledgement. A timed-out item is");
        sb.AppendLine("logged as `CHECKLIST_TIMEOUT` and the drill advances, so one stuck item can never");
        sb.AppendLine("deadlock a trial.");
        sb.AppendLine();

        // ---- marker vocabulary ----
        sb.AppendLine("## Event-marker vocabulary");
        sb.AppendLine();
        sb.AppendLine("Closed set of " + EventMarkers.All.Length + " tags (`Assets/Scripts/EventMarkers.cs`):");
        sb.AppendLine();
        sb.AppendLine("```");
        for (int i = 0; i < EventMarkers.All.Length; i++)
        {
            sb.Append(EventMarkers.All[i].PadRight(24));
            if ((i + 1) % 3 == 0) sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine();

        string path = Path.Combine(root, "FINAL_MISSION_DESIGN.md");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));   // no BOM
        Debug.Log("[DocGen] wrote " + path);

        WriteTelemetrySchema(root);
    }

    static void AppendMission(StringBuilder sb, MissionDefinition m, CultureInfo ci)
    {
        sb.AppendLine();
        sb.AppendLine("### " + m.Id + " — " + m.Name);
        sb.AppendLine();
        sb.AppendLine("| field | value |");
        sb.AppendLine("|-------|-------|");
        Row(sb, "Experimental axis", m.Axis == LoadAxis.Cognitive
                ? "cognitive (manual demand matched within the phase row)"
                : "psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale)");
        Row(sb, "Workload class", m.ClassTag);
        Row(sb, "Variant", m.Variant + " of " + MissionLibrary.VariantCount);
        Row(sb, "Cognitive mechanism", m.Mechanism);
        Row(sb, "Flight phase", m.Phase.ToString());
        Row(sb, "Start", m.Start + " at " + m.StartPos.ToString("F0"));
        Row(sb, "Altitude / airspeed / heading", m.StartAltitudeM.ToString("F0", ci) + " m / " +
                m.StartAirspeedKmh.ToString("F0", ci) + " km/h / " + m.StartHeadingDeg.ToString("F0", ci) + "°");
        Row(sb, "Targets", m.TargetAltitudeM.ToString("F0", ci) + " m ±" + m.AltToleranceM.ToString("F0", ci) +
                ", " + m.TargetHeadingDeg.ToString("F0", ci) + "° ±" + m.HdgToleranceDeg.ToString("F0", ci));
        Row(sb, "Weather / wind / visibility", "turbulence " + m.AmbientTurbulence.ToString("F2", ci) +
                " ambient, wind " + m.WindReport + " (crosswind " + m.CrosswindMs.ToString("F1", ci) +
                " m/s, headwind " + m.HeadwindMs.ToString("F1", ci) + " m/s), visibility index " +
                m.Visibility01.ToString("F2", ci));
        Row(sb, "Configuration / fuel", "flaps " + m.StartFlaps01.ToString("F2", ci) + ", " +
                m.StartFuelL.ToString("F0", ci) + " L");
        Row(sb, "Duration / in-task baseline", m.DurationS.ToString("F0", ci) + " s / " +
                m.InTaskBaselineS.ToString("F0", ci) + " s");
        Row(sb, "Goal", m.Goal.ToString());
        Row(sb, "Predicted Load Index", m.Profile.PLI.ToString("F1", ci) + " / 100");
        sb.AppendLine();
        sb.AppendLine("**Objective** — " + m.Objective);
        sb.AppendLine();
        sb.AppendLine("**Brief to the participant** — " + m.Brief);
        sb.AppendLine();

        sb.AppendLine("**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)");
        sb.AppendLine();
        if (m.Events.Count == 0) sb.AppendLine("_none_");
        else
        {
            sb.AppendLine("| t (s) | jitter | type | detail |");
            sb.AppendLine("|-------|--------|------|--------|");
            // Sorted for reading. The engine sorts by the JITTERED time at trial
            // start, so the running order can differ slightly from this table where
            // two events are close together and one of them carries jitter.
            var ordered = new System.Collections.Generic.List<ScenarioEvent>(m.Events);
            ordered.Sort((a, b) => a.Time.CompareTo(b.Time));
            foreach (var e in ordered)
                sb.AppendLine("| " + e.Time.ToString("F0", ci) + " | ±" + e.Jitter.ToString("F0", ci) + " | " +
                              e.Type + " | " + Detail(e) + " |");
        }
        sb.AppendLine();

        sb.AppendLine("**Demand profile** (0-4 on each dimension)");
        sb.AppendLine();
        sb.AppendLine("| dimension | rating |");
        sb.AppendLine("|-----------|--------|");
        foreach (var kv in m.Profile.AsDictionary())
            sb.AppendLine("| " + kv.Key.Replace('_', ' ') + " | " + new string('█', kv.Value) + " " + kv.Value + " |");
        sb.AppendLine();

        sb.AppendLine("**Pre-registered expected NASA-TLX** — mental " + m.Expected.Mental +
                      ", physical " + m.Expected.Physical + ", temporal " + m.Expected.Temporal +
                      ", performance " + m.Expected.Performance + ", effort " + m.Expected.Effort +
                      ", frustration " + m.Expected.Frustration + " → RTLX " + m.Expected.RTLX.ToString("F0", ci) +
                      ". *A prediction recorded before data collection; never a measurement.*");
        sb.AppendLine();
        Para(sb, "Cognitive-load analysis", m.LoadRationale);
        Para(sb, "EEG relevance", m.EegRelevance);
        Para(sb, "Expected errors", m.ExpectedErrors);
        Para(sb, "Success criteria", m.SuccessCriteria);
        Para(sb, "Failure conditions", m.FailureConditions);
        Para(sb, "Aviation basis", m.AviationBasis);
        Para(sb, "Approximations / not modelled", m.Approximations);
        sb.AppendLine("**Required event markers** — `" + string.Join("`, `", m.RequiredMarkers) + "`");
        sb.AppendLine();
        sb.AppendLine("---");
    }

    static string JoinVariantHeaders()
    {
        var sb = new StringBuilder();
        for (int v = 1; v <= MissionLibrary.VariantCount; v++) { if (v > 1) sb.Append(" | "); sb.Append("variant " + v); }
        return sb.ToString();
    }

    static string RepeatCol(int n)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.Append("---|");
        return sb.ToString();
    }

    static void Row(StringBuilder sb, string k, string v) => sb.AppendLine("| " + k + " | " + v + " |");
    static void Para(StringBuilder sb, string k, string v)
    {
        if (string.IsNullOrEmpty(v)) return;
        sb.AppendLine("**" + k + "** — " + v);
        sb.AppendLine();
    }

    static string Detail(ScenarioEvent e)
    {
        switch (e.Type)
        {
            case ScenarioEventType.SystemFailure: return "`" + e.Failure + "` severity " + e.Severity.ToString("F2");
            case ScenarioEventType.Checklist: return "`" + e.ChecklistId + "` — " + e.Label;
            case ScenarioEventType.HeadingChange: return "heading → " + e.Value.ToString("F0");
            case ScenarioEventType.AltitudeChange: return "altitude → " + e.Value.ToString("F0") + " m";
            case ScenarioEventType.Weather: return "intensity " + e.Value.ToString("F2") + " for " + e.Duration.ToString("F0") + " s";
            default: return e.Label + (e.RequiresResponse ? "  *(respond within " + e.ResponseWindow.ToString("F1") + " s)*" : "");
        }
    }

    static string Mechanism(MissionDefinition m)
    {
        var p = m.Profile;
        // Name the dimension with the largest weighted contribution — but only if it
        // is actually SUBSTANTIAL (rating >= 2). Picking the argmax unconditionally
        // labelled the quiet baseline missions "diagnosis under ambiguity" purely
        // because MentalDemand carries the heaviest weight, even at a rating of 1.
        // A mission with nothing above 1 is doing exactly what it says on the tin:
        // continuous manual tracking and monitoring, with no cognitive load driver.
        string best = null; float bestW = -1f; int bestV = 0;
        void T(string name, int v, string key)
        {
            float w = v * WorkloadProfile.Weights[key];
            if (w > bestW) { bestW = w; best = name; bestV = v; }
        }
        T("working memory / comms turnover", p.WorkingMemory, "WorkingMemory");
        T("diagnosis under ambiguity", p.MentalDemand, "MentalDemand");
        T("time pressure", p.TemporalDemand, "TemporalDemand");
        T("decision complexity", p.DecisionComplexity, "DecisionComplexity");
        T("attention switching / concurrency", p.AttentionSwitching, "AttentionSwitching");
        T("situation awareness under degraded input", p.SituationAwareness, "SituationAwareness");
        T("procedural execution", p.ProceduralLoad, "ProceduralLoad");
        T("perceptual-motor tracking", p.ManualControl, "ManualControl");
        if (best == null || bestV < 2) return "continuous manual tracking (no dominant load driver)";
        return best;
    }

    static string Abnormality(MissionDefinition m)
    {
        // Report what actually defines the mission, in order of how much it changes
        // the pilot's task: a systems failure, then conflicting traffic, then weather.
        // (Weather alone is never a class-defining factor in this set — see the design
        // principle in MissionLibrary's header.)
        foreach (var e in m.Events)
            if (e.Type == ScenarioEventType.SystemFailure) return "`" + e.Failure + "`";
        int trafficCount = 0; bool wx = false;
        foreach (var e in m.Events)
        {
            if (e.Type == ScenarioEventType.Traffic) trafficCount++;
            if (e.Type == ScenarioEventType.Weather) wx = true;
        }
        if (trafficCount > 0)
            return (trafficCount > 1 ? trafficCount + "x traffic" : "traffic") + (wx ? " + weather" : "");
        if (wx) return "weather";
        return "none (normal operations)";
    }

    static string Bar(int v) => new string('●', v) + new string('○', 4 - v);

    // TELEMETRY_SCHEMA is generated too, because the column list lives in code and a
    // hand-written copy would go stale the first time a field is added.
    static void WriteTelemetrySchema(string root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FINAL_TELEMETRY_SCHEMA");
        sb.AppendLine();
        sb.AppendLine("> Generated by `Assets/Editor/DocGen.cs` from `ExperimentLogger.TelemetryHeader()`.");
        sb.AppendLine();
        sb.AppendLine("Sample rate: **" + ExperimentLogger.TelemetryHz.ToString("F0") + " Hz**.");
        sb.AppendLine("One `telemetry.csv` per trial. Columns, in order:");
        sb.AppendLine();
        sb.AppendLine("| # | column | notes |");
        sb.AppendLine("|---|--------|-------|");
        var cols = ExperimentLogger.TelemetryHeader().Split(',');
        for (int i = 0; i < cols.Length; i++)
            sb.AppendLine("| " + (i + 1) + " | `" + cols[i] + "` | " + ColNote(cols[i]) + " |");
        sb.AppendLine();
        sb.AppendLine("## events.csv");
        sb.AppendLine();
        sb.AppendLine("| column | notes |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine("| `seq` | 1-based marker sequence within the trial |");
        sb.AppendLine("| `t_mission` | seconds since `MISSION_START` |");
        sb.AppendLine("| `t_host` | monotonic host clock, seconds since trial start — **the master clock** |");
        sb.AppendLine("| `t_unix` | UTC unix seconds, ms resolution — for cross-machine alignment |");
        sb.AppendLine("| `t_lsl` | liblsl `local_clock()`, or `-1` when LSL is not installed |");
        sb.AppendLine("| `marker` | one of the " + EventMarkers.All.Length + " tags in `EventMarkers.cs` |");
        sb.AppendLine("| `detail` | free text, commas replaced by `;` |");
        sb.AppendLine("| `mission_phase` | CICTT-style phase of the mission |");
        sb.AppendLine("| `altitude_m`, `airspeed_kmh`, `heading_deg` | aircraft state at the marker |");
        sb.AppendLine();
        string path = Path.Combine(root, "FINAL_TELEMETRY_SCHEMA.md");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));   // no BOM
        Debug.Log("[DocGen] wrote " + path);
    }

    static string ColNote(string c)
    {
        switch (c)
        {
            case "t_mission": return "seconds since MISSION_START";
            case "t_host": return "monotonic host clock — the master time base";
            case "t_unix": return "UTC unix seconds";
            case "t_lsl": return "liblsl clock, -1 when unavailable";
            case "ind_airspeed_kmh": return "**indicated** — differs from true under a pitot/static failure";
            case "ind_altitude_m": return "**indicated** — differs from true under a static failure";
            case "ind_vspeed_ms": return "**indicated**";
            case "flaps_actual": return "where the flaps really are";
            case "flaps_selected": return "the detent the pilot selected (differs when the motor has failed)";
            case "ctrl_jerk": return "sum of |Δ control| per second — the motor-artifact regressor";
            case "trim": return "elevator trim, +1 nose up .. -1 nose down. Trim is the one control that REDUCES workload — a participant holding a sustained elevator force all trial is carrying a motor load the design intends them to trim off";
            case "elevator_cmd": return "stick + trim combined (`pitch - trim x 0.35`) — this, not `pitch`, is what the elevator actually does. Separates a pilot fighting the aeroplane from one who has trimmed it";
            case "brake_pressure": return "wheel-brake pressure 0-1, analog. Ground only — brake input is ignored in flight";
            case "ovr_pitch": return "1 when something other than the keyboard commanded pitch this frame (a cockpit control, VR hand, or scripted pilot)";
            case "ovr_roll": return "as `ovr_pitch`, for roll";
            case "ovr_yaw": return "as `ovr_pitch`, for yaw";
            case "ovr_throttle": return "as `ovr_pitch`, for throttle";
            case "ovr_trim": return "as `ovr_pitch`, for trim";
            case "ovr_brake": return "as `ovr_pitch`, for the wheel brakes";
            case "control_held": return "the id of the cockpit control physically held this frame (`yoke`, `throttle`, `trim`, `flaps`, `brake`, ...) or empty. **The key column for separating motor activity from cognitive workload**: comparing LOW and HIGH windows in which this is empty throughout tests whether a workload effect survives with the hand movement removed. See `VR_EXPERIMENT_CONSIDERATIONS.md` §1";
            case "cross_track_m": return "lateral offset from the runway centreline (x = 0)";
            case "alt_err_m": return "**only interpretable while `segment` is BASELINE, or in a hold mission.** On a landing mission the target stays at the level-off altitude, so this grows as the aeroplane descends on the approach — use `cross_track_m` and the touchdown metrics there instead";
            case "hdg_err_deg": return "error against the currently assigned heading";
            case "target_alt_m": return "the assigned altitude; not a glidepath target on an approach";
            case "segment": return "`BASELINE` during the in-task baseline, `TASK` after, `REST_EO`/`REST_EC` in rest blocks";
            case "armed_failure": return "the abnormality currently armed, or `None`";
            case "carb_ice_01": return "modelled induction-ice accretion 0-1 (not visible to the pilot)";
            case "bus_volts": return "electrical bus voltage";
            case "battery_ah": return "remaining battery charge";
            default: return "";
        }
    }
}
