using UnityEngine;

/// <summary>
/// Central orchestrator. Builds the world + Cessna + cockpit, runs the state
/// machine (Menu -> Flying -> Results), drives per-level sampling/logging, and
/// handles restart (R) and return-to-menu (Esc / Enter).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; } = GameState.Menu;

    public GameObject WorldRoot { get; private set; }
    public CessnaPhysics Aircraft { get; private set; }
    public LevelManager Level { get; private set; }
    public FlightDataLogger Logger { get; private set; }

    public float ElapsedFlightTime { get; private set; }
    public bool Crashed { get; private set; }

    // Phase 4 participant management
    public bool ParticipantReady { get; private set; }

    // Phase 3 crash visuals
    public CrashEffects Effects { get; private set; }

    // Phase 2 scenario engine
    public ScenarioEngine ScenarioRunner { get; private set; }
    public bool ScenarioActive { get; private set; }
    public bool InScenarioMenu { get; set; }
    public bool InCampaignMenu { get; set; }
    public bool SessionReportActive { get; private set; }
    public System.Collections.Generic.List<ScenarioResult> SessionResults { get; } = new System.Collections.Generic.List<ScenarioResult>();
    bool inSession;
    readonly System.Collections.Generic.Queue<Scenario> sessionQueue = new System.Collections.Generic.Queue<Scenario>();

    // ---- cognitive-load experiment ------------------------------------------
    public BaselineRunner Baseline { get; private set; }
    /// <summary>True while the counterbalanced 12-mission session is running.</summary>
    public bool ExperimentRunning { get; private set; }
    /// <summary>1-based index of the trial currently being flown in the session.</summary>
    public int ExperimentTrialIndex { get; private set; }
    public int ExperimentTrialTotal { get; private set; }
    public string ExperimentStatus { get; private set; } = "";
    readonly System.Collections.Generic.Queue<System.Action> experimentQueue =
        new System.Collections.Generic.Queue<System.Action>();

    // The post-trial NASA-TLX / BEDFORD self-report was REMOVED from this project. It
    // sat between the end of every trial and the score breakdown, and filling it in
    // twelve times per session cost more than it returned here. The trial's files are
    // now closed directly by ScenarioEngine.CloseTrial() at the moment the trial ends.
    // The PREDICTED workload model (WorkloadModel/PLI, MissionSpec.Expected) is a
    // separate thing and is untouched.

    RunwayInfo runway;
    public RunwayInfo Runway => runway;
    const float LogInterval = 0.1f;     // 10 Hz
    float logAccum;

    // ═══════════════════════════════════════════════════════════════════════════
    // UI-ONLY MODE
    // ═══════════════════════════════════════════════════════════════════════════
    // This is the merged experiment build, so UiOnly is OFF: pressing Play goes through
    // participant-ID entry, the mission menu and the session runner, and trials are
    // recorded. The UI working copy kept it ON so pressing Play dropped straight into
    // FREE FLIGHT with nothing to click through while the cockpit was being looked at.
    //
    // NOTHING IS DELETED. ParticipantUI and MenuUI still compile and are still correct;
    // they are simply not instantiated. Every mission, scenario, baseline, questionnaire
    // and telemetry path is untouched — the 42-mission bank still runs and still passes.
    // The single change is which two components get added and which state the game starts
    // in. Set this to false and the full experiment build is back, with no other edit.
    //
    // With UiOnly on, free flight is the mode chosen because LevelManager.Measured is
    // false for it: no participant file is opened and no trial data is written. That is
    // exactly why it must stay FALSE here — a build with it on records nothing.
    public const bool UiOnly = false;

    /// <summary>UI-only applies to a person pressing Play, never to a batch harness.
    ///
    /// Every battery in this project runs with -batchmode and drives the state machine
    /// itself — StartSingleMission, ControlCheckMode.Enter, and so on. Starting a free
    /// flight underneath them would put the aeroplane in the air before they set it up.
    /// Keying off batch mode rather than a list of harness flags means a battery added
    /// later is covered without anyone remembering to add its flag here.
    ///
    /// -uionly forces it back on for a batch run. Without that there is no way to exercise
    /// this path in a headless check at all — the mode is off in batch mode by definition —
    /// and an untested startup path is exactly the kind of thing that is discovered by the
    /// person pressing Play.</summary>
    static bool UiOnlyActive =>
        UiOnly && (!Application.isBatchMode || HasArg("-uionly"));

    static bool HasArg(string flag)
    {
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == flag) return true;
        return false;
    }

    void Awake()
    {
        // Singleton guard: if a GameManager already exists (e.g. a saved scene has one
        // AND Bootstrap spawns one), destroy this duplicate so we don't build the world
        // twice and end up with a half-initialised copy whose Level is null.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        WorldRoot = new GameObject("FlightSimWorld");
        runway = WorldBuilder.BuildEnvironment(WorldRoot.transform);
        Aircraft = AircraftBuilder.Build(runway.Start, runway.Rot, WorldRoot.transform);

        // Systems + systems controls live on the aircraft. Added here (not lazily) so
        // they exist for the whole app lifetime and every trial resets the SAME
        // component instance rather than creating a fresh one.
        if (Aircraft.GetComponent<AircraftSystems>() == null) Aircraft.gameObject.AddComponent<AircraftSystems>();
        if (Aircraft.GetComponent<SystemsInput>() == null) Aircraft.gameObject.AddComponent<SystemsInput>();

        Logger = gameObject.AddComponent<FlightDataLogger>();
        gameObject.AddComponent<Hud2D>();
        // The two screens that stand between Play and the aeroplane. Skipped in UI-only
        // mode; ParticipantReady is read by nothing else, so it simply stops mattering.
        if (!UiOnlyActive)
        {
            gameObject.AddComponent<ParticipantUI>();   // blocks menu until participant is confirmed
            gameObject.AddComponent<MenuUI>();
        }
        gameObject.AddComponent<ResultsUI>();
        gameObject.AddComponent<ScenarioHud>();
        // VR runtime + camera rig. BOTH are always present and both no-op when there is
        // no headset, so there is one build and one simulation rather than a VR fork.
        gameObject.AddComponent<VRRuntime>();
        gameObject.AddComponent<VRCameraRig>();

        Baseline = gameObject.AddComponent<BaselineRunner>();
        gameObject.AddComponent<ExperimentUI>();     // session runner + baseline screens
        gameObject.AddComponent<VoiceCallouts>();   // tiered spoken flight + mission callouts
        // v26: the separate Scenarios + Campaign menu boxes were folded into the one
        // unified MenuUI (Free Flight + open mission list). Those UI components are no
        // longer instantiated; their code remains (graded-session/CLI flow intact).

        Level = new LevelManager();
        Level.Init(Aircraft, runway);

        ScenarioRunner = gameObject.AddComponent<ScenarioEngine>();
        ScenarioRunner.Init(this);

        // Camera views: cockpit (first-person) + external chase + orbit, toggled with C.
        var cockpitCam = Aircraft.GetComponentInChildren<Camera>();
        var vm = gameObject.AddComponent<ViewManager>();
        vm.Init(Aircraft.transform, cockpitCam);
        Effects = gameObject.AddComponent<CrashEffects>();
        Effects.Init(Aircraft, vm);

        Aircraft.ResetTo(runway.Start, runway.Rot, false, 0f);   // park on runway for the menu
        State = GameState.Menu;
    }

    /// <summary>In UI-only mode, go flying. Deliberately in Start rather than Awake: the
    /// cockpit, the interactors and the camera rig are built by other components' Awake
    /// calls, and StartFlight resets the aeroplane, so it has to run after all of them
    /// rather than in the middle of the list.</summary>
    void Start()
    {
        if (!UiOnlyActive) return;
        StartFlight(FlightMode.FreeFlight);
        Debug.Log("[GameManager] UI-only mode: started free flight directly."
                + "  State=" + State
                + "  mode=" + (Level != null ? Level.Mode.ToString() : "?")
                + "  measured(logging)=" + (Level != null && Level.Measured)
                + "  ParticipantUI=" + (GetComponent<ParticipantUI>() == null ? "absent" : "PRESENT")
                + "  MenuUI=" + (GetComponent<MenuUI>() == null ? "absent" : "PRESENT"));
    }

    public void StartFlight(FlightMode mode)
    {
        Effects?.ClearEffects();
        Logger.Close();
        Level.StartMode(mode);
        if (Level.Measured) Logger.Begin(mode);
        ElapsedFlightTime = 0f;
        logAccum = 0f;
        Crashed = false;
        ScenarioActive = false;
        State = GameState.Flying;
    }

    // ---- Phase 4 participant entry ----
    public void SetParticipantReady()
    {
        ParticipantReady = true;
        LSLSync.Init();   // open LSL outlet (no-op without LSL4Unity package)
    }

    void OnApplicationQuit() => LSLSync.Shutdown();

    // ---- Phase 2 scenario flow ----
    public void OpenScenarioMenu() => InScenarioMenu = true;
    public void CloseScenarioMenu() => InScenarioMenu = false;

    // ---- campaign (structured level ladder) ----
    public void OpenCampaignMenu() => InCampaignMenu = true;
    public void CloseCampaignMenu() => InCampaignMenu = false;

    public void StartCampaignStage(int num)
    {
        var s = Campaign.Get(num);
        if (s == null) return;
        InCampaignMenu = false;
        StartSingleScenario(Campaign.BuildScenario(s));
    }

    void RecordCampaign()
    {
        var c = ScenarioRunner.Current;
        if (c != null && c.CampaignStage > 0 && ScenarioRunner.Result != null)
            Campaign.Record(c.CampaignStage, ScenarioRunner.Result.Score, ScenarioRunner.Result.Passed);
    }

    public void StartScenario(Scenario s)
    {
        Effects?.ClearEffects();
        Logger.Close();
        InScenarioMenu = false;
        ScenarioActive = true;
        ElapsedFlightTime = 0f;
        Crashed = false;
        ScenarioRunner.Begin(s);
        State = GameState.Flying;
    }

    public void StartSingleScenario(Scenario s)
    {
        inSession = false; SessionReportActive = false; SessionResults.Clear();
        StartScenario(s);
    }

    public void StartGradedSession()
    {
        sessionQueue.Clear();
        SessionResults.Clear();
        SessionReportActive = false;
        inSession = true;
        foreach (var s in ScenarioLibrary.GradedSession()) sessionQueue.Enqueue(s);
        if (sessionQueue.Count > 0) StartScenario(sessionQueue.Dequeue());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COGNITIVE-LOAD EXPERIMENT SESSION
    // ═══════════════════════════════════════════════════════════════════════════
    //  eyes-open rest -> eyes-closed rest -> 12 counterbalanced missions
    //  (each followed by its NASA-TLX) -> eyes-open rest.
    //  The whole sequence is a queue of actions so the flow is explicit and one
    //  step can be aborted by the experimenter without corrupting the rest.

    /// <summary>Start the full counterbalanced experimental session.</summary>
    public void StartExperimentSession()
    {
        ExperimentSession.Begin(ParticipantManager.ID, ParticipantManager.Session);
        experimentQueue.Clear();
        SessionResults.Clear();
        SessionReportActive = false;
        ExperimentRunning = true;
        ExperimentTrialIndex = 0;
        ExperimentTrialTotal = ExperimentSession.Order.Count;

        experimentQueue.Enqueue(() => RunBaseline(BaselineKind.RestEyesOpen));
        experimentQueue.Enqueue(() => RunBaseline(BaselineKind.RestEyesClosed));
        foreach (var id in ExperimentSession.Order)
        {
            string missionId = id;   // capture
            experimentQueue.Enqueue(() => RunExperimentTrial(missionId));
        }
        experimentQueue.Enqueue(() => RunBaseline(BaselineKind.RestEyesOpen));
        experimentQueue.Enqueue(FinishExperimentSession);

        AdvanceExperiment();
    }

    /// <summary>Fly ONE mission on its own, outside a session (practice / debugging).
    /// Still writes a full, properly structured trial folder.</summary>
    public void StartSingleMission(string missionId)
    {
        var m = MissionLibrary.Get(missionId);
        if (m == null) return;
        if (!ExperimentLogger.SessionOpen)
            ExperimentSession.Begin(ParticipantManager.ID, ParticipantManager.Session);
        ExperimentRunning = false;
        ExperimentStatus = "Single mission: " + m.Id;
        StartSingleScenario(m.ToScenario());
    }

    void AdvanceExperiment()
    {
        if (experimentQueue.Count == 0) { ExperimentRunning = false; return; }
        var step = experimentQueue.Dequeue();
        step();
    }

    void RunBaseline(BaselineKind kind)
    {
        ExperimentStatus = "Baseline — " + (kind == BaselineKind.RestEyesOpen ? "eyes open" : "eyes closed");
        Effects?.ClearEffects();
        ScenarioActive = false;
        State = GameState.Baseline;
        Baseline.Begin(kind, ExperimentSession.RestBaselineS, Aircraft, () =>
        {
            State = GameState.Menu;
            AdvanceExperiment();
        });
    }

    void RunExperimentTrial(string missionId)
    {
        var m = MissionLibrary.Get(missionId);
        if (m == null) { AdvanceExperiment(); return; }
        ExperimentTrialIndex++;
        ExperimentStatus = "Trial " + ExperimentTrialIndex + "/" + ExperimentTrialTotal +
                           " — " + m.Id + " [" + m.ClassTag + "]";
        StartScenario(m.ToScenario());
    }

    void FinishExperimentSession()
    {
        ExperimentRunning = false;
        ExperimentStatus = "Session complete — " + SessionResults.Count + " trials recorded.";
        if (SessionResults.Count > 0)
        {
            SessionReporter.Generate(SessionResults);
            ParticipantManager.RecordSessionComplete();
            SessionReportActive = true;
            State = GameState.Results;
        }
        else ToMenu();
    }

    /// <summary>Emergency stop for the experimenter: abandon the session cleanly,
    /// flushing what has been recorded rather than losing it.</summary>
    public void AbortExperimentSession()
    {
        experimentQueue.Clear();
        ExperimentRunning = false;
        ExperimentStatus = "Session aborted by experimenter.";
        if (ScenarioActive) ExitScenarioToMenu(); else ToMenu();
    }

    // ---- control check bench -------------------------------------------------
    bool checkWasMenu;
    public void EnterControlCheck()
    {
        checkWasMenu = State == GameState.Menu;
        Effects?.ClearEffects();
        ScenarioActive = false;
        Aircraft.ResetTo(runway.Start, runway.Rot, false, 0f);
        Aircraft.throttle = 0f;
        // Flying state so the controller runs and the controls are live — but no
        // scenario is active, so nothing is recorded.
        State = GameState.Flying;
    }

    public void ExitControlCheck()
    {
        Aircraft.GetComponent<AircraftController>()?.ResetConfiguration(0f);
        CockpitControlRig.Instance?.ResetAll(0f);
        Aircraft.GetComponent<AircraftSystems>()?.ResetAll();
        ToMenu();
    }

    void AdvanceSessionOrMenu()
    {
        if (SessionReportActive) { ExitScenarioToMenu(); return; }

        // Cognitive-load experiment: record the trial, then run the next queued step
        // (another mission, a baseline block, or the session report).
        if (ExperimentRunning)
        {
            if (ScenarioRunner.Result != null) SessionResults.Add(ScenarioRunner.Result);
            ScenarioRunner.Abort();
            ScenarioActive = false;
            AdvanceExperiment();
            return;
        }

        if (inSession && ScenarioRunner.Result != null) SessionResults.Add(ScenarioRunner.Result);
        if (sessionQueue.Count > 0) { StartScenario(sessionQueue.Dequeue()); return; }
        if (inSession && SessionResults.Count > 0)
        {
            SessionReporter.Generate(SessionResults);
            ParticipantManager.RecordSessionComplete();
            SessionReportActive = true;
            return;
        }
        ExitScenarioToMenu();
    }

    void ExitScenarioToMenu()
    {
        ScenarioRunner.Abort();
        ScenarioActive = false;
        inSession = false;
        ExperimentRunning = false;
        experimentQueue.Clear();
        SessionReportActive = false;
        sessionQueue.Clear();
        ToMenu();
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Flying:
                float dt = Time.deltaTime;
                ElapsedFlightTime += dt;

                if (ScenarioActive) { UpdateScenarioFlying(dt); break; }
                if (Level == null) break;   // safety net — never spam if init was incomplete

                Level.Tick(dt);

                if (Level.Measured)
                {
                    logAccum += dt;
                    if (logAccum >= LogInterval)
                    {
                        Logger.Sample(Aircraft, Level, ElapsedFlightTime);
                        logAccum = 0f;
                    }
                }

                if (Aircraft.Crashed) { Crash(); break; }
                if (Aircraft.AltitudeM < -40f) { StartFlight(Level.Mode); break; } // fell out of the world -> reset

                if (Input.GetKeyDown(KeyCode.R)) StartFlight(Level.Mode);
                else if (Input.GetKeyDown(KeyCode.Escape)) ToMenu();
                else if (Level.IsComplete) Finish();
                break;

            case GameState.Results:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (ScenarioActive) AdvanceSessionOrMenu(); else ToMenu();
                }
                else if (Input.GetKeyDown(KeyCode.R))
                {
                    if (ScenarioActive) { Effects?.ClearEffects(); ScenarioRunner.Restart(); Crashed = false; State = GameState.Flying; }
                    else StartFlight(Level.Mode);
                }
                break;
        }
    }

    void UpdateScenarioFlying(float dt)
    {
        ScenarioRunner.Tick(dt);

        if (Aircraft.Crashed)
        {
            var tier = Aircraft.Landing != LandingTier.None ? Aircraft.Landing : LandingTier.Destroyed;
            Effects?.Trigger(tier, Aircraft.transform.position);
            ScenarioRunner.OnCrash(); RecordCampaign(); Crashed = true;
            ScenarioRunner.CloseTrial(); State = GameState.Results; return;
        }
        if (Aircraft.AltitudeM < -40f) { ScenarioRunner.Restart(); return; }   // fell out of the world -> restart trial

        if (Input.GetKeyDown(KeyCode.R)) ScenarioRunner.Restart();
        else if (Input.GetKeyDown(KeyCode.Escape)) ExitScenarioToMenu();
        else if (ScenarioRunner.IsComplete) { RecordCampaign(); ScenarioRunner.CloseTrial(); State = GameState.Results; }
    }

    void Finish()
    {
        if (Level.Measured && Level.Result != null)
        {
            Logger.WriteSummary(Level.Result);
            Logger.Close();
        }
        State = GameState.Results;
    }

    void Crash()
    {
        Crashed = true;
        var tier = Aircraft.Landing != LandingTier.None ? Aircraft.Landing : LandingTier.Destroyed;
        Effects?.Trigger(tier, Aircraft.transform.position);
        if (Level.Measured) Logger.Close();
        State = GameState.Results;   // ResultsUI shows the crash; R restarts, Enter -> menu
    }

    void ToMenu()
    {
        Effects?.ClearEffects();
        Logger.Close();
        Crashed = false;
        Aircraft.ResetTo(runway.Start, runway.Rot, false, 0f);
        State = GameState.Menu;

        // THERE IS NO MENU TO RETURN TO IN UI-ONLY MODE.
        //
        // MenuUI is not instantiated, so Escape while flying — and Enter on the results
        // screen after a crash — would both drop the player into GameState.Menu with
        // nothing drawn: a parked aeroplane, an empty screen and no way forward. Free
        // flight IS the application here, so "back to the start" means back on the runway
        // and flying, which is also what Escape does in every other sandbox sim.
        if (UiOnlyActive) StartFlight(FlightMode.FreeFlight);
    }

    // ---- programmatic control API ----------------------------------------------
    // These mirror the keyboard paths (Enter / R / Escape) exactly, so the state
    // machine can be driven from code — by the automated mission-test harness, and
    // by any future alternative input (controller, footswitch, experimenter console)
    // without depending on an OnGUI click landing.

    /// <summary>Mirrors Enter/Return on the Results screen.</summary>
    public void Confirm()
    {
        if (State != GameState.Results) return;
        if (ScenarioActive) AdvanceSessionOrMenu(); else ToMenu();
    }

    /// <summary>Mirrors R — restart in Flying or Results.</summary>
    public void RestartTrial()
    {
        switch (State)
        {
            case GameState.Flying:
                if (ScenarioActive) ScenarioRunner.Restart(); else StartFlight(Level.Mode);
                break;
            case GameState.Results:
                if (ScenarioActive) { Effects?.ClearEffects(); ScenarioRunner.Restart(); Crashed = false; State = GameState.Flying; }
                else StartFlight(Level.Mode);
                break;
        }
    }

    /// <summary>Mirrors Escape — bail out to the main menu while flying.</summary>
    public void BackToMenu()
    {
        if (State != GameState.Flying) return;
        if (ScenarioActive) ExitScenarioToMenu(); else ToMenu();
    }
}
