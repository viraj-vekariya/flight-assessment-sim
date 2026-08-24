# FINAL_ARCHITECTURE

How the pieces fit together and, more usefully, **which direction information flows** —
because that is what determines whether the measurement infrastructure can be trusted.

For *where each piece came from*, see `MERGE_ARCHITECTURE.md`.
For *what each piece approximates*, see `MISSION_IMPLEMENTATION.md`.

---

## 1. The shape of the thing

```
                        FLIGHTASSESSMENTSIM - FINAL
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
   FLIGHT SYSTEM               EXPERIMENT                    COCKPIT
        │                           │                           │
  CessnaPhysics             ExperimentSession            CockpitBuilder
   ├ turn coordination       ├ Latin square order         ├ RealCockpit (GLB)
   ├ rate damping            ├ session seed               ├ LivePFD
   └ 5 failure hooks ◄──┐    └ BaselineRunner             ├ LiveMFD
  AircraftController    │          │                      └ CockpitInteraction
   └ flap authority ◄───┤    ScenarioEngine                        │
  AircraftSystems ──────┘     ├ MissionLibrary (12)                │
   ├ engine/fuel/electrical   ├ ChecklistLibrary (6)               │
   ├ pitot-static             ├ TrafficAircraft[]                  │
   └ airframe                 └ taxi state machine                 │
  WorldBuilder                       │                             │
   ├ terrain + PadFlatten            │                             │
   └ AerodromeBuilder                │                             │
        │                            │                             │
        └────────────────┬───────────┴─────────────────────────────┘
                         │
                  PILOT INTERACTION
              (keyboard · mouse cockpit · systems switches)
                         │
                         ▼
                 ExperimentLogger          ◄── the single write point
                         │
        ┌────────────────┼────────────────┬──────────────┐
        │                │                │              │
   telemetry.csv     events.csv     nasa_tlx.json   metadata.json
    50 Hz, 77 col    53-tag vocab    participant's   spec + PRE-REGISTERED
    physics-locked   4 clocks each   own responses   predictions
        │                │
        └────────┬───────┘
                 │
        EEG SYNCHRONISATION  (LSL outlet · sync.json · eeg/)
                 │
                 ▼
       analysis/build_dataset.py
                 │
        trials.csv + windows_index.csv
         (carrying the GROUP KEYS that
          make a non-leaking split possible)
```

---

## 2. The four contracts that make this a measuring instrument

Everything else is a flight simulator. These four are what make it an experiment.

### 2.1 One write point

**All** experimental data goes through `ExperimentLogger`. Nothing else opens a file in
the experiment tree. Consequences: every row of every file shares one clock source; a
change to the schema happens in one place; and there is no way for a subsystem to
quietly record something in a different format.

`ScenarioLogger` still exists for the legacy campaign/free-flight path, which writes
elsewhere and is not experiment data.

### 2.2 The clock is the physics step, not the frame

`Time01` and the telemetry sampler both advance in `FixedUpdate`. Unity's
`fixedDeltaTime` is 0.02 s, so telemetry is **exactly 50 Hz regardless of frame rate**
and event firing is deterministic for a given seed regardless of the machine.

This was not always true, and the failure mode was insidious: frame-driven sampling
fell to ~30 Hz at 30 fps, i.e. *the busiest missions were sampled worst*, so the sample
rate correlated with the independent variable. Do not reintroduce frame-driven
sampling.

### 2.3 Failures reach the aeroplane through exactly five hooks

`AircraftSystems` may touch the flight model only via
`powerAvailable01`, `extraCd`, `extraRollTorque`, `extraYawTorque` and
`flapAuthority01`. With every system healthy these are `1 / 0 / 0 / 0 / 1` and the
flight model is bit-identical to one with no systems layer at all.

That is what lets you assert that a mission's abnormality — and nothing else about the
mission — changed how the aeroplane behaved.

### 2.4 `TRIGGER_ARMED` ≠ `CUE_ONSET`

The instant the simulation injects an abnormality and the instant the pilot could first
perceive it are separate events with separate markers. For an abrupt failure they
coincide; for a gradual one they can be tens of seconds apart.

**Epoch on `CUE_ONSET`.** The test harness enforces this: a trial that emits
`TRIGGER_ARMED` without a subsequent `CUE_ONSET` is a test failure, because it would
have no valid epoch zero.

---

## 3. Control-flow: what happens when you press RUN FULL SESSION

```
MenuUI ──► GameManager.StartExperimentSession()
              │
              ├─ ExperimentSession.Begin(code, session)
              │     ├ Latin-square row from the participant code
              │     ├ de-clump runs of the same class
              │     └ ExperimentLogger.BeginSession() → session.json
              │
              └─ builds a queue of steps:
                    RunBaseline(EyesOpen)      ─┐
                    RunBaseline(EyesClosed)     │  each step calls
                    RunExperimentTrial(id) x12  ├─ AdvanceExperiment()
                    RunBaseline(EyesOpen)       │  when it finishes
                    FinishExperimentSession    ─┘

RunExperimentTrial(id)
   └─ MissionLibrary.Get(id).ToScenario()
        └─ ScenarioEngine.Begin(scenario, seedOffset)
             ├ AircraftSystems.ResetAll()          no failure survives a trial
             ├ AircraftController.ResetConfiguration()   no flap survives a trial
             ├ ScheduleEvents()                    jitter drawn from the trial seed
             ├ ExperimentLogger.BeginTrial()       folder + metadata + eeg/README
             └ MISSION_START, BASELINE_START
        ...
        Complete() → MISSION_SUCCESS/FAILURE, MISSION_END, performance.json
                     (logger stays OPEN)
   └─ GameManager.BeginWorkloadQuestionnaire() → TLX_START
        └─ ResultsUI form → SubmitWorkloadRating()
             ├ nasa_tlx.json
             ├ TLX_SUBMIT
             └ logger CLOSED here, not at MISSION_END
```

The logger stays open through the questionnaire on purpose: the EEG is still recording
during it, so the questionnaire period has to be bracketed on the same clock in order
to be **excluded** from task epochs.

---

## 4. Adding things without breaking the contracts

| To add… | Do this | Do not |
|---|---|---|
| a new abnormality | add a `FailureKind`, model it in `AircraftSystems`, drive it through the five hooks, give it a perceptibility rule in `RaiseCueIfPerceptible` | touch `CessnaPhysics` directly from mission code |
| a new mission | add a builder to `MissionLibrary` with a full `WorkloadProfile` and `ExpectedTlx`; keep the phase × class grid balanced; regenerate docs; run the battery | leave the grid unbalanced, or hand-edit the generated docs |
| a new marker | add it to `EventMarkers.All` | emit a bare string — the harness rejects markers outside the vocabulary |
| a new telemetry column | add it to `ExperimentLogger.TelemetryHeader()` **and** the matching `Sample()` write, in the same order | add one without the other; the harness checks column counts per row |
| a new headless tool | make it stand down for `-missiontest`, `-probe`, `-screens` if it writes control inputs | let two harnesses drive the aeroplane at once — this exact bug silently invalidated a whole battery run |

---

## 5. Deliberate architectural choices worth knowing

* **Everything is built from code at runtime.** No prefabs, no wired scenes. `Bootstrap`
  spawns `GameManager`, which builds the world, the aircraft and the cockpit. The single
  launch path removes an entire class of "which scene was that recorded in?" ambiguity,
  and it is why the project has no scene-management strategy to get wrong.
* **The cockpit is a real GLB model, loaded at runtime, with graceful fallback.** If
  `cessna_g1000.glb` is missing, the code-built cockpit is used and the sim still runs —
  visibly different, so the failure cannot pass unnoticed.
* **The legacy campaign/free-flight layer was kept, not deleted.** Scenarios with
  `Scenario.Mission == null` take the old code path and write the old flat CSV. Free
  flight is how participants familiarise, and it must not write experiment data.
* **The VR layer was dropped, not ported.** See `MERGE_ARCHITECTURE.md` §4. Final is the
  flatscreen experiment build; the VR project still exists separately.
