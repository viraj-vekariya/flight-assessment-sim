# FINAL_EEG_INTEGRATION

How EEG gets attached to this simulator, how it is aligned, and how the eventual
dataset should be built without fooling yourself.

**The simulator does not record, generate, estimate or simulate EEG.** It emits
timestamped markers and telemetry, and leaves a labelled, empty folder for the real
recording. Nothing in this repository will ever produce a number that looks like a
brain signal but is not one.

---

## 1. What the simulator provides

```
Unity ──► events.csv       every marker, four clocks each
      ──► telemetry.csv    50 Hz aircraft, control and systems state (physics-locked)
      ──► LSL outlet       the same markers, live, on liblsl's clock
      ──► eeg/sync.json    clock offsets captured at trial start
      ──► eeg/             empty, for you to put the recording in
```

Implemented in `ExperimentLogger.cs`, `EventMarkers.cs`, `LSLSync.cs`.

The marker vocabulary is **53 tags**. Seven of them describe physical cockpit interaction
— `ControlGrab`, `ControlRelease`, `FlapSelected`, `TrimChanged`, `ThrottleChanged`,
`BrakeApplied`, `BrakeReleased` — and exist for the analysis in §5b, not for realism.
They are **debounced on purpose**: a continuous control emits a marker only when it moves
past a threshold (0.10 for trim, 0.15 for throttle) and no more than once a second, so a
pilot holding the yoke does not bury the event stream under thousands of hand-movement
markers. Continuous detail lives in the 50 Hz telemetry, where it belongs.

---

## 2. Turning on the live marker stream

The LSL path is optional and off by default, because the package is not vendored.

1. Unity → Window → Package Manager → **+** → *Add package from git URL*:
   `https://github.com/labstreaminglayer/LSL4Unity.git`
2. Project Settings → Player → Other Settings → **Scripting Define Symbols**: add
   `LSL4UNITY`.
3. Recompile.

With the define absent, every `LSLSync` method is an inert no-op, `t_lsl` is written
as `-1`, and `sync.json` records `"lsl_available": false`. Nothing pretends a link
exists.

**Recording run sheet**

1. Start the amplifier's LSL stream.
2. Start LabRecorder. Tick **both** the EEG stream and `FlightSimMarkers`. Record.
3. Press Play in the simulator, run the session.
4. Stop recording. The `.xdf` now holds EEG and markers on one clock.
5. Copy the `.xdf` into the trial's `eeg/` folder (or keep one session-level file and
   say so in `eeg_notes.txt` — either is fine, but be consistent across participants).

The stream is self-describing: its `desc` carries the experiment name, participant
code, session number, and the entire marker vocabulary, so the `.xdf` can be
interpreted years later without this repository.

---

## 2b. The telemetry rate is guaranteed, not nominal

`telemetry.csv` is sampled on the **physics step**, not the render frame. Unity's
default `fixedDeltaTime` is 0.02 s, so the rate is exactly 50 Hz and does not depend on
frame rate.

This matters more than it sounds. When sampling was frame-driven it could only ever
fire once per frame — 30 Hz at 30 fps — and the rate therefore fell exactly where the
scene was busiest, which is to say in the **highest-workload missions**. A sample rate
that correlates with the independent variable is not a nuisance, it is a confound in
the measurement instrument itself. It is fixed; do not reintroduce frame-driven
sampling.

The same change makes event firing deterministic for a given seed regardless of the
machine, which is what makes a trial reproducible on a different computer.

## 3. The clock contract

Every row of `events.csv` and `telemetry.csv` carries four time fields:

| field | source | properties |
|---|---|---|
| `t_mission` | seconds since `MISSION_START` | convenient; resets each trial |
| `t_host` | `Time.realtimeSinceStartupAsDouble` | **monotonic master.** Not affected by `Time.timeScale`, does not accumulate frame-rate error |
| `t_unix` | `DateTime.UtcNow` | ms resolution; the cross-machine bridge |
| `t_lsl` | `LSL.local_clock()` | `-1` when LSL is absent |

`t_host` is the master because it is the only one that is both monotonic and free of
accumulation error. The previous logger summed `Time.deltaTime`, which drifts over a
300 s trial; that is fixed.

### Alignment recipes

**Case 1 — LSL (preferred).** The markers are already inside the `.xdf` beside the
EEG on the same clock. Align on the marker stream directly. No offset, no arithmetic.

**Case 2 — a separate recorder (BrainVision, Actiview, an SD-card amplifier).**
Record one physical event both systems can see (a TTL pulse, a hardware button, a
sharp clap in front of an accelerometer). Then:

```
offset = eeg_time(shared_event) − t_unix(nearest marker to the shared event)
eeg_time(any marker) = t_unix(marker) + offset
```

`sync.json` stores `trial_start_unix_s`, `trial_start_host_s`, `trial_start_lsl_s`
and the pairwise offsets so this is recoverable long after the session.

**Verify the offset, do not assume it.** Check a second shared event near the end of
the session. If the implied offset has drifted by more than a few milliseconds, the
two clocks have different rates and you need a linear fit, not a constant.

---

## 4. Which marker is epoch zero — this matters more than anything else here

`TRIGGER_ARMED` and `CUE_ONSET` are **different events and are logged separately.**

* `TRIGGER_ARMED` — the instant the simulation injected the abnormality.
* `CUE_ONSET` — the instant the symptom first became perceptible to the pilot.

For an abrupt failure (engine failure, door open) they are simultaneous. For a
gradual one they are not: carburettor ice is armed at t ≈ 90 s but the power loss
does not become noticeable until t ≈ 115–130 s. Averaging on `TRIGGER_ARMED` would
smear any evoked response across a 40 s window and destroy it.

**Use `CUE_ONSET` for anything event-related.** For response-locked analysis use
`PILOT_FIRST_RESPONSE`, which is the first control or switch action after the cue
that exceeds a deadband — an objective latency that does not depend on the
participant pressing an "I noticed" key.

The test harness enforces this: a trial that emits `TRIGGER_ARMED` without a
subsequent `CUE_ONSET` is reported as a failure, because it would have no valid
epoch zero.

Also exclude the questionnaire period. `TLX_START` and `TLX_SUBMIT` bracket it, and
it is recorded but is not a task epoch.

---

## 5. Building the dataset — and the leakage trap

This is where EEG workload papers most often go wrong, and the failure mode produces
*better-looking* numbers, so it does not announce itself.

### The trap

A 300 s trial windowed at 2 s with 50% overlap gives ~300 windows. Shuffle all
windows from all participants and split 80/20 at random, and you get a beautiful
accuracy — because adjacent windows share raw samples, and because windows from the
same participant in the same session share that participant's individual alpha peak,
skull thickness, electrode placement and hair. The model learns *who and when*, not
*how loaded*. Published work has repeatedly shown this inflates results, and that
overlapping train/test windows inflate them further as overlap increases.

### The rule

**Split by participant, never by window.**

```
Leave-One-Subject-Out (LOSO)
    for each participant p:
        train on all trials of all participants except p
        test  on all trials of p
    report mean ± SD across folds, and the per-fold numbers
```

If you also want a within-subject number (a legitimate, different question — "can we
track load in a pilot we have calibrated on?"), report it **separately and labelled
as such**, and split it by *trial*, not by window: hold out whole trials, never
windows from a trial that also appears in training.

Additional rules that follow from the same logic:

* **Fit every normalisation inside the training fold.** A z-score computed over the
  whole dataset leaks test statistics into training. Baseline normalisation per
  participant is fine and encouraged — it is applied within-subject and does not
  cross the fold boundary.
* **Do not pool sessions without normalising within session.** Cross-session
  generalisation in EEG workload decoding collapses badly — one flight-simulator
  study reports within-session accuracy of ~80% falling to near chance (~35%) when
  tested on a different session's data. A model that has not been evaluated across
  sessions should not be described as generalising.
* **Report the chance level and the class balance.** Three balanced classes is 33%.
  With 12 trials per participant and LOSO over a small N, confidence intervals are
  wide; give them.
* **Beware the trial-identity shortcut.** With only 12 trials per participant, a
  model can learn "this is mission H3" from idiosyncratic features and read the label
  off that. Grouping by mission as well as by participant (leave-one-mission-out,
  as a secondary analysis) is a cheap check.

---

## 5b. Separating motor activity from cognitive workload

This is the analysis that decides whether a workload result is believable, and the
apparatus was instrumented specifically to make it possible.

**The problem.** HIGH missions involve more hand movement than LOW missions — a failure
drill genuinely requires more switch throws than straight-and-level cruise. Motor
activity produces EEG. So a classifier could reach a high accuracy by detecting *arm
movement* while appearing to detect *workload*, and nothing in the confusion matrix would
reveal it.

**What is logged.** `control_held` names the control physically held in every 50 Hz
telemetry row (empty when the pilot's hands are off), the `ovr_*` columns say which axes
were externally commanded, `ctrl_jerk` gives a continuous motor regressor, and the seven
interaction markers timestamp discrete actions on the EEG clock.

**The matched-quiet-window analysis — run this before believing any classification
result:**

```python
# windows in which NO cockpit control was touched at all
quiet = w[(w.control_held_frac == 0.0) & (w.ctrl_jerk_mean < thresh)]
# then repeat the LOW vs HIGH comparison on `quiet` alone
```

If the workload effect survives on quiet windows only, it is not motor artefact. If it
disappears, **say so** — that is a real finding about the apparatus, and it is far better
found by you than by an examiner.

Three weaker fallbacks, in decreasing order of strength:

1. **Epoch exclusion** — drop any window containing a control action. Clean, but costly
   in HIGH trials, which is itself an asymmetry worth reporting (see the artefact-
   rejection warning in `DESIGN_CRITIQUE.md`).
2. **Covariate regression** — include actions-per-window or held-fraction as a covariate
   and report the effect with and without it.
3. **A motor-only control condition** — a mission with high physical action and low
   cognitive demand would bound the motor contribution directly. Not currently in the
   battery; worth adding if a reviewer presses on this.

**Whatever you do, report the number of control actions per condition.** It is a
one-line table and it pre-empts the first question any reviewer will ask.

---

## 6. A defensible processing pipeline

This is a starting point that matches common practice in the aviation-EEG
literature, not a prescription. Whatever you do, write down what you did.

**Preprocess**
1. Band-pass 0.5–45 Hz; notch at the local mains frequency (50 Hz in India).
2. Re-reference (average, or linked mastoids — state which).
3. Ocular artifact: ICA, or regression on an EOG channel. Blinks are the dominant
   artifact and the frontal electrodes that carry the theta signal are the ones most
   affected by them, so this step is not optional for a frontal-theta measure.
4. Reject or interpolate bad channels; mark bad segments. **Report how much data was
   rejected per condition** — if HIGH loses more data than LOW because participants
   moved more, that alone can create a spurious class difference.

**Window**
5. 2 s windows (Hann), 50% overlap for training density — but see §5: overlap is
   only safe *within* a fold, never across the train/test boundary.

**Features**
6. Band power: delta 1–4, theta 4–8, alpha 8–13, beta 13–30 Hz, per channel.
   Prefer **individually determined alpha bounds** from the eyes-closed resting
   block's alpha peak rather than fixed edges, and say which you used.
7. The workload-relevant derived indices, all of which have literature behind them:
   - **frontal-midline theta** (Fz) — rises with working-memory load
   - **parietal alpha** (Pz/POz) — falls with load
   - **theta/alpha ratio** — often outperforms either band alone
   - **engagement index** β/(α+θ)
8. Optional non-spectral features: sample entropy, Hjorth parameters, connectivity.

**Normalise**
9. Per participant, per session, z-score each feature against **that session's pooled
   in-task baseline segments** (`segment == "BASELINE"` in `telemetry.csv`, bracketed
   by `BASELINE_START`/`BASELINE_END` in `events.csv`).
   Report the eyes-open resting normalisation as a sensitivity analysis. Do not use
   the eyes-closed rest for normalisation — the eyes-open/closed alpha difference
   dwarfs the workload effect. See `FINAL_EXPERIMENT_PROTOCOL.md` §4.5 and
   `ExperimentSession.cs` for why there are three baselines.

**Classify**
10. Start with a linear model (regularised logistic regression, LDA) before anything
    deep. With ~10 participants × 12 trials, a deep network has far more capacity
    than the data supports, and the honest comparison is against a simple baseline.
11. LOSO. Report mean ± SD **and** per-fold accuracy, plus the confusion matrix —
    the interesting failure is MEDIUM being absorbed into LOW and HIGH, and only the
    confusion matrix shows it.

**Fuse (optional, and where this design has an advantage)**
12. The simulator gives synchronous behavioural measures — probe reaction time, probe
    misses, checklist item latencies, `ctrl_jerk`, tracking error. A model combining
    EEG with behaviour should beat EEG alone; if it does not, that is worth reporting.
13. `ctrl_jerk` also serves as the **motor-artifact regressor**: show that the EEG
    class effect survives controlling for control activity, or report honestly that
    it does not separate.

---

## 7. What is genuinely not solved here

* **No hardware has been connected.** The LSL path is written and compiles, but it
  has never been tested against a live amplifier. Budget time to verify the marker
  stream end-to-end before the first real participant, not during.
* **No EEG data exists**, so nothing in this repository has been validated against
  brain signals. Every EEG claim in these documents is a prediction from the
  literature.
* **VR and EEG do not co-exist comfortably.** A Quest 2 head strap crosses frontal,
  central and parietal electrode sites — including where the alpha and frontal-theta
  measures live. Strap pressure also drifts impedance over a session, and head movement
  in VR is both unavoidable and correlated with looking behaviour, which is itself
  correlated with workload — so the artefact and the signal share a cause. The default
  recommendation stands: **run the study desktop**. If VR is used, do the hardware pilot
  in `VR_EXPERIMENT_CONSIDERATIONS.md` §3 first, and never pool the two modalities —
  `input_modality` is in `session.json` and every `metadata.json` precisely so this can
  be enforced in the analysis rather than remembered.
* **Mains frequency and impedance targets** are lab-specific and are not encoded
  anywhere in this repository. Record them in `eeg_notes.txt` per session.
