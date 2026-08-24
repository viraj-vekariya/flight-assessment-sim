# FINAL_EXPERIMENT_PROTOCOL

How to run one session, why each control is there, and the two design decisions that
had to be argued rather than assumed.

Read `FINAL_MISSION_DESIGN.md` for what the missions are and `COGNITIVE_LOAD_MODEL.md`
for what the classes mean.

---

## 1. Design at a glance

| | |
|---|---|
| Design | within-subject, **4 flight phases × 3 workload classes**, one trial per cell |
| Independent variable | cognitive workload class (LOW / MEDIUM / HIGH) |
| Controlled factor | flight phase (take-off, climb, cruise, approach) — **fully crossed** with class |
| Dependent variables | EEG band-power features; NASA-TLX (raw) + Bedford; task performance |
| Trial length | **300 s, identical for every mission** |
| In-task baseline | **first 60 s of every mission**, un-manipulated |
| Resting baselines | 120 s eyes-open + 120 s eyes-closed before; 120 s eyes-open after |
| Order | balanced (Williams) Latin square by participant, de-clumped by class |
| Session | ~75 min recorded, ~2 h door to door |

**The crossing is the headline design property.** Because every class appears exactly
once in every phase, a LOW-vs-HIGH difference cannot be a cruise-vs-landing difference.
The previous version of this mission set did not have that property — HIGH was mostly
approaches and LOW was mostly cruise — and no analysis could have separated phase from
workload afterwards.

---

## 2. The two decisions that had to be argued

### 2.1 Trial duration: 300 s, not the requirements' 10–15 minutes

The requirements document specifies "approximately 10–15 minutes from starting position
to take-off" for each take-off task. This project uses **300 s** for every mission. The
conflict is real and the resolution is deliberate.

**The case for 10–15 minutes** is *aviation realism*: a full gate-to-airborne sequence
at a real airport takes that long, and a taxi that short arguably under-represents the
navigation and communication load of a real departure.

**The case against it is arithmetic.** Twelve trials at 12 minutes is **144 minutes of
flying**, plus ~18 minutes of questionnaires, plus 6 minutes of resting baselines, plus
breaks — close to **three hours of continuous EEG recording**. Over that period:

* electrode gel dries and impedances drift, so signal quality is systematically worse
  at the end of the session than at the start;
* participant fatigue rises monotonically, and fatigue changes exactly the frontal
  theta and parietal alpha measures the study depends on;
* counterbalancing spreads that drift across conditions but does not remove it — it
  converts a bias into variance, and with a small sample that variance can easily
  exceed the workload effect being measured.

**The resolution.** Trial duration is a *hard experimental constraint* (it must be
constant across classes or time-on-task confounds the class effect), and total session
length is a *hard practical constraint* on EEG quality. 300 s satisfies both, gives
~150 artifact-free 2 s windows per trial, and sits in the range used by comparable
flight-simulator EEG work (5.5 min per condition in Zhou et al. 2025).

**What was preserved from the requirement.** The take-off missions really do start at a
parking stand and really do taxi a route with signage, checkpoints and a hold-short
line — the *task structure* the requirement asks for is intact. What was compressed is
the *distance*: the stand sits ~330 m from the runway (a small-field layout) rather
than the aeroplane being sped up, so taxi speed, steering, braking and the hold-short
decision all stay realistic.

**If a longer take-off task is wanted**, the defensible way to get it is a *separate
study* with fewer conditions per session — not by making one condition longer than the
others.

### 2.2 The in-task baseline differs between phases, and that is correct

The 60 s baseline at the head of a taxi mission is *taxiing*; at the head of a cruise
mission it is *straight-and-level*. That is deliberate:

* The class contrast is made **within a phase row** (L1↔M1↔H1 are all taxi missions),
  and within a row all three baselines are the same activity. That is exactly the
  matching a baseline-to-task contrast requires.
* Forcing every mission to begin straight-and-level would be impossible for a taxi
  mission, and forcing every mission to begin taxiing would be absurd for a cruise one.
* The **resting baselines** (§4.4) remain the cross-phase reference.

---

## 3. The controls, and what each protects

| Control | Protects against |
|---|---|
| **Phase crossed with class** | phase being mistaken for workload — the largest threat in the previous design |
| **Constant 300 s** | time-on-task, fatigue and vigilance decrement confounding class |
| **Manual demand matched within each phase row** (spread ≤ 1 of 4) | an EEG "workload" effect that is really muscle activity |
| **`ctrl_jerk` logged at 50 Hz** | gives an explicit motor-activity regressor to test that |
| **60 s un-manipulated baseline in every mission** | no per-mission reference to contrast against |
| **Seeded jitter (±6…±22 s) on every load event** | participants learning *when* the failure comes |
| **Balanced Latin square + class de-clumping** | order, learning and carry-over effects; runs of same-class trials |
| **No score shown before the questionnaire** | performance feedback contaminating the self-report |
| **Aircraft configuration reset every trial** | flap/spoiler selections carrying into the next mission |
| **Telemetry on the physics step** | sample rate varying with frame rate, i.e. with how busy the mission is |
| **One aircraft, one cockpit, one input method, one airfield** | any of these becoming an accidental independent variable |

---

## 4. Session run sheet

### 4.1 Before the participant arrives

- [ ] Assign a **study code** (e.g. `P07`). Never a name, email or date of birth. The
      simulator stores only the code; the code↔person mapping lives on paper or in a
      separate access-controlled file, never in this repository.
- [ ] Confirm ethics/consent approval covers what is actually recorded: EEG, flight
      telemetry, questionnaire responses.
- [ ] Disk: a full 12-trial session writes roughly 60–80 MB before EEG.
- [ ] If using LSL: install LSL4Unity, add the `LSL4UNITY` scripting define, and
      confirm the `FlightSimMarkers` stream appears in LabRecorder **before** the
      participant is capped.
- [ ] **Decide the input modality and do not change it.** `input_modality` is recorded
      as `desktop` or `vr` in `session.json` and every trial's `metadata.json`, and the
      two **must never be pooled** — they differ in visual field, head movement, control
      interface and fatigue. Pick one for the study, or treat modality as an explicit
      between-subject factor. See `VR_EXPERIMENT_CONSIDERATIONS.md` §2.
- [ ] **If running VR**, additionally:
      - the EEG-under-headset pilot has been done and frontal impedances survived a
        full session (`VR_EXPERIMENT_CONSIDERATIONS.md` §3) — this is a blocker, not a
        nicety;
      - `XRSetup.Verify` reports the OpenXR loader assigned (Windows only — macOS has
        no OpenXR runtime and will always run desktop);
      - the Simulator Sickness Questionnaire is printed and ready for pre and post.

### 4.2 Intake (~15 min, not recorded)

Record against the study code — these are covariates the analysis needs and cannot
recover later:

| Field | Why |
|---|---|
| total flight hours; hours in type; licence | expertise is the biggest moderator of emergency workload, and the H2 > H4 prediction depends on it |
| simulator / video-game hours per week | familiarity with the control interface |
| sleep last night; hours since waking | fatigue lowers the ceiling on every manipulation |
| caffeine / nicotine in the last 2 h | affects EEG spectra directly |
| vision normal or corrected | the task is visual |
| handedness | relevant to lateralised measures |
| time of day | circadian effects on alpha |

### 4.3 Familiarisation (~10 min, not recorded)

**FREE FLIGHT** from the main menu. The participant must be able to taxi, take off,
hold an altitude and heading, and land **before** any recorded trial. Then walk through
the systems keys (`H` `J` `K` `L`) and the acknowledge key (`SPACE`) on the ground, and
show them the cockpit interaction.

**Cover elevator trim explicitly.** It is the one control that reduces workload, and a
participant who does not know it exists will hold a sustained elevator force through
every trial — reintroducing exactly the confound trim was added to remove. Trim is
`[` / `]` on the keyboard and the wheel on the centre console in the cockpit.

**VR: run CONTROL CHECK first, before free flight.** Seat the participant, recentre
(both grips + both triggers), and have them touch every control in turn while watching
the readout — yoke, throttle, trim wheel, flap lever, brake, and the four switches. It
takes forty seconds and it catches a dead controller, a mis-tracked hand or an
unreachable control *before* an hour of EEG is spent rather than after.
`VR_CALIBRATION.md` has the full procedure.

This is not politeness. If a participant is still learning the controls in trial 1,
trial 1 measures interface learning — and because order is counterbalanced, that noise
lands on a different condition for every participant.

### 4.4 EEG setup (~20 min)

Cap, gel, impedance check. With the amplifier streaming and LabRecorder recording,
press **RUN FULL SESSION**.

### 4.5 The recorded session (~75 min)

Driven automatically:

1. **Rest, eyes open — 120 s.** Aircraft parked and frozen, fixation cross on screen.
   *Primary normalisation reference* — eyes-open because every task condition is
   eyes-open, and the eyes-open/closed alpha difference dwarfs any workload effect.
2. **Rest, eyes closed — 120 s.** *Quality control only.* Alpha should rise sharply on
   eye closure; if it does not, the posterior electrodes are not working and you have
   found out in 2 minutes instead of after an hour. Also gives the individual alpha
   peak frequency. **Never use it for workload normalisation.**
3. **Twelve trials.** Each: 60 s baseline → task → NASA-TLX + Bedford.
4. **Rest, eyes open — 120 s.** Lets session drift be measured rather than assumed away.

Offer a break every four trials. **Log whether a break was taken and how long** —
unequal breaks are a nuisance variable worth recording rather than pretending away.

`Esc` ends a rest block early (participant discomfort); the actual duration is in the
markers, so a short block is never silent.

### 4.6 Immediately after

- [ ] Stop LabRecorder. Copy the `.xdf` into each trial's `eeg/` folder, or keep one
      session-level recording and say so in `eeg_notes.txt` — either works, be consistent.
- [ ] Record end-of-session impedances and any channels that went bad.
- [ ] Note anything unusual: comments, interruptions, an aborted mission, sim sickness.

---

## 5. Data out

```
FlightSimData/experiment/<CODE>/<SESSION>/
    session.json                 order, seed, build info, input_modality, vr_status
    T01_L1/ T02_M3/ ...          one folder per trial, in presentation order
        telemetry.csv            50 Hz, 77 columns (incl. trim, brake, and
                                 which control is held)
        events.csv               markers, four clocks each
        nasa_tlx.json            the participant's actual response
        metadata.json            spec + pre-registered predictions
        performance.json         objective outcome metrics
        eeg/ sync.json README.txt
```

Columns: `FINAL_TELEMETRY_SCHEMA.md`. Alignment: `FINAL_EEG_INTEGRATION.md`.

---

## 6. Threats to validity

| Threat | Status |
|---|---|
| Phase confounded with class | **Designed out** — fully crossed |
| Time-on-task confounded with class | **Controlled** — constant 300 s |
| Order / learning / carry-over | **Controlled** — balanced Latin square + de-clumping |
| Predictable failure onset | **Controlled** — seeded jitter on every load event |
| Configuration carry-over between trials | **Controlled** — reset every trial |
| Sample rate varying with mission busyness | **Controlled** — telemetry on the physics step |
| Score feedback biasing self-report | **Controlled** — no score before TLX |
| Motor artifact driving "workload" | **Partly controlled** — manual demand matched within each row, `ctrl_jerk` logged as a regressor, L4/H4 matched pair. Not eliminated. |
| Baseline mismatched to task | **Controlled within a phase row**, plus three resting baselines |
| Expertise dominating the effect | **Measured, not controlled** — flight hours as a covariate. The H2 > H4 prediction is the most vulnerable claim in the study. |
| Fatigue over 75 minutes | **Partly controlled** — counterbalancing spreads it; the closing eyes-open rest lets drift be measured |
| Simulator sickness | **Not controlled** — no motion platform helps, but nausea alters EEG. Ask, log, be prepared to exclude. |
| Cross-session drift | **Not controlled** — do not pool sessions without normalising within session |
| VR vs flatscreen | **Excluded by design** — see §8 |
| Single-pilot only | **Documented** — findings do not transfer to multi-crew without argument |

---

## 7. Sample size

No power analysis has been run, because there is no pilot data to estimate an effect
size from. Do this rather than guessing:

1. Run **3–5 pilot participants** through the full session.
2. Compute the within-subject effect size for RTLX across the three classes.
3. Size the main sample from that.

For **subject-independent** EEG classification the binding constraint is the number of
*participants*, not windows. Ten participants gives ten LOSO folds and wide confidence
intervals; report them as such rather than quoting one accuracy number.

---

## 8. Things that must never be done to this data

* Do not report `expected_nasa_tlx` as results — pre-registered predictions, labelled
  as such in every `metadata.json`.
* Do not analyse the `TEST01` participant folder — synthetic harness output.
* Do not split adjacent EEG windows from one participant across train and test
  (`FINAL_EEG_INTEGRATION.md` §5).
* Do not run this study in VR, and never pool VR with flatscreen data. An EEG cap under
  a Quest 2 strap sits on exactly the occipital and parietal sites the alpha measure
  depends on, and VR changes measured workload independently of the manipulation.
