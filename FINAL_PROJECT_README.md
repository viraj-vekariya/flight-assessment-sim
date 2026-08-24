# FlightAssessmentSim — Final

The consolidated project: a photoreal Cessna 172 glass cockpit driving a **twelve-mission
EEG cognitive-workload experiment**. This is the only project to use from now on.

Working title: *Automatic Workload Assessment using Brain Signal Classification*.

It combines the cockpit, displays and interaction from `FlightAssessmentSim - Extra - Copy`
with the complete research apparatus from `FlightAssessmentSim - VR`. Both source
projects are untouched and remain on disk as references. See `MERGE_ARCHITECTURE.md`.

---

## Read these, in order

| # | Document | Answers |
|---|---|---|
| 1 | **`FINAL_MISSION_DESIGN.md`** | What the twelve missions are. *Generated from code — never hand-edit.* |
| 2 | **`DESIGN_CRITIQUE.md`** | What is wrong with this experiment. Read it second, not last. |
| 3 | `MERGE_ARCHITECTURE.md` | What came from where, and what was deliberately dropped |
| 4 | `FINAL_EXPERIMENT_PROTOCOL.md` | How to run a session; the duration decision; threats to validity |
| 5 | `COGNITIVE_LOAD_MODEL.md` | Why each mission got its class, and how to prove the labelling wrong |
| 6 | `FINAL_EEG_INTEGRATION.md` | How EEG attaches and aligns; the data-leakage trap |
| 7 | `NASA_TLX.md` | The subjective instrument and the two corrections made to it |
| 8 | `FINAL_TELEMETRY_SCHEMA.md` | Every logged column. *Also generated from code.* |
| 9 | `MISSION_IMPLEMENTATION.md` | Architecture and **every approximation the simulator makes** |
| 10 | `FINAL_TEST_REPORT.md` | What is verified, what the testing found, what is **not** verified |
| 11 | `RESEARCH_REFERENCES.md` | Sources, each marked verified / standard / needs-checking |
| 12 | **`COCKPIT_CONTROLS.md`** | Every physical cockpit control and how it is wired |
| 13 | `CONTROL_SENSITIVITY.md` | Every travel, capture radius and time constant, and why |
| 14 | `VR_INTERACTION.md` | How VR works, and **what is not yet verified on hardware** |
| 15 | `VR_CALIBRATION.md` | The two-minute per-participant VR setup |
| 16 | **`VR_EXPERIMENT_CONSIDERATIONS.md`** | What VR does to the science. Read before collecting VR data. |
| 17 | `VR_PHASE_REPORT.md` | The cockpit/VR phase: what was built, fixed, measured, and left unverified |

---

## Open the project

Unity **6000.0.77f1**, Built-in Render Pipeline. Unity Hub → Add → this folder → open →
press **Play**. Everything (terrain, aerodrome, aircraft, cockpit) is generated from C#
at runtime; there is no scene to set up and nothing to wire in the inspector.

The real cockpit is loaded at runtime from
`Assets/StreamingAssets/Cockpit/cessna_g1000.glb` via glTFast. If that file is missing
the code-built cockpit is used instead and the sim still runs — you will see it
immediately, because the panel becomes flat-shaded primitives.

> Surfaces pink? The project was switched to URP. `Packages/manifest.json` deliberately
> excludes it.

---

## Run an experiment session

1. Press **Play**.
2. Enter the participant's **study code** (never a name — see *Data* below).
3. **RUN FULL SESSION.** The simulator drives everything from there:

```
eyes-open rest 120 s → eyes-closed rest 120 s
   → 12 counterbalanced missions, each: 60 s baseline → task → NASA-TLX + Bedford
      → eyes-open rest 120 s → session report
```

Order is a balanced Latin square indexed by the participant code, so it is
reproducible rather than random. The experimenter's status strip between trials shows
the trial number, the realised order, the seed and the data path.

**Do the familiarisation flight first** (**FREE FLIGHT**, not recorded). A participant
still learning the controls during trial 1 makes trial 1 measure interface learning,
and counterbalancing then spreads that noise onto a different condition for every
person.

### Run one mission

Main menu → pick a class → click a mission. Writes a complete, properly structured
trial folder, so it is also how you debug. `?` opens the full specification, including
the pre-registered predictions.

---

## Controls

| | |
|---|---|
| **Flight** | `W`/`S` pitch · `A`/`D` roll · `Q`/`E` rudder · `Shift`/`Ctrl` throttle · `[`/`]` elevator trim · `F` flaps · `B` brakes |
| **Cockpit (mouse)** | **LEFT**-drag the yoke, throttle, trim wheel, flap lever and brake; click switches. **RIGHT**-drag looks around. |
| **Cockpit (VR)** | **grip** to grab a control · **trigger** to press a switch · **A/X** to acknowledge · both grips + both triggers to recentre |
| **Systems** | `H` carburettor heat · `J` fuel selector · `K` shed electrical load · `L` alternate static |
| **Task** | `SPACE` acknowledge a prompt, or a checklist CHECK item |
| **Other** | `C` view · `R` restart trial · `Esc` menu (or end a rest block early) |

The nine physical cockpit controls (yoke, throttle, trim wheel, flap lever, brake, carb
heat, fuel selector, load shed, alternate static) are built on the real GLB geometry and
are operated identically by mouse and by VR controller — see `COCKPIT_CONTROLS.md`. The
keyboard remains fully live at all times: touching a cockpit control takes ownership of
that axis for one frame and hands it straight back, so the two never fight.

**Spoilers are deliberately not exposed.** A 172 has none, and offering a control the
aircraft does not have would generate behavioural data unrelated to the flight model.

**Elevator trim is new and matters.** Without it every mission carried a sustained
elevator force whose size differed by mission — an uncontrolled motor load correlated
with mission class by accident. See `CONTROL_SENSITIVITY.md` §3.

The systems keys matter: the abnormal missions are only meaningful if the pilot can
actually perform the memory items. Every press is timestamped as a
`CONFIGURATION_CHANGE` marker on the same clock as the EEG, so "when did the pilot
apply carb heat" is answerable to the millisecond without asking them.

---

## The twelve missions

Four flight phases × three workload classes, **fully crossed** — every class appears
exactly once in every phase, so a class effect can never be a phase effect.

| | LOW | MEDIUM | HIGH |
|---|---|---|---|
| **Taxi / take-off** | L1 normal departure | M1 traffic + two-part clearance | H1 amended clearance, blocked runway, conflict |
| **Climb / departure** | L2 assigned climb | M2 cabin door opens | H2 alternator failure |
| **Cruise / en-route** | L3 level hold | M3 multi-part re-clearances | H3 unreliable instruments |
| **Approach / landing** | L4 normal landing | M4 weather + late runway change | H4 engine failure |

Every mission is **300 s** with a **60 s un-manipulated in-task baseline**.
Full specifications, demand profiles and pre-registered predictions:
`FINAL_MISSION_DESIGN.md`.

---

## Where the data goes

```
~/Library/Application Support/DefaultCompany/FlightAssessmentSimFinal/
  FlightSimData/experiment/<CODE>/<SESSION>/
      session.json                order, seed, build info
      T01_L1/ T02_M3/ ...         one folder per trial, named in presentation order
          telemetry.csv           50 Hz, 77 columns
          events.csv              markers, four clocks each
          nasa_tlx.json           the participant's actual response
          metadata.json           spec + pre-registered predictions
          performance.json        objective outcome metrics
          eeg/  sync.json  README.txt      <- put the EEG recording here
```

Build an analysis table:

```bash
python3 analysis/build_dataset.py <experiment_root> -o out/
```

→ `trials.csv` (one row per trial, ~75 derived columns) and `windows_index.csv`
(2 s windows carrying the participant/session/mission keys needed for a **non-leaking**
split).

---

## Verify it still works

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
PROJ="$PWD"

# compile
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" -logFile compile.log

# fly all twelve missions and check the recorded files (exit 0 = all checks passed)
"$UNITY" -batchmode -projectPath "$PROJ" \
         -executeMethod PlayCapture.RunMissionTest -missiontest -logFile test.log

# every cockpit control: wiring, direction, gearing, reset, frame-rate independence
"$UNITY" -batchmode -projectPath "$PROJ" \
         -executeMethod PlayCapture.RunControlTest -controltest -logFile ctest.log

# regenerate the generated docs from the code
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
         -executeMethod DocGen.Generate -logFile docgen.log

# check the XR loader chain is actually configured (not just that OpenXR is installed)
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
         -executeMethod XRSetup.Verify -logFile xr.log
```

Interactively there is also **CONTROL CHECK** on the main menu (or `-controlcheck`): a
bench that shows every control's live value. Run it before every VR session — it catches
a dead controller in forty seconds instead of after an hour of EEG.

> **Do not add `-nographics` to the mission battery.** The real cockpit's PFD and MFD
> render to off-screen cameras, and Unity's null graphics device crashes in the
> dynamic batch renderer when they do. `-batchmode` alone is correct.

Run the battery after **any** change to the missions, the systems model or the flight
model. Between them the two batteries have caught fourteen defects that were invisible on
paper — see `FINAL_TEST_REPORT.md`.

Cockpit/visual verification tools (from the cockpit project's own toolchain) still
work: add `-screens`, `-probe` or `-flighttest` with `-executeMethod PlayCapture.Run`.

---

## Adding a mission

1. Add a builder to `Assets/Scripts/MissionLibrary.cs` following the existing pattern —
   initial conditions, event schedule, a `WorkloadProfile`, an `ExpectedTlx`, and the
   prose fields (rationale, EEG relevance, expected errors, aviation basis,
   **approximations**).
2. Add its id to the phase/class grid. The set must stay balanced: if you add one, the
   crossing is broken unless you add one per class.
3. Regenerate the docs (`DocGen.Generate`) — `FINAL_MISSION_DESIGN.md` and
   `FINAL_TELEMETRY_SCHEMA.md` are generated, so they cannot drift.
4. Run the battery.

## Debugging a failure

* The battery writes `mission_test_report.txt` into the data folder and echoes it to
  the log under `[MTEST]`.
* A mission that runs but records nothing → check `events.csv` exists and that
  `MISSION_START` appears exactly once.
* A failure that never produces `CUE_ONSET` → `AircraftSystems.RaiseCueIfPerceptible`
  never fired, so the symptom never became perceptible. That is a real defect: without
  it the trial has no valid EEG epoch zero.
* Aircraft behaving oddly in a headless run → check nothing else is driving the
  controls. `FlightTest` auto-runs under `-batchmode` and now stands down for
  `-missiontest`, `-probe` and `-screens`; anything new that writes `pitchInput` must
  do the same.

---

## Three things to be clear about

* **No participant has flown this, and no EEG has ever been recorded.** Every
  statement about workload in these documents is a prediction from the literature,
  labelled as such. The `expected_nasa_tlx` values in each `metadata.json` are
  pre-registered predictions and must never be reported as results.
* **The `TEST01` participant folder is synthetic** — written by the automated harness
  with fixed questionnaire values to exercise the write path. Never analyse it;
  `build_dataset.py` excludes it by default.
* **Run the study flatscreen, not in VR.** An EEG cap under a Quest 2 head strap sits
  on exactly the occipital and parietal sites the alpha measure depends on, and VR
  changes measured workload anyway. This project is deliberately the flatscreen build;
  `FlightAssessmentSim - VR/` still exists for the VR demonstration, and the two must
  never be pooled in one analysis.
