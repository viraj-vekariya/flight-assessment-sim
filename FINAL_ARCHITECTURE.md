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
    50 Hz, 82 col    53-tag vocab    participant's   spec + axis + variant
    physics-locked   4 clocks each   own responses   + PRE-REGISTERED preds
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

## 2. The seven contracts that make this a measuring instrument

Everything else is a flight simulator. These are what make it an experiment.

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

### 2.4 Zero wind is bit-identical

`WindModel.Sample()` returns exactly `Vector3.zero` when no wind is configured, and every
aerodynamic term in `CessnaPhysics` uses the AIR-relative velocity — which, with no wind,
*is* the ground-relative velocity. A calm mission therefore flies the same flight model it
flew before the wind layer existed, to the bit. That is what let the wind model be added
without re-validating the eleven missions that do not use it, and it is the same contract
`AircraftSystems`' five hooks have.

The corollary is a rule: **a cognitive-axis mission must not carry wind.** Manual demand
is matched within each phase row on that axis, and a crosswind raises manual demand by
construction. The mission battery enforces it (`design check (g)`).

### 2.5 One driver at a time

Six headless harnesses can write control inputs. If two run at once they fight every
frame, and the symptom is not a crash — it is a battery that completes, reports numbers,
and is wrong. `SimDriver.Claim()` gives control to the first claimant and makes any later
one stand down, loudly.

The previous defence was an opt-out list inside `FlightTest`: it spawned on *any*
`-batchmode` run unless the command line named one of seven flags. That is a defence that
decays, and it had already decayed — `-windtest` was not on the list, so the entire first
wind battery ran with `FlightTest` flying the aeroplane, `AircraftController` disabled
behind everyone's back, and every commanded input discarded. A claim cannot decay.

### 2.6 The motor confound is measured, not assumed

The cognitive axis rests on manual demand being matched within a phase row. That is a
claim about the human, and a claim about the human is only as good as the measurement
that checks it. `ControlActivity` accumulates control travel, movement rate, per-axis
standard deviation and hands-on fraction on **every physics step** (not on the telemetry
tick — the metric is an integral of movement, and sampling it at the telemetry rate would
alias fast stick reversals into a smaller number).

On the cognitive axis these are a **manipulation check**: within a row, the three classes
should *not* differ much on them, and if they do, that row's EEG contrast must be reported
with the caveat. On the psychomotor axis they are the **covariate** an EEG effect has to
survive adjustment for.

The standard-deviation terms earn their place separately from the rate terms: they are
what distinguishes a pilot holding a large steady correction (a crosswind crab — high
deflection, low variability) from a pilot chasing the aeroplane (over-control — similar
deflection, high variability). Those look identical on a rate measure and are not the
same behaviour at all.

### 2.7 `TRIGGER_ARMED` ≠ `CUE_ONSET`

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
| a new mission variant | give it a `Mechanism` no sibling in its cell uses, keep its `ManualControl` inside the row's spread, and keep its PLI within 8 of its siblings' | assume it is exchangeable — the battery checks all three |
| a crosswind or wind condition | put it on the **psychomotor axis** | put wind on a cognitive-axis mission |
| a departure heading | check the terrain-clearance report in the battery output | assume the aerodrome plain covers it |
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
* **The VR layer was reinstated after the merge.** `MERGE_ARCHITECTURE.md` §4 records it
  being dropped; it was subsequently rebuilt properly — OpenXR with the loader chain
  actually configured, a seated origin at the measured pilot eye point, and one
  interaction model with two hardware paths (`CockpitInteractorMouse` and
  `CockpitInteractorVR` both reduce to grab/update/release/click, so desktop and VR
  operate an *identical* cockpit). The scientific caution in that section still stands and
  is not superseded: an EEG cap under a head strap sits on the occipital and parietal
  sites the alpha measure depends on, and VR changes measured workload anyway. **VR and
  flatscreen sessions must never be pooled**, which is why `input_modality` is recorded
  per session.
* **The air mass is a first-class part of the flight model.** `WindModel` (steady wind,
  power-law surface shear, optional discrete shear layer, seeded gusts) is what makes a
  crosswind task possible at all. Before it, `MissionDefinition.CrosswindMs` was declared,
  written by nothing and read by nothing, and every landing was flown in dead calm.
* **The aerodrome sits on a 3.2 km plain, not a corridor.** The flattened pad used to be
  260 m wide and 1.6 km long — exactly enough for departures that go straight ahead, which
  was all the old mission set contained. Once missions began assigning departure turns,
  the aeroplane left it laterally while still climbing through 150 m. Flattening only ever
  lowers terrain, so widening it cannot make a previously verified mission less safe.
* **Physical flight controls have a path in.** `HardwareInput` reads six named axes with
  per-axis calibration and stands down when no device is present or a harness is driving.
  See `HARDWARE_CONTROLS.md`.
