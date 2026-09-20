# DATA MANAGEMENT

How data is recorded for 30–50 participants flying at their own pace, how it is turned
into an analysis-ready dataset, and which trials you are allowed to use.

---

## 1. The rule that matters

**Filter on `usable == "true"` in `export/index.csv`. Nothing else.**

`usable` = `complete` **AND** `continuous`.

| flag | meaning | how it fails |
|---|---|---|
| `complete` | the participant flew the trial to the end | they quit, crashed out, or closed the game |
| `continuous` | no hole in *wall-clock* time inside the trial | they paused or alt-tabbed mid-trial |

A trial can be **complete and still unusable.** This is not hypothetical — it is already in
the data. `P002/S01_20260906T173743/T01_L3V3` contains a **3,197-second (53-minute) jump**
in `t_host` at `t_mission = 60.02`, the instant the in-task baseline ended. Someone walked
away and came back.

`t_mission` advances per frame, so across that hole it stepped a clean 0.02 s and shows
**nothing at all**. Only `t_host` recorded it. Had you epoched EEG on `t_mission`, the task
segment would have been matched against the wrong 53 minutes of brain data, and every check
downstream would have passed.

The simulator now marks this at the source: a `RECORDING_GAP` event, plus `recording_gaps`,
`paused_total_s`, `max_gap_s` and `continuous` in each trial's `trial.json`.

**The threshold is measured, not guessed.** Across 34 clean battery trials the worst engine
hitch (asset load, GC) was **0.638 s**. The one real participant pause on disk was
**3,197 s**. The discontinuity threshold sits at **2.0 s** — about 3× above the worst hitch
and three orders of magnitude below a human walking away.

Gaps over 0.5 s are still *logged* as events, because a hitch is worth seeing; only gaps
over 2.0 s make a trial discontinuous. The first cut used 0.5 s for both jobs and marked
2 of 34 perfectly clean trials unusable — a filter that rejects good data is worse than no
filter. `build_dataset.py` recomputes continuity from the telemetry whenever a `trial.json`
was written under a different threshold, so that mistake cannot be baked into the dataset.

---

## 2. Running a participant

No sittings, no schedule. A participant flies whenever they like, as many missions as they
like, and stops whenever they like.

1. Launch, enter the participant ID (a study code — never a name).
2. Free flight for as long as they want, to learn the aeroplane. Nothing is recorded.
3. Pick a mission. **The moment a mission starts, a trial folder opens and recording
   begins.** No further action is needed.
4. Quit whenever. Five missions today and five tomorrow is fine.

Each visit creates its own session folder. The visit number is counted from the folders
already on disk, so a participant who quits half-way is still numbered correctly.

> **Known:** the two `P002` visits on disk are *both* `S01`, from the old PlayerPrefs
> counter that only incremented on a fully completed session. Fixed going forward. For
> existing data, use `session_id` (which carries a timestamp) as the key, never `visit`.

---

## 3. What lands on disk

```
FlightSimData/experiment/<PID>/<SESSION>/
    session.json                participant, seeds, build, modality
    T01_L1/  T02_M3/  …         one folder per trial, ordinal + mission id
        telemetry.csv           50 Hz × 82 columns  (15,000 rows for a 300 s trial)
        events.csv              every marker, four clocks each
        metadata.json           mission spec + the pre-registered prediction
        performance.json        objective outcome
        trial.json              ← IS THIS TRIAL USABLE
        eeg/sync.json           clock offsets
        eeg/                    ← drop the EEG recording here
```

Each folder is self-contained by design: nothing is half-written and nothing depends on
anything else. That is right for recording and wrong for analysis, which is what §4 fixes.

---

## 4. Building the dataset

```bash
python3 tools/build_dataset.py                       # index + QC + validation
python3 tools/build_dataset.py --materialise LOW,MEDIUM,HIGH   # also the big per-class CSVs
```

Stdlib only. No pandas, no Unity — it runs on the analysis machine.

Writes to `FlightSimData/export/`:

| file | what it is |
|---|---|
| `index.csv` | **one row per trial** — every join key plus QC. The spine. |
| `participants.csv` | per person: visits, attempted, complete, per-class counts |
| `coverage.csv` | participant × mission matrix — who still owes you what |
| `events_all.csv` | every event from every trial, keys prepended |
| `VALIDATION.txt` | every problem found, named by trial |
| `manifest.json` | counts, join keys, the EEG contract |

**Why an index instead of one big CSV.** 40 participants × 42 missions × 15,000 rows × 82
columns ≈ **25 million rows, ~15 GB**. Writing per-class *and* per-mission *and*
per-participant copies triples that to 45 GB of duplicated numbers that immediately drift
apart. The index points at the telemetry; `--materialise` is there when you genuinely want
the concatenated files.

Synthetic ids (`TEST01`, `SHOTS`, `DESIGN`, …) are excluded by default. The mission battery
runs as `TEST01` and writes folders identical in shape to a real participant's — there are
86 of them on this machine. That is exactly how synthetic data ends up in a model.

---

## 5. Merging EEG

```bash
python3 tools/eeg_epochs.py --markers CUE_ONSET --pre 2 --post 8
```

Writes `export/epochs.csv`: for every event in every **usable** trial, the moment it
happened in all four clocks, plus the epoch window. Slice your recording on whichever clock
it shares with the amplifier.

**Clock rules**

| clock | use |
|---|---|
| `t_host` | **the master.** Monotonic, never jumps. Align on this. |
| `t_lsl` | best if EEG was recorded through LSL — one shared clock, no offset to measure |
| `t_unix` | only for aligning across two machines |
| `t_mission` | **plots only. Never alignment.** See §1. |

**Epoch zero for a failure mission is `CUE_ONSET`**, the first moment the pilot could
perceive the failure — not `TRIGGER_ARMED`, which is when the simulator scheduled it and
can be seconds earlier. Epoching on `TRIGGER_ARMED` gives you pre-stimulus data labelled as
post-stimulus.

**Join keys:** `participant_id`, `session_id`, `mission_id`, `trial_ordinal`.
**Labels:** `condition` (LOW/MEDIUM/HIGH) and `axis`. Keep `axis` — the 6 crosswind missions
measure psychomotor load, a different construct from the 36 cognitive ones, and pooling them
into one workload scale merges two things that are not the same thing.
**Always keep `trial_ordinal`** — it is your learning/fatigue regressor.

**Split leave-one-subject-out.** Within-subject splits leak and will inflate accuracy.

---

## 6. Open before collection

- **LSL is not running.** `lsl_available: false` in every session on disk, and `t_lsl` is
  `-1` in every row. Without it, the sim clock and the amplifier clock must be reconciled by
  hand for every participant. Get LSL working first — it is the single highest-value fix
  for EEG merging.
- **`audio_valid_for_experiment: false`** in the last session. Expected headless; verify it
  flips to `true` in the real build, because several MEDIUM missions use ATC callouts as
  their load driver.
