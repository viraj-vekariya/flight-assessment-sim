# DESIGN_CRITIQUE

A deliberately hostile review of this experiment, written before any data exists.
Each question is answered honestly: **controlled**, **partly controlled**, or
**open** — and where something is open, it says so rather than dressing it up.

If you are examining this project, read this document second, after
`MISSION_DESIGN.md`. It is where the weaknesses are, and they are here on purpose.

---

### Could a class difference actually be a flight-phase difference?

**Designed out — and this was a real defect in the previous version of this set.**

The earlier twelve missions were 6 cruise, 1 climb, 4 approach and 1 circuit, with HIGH
concentrated in the approaches and LOW in the cruises. Flight phase was therefore
confounded with workload class: a LOW-vs-HIGH difference could have been a
cruise-vs-landing difference in psychomotor demand, visual flow and intrinsic phase
workload, and nothing in the analysis could have separated them.

The set is now a **fully crossed 4 phases × 3 classes grid**. Every class appears
exactly once in every phase; every mission has a phase-matched LOW baseline; and manual
demand is matched *within* each phase row, which is where the contrast is made.

Residual: with one trial per cell there is no within-cell replication, so a
phase × class interaction cannot be estimated from a single session. Model phase as a
factor, not as noise.

### Is LOW genuinely low cognitive workload — or is it just boring?

**Partly controlled, and this is a real risk.**

The inverted-U is the trap: an empty task produces vigilance decrement, and both
overload and *underload* degrade performance, so a mission with nothing in it would
sit at the wrong end of the curve and its EEG would not be "low workload" — it would
be disengagement, which looks different again.

The LOW missions therefore all keep the pilot in a continuous, closed control loop.
L3 has the **tightest** tolerances in the whole set (±60 m / ±10°) precisely so it
cannot drift into idling. L2 adds task turnover, L4 adds procedural work, L3 adds
real psychomotor demand.

Still open: 300 s of hand-flown straight-and-level is monotonous, and it is the
condition most likely to show a time-on-task effect within the trial. **Check this
before trusting the LOW class**: split each LOW trial into early and late halves and
compare. If the second half diverges, the class is measuring boredom in its tail.

### Does the taxi phase actually carry cognitive load, or is it filler?

**Plausible but untested.** The argument for including it: taxiing is surface
NAVIGATION under ATC instruction with a hard procedural gate (the hold-short line), so
it loads working memory, attention switching and procedural compliance while costing
almost nothing psychomotor — which is precisely the demand profile this study wants to
isolate from stick-and-rudder effort. H1's blocked-runway hold is the clearest case in
the whole set of high cognitive load with the aeroplane *stationary*.

The argument against: participants who are not pilots may not experience a hold-short
line as a real constraint, in which case the load is much lower than modelled.

**Check it directly**: H1's PLI is the highest in the set (87). If its measured RTLX is
not also near the top, the taxi manipulation is not landing and the take-off row should
be re-scored.

### Is MEDIUM genuinely between LOW and HIGH, or an arbitrary middle?

**Open — this is the weakest of the three labels, and the one most likely to fail.**

The Predicted Load Index separates the classes cleanly (LOW 13–29, MEDIUM 47–52,
HIGH 76–90) with no overlap. But PLI is a prediction from asserted weights, not a
measurement, and three-level workload manipulations commonly collapse to two: the
middle condition gets absorbed by whichever end it sits nearer.

**Report the confusion matrix, always.** If MEDIUM is being split between LOW and
HIGH, that is the single most useful thing this study can find out, and a headline
accuracy number hides it completely.

### Is HIGH actually cognitively demanding, or just dangerous?

**Controlled by design, and testable.**

This was the central design constraint. HIGH is built from *interaction between
concurrent tasks*, ambiguity and working-memory turnover — never from "everything
fails at once". Two deliberate structural choices make the claim falsifiable rather
than assumed:

* **H2 (an alternator failure) scores above H4 (an engine failure).** H2 is
  undramatic and has the highest decision complexity in the set — forward reasoning
  about a depleting resource whose consumption the pilot controls, while flying an
  approach. H4 is terrifying and has almost nothing to diagnose. If the measures rank
  H4 above H2, they are tracking threat, not demand.
* **M2 (cabin door open) is MEDIUM, not HIGH**, despite being the most startling
  event in the set — because the handbook is explicit that the hazard is the pilot's
  reaction, not the aeroplane. If M2's EEG and TLX land up with the HIGH missions, we
  are measuring arousal.

### Could an experienced pilot find an emergency *easier* than a medium-complexity task?

**Yes — and the design predicts exactly that, which is why it is fragile.**

The H2 > H4 prediction depends on the participant having a rehearsed engine-failure
schema. A low-hours student does not. With a homogeneous student sample this specific
prediction may simply be wrong, and it will be wrong for an interesting reason.

Flight hours and simulator experience are collected as covariates. **Do not average
this prediction across a mixed-experience sample** — plot it against hours.

### Are there excessive physical-control confounds?

**Partly controlled. The most important residual threat in the study.**

Manual control is scored 2–3 in LOW/MEDIUM and 3–4 in HIGH, and carries the lowest
weight in the model. Three specific defences:

1. `ctrl_jerk` is logged at 50 Hz as an explicit **motor-artifact regressor**.
2. **L4 and H4 are a matched pair** (same phase) — comparable psychomotor demand, opposite
   cognitive demand. If the indices separate them, the separation is cognitive.
3. M4 (turbulence) is the mission most exposed to this, and its independent,
   motor-light measure is probe reaction time rather than tracking error.

Not eliminated. HIGH missions do involve more control activity. **Report the
EEG effect with and without `ctrl_jerk` as a covariate**, and if it does not survive,
say so.

**Updated after the physical-cockpit and VR work.** Three things changed here, two of
them improvements:

4. **Every discrete control action is now timestamped**, not merely inferred from
   `ctrl_jerk`. `ControlGrab`, `ControlRelease`, `FlapSelected`, `TrimChanged`,
   `ThrottleChanged`, `BrakeApplied` and `BrakeReleased` are markers on the EEG clock,
   and `control_held` names the held control in every 50 Hz telemetry row. This enables
   the **matched-quiet-window analysis**: compare LOW and HIGH windows in which *no*
   control was touched at all. If the workload signal survives that, it is not motor
   artefact. This is now the primary defence and should be run before any
   classification result is believed.
5. **Elevator trim removed a confound rather than adding one.** Before trim, every
   mission required a sustained elevator force whose magnitude differed by mission
   through speed and configuration — an uncontrolled, unlogged motor load that
   correlated with mission class by accident. It is now a discrete, logged, optional
   action. Sensitivity check: one full point of manual demand is worth 1.25 PLI points,
   and a uniform shift cannot change a difference, so the class gaps (LOW→MED 20.3,
   MED→HIGH 18.7) are unchanged and **the published PLI values stand**. See
   `CONTROL_SENSITIVITY.md` §3.
6. **VR raises this threat, and the mitigations are deliberate.** Position-based grabs,
   generous capture radii, a mild 1.25 expo and no gesture recognition all keep motor
   difficulty low and consistent rather than realistic. Wrist rotation was explicitly
   rejected as a control law because it couples aircraft input to forearm posture and
   drifts with fatigue over a 300 s trial. See `VR_EXPERIMENT_CONSIDERATIONS.md` §1 for
   the full accounting, including the honest admission that HIGH missions require more
   hand movement than LOW ones and always will.

### Are the missions too different in duration?

**Controlled.** Every mission is 300 s nominal with a 60 s in-task baseline.
Landing missions can end at touchdown; the geometry puts that around t ≈ 235 s, and
300 s is a hard ceiling. **Report the realised durations** — do not assume they were
equal.

### Could EEG differences come from muscle movement?

**Partly controlled** — see the physical-control answer. Additionally: the
eyes-closed resting block gives a zero-movement artifact floor, and the frozen
aircraft during rest blocks means those segments have guaranteed-zero control input.

Note the asymmetry that matters most: if HIGH trials lose more data to artifact
rejection than LOW trials, **that alone can create a spurious class difference**.
Report rejected-epoch counts per condition.

**If VR is used this becomes substantially worse**, and for a reason the software cannot
fix: a headset strap crosses the frontal and central electrode sites that carry most of
the workload signal, head movement is unavoidable and correlates with looking behaviour
which itself correlates with workload, and neck EMG from supporting the headset
contaminates the high-frequency bands. Run a hardware pilot — full cap, headset on, full
session duration, impedances checked at both ends — **before** committing to VR data
collection. `VR_EXPERIMENT_CONSIDERATIONS.md` §3.

### Could learning effects distort the results?

**Partly controlled.** A balanced Latin square puts every mission in every ordinal
position equally often across a cycle of 12 participants, and a de-clumping pass
prevents runs of three same-class trials. Familiarisation is mandatory before any
recorded trial.

Not eliminated: 12 trials is a lot of learning in one session, and with N in the tens
the square is only balanced *in expectation*. **Include trial ordinal as a covariate**
and check whether the class effect changes across the session.

### Could participants predict when the failure will occur?

**Controlled.** Every load-inducing event carries seeded jitter (±6 to ±22 s). A
participant who repeats a mission gets a different schedule; the realised times are in
each trial's `metadata.json`, so reproducibility is preserved.

Residual: a participant can still learn *that* something will happen in a given
mission. Nothing in a within-subject design fixes that. It argues against running the
same participant twice, and for treating a second session as a different dataset.

### Are there sufficient baseline periods?

**Controlled, and this is a genuine strength.** Three references, chosen because no
single one is sufficient: 120 s eyes-open rest (primary normalisation — matches the
ocular state of every task condition), 120 s eyes-closed (quality control and
individual alpha peak only, *never* normalisation), and a 60 s in-task baseline at
the head of every mission that is the **identical straight-and-level task in all
twelve**. The eyes-open rest repeats at session end so drift is measurable.

### Could simulator sickness distort the EEG?

**Open. Not controlled.** There is no motion platform and the cockpit view is fixed,
which helps, but nausea alters EEG and heart rate and there is no mitigation here.
Ask about it, log it, be willing to exclude a participant, and record whether the
session was aborted.

**VR update.** The design already minimises the risk — seated, stable cockpit frame of
reference, and `VRCameraRig` applies **no comfort effects** (no vignetting, no snap
turning) because those would alter the visual scene between participants. But sickness is
still not *measured*. If VR is used, administer the Simulator Sickness Questionnaire
before and after each session, record it alongside TLX, and pre-register an exclusion
rule. Susceptibility is not randomly distributed, so sickness-driven dropout is not
random dropout.

### Is the workload classification experimentally testable?

**Yes, and the revision rule is fixed in advance** (`COGNITIVE_LOAD_MODEL.md` §5):
manipulation check on RTLX, behavioural convergence on probe performance, and
Spearman ρ between PLI and measured RTLX across the twelve. If a mission's measured
RTLX sits closer to another class's mean than its own, it gets relabelled and **both**
classifications are reported.

---

## Threats this design does not address at all

Listed separately so they are not buried among the mitigated ones.

* **No pilot has flown it.** Every claim about workload is a prediction.
* **No EEG hardware has been connected.** The LSL path is written and compiles;
  it has never been tested against an amplifier.
* **No power analysis.** There is no pilot data to estimate an effect size from.
  Run 3–5 participants first, then size the sample from the observed within-subject
  effect. For subject-independent classification the binding constraint is the number
  of *participants*, not windows.
* **The demand scores are one rater's task analysis** with no inter-rater
  reliability. Two or three independent scorers before data collection would cost
  almost nothing and materially strengthen the model.
* **Single-pilot only.** Nothing here transfers to multi-crew workload without an
  argument.
* **Traffic is a scripted prop.** Visible aircraft now exist (M1, H1), so the
  visual-search component of a conflict is simulated — a gap the previous version had.
  But they fly fixed paths, cannot collide, and do not react to the player. Conflict is
  scripted geometry, not emergent. That is a deliberate trade: emergent conflict would
  make the trigger time vary between participants and destroy the event-locked epoching
  the design depends on.
* **No voice R/T**, so "readback" measures acceptance latency, not readback accuracy.
* **VR is excluded**, not solved: an EEG cap under a Quest 2 strap fouls the exact
  occipital and parietal sites the alpha measure lives at. Run flatscreen; never pool
  VR and flatscreen data.

---

## The three things most likely to go wrong

If you only guard against three, make them these:

1. **MEDIUM collapses** into LOW and HIGH. Watch the confusion matrix, not the
   accuracy.
2. **The EEG effect is motor activity.** Regress on `ctrl_jerk`; check the L3/H4
   matched pair; report artifact rejection per condition.
3. **The classifier learns the participant, not the workload.** Split by participant
   (leave-one-subject-out), never by window. `analysis/build_dataset.py` emits the
   group keys and refuses to make this easy to get wrong.
