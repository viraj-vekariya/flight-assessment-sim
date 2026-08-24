// MissionTestHarness — automated end-to-end test of all twelve missions.
//
// WHY THIS EXISTS
//   A mission set that "looks right" in a design document is worth very little. Every
//   claim the design makes is a claim about the SIMULATOR'S BEHAVIOUR — that the
//   failure actually fires, that the checklist actually completes, that the markers
//   an EEG analysis will epoch on actually appear, that a trial actually resets. This
//   harness flies all twelve, with a scripted pilot, and checks those claims against
//   the files that land on disk.
//
// HOW TO RUN
//   /Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity \
//     -batchmode -nographics -projectPath "<project>" \
//     -executeMethod PlayCapture.RunMissionTest -logFile test.log
//   (no -quit: PlayCapture exits the editor itself when the harness reports done)
//
//   The report is written to persistentDataPath/mission_test_report.txt and echoed
//   to the log with the [MTEST] prefix.
//
// THE SCRIPTED PILOT
//   A simple attitude-hold autopilot (the same shape as FlightTest's) plus a rule
//   set for the task actions: acknowledge any pending probe after a fixed delay,
//   work checklist items, and operate the systems switches the drills call for. It
//   is deliberately COMPETENT BUT NOT PERFECT — it flies the aeroplane and does the
//   drills, which is what is needed to exercise the success paths. It is NOT a model
//   of a human pilot and its performance numbers are not data.
//
// WHAT IT DOES NOT TEST
//   Anything that needs a human: whether the missions FEEL like the workload class
//   they are labelled, whether the startle startles, whether the TLX separates the
//   conditions. Those are empirical questions for the participants, not for a test.

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class MissionTestHarness : MonoBehaviour
{
    /// <summary>Sim seconds per real second. The physics timestep is untouched, so the
    /// flight model and the 50 Hz telemetry are unaffected — there are simply more
    /// physics steps per wall second.
    ///
    /// Reduced from 20x to 8x when the real GLB cockpit was merged in. Rendering the
    /// photoreal cockpit plus two off-screen display cameras is expensive enough that
    /// at 20x Unity hit Time.maximumDeltaTime and started clamping the physics
    /// catch-up, so the simulation silently ran slower than requested. 8x completes a
    /// 12 x 300 s battery in about 8 minutes without clamping.</summary>
    public const float TimeScale = 8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        bool on = false;
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-missiontest") on = true;
        if (!on) return;
        {
                    // One driver at a time — see SimDriver. A second driver does not
                    // crash, it quietly changes every number the battery reports.
                    if (!SimDriver.Claim("MissionTestHarness")) return;
                    new GameObject("MissionTestHarness").AddComponent<MissionTestHarness>();
                }
    }

    /// <summary>Set true by the harness when the whole battery has finished, so the
    /// editor-side driver knows it can exit.</summary>
    public static bool Finished { get; private set; }
    public static int Failures { get; private set; }

    enum Stage { Wait, Fly, Rate, Check, Done }

    Stage stage = Stage.Wait;
    GameManager gm;
    CessnaPhysics ac;
    AircraftSystems sys;
    AircraftController ctl;
    ScenarioEngine eng;
    List<string> queue = new List<string>();
    int idx = -1;
    string currentId = "";
    string currentDir = "";
    float stageT, ackDelay, lastPitch;
    readonly StringBuilder report = new StringBuilder();
    readonly List<string> problems = new List<string>();
    readonly List<string> outcomes = new List<string>();
    int restartTested;
    bool navDiagged;
    bool navDiag;            // verbose per-mission steering log (-navdiag)
    float lastNavLog;

    // ═══════════════════════════════════════════════════════════════════════════
    // DESIGN CHECKS — properties of the mission TABLE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Assert the structural claims the experimental design rests on. Each of
    /// these is a claim MISSION_BANK_DESIGN.md makes in prose; here it is enforced, so
    /// the prose cannot drift away from the table.</summary>
    void CheckBankDesign()
    {
        report.AppendLine("BANK DESIGN CHECKS");
        report.AppendLine("------------------");
        var all = MissionLibrary.All();
        var cognitive = MissionLibrary.CognitiveAxis();
        var psychomotor = MissionLibrary.PsychomotorAxis();
        report.AppendLine($"bank: {all.Count} missions  ({cognitive.Count} cognitive axis, {psychomotor.Count} psychomotor axis)");

        // (a) The grid is complete: every (phase, class) cell has every variant.
        foreach (var phase in MissionLibrary.Rows)
            foreach (WorkloadClass c in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
                for (int v = 1; v <= MissionLibrary.VariantCount; v++)
                    if (MissionLibrary.Cell(phase, c, v) == null)
                        Problem("design", $"grid hole: {phase}/{c} has no variant {v}");

        // (b) Ids are unique. A duplicate id silently overwrites a trial folder.
        var seen = new HashSet<string>();
        foreach (var m in all) if (!seen.Add(m.Id)) Problem("design", "duplicate mission id: " + m.Id);

        // (c) THE MECHANISM RULE. The three variants of a cell must reach their class by
        // DIFFERENT cognitive mechanisms. If they repeat, the bank is three copies of the
        // same trial wearing different names and buys nothing but apparent breadth.
        foreach (var phase in MissionLibrary.Rows)
            foreach (WorkloadClass c in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
            {
                var mech = new List<string>();
                for (int v = 1; v <= MissionLibrary.VariantCount; v++)
                {
                    var m = MissionLibrary.Cell(phase, c, v);
                    if (m == null) continue;
                    if (string.IsNullOrWhiteSpace(m.Mechanism)) { Problem("design", m.Id + " declares no Mechanism"); continue; }
                    if (mech.Contains(m.Mechanism))
                        Problem("design", $"{phase}/{c}: variant {v} repeats mechanism \"{m.Mechanism}\"");
                    mech.Add(m.Mechanism);
                }
            }

        // (d) MOTOR MATCHING WITHIN EACH PHASE ROW, per variant index. This is the
        // property that licenses reading a class difference as cognitive rather than
        // muscular, and it is the single most important structural claim in the design.
        // Spread of <= 1 on the 0-4 ManualControl scale is the declared tolerance.
        for (int v = 1; v <= MissionLibrary.VariantCount; v++)
            foreach (var phase in MissionLibrary.Rows)
            {
                int lo = 99, hi = -1; string detail = "";
                foreach (WorkloadClass c in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
                {
                    var m = MissionLibrary.Cell(phase, c, v);
                    if (m == null) continue;
                    int mc = m.Profile.ManualControl;
                    lo = Mathf.Min(lo, mc); hi = Mathf.Max(hi, mc);
                    detail += $"{m.Id}={mc} ";
                }
                if (hi - lo > 1) Problem("design", $"manual demand not matched in {phase} v{v}: spread {hi - lo}  [{detail.Trim()}]");
            }

        // (e) The classes must actually ORDER on the predicted load index inside every
        // cell column. A HIGH that scores below its own row's MEDIUM is a mislabelled
        // mission, and no amount of data would rescue the contrast.
        for (int v = 1; v <= MissionLibrary.VariantCount; v++)
            foreach (var phase in MissionLibrary.Rows)
            {
                var l = MissionLibrary.Cell(phase, WorkloadClass.Low, v);
                var md = MissionLibrary.Cell(phase, WorkloadClass.Medium, v);
                var h = MissionLibrary.Cell(phase, WorkloadClass.High, v);
                if (l == null || md == null || h == null) continue;
                if (!(l.Profile.PLI < md.Profile.PLI && md.Profile.PLI < h.Profile.PLI))
                    Problem("design", $"PLI not ordered in {phase} v{v}: " +
                            $"{l.Id}={l.Profile.PLI:F1} {md.Id}={md.Profile.PLI:F1} {h.Id}={h.Profile.PLI:F1}");
            }

        // (e2) WITHIN-CELL EXCHANGEABILITY. The three variants of a cell are supposed to
        // be interchangeable realisations of it, so their PREDICTED loads must agree. If
        // variant 2 of a MEDIUM cell scores 17 points above variant 1, the two are not
        // two versions of MEDIUM — one of them is drifting toward the next class, and a
        // participant's workload class would depend on which variant they happened to be
        // assigned. That is variant becoming a confound with class, which is precisely
        // what the rotation is designed to prevent.
        //
        // 8 points on a 0-100 scale is the declared tolerance: tight enough to mean
        // something, loose enough to permit variants that genuinely load different
        // dimensions. This check found a real defect when it was written — the new
        // variants had been anchored one point high on MentalDemand across the board,
        // giving within-cell spreads up to 17.5. See FINAL_CHANGE_REPORT.md.
        const float VariantPliTolerance = 8f;
        foreach (var phase in MissionLibrary.Rows)
            foreach (WorkloadClass c in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
            {
                float lo = 1e9f, hi = -1e9f; string detail = "";
                for (int v = 1; v <= MissionLibrary.VariantCount; v++)
                {
                    var mm = MissionLibrary.Cell(phase, c, v);
                    if (mm == null) continue;
                    float pli = mm.Profile.PLI;
                    lo = Mathf.Min(lo, pli); hi = Mathf.Max(hi, pli);
                    detail += $"{mm.Id}={pli:F1} ";
                }
                if (hi - lo > VariantPliTolerance)
                    Problem("design", $"{phase}/{c}: variants disagree on predicted load by " +
                            $"{hi - lo:F1} (tolerance {VariantPliTolerance:F0})  [{detail.Trim()}]");
            }

        // (f) Every mission must carry the metadata a reader needs to reproduce it. An
        // empty rationale is how a mission that cannot be defended gets into a bank.
        foreach (var m in all)
        {
            if (string.IsNullOrWhiteSpace(m.LoadRationale)) Problem("design", m.Id + ": no LoadRationale");
            if (string.IsNullOrWhiteSpace(m.Approximations)) Problem("design", m.Id + ": no Approximations");
            if (string.IsNullOrWhiteSpace(m.AviationBasis)) Problem("design", m.Id + ": no AviationBasis");
            if (m.RequiredMarkers == null || m.RequiredMarkers.Length == 0) Problem("design", m.Id + ": no RequiredMarkers");
            if (Mathf.Abs(m.DurationS - MissionLibrary.StandardDurationS) > 0.01f)
                Problem("design", m.Id + $": duration {m.DurationS} != the standard {MissionLibrary.StandardDurationS}");
            if (Mathf.Abs(m.InTaskBaselineS - MissionLibrary.StandardBaselineS) > 0.01f)
                Problem("design", m.Id + $": in-task baseline {m.InTaskBaselineS} != the standard {MissionLibrary.StandardBaselineS}");
        }

        // (g) The crosswind axis must be a dose-response series, not a set of scenarios:
        // crosswind must increase strictly with the level, and no crosswind mission may
        // be on the cognitive axis.
        foreach (var m in cognitive)
            if (Mathf.Abs(m.CrosswindMs) > 1.0f)
                Problem("design", m.Id + $": cognitive-axis mission carries {m.CrosswindMs:F1} m/s of crosswind — " +
                        "manual demand is supposed to be matched on this axis");
        for (int i = 1; i < MissionLibraryCrosswind.Levels.Length; i++)
            if (MissionLibraryCrosswind.Levels[i] <= MissionLibraryCrosswind.Levels[i - 1])
                Problem("design", "crosswind levels are not strictly increasing");

        // (h) Every session a participant could be given must be a complete crossed grid.
        for (int p = 0; p < 12; p++)
        {
            var sess = MissionLibrary.SessionMissions(p);
            if (sess.Count != 12) { Problem("design", $"participant {p}: session has {sess.Count} missions, expected 12"); continue; }
            foreach (var phase in MissionLibrary.Rows)
            {
                int n = 0, variants = 0, firstV = -1;
                foreach (var m in sess) if (m.Phase == phase)
                { n++; if (firstV < 0) firstV = m.Variant; if (m.Variant != firstV) variants++; }
                if (n != 3) Problem("design", $"participant {p}: {phase} row has {n} missions, expected 3");
                if (variants != 0) Problem("design", $"participant {p}: {phase} row mixes variants — the class contrast would not be variant-matched");
            }
        }

        CheckTerrainClearance();
        WriteVariantEquivalenceTable();
        report.AppendLine(problems.Count == 0 ? "design checks: OK" : "design checks: " + problems.Count + " PROBLEM(S)");
        report.AppendLine();
    }

    /// <summary>Fly every mission's NOMINAL track against the real terrain and report the
    /// minimum clearance.
    ///
    /// WHY THIS EXISTS
    ///   The aerodrome sits on a flattened pad — 260 m either side of the centreline,
    ///   1.6 km out on departure — and beyond it the ground rises to about 350 m. A
    ///   mission that assigns a heading off the runway axis at a low altitude therefore
    ///   sends the aeroplane into a hill, and NOTHING in the mission table shows it: the
    ///   numbers look perfectly reasonable, and the failure only appears when someone
    ///   flies it. It appeared exactly that way here — M1V2 assigned a 330 degree
    ///   departure and the battery reported "Destroyed (terrain impact)" at 131 m.
    ///
    ///   Checking clearance by eye does not scale to 42 missions and would not survive
    ///   the next edit. So the nominal track is reconstructed from the mission's own
    ///   assigned headings and altitudes, the terrain is sampled underneath it, and a
    ///   mission whose clearance falls below the margin is a design failure — not a
    ///   flyability note.
    ///
    /// WHAT IT IS NOT
    ///   It checks the NOMINAL track — the one a perfectly-flying pilot follows. A
    ///   participant who wanders 500 m off track can still find terrain, which is a
    ///   property of flying near hills and is what the crash detection is for. The
    ///   margin below is deliberately generous to absorb ordinary tracking error.</summary>
    void CheckTerrainClearance()
    {
        const float MarginM = 90f;      // required clearance below the nominal track
        const float StepM = 120f;       // sample spacing along the track
        const float TrackM = 7000f;     // how far out to check

        var sb = new StringBuilder();
        sb.AppendLine("terrain clearance along each mission's nominal track (margin " + MarginM.ToString("F0") + " m):");
        foreach (var m in MissionLibrary.All())
        {
            // Reconstruct the assigned heading/altitude the mission ends up on: start
            // with its declared targets, then apply every scheduled target change.
            float hdg = m.TargetHeadingDeg, alt = m.TargetAltitudeM;
            float climbAlt = alt;
            foreach (var e in m.Events)
            {
                if (e.Type == ScenarioEventType.HeadingChange) hdg = e.Value;
                if (e.Type == ScenarioEventType.AltitudeChange) { alt = e.Value; climbAlt = Mathf.Min(climbAlt, e.Value); }
            }
            // THE FLIGHT PROFILE. Taken from flown telemetry, not assumed: L1's recorded
            // climb-out is 59 m at z=712, 144 m at z=1513, 236 m at z=2314 — a steady
            // 11% gradient from a lift-off point near the departure end of the runway.
            // An earlier version of this check assumed 7% from the THRESHOLD and
            // reported every mission as failing, including the verified ones. A check
            // that cries wolf on known-good missions is worse than no check, because
            // the first thing anyone does is stop believing it.
            bool landing = m.Goal == ScenarioGoal.Land;
            bool ground0 = m.Start == ScenarioStart.Runway || m.Goal == ScenarioGoal.TaxiTakeoff;
            Vector3 p0 = landing ? m.StartPos
                       : ground0 ? new Vector3(0f, 0f, Aerodrome.RunwayHalfLength) : m.StartPos;
            // A landing mission's profile is the approach path: from where it starts to
            // the runway threshold, over the ACTUAL distance between them.
            Vector3 thr = new Vector3(0f, 0f, Aerodrome.ThresholdZ);
            float approachRun = landing ? Vector3.Distance(new Vector3(p0.x, 0f, p0.z), thr) : 0f;

            float worst = 1e9f; float worstAt = 0f; Vector3 worstP = p0;

            // A NAVIGATION mission does not fly the assigned heading — it flies its
            // ROUTE, and a leg can point somewhere the heading never does. Checking the
            // heading for those missions checks a track the aeroplane never flies.
            if (m.Goal == ScenarioGoal.Navigate && m.Waypoints.Count > 0)
            {
                Vector3 legFrom = p0;
                float travelled = 0f;
                for (int wi = 0; wi < m.Waypoints.Count; wi++)
                {
                    Vector3 legTo = m.Waypoints[wi].Pos;
                    Vector3 flat = new Vector3(legTo.x - legFrom.x, 0f, legTo.z - legFrom.z);
                    float legLen = flat.magnitude;
                    if (legLen < 1f) { legFrom = legTo; continue; }
                    Vector3 ld = flat / legLen;
                    for (float d = 0f; d <= legLen; d += StepM)
                    {
                        Vector3 pt = legFrom + ld * d;
                        float hgt = Mathf.Min(alt, m.StartAltitudeM + (travelled + d) * 0.11f);
                        if (hgt < 60f) continue;
                        float g2 = WorldBuilder.SampleGroundY(pt.x, pt.z);
                        if (g2 < 20f) continue;
                        float cl = hgt - g2;
                        if (cl < worst) { worst = cl; worstAt = travelled + d; worstP = pt; }
                    }
                    travelled += legLen;
                    legFrom = legTo;
                }
                if (worst > 1e8f)
                { report.AppendLine($"   {m.Id,-6} route {m.Waypoints.Count} legs, {travelled:F0} m — no rising terrain"); continue; }
                report.AppendLine($"   {m.Id,-6} route {m.Waypoints.Count} legs, {travelled:F0} m  min clearance {worst,7:F0} m at {worstAt:F0} m");
                if (worst < MarginM)
                    Problem("design", $"{m.Id}: route clears terrain by only {worst:F0} m (need {MarginM:F0}) " +
                            $"at x={worstP.x:F0} z={worstP.z:F0}");
                continue;
            }

            Vector3 dir = landing ? (thr - new Vector3(p0.x, 0f, p0.z)).normalized
                                  : new Vector3(Mathf.Sin(hdg * Mathf.Deg2Rad), 0f, Mathf.Cos(hdg * Mathf.Deg2Rad));
            float span = landing ? approachRun : TrackM;
            for (float d = 0f; d <= span; d += StepM)
            {
                Vector3 p = p0 + dir * d;
                float here = landing
                           ? Mathf.Lerp(m.StartAltitudeM, 0f, Mathf.Clamp01(d / Mathf.Max(1f, approachRun)))
                           : Mathf.Min(alt, m.StartAltitudeM + d * 0.11f);
                // SKIP the parts of the profile that are DELIBERATELY near the ground:
                // the take-off roll and initial climb, and the flare. A clearance check
                // that flags an aeroplane for being 20 ft up on short final is measuring
                // the wrong thing, and would bury the one real hit among 24 false ones —
                // which is exactly what the first version of this check did.
                if (here < 60f) continue;
                float gY = WorldBuilder.SampleGroundY(p.x, p.z);
                // Only measure against ground that is actually RISING. The aerodrome
                // plain is at elevation zero, so over it "clearance" is simply the
                // aeroplane's height, and a climb-out passing 66 m over flat grass is
                // not a terrain problem — flagging it would bury the real hits. What
                // this check is for is ground that comes UP to meet the aeroplane.
                if (gY < 20f) continue;
                float clear = here - gY;
                if (clear < worst) { worst = clear; worstAt = d; worstP = p; }
            }
            if (worst > 1e8f)
            { report.AppendLine($"   {m.Id,-6} hdg {hdg,5:F0}  alt {alt,5:F0}  no rising terrain on the nominal track"); continue; }
            string line = $"   {m.Id,-6} hdg {hdg,5:F0}  alt {alt,5:F0}  min clearance {worst,7:F0} m at {worstAt,5:F0} m out";
            sb.AppendLine(line);
            if (worst < MarginM)
                Problem("design", $"{m.Id}: nominal track clears terrain by only {worst:F0} m " +
                        $"(need {MarginM:F0}) at {worstAt:F0} m out on heading {hdg:F0}, " +
                        $"x={worstP.x:F0} z={worstP.z:F0}");
        }
        report.Append(sb.ToString());
    }

    /// <summary>Write the a-priori half of the variant-exchangeability evidence: for
    /// every cell, the declared demand parameters of each variant side by side.
    ///
    /// This is what a reader needs in order to judge whether the three variants of a
    /// cell are interchangeable representations of it, and it is deliberately written
    /// BEFORE any participant data exists — the empirical half (achieved flight
    /// performance and control activity per variant, with effect sizes and confidence
    /// intervals) is computed from the collected trials by the analysis, against this
    /// table as its pre-registered reference.</summary>
    void WriteVariantEquivalenceTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("cell,phase,class,variant,id,mechanism,pli,manual_control,mental,temporal,decision," +
                      "working_memory,attention_switching,situation_awareness,perception,procedural," +
                      "uncertainty,communication,error_consequence,expected_rtlx,duration_s,baseline_s," +
                      "events,visibility_01,ambient_turbulence,crosswind_ms,headwind_ms");
        foreach (var phase in MissionLibrary.Rows)
            foreach (WorkloadClass c in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
                for (int v = 1; v <= MissionLibrary.VariantCount; v++)
                {
                    var m = MissionLibrary.Cell(phase, c, v);
                    if (m == null) continue;
                    var p = m.Profile;
                    sb.AppendLine($"{m.CellKey},{phase},{c},{v},{m.Id},\"{m.Mechanism}\",{p.PLI:F2},{p.ManualControl}," +
                                  $"{p.MentalDemand},{p.TemporalDemand},{p.DecisionComplexity},{p.WorkingMemory}," +
                                  $"{p.AttentionSwitching},{p.SituationAwareness},{p.Perception},{p.ProceduralLoad}," +
                                  $"{p.Uncertainty},{p.Communication},{p.ErrorConsequence},{m.Expected.RTLX:F1}," +
                                  $"{m.DurationS:F0},{m.InTaskBaselineS:F0},{m.Events.Count},{m.Visibility01:F2}," +
                                  $"{m.AmbientTurbulence:F2},{m.CrosswindMs:F2},{m.HeadwindMs:F2}");
                }
        try
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "variant_equivalence.csv");
            File.WriteAllText(path, sb.ToString());
            report.AppendLine("wrote variant_equivalence.csv");
        }
        catch (System.Exception e) { report.AppendLine("could not write variant_equivalence.csv: " + e.Message); }
    }

    void Start()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Time.timeScale = TimeScale;
        Time.maximumDeltaTime = 0.5f;
        report.AppendLine("MISSION TEST REPORT");
        report.AppendLine("===================");
        report.AppendLine("generated " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("unity " + Application.unityVersion + "   timeScale " + TimeScale);
        report.AppendLine();
    }

    void Update()
    {
        if (stage == Stage.Done) return;

        if (gm == null)
        {
            gm = GameManager.Instance;
            if (gm == null || gm.Aircraft == null) return;
            ac = gm.Aircraft;
            sys = ac.GetComponent<AircraftSystems>();
            ctl = ac.GetComponent<AircraftController>();
            eng = gm.ScenarioRunner;
            // CRITICAL: AircraftController writes phys.pitchInput/rollInput/yawInput
            // EVERY frame from the keyboard. With it enabled, the scripted pilot and
            // the (empty) keyboard fight for the controls and whichever component
            // happens to run last in Unity's arbitrary script order wins — so the
            // harness's inputs were being zeroed nondeterministically and the
            // aeroplane flew itself into the ground while the log showed
            // in_pitch = 0.00. Disable it and drive the physics directly, exactly as
            // the older FlightTest harness does.
            if (ctl != null) ctl.enabled = false;
            // Defensive: destroy any other harness that also writes the control inputs.
            foreach (var ft in FindObjectsByType<FlightTest>(FindObjectsSortMode.None))
            { Log("destroying rival control driver: FlightTest"); Destroy(ft.gameObject); }
            ParticipantManagerSetIfNeeded();
            // DESIGN CHECKS FIRST. These are properties of the mission TABLE, not of a
            // flight, so they can be checked before anything is flown — and if the grid
            // is malformed there is no point flying 42 missions to find out.
            CheckBankDesign();

            // -bankquick flies one variant index only (the twelve a single participant
            // would actually fly) instead of the whole bank. The full bank is ~42 x 300 s
            // even at 8x, so the quick form exists for iteration; the full form is what
            // must pass before data collection.
            bool quick = false;
            string only = null;
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-navdiag") navDiag = true;
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-bankquick") quick = true;
                // -onlymission:ID flies ONE mission. Diagnosing a single mission by
                // re-running all 42 is a 90-minute feedback loop, which is slow enough
                // that you stop investigating and start guessing.
                if (a.StartsWith("-onlymission:")) only = a.Substring("-onlymission:".Length);
            }
            if (!string.IsNullOrEmpty(only))
            {
                queue.Add(only);
                report.AppendLine("SINGLE MISSION: " + only);
            }
            else if (quick)
            {
                foreach (var m in MissionLibrary.SessionMissions(0)) queue.Add(m.Id);
                report.AppendLine("QUICK MODE: one variant index only (" + queue.Count + " missions).");
            }
            else
            {
                foreach (var m in MissionLibrary.All()) queue.Add(m.Id);
            }
            report.AppendLine("missions queued: " + queue.Count);
            Log("harness up, " + queue.Count + " missions queued");
            NextMission();
            return;
        }

        stageT += Time.unscaledDeltaTime;

        switch (stage)
        {
            case Stage.Fly: FlyTick(); break;
            case Stage.Rate: RateTick(); break;
            case Stage.Check: CheckTick(); break;
        }
    }

    void ParticipantManagerSetIfNeeded()
    {
        if (!ParticipantManager.IsSet) ParticipantManager.SetID("TEST01");
        gm.SetParticipantReady();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DRIVING ONE MISSION
    // ═══════════════════════════════════════════════════════════════════════════

    void NextMission()
    {
        idx++;
        if (idx >= queue.Count) { Finish(); return; }
        currentId = queue[idx];
        var m = MissionLibrary.Get(currentId);
        report.AppendLine();
        report.AppendLine("── " + m.Id + "  " + m.Name + "  [" + m.ClassTag + "]  PLI " + m.Profile.PLI.ToString("F1"));
        Log("start " + m.Id);

        gm.StartSingleMission(currentId);
        // ScenarioEngine resets configuration through AircraftController, which is
        // disabled here, so do it directly for the harness's runs.
        ac.flaps = m.StartFlaps01; ac.spoiler = 0f; ac.braking = false; ac.brakeInput01 = 0f; ac.trim = 0f;
        ac.yawInput = 0f;
        currentDir = eng.Experiment != null ? eng.Experiment.TrialDir : "";
        stage = Stage.Fly; stageT = 0f; ackDelay = 0f; navDiagged = false; lastNavLog = -99f;
        lastPitch = ac.PitchDeg;
    }

    void FlyTick()
    {
        if (gm.State != GameState.Flying) { AfterFlight(); return; }
        Autopilot();
        TaskActions();

        // Hard wall-clock guard: a mission is 300 sim s, so at 8x it should take ~40 s
        // real. 300 s real means something is stuck.
        if (stageT > 300f)
        {
            Problem(currentId, "TIMEOUT — mission did not end within 120 s wall clock");
            gm.AbortExperimentSession();
            AfterFlight();
        }
    }

    void AfterFlight()
    {
        // The trial folder is captured BEFORE the questionnaire, because submitting
        // closes the logger and clears the engine's current trial.
        if (!string.IsNullOrEmpty(eng.Experiment.TrialDir)) currentDir = eng.Experiment.TrialDir;
        stage = Stage.Rate; stageT = 0f;
    }

    void RateTick()
    {
        if (!gm.WorkloadQuestionnaireActive)
        {
            // Some paths (abort) skip the questionnaire; just move on.
            if (stageT > 4f) { stage = Stage.Check; stageT = 0f; }
            return;
        }
        // Submit a filled-in response. These are SYNTHETIC values that exist only to
        // exercise the write path — they are written into a TEST01 participant folder
        // which must never be mixed with real data.
        gm.SubmitWorkloadRating(new WorkloadRating
        {
            MentalDemand = 45, PhysicalDemand = 30, TemporalDemand = 40,
            Performance = 35, Effort = 50, Frustration = 25, Bedford = 4
        });
        stage = Stage.Check; stageT = 0f;
    }

    void CheckTick()
    {
        if (stageT < 0.5f) return;   // let the file handles flush
        Validate(currentId, currentDir);

        // Restart is exercised once, on the first mission, then the harness moves on.
        if (restartTested == 0)
        {
            restartTested = 1;
            TestRestart();
            return;
        }
        gm.Confirm();             // same path as Enter on the results screen
        NextMission();
    }

    /// <summary>Restart must produce a clean trial: fresh folder, fresh jitter, no
    /// leaked failure state from the run before it.</summary>
    void TestRestart()
    {
        var before = eng.Experiment.TrialDir;
        gm.RestartTrial();
        if (gm.State != GameState.Flying) { Problem("RESTART", "R did not re-enter the Flying state"); }
        else
        {
            var after = eng.Experiment.TrialDir;
            if (after == before) Problem("RESTART", "restart reused the previous trial folder — trials would overwrite");
            else report.AppendLine("   restart: new trial folder allocated  OK");
            if (sys != null && sys.Armed != FailureKind.None)
                Problem("RESTART", "a failure survived the restart: " + sys.Armed);
            else report.AppendLine("   restart: systems reset clean  OK");
            if (eng.Time01 > 2f) Problem("RESTART", "mission clock did not reset (t=" + eng.Time01.ToString("F1") + ")");
            else report.AppendLine("   restart: mission clock reset  OK");
        }
        // Abandon the restarted run and continue the battery.
        gm.BackToMenu();
        stage = Stage.Fly; stageT = 0f;
        // Re-enter the check path shortly; the aborted run has nothing more to give.
        stage = Stage.Check; stageT = 0f; restartTested = 2;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // THE SCRIPTED PILOT
    // ═══════════════════════════════════════════════════════════════════════════

    void Autopilot()
    {
        float dt = Time.deltaTime;
        var m = eng.Mission;
        if (m == null) return;

        if (ctl != null && ctl.enabled) ctl.enabled = false;   // stay the only writer

        // Taxi / take-off missions get their own ground driver until they are airborne.
        if (m.Goal == ScenarioGoal.TaxiTakeoff && eng.TaxiPhase < 3) { TaxiDriver(dt); return; }

        // ── the scripted pilot ────────────────────────────────────────────────────
        // PITCH holds the vertical path, THROTTLE holds the airspeed. Controlling
        // pitch alone (the first version of this harness) let the aeroplane
        // accelerate to well past 250 km/h in every descent and then diverge — which
        // is a real thing an unstabilised light single does, but it meant the harness
        // was testing its own bad flying rather than the missions.
        bool landing = m.Goal == ScenarioGoal.Land;
        bool engineOut = sys != null && sys.Engine == EngineState.Failed;

        float wantAlt, wantHdg, wantSpeed;
        Vector3 p = ac.transform.position;

        if (landing)
        {
            // Aim at a touchdown zone 150 m INSIDE the threshold, not at the
            // threshold itself, and fly a realistic light-single approach speed.
            // At 150 km/h the scripted pilot floated ~900 m past a 610 m runway
            // every time; a C172-class aeroplane approaches nearer 120-130 km/h.
            float toGo = Mathf.Max(60f, -p.z - 150f);
            bool cleared = eng.Time01 >= MissionLibrary.BaselineEndT || m.Id == "H4";
            wantAlt = cleared ? Mathf.Min(m.StartAltitudeM, toGo * 0.055f) : m.StartAltitudeM;
            wantHdg = Mathf.Atan2(-p.x, Mathf.Max(60f, -p.z)) * Mathf.Rad2Deg;
            wantSpeed = ac.AltitudeM > 120f ? 145f : 128f;     // slow down on final
            if (ac.AltitudeM < 25f) wantSpeed = 115f;          // over the fence
        }
        else
        {
            wantAlt = eng.CurTargetAlt;
            // NAVIGATION. On a Navigate-goal mission the assigned HEADING is not where
            // the aeroplane should point — the WAYPOINT is. Without this the scripted
            // pilot flew the mission's initial heading forever and every navigation
            // mission was reported INCOMPLETE, which looks like a broken mission and is
            // actually a blind autopilot.
            if (!navDiagged && eng.Current != null)
            {
                navDiagged = true;
                Log($"NAVDIAG {eng.Current.Id}: scenarioGoal={eng.Current.Goal} missionGoal={m.Goal} " +
                    $"hasWpt={eng.HasWaypoint} wpts={eng.Current.Waypoints.Count} curTargetHdg={eng.CurTargetHdg:F1}");
            }
            wantHdg = (eng.HasWaypoint && eng.Current != null && eng.Current.Goal == ScenarioGoal.Navigate)
                    ? Mathf.Atan2(eng.WaypointPos.x - ac.transform.position.x,
                                  eng.WaypointPos.z - ac.transform.position.z) * Mathf.Rad2Deg
                    : eng.CurTargetHdg;
            wantSpeed = 180f;                                  // cruise / climb
        }
        if (engineOut) wantSpeed = 130f;                       // best glide

        // ---- throttle: airspeed hold (no engine = no throttle) ----
        float spdErr = ac.AirspeedKmh - wantSpeed;
        float thrCmd = engineOut ? 0f : Mathf.Clamp01(ac.throttle - spdErr * 0.004f * dt * 60f);
        // Bias the trim point with the altitude error so the aeroplane still climbs
        // and descends rather than only chasing speed.
        if (!engineOut) thrCmd = Mathf.Clamp01(thrCmd - (ac.AltitudeM - wantAlt) * 0.0004f);
        ac.throttle = Mathf.MoveTowards(ac.throttle, thrCmd, dt * 0.7f);

        // ---- pitch: vertical path, with a hard speed guard ----
        float altErr = ac.AltitudeM - wantAlt;
        float targetPitch = Mathf.Clamp(-altErr * 0.035f, -4.5f, 7f);

        // Flare: raise the nose between 22 m and 6 m, then EASE OFF so the aeroplane
        // settles instead of floating in ground effect. Without the easing the
        // scripted pilot held 5 deg at 4 m and never touched down inside the trial.
        if (landing && ac.AltitudeM < 22f)
        {
            targetPitch = ac.AltitudeM > 5f
                ? Mathf.Lerp(1.0f, 4f, Mathf.Clamp01((22f - ac.AltitudeM) / 17f))
                : Mathf.Lerp(4f, 1.5f, Mathf.Clamp01((5f - ac.AltitudeM) / 5f));
        }

        // Speed guards. The overspeed guard raises the nose to bleed speed — but ONLY
        // when the aeroplane is at or below its target altitude. Applying it
        // unconditionally meant that an aeroplane which was both too high AND too fast
        // got a nose-up command, so it climbed instead of descending, and then stayed
        // fast because it never came down: a runaway that pinned the pitch at the guard
        // value for the whole trial.
        if (ac.AirspeedKmh > wantSpeed + 45f && altErr <= 0f) targetPitch = Mathf.Max(targetPitch, 4f);
        if (ac.AirspeedKmh < 105f || ac.StallWarning) targetPitch = Mathf.Min(targetPitch, -1.5f);

        float pitchRate = (ac.PitchDeg - lastPitch) / Mathf.Max(dt, 1e-4f);
        lastPitch = ac.PitchDeg;
        // NOTE: in this flight model NEGATIVE pitchInput is NOSE UP.
        ac.pitchInput = Mathf.Clamp(-0.045f * (targetPitch - ac.PitchDeg) + 0.012f * pitchRate, -0.6f, 0.6f);

        float targetBank = Mathf.Clamp(Mathf.DeltaAngle(ac.HeadingDeg, wantHdg) * 1.0f, -22f, 22f);

        // BANK PROTECTION — using a TRUE bank angle over the full +/-180 range.
        //
        // CessnaPhysics.RollDeg is asin(-right.y), which folds: an aeroplane rolled
        // to 158 deg (i.e. inverted) reports 22 deg, indistinguishable from a gentle
        // turn. The first version of this guard used RollDeg and was therefore blind
        // to inverted flight — the scripted pilot sat "at 22 deg of bank", inverted,
        // at full power, descending at 10 m/s, and flew into the ground while its
        // own instrument said everything was fine. Computing the bank from the up
        // vector as well as the right vector removes the ambiguity.
        Vector3 up = ac.transform.up, right = ac.transform.right;
        float trueBank = Mathf.Atan2(-right.y, up.y) * Mathf.Rad2Deg;   // -180..180
        bool inverted = up.y < 0.2f;

        if (Mathf.Abs(trueBank) > 35f || inverted)
        {
            targetBank = 0f;
            // Never pull while steeply banked or inverted — that tightens the spiral
            // instead of arresting the descent. Roll level first.
            ac.pitchInput = Mathf.Clamp(ac.pitchInput, -0.05f, 0.6f);
        }
        ac.rollInput = Mathf.Clamp(0.035f * (targetBank - trueBank), -0.7f, 0.7f);
        ac.yawInput = 0f;
        if (navDiag && eng.Time01 - lastNavLog > 20f)
        {
            lastNavLog = eng.Time01;
            Log($"NAV t={eng.Time01:F0} wantHdg={wantHdg:F1} hdg={ac.HeadingDeg:F1} " +
                $"tgtBank={targetBank:F1} trueBank={trueBank:F1} roll={ac.rollInput:F3} " +
                $"hasWpt={eng.HasWaypoint} goal={eng.Current.Goal}");
        }
    }

    /// <summary>Scripted ground pilot: steers along the taxi route with the rudder,
    /// holds a walking-pace taxi speed, STOPS at the hold-short line, and rolls only
    /// once cleared. It deliberately respects the hold-short gate — a harness that
    /// blundered onto the runway would mask the very incursion check the mission is
    /// there to measure.</summary>
    void TaxiDriver(float dt)
    {
        Vector3 p = ac.transform.position;

        // Where are we going? The engine publishes the active route waypoint.
        Vector3 target = eng.HasWaypoint ? eng.WaypointPos : Aerodrome.LineUpPos;
        if (eng.TaxiPhase >= 2) target = new Vector3(0f, 0f, 400f);   // cleared: line up and roll north

        Vector3 d = target - p; d.y = 0f;
        float wantHdg = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float hdgErr = Mathf.DeltaAngle(ac.HeadingDeg, wantHdg);

        // Steer with the rudder (ground steering), never with the ailerons.
        ac.yawInput = Mathf.Clamp(hdgErr * 0.05f, -1f, 1f);
        ac.rollInput = 0f;

        if (eng.TaxiPhase == 1)
        {
            // Holding short: stop and wait. Brakes on, throttle closed.
            ac.throttle = 0f;
            ac.braking = ac.AirspeedKmh > 1f; ac.brakeInput01 = ac.braking ? 1f : 0f;
            ac.pitchInput = 0f;
            return;
        }

        if (eng.TaxiPhase >= 2)
        {
            // Cleared: full power, keep straight, rotate at Vr.
            ac.braking = false;
            ac.throttle = 1f;
            ac.yawInput = Mathf.Clamp(hdgErr * 0.04f, -0.6f, 0.6f);
            // NOTE: negative pitchInput is NOSE UP in this flight model.
            ac.pitchInput = ac.AirspeedKmh < 100f ? 0f : -0.32f;
            return;
        }

        // Taxiing: hold ~18 km/h, and slow down for the hold-short line.
        float distToHold = Mathf.Abs(Aerodrome.HoldShortZ - p.z);
        float wantKmh = (!eng.HoldingShort && distToHold < 60f && p.z < Aerodrome.HoldShortZ) ? 8f : 18f;
        float err = ac.AirspeedKmh - wantKmh;
        ac.throttle = Mathf.Clamp01(ac.throttle - err * 0.004f);
        ac.braking = err > 6f; ac.brakeInput01 = ac.braking ? 0.8f : 0f;
        ac.pitchInput = 0f;
    }

    void TaskActions()
    {
        // Acknowledge a pending probe/decision/readback after a plausible latency,
        // so response markers and reaction times are actually produced.
        //
        // NOTE the accumulator handling. An earlier version zeroed ackDelay on the
        // line just above the branch that increments it, so it could never exceed one
        // frame's dt and NO acknowledgement was ever sent — every CHECK item in every
        // drill reached its 20 s timeout instead. The ack path was therefore untested
        // while the report looked healthy. Reset only when there is nothing to
        // acknowledge.
        var cl = eng.Checklist;
        bool checkItemPending = cl != null && !cl.Complete &&
                                cl.Current != null && cl.Current.Kind == ChecklistItemKind.Check;

        if (eng.ResponsePending)
        {
            ackDelay += Time.deltaTime;
            if (ackDelay > 1.2f) { eng.ExternalAck(); ackDelay = 0f; }
            return;
        }
        if (checkItemPending)
        {
            // Acknowledge check items on a delay, so per-item latencies are non-zero
            // and the CHECKLIST_ITEM timing data is meaningful.
            ackDelay += Time.deltaTime;
            if (ackDelay > 1.5f) { eng.ExternalAck(); ackDelay = 0f; }
            return;
        }
        ackDelay = 0f;

        if (cl == null || cl.Complete || sys == null) return;
        var item = cl.Current;
        if (item == null) return;

        // Do items: operate the actual control the drill calls for.
        string L = item.Label;
        if (L.Contains("[H]")) sys.SetCarbHeat(true);
        else if (L.Contains("[J]")) sys.SetSelector(FuelSelector.Left);
        else if (L.Contains("[K]")) sys.SetLoadShed(true);
        else if (L.Contains("[L]")) sys.SetAlternateStatic(true);
        else if (L.Contains("[F]"))
        {
            // AircraftController is disabled for the harness, so its flap stepping
            // does not run — move the surface directly, honouring flap authority so
            // a failed flap motor still behaves like a failed flap motor.
            float cmd = Mathf.Min(0.5f, ac.flapAuthority01);
            ac.flaps = Mathf.MoveTowards(ac.flaps, cmd, Time.deltaTime * 0.5f);
        }
        // "AIRSPEED — BEST GLIDE" is satisfied by the autopilot's glide handling.
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VALIDATION — read what actually landed on disk
    // ═══════════════════════════════════════════════════════════════════════════

    void Validate(string id, string dir)
    {
        var m = MissionLibrary.Get(id);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        { Problem(id, "no trial folder was created"); return; }

        string tel = Path.Combine(dir, "telemetry.csv");
        string evp = Path.Combine(dir, "events.csv");
        string meta = Path.Combine(dir, "metadata.json");
        string tlx = Path.Combine(dir, "nasa_tlx.json");
        string sync = Path.Combine(dir, "eeg", "sync.json");

        foreach (var f in new[] { tel, evp, meta, sync })
            if (!File.Exists(f)) Problem(id, "missing file " + Path.GetFileName(f));
        if (!File.Exists(tlx)) Problem(id, "nasa_tlx.json was not written");

        if (!File.Exists(tel) || !File.Exists(evp)) return;

        // ---- telemetry -------------------------------------------------------
        var tlines = File.ReadAllLines(tel);
        int headerCols = tlines.Length > 0 ? tlines[0].Split(',').Length : 0;
        int rows = tlines.Length - 1;
        if (rows < 100) Problem(id, "telemetry has only " + rows + " rows");
        double prev = -1; int ragged = 0, nonMono = 0;
        for (int i = 1; i < tlines.Length; i++)
        {
            var parts = tlines[i].Split(',');
            if (parts.Length != headerCols) ragged++;
            if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double t))
            { if (t < prev - 1e-6) nonMono++; prev = t; }
        }
        if (ragged > 0) Problem(id, ragged + " telemetry rows have the wrong column count");
        if (nonMono > 0) Problem(id, nonMono + " telemetry rows go backwards in time");
        report.AppendLine("   telemetry: " + rows + " rows x " + headerCols + " cols, monotonic  OK");

        // ---- events ----------------------------------------------------------
        var elines = File.ReadAllLines(evp);
        var seen = new Dictionary<string, int>();
        double pe = -1; int eNonMono = 0;
        var vocabulary = new HashSet<string>(EventMarkers.All);
        var unknown = new HashSet<string>();
        for (int i = 1; i < elines.Length; i++)
        {
            var parts = elines[i].Split(',');
            if (parts.Length < 6) continue;
            string tag = parts[5];
            seen[tag] = seen.TryGetValue(tag, out int c) ? c + 1 : 1;
            if (!vocabulary.Contains(tag)) unknown.Add(tag);
            if (double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double t))
            { if (t < pe - 1e-6) eNonMono++; pe = t; }
        }
        if (eNonMono > 0) Problem(id, eNonMono + " event rows go backwards in time");
        if (unknown.Count > 0) Problem(id, "markers outside the declared vocabulary: " + string.Join(" ", unknown));

        int starts = seen.TryGetValue(EventMarkers.MissionStart, out int ms) ? ms : 0;
        if (starts != 1) Problem(id, "MISSION_START appears " + starts + " times (expected exactly 1)");
        int ends = seen.TryGetValue(EventMarkers.MissionEnd, out int me) ? me : 0;
        if (ends != 1) Problem(id, "MISSION_END appears " + ends + " times (expected exactly 1)");

        foreach (var need in m.RequiredMarkers)
            if (!seen.ContainsKey(need)) Problem(id, "required marker missing: " + need);

        // TLX markers must bracket the questionnaire and be inside the same file.
        if (!seen.ContainsKey(EventMarkers.TlxStart)) Problem(id, "TLX_START missing");
        if (!seen.ContainsKey(EventMarkers.TlxSubmit)) Problem(id, "TLX_SUBMIT missing");

        // A mission that arms a failure must ALSO show the pilot-perceptible cue,
        // or the epoch the EEG analysis needs does not exist.
        if (seen.ContainsKey(EventMarkers.TriggerArmed) && !seen.ContainsKey(EventMarkers.CueOnset))
            Problem(id, "TRIGGER_ARMED fired but CUE_ONSET never did — no valid EEG epoch zero");

        // Checklist integrity: started implies items and a completion (or timeouts).
        if (seen.ContainsKey(EventMarkers.ChecklistStart))
        {
            int items = seen.TryGetValue(EventMarkers.ChecklistItem, out int ci) ? ci : 0;
            if (items == 0) Problem(id, "CHECKLIST_START with no CHECKLIST_ITEM");
            if (!seen.ContainsKey(EventMarkers.ChecklistComplete))
                report.AppendLine("   note: checklist did not complete within the trial (allowed)");
            int touts = seen.TryGetValue(EventMarkers.ChecklistTimeout, out int ct) ? ct : 0;
            if (touts > 0)
                report.AppendLine("   note: " + touts + " checklist item(s) timed out — check the item is reachable");
        }

        var tags = new List<string>(seen.Keys); tags.Sort();
        report.AppendLine("   events: " + (elines.Length - 1) + " rows; " + string.Join(" ", tags));

        // ---- FLYABILITY (reported, never a hard failure) ----------------------
        // Whether the SCRIPTED pilot managed the mission is not a test of the
        // mission — it is a test of the autopilot in this file, which is not a model
        // of a human. But a mission the scripted pilot can never complete is worth
        // looking at before a participant is asked to fly it, so it is surfaced here
        // rather than buried.
        string outcome = seen.ContainsKey(EventMarkers.MissionSuccess) ? "SUCCESS"
                       : seen.ContainsKey(EventMarkers.Crash) ? "CRASHED" : "INCOMPLETE";
        report.AppendLine("   scripted-pilot outcome: " + outcome +
                          (outcome == "SUCCESS" ? "" : "   <- review flyability, not a test failure"));
        outcomes.Add(id + "=" + outcome);

        // ---- the in-task baseline actually exists ----------------------------
        if (m.InTaskBaselineS > 0f)
        {
            int baselineRows = 0;
            int segCol = System.Array.IndexOf(tlines[0].Split(','), "segment");
            if (segCol >= 0)
                for (int i = 1; i < tlines.Length; i++)
                {
                    var parts = tlines[i].Split(',');
                    if (segCol < parts.Length && parts[segCol] == "BASELINE") baselineRows++;
                }
            float expect = m.InTaskBaselineS * ExperimentLogger.TelemetryHz;
            if (baselineRows < expect * 0.7f)
                Problem(id, "in-task baseline is short: " + baselineRows + " rows, expected ~" + expect.ToString("F0"));
            else report.AppendLine("   in-task baseline: " + baselineRows + " rows  OK");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════

    void Problem(string id, string what)
    {
        Failures++;
        problems.Add(id + ": " + what);
        report.AppendLine("   FAIL  " + what);
        Log("FAIL " + id + " — " + what);
    }

    void Finish()
    {
        stage = Stage.Done;
        Time.timeScale = 1f;

        report.AppendLine();
        report.AppendLine("SUMMARY");
        report.AppendLine("-------");
        report.AppendLine("missions run : " + queue.Count);
        report.AppendLine("problems     : " + problems.Count);
        report.AppendLine("scripted-pilot outcomes: " + string.Join("  ", outcomes));
        report.AppendLine("  (outcomes are a flyability sanity check, NOT part of the pass/fail.");
        report.AppendLine("   The scripted pilot is a crude autopilot, not a model of a participant.)");
        foreach (var p in problems) report.AppendLine("  - " + p);
        if (problems.Count == 0) report.AppendLine("  (none)");
        report.AppendLine();
        report.AppendLine("Data written under: " + ExperimentLogger.ExperimentRoot);
        report.AppendLine("NOTE: this battery runs as participant TEST01. Its folders contain SYNTHETIC");
        report.AppendLine("NASA-TLX values submitted by the harness and must never be analysed as data.");

        // Write to BOTH persistentDataPath and the project root. The project root is
        // where the control and wind batteries write and where the documentation tells a
        // reader to look — and a STALE copy sitting there while the live one is buried in
        // ~/Library is an excellent way to read yesterday's result and believe it is
        // today's. (That happened during this work: a report dated the previous day, with
        // the old 12 missions and the old 77-column telemetry, was sitting in the project
        // root looking entirely plausible.)
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        try { File.WriteAllText(Path.Combine(projectRoot, "mission_test_report.txt"), report.ToString()); } catch { }
        string path = Path.Combine(Application.persistentDataPath, "mission_test_report.txt");
        File.WriteAllText(path, report.ToString(), new System.Text.UTF8Encoding(false));
        Log("done. problems=" + problems.Count + " report=" + path);
        Debug.Log("[MTEST-REPORT]\n" + report.ToString());
        Finished = true;
    }

    static void Log(string s) => Debug.Log("[MTEST] " + s);
}
