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
            foreach (var m in MissionLibrary.All()) queue.Add(m.Id);
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
        stage = Stage.Fly; stageT = 0f; ackDelay = 0f;
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
            wantHdg = eng.CurTargetHdg;
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

        string path = Path.Combine(Application.persistentDataPath, "mission_test_report.txt");
        File.WriteAllText(path, report.ToString(), new System.Text.UTF8Encoding(false));
        Log("done. problems=" + problems.Count + " report=" + path);
        Debug.Log("[MTEST-REPORT]\n" + report.ToString());
        Finished = true;
    }

    static void Log(string s) => Debug.Log("[MTEST] " + s);
}
