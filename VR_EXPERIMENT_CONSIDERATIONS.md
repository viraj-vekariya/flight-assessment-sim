# VR_EXPERIMENT_CONSIDERATIONS

What adding VR does to the science. Written as an audit, not a sales sheet — VR makes
this experiment better in some ways and creates real problems in others, and the problems
are stated first.

---

## 1. The central question

> **Does physical interaction itself create workload, and can the analysis separate
> cognitive workload from motor workload?**

**Yes to the first. Partly to the second.** Reaching for a trim wheel and turning it is
work. It consumes attention, it produces motor cortex activity, and it appears in EEG.
That is unavoidable in any interface that is not a thought-reading device — the desktop
version has the same problem with keyboards, in smaller and less realistic form.

What matters is not whether motor demand exists but whether it **varies with the
independent variable**. If HIGH missions require more physical action than LOW missions,
then motor activity is confounded with cognitive load, and a classifier could reach high
accuracy by detecting *arm movement* while appearing to detect *workload*.

### It does vary. Here is the honest accounting.

| Class | Typical control actions per 300 s trial |
|---|---|
| LOW | throttle set once or twice, occasional trim, flaps on approach (L4) |
| MEDIUM | above + flap and power changes, configuration changes, a decision |
| HIGH | above + failure checklists, fuel selector, load shed, alt static, carb heat |

**A HIGH mission involves more hand movement than a LOW mission.** This is not an artefact
that can be tuned away — it is a property of what high-workload flying *is*. An engine
failure genuinely requires more physical actions than straight-and-level cruise.

### What the design does about it

**1. Motor demand is bounded and small relative to the cognitive manipulation.** The
missions' differences are dominated by information rate, time pressure, decision
requirement, prospective memory and interruption — not by how many switches get thrown.
The Predicted Load Index carries `ManualControl` at weight **0.05 of 12 dimensions**;
throwing three extra switches does not move a mission between classes. Ten of the twelve
dimensions are cognitive.

**2. Every control action is timestamped**, so motor activity is a *measured covariate*,
not an unknown. `ControlGrab`, `ControlRelease`, `FlapSelected`, `TrimChanged`,
`ThrottleChanged`, `BrakeApplied`, `BrakeReleased` are in the marker stream, and
`control_held` is in every 50 Hz telemetry row. This is the single most important
mitigation, because it makes the confound analysable:

* **Epoch exclusion** — drop EEG windows in which a control was held. Costly but clean.
* **Covariate regression** — include actions-per-window or held-fraction in the model.
* **Matched-quiet-window analysis** — compare LOW and HIGH windows in which *no* control
  was touched. If the workload signal survives, it is not motor artefact. **This is the
  strongest available check and it should be run before any classification result is
  believed.**
* **Motor-only control condition** — if the study can afford it, a mission with HIGH
  physical action and LOW cognitive demand would bound the motor contribution directly.
  Not currently in the battery; worth adding if a reviewer presses.

**3. Trim removed a confound rather than adding one.** Before trim, every mission required
a sustained elevator force whose *magnitude differed by mission* through speed and
configuration. That was an uncontrolled, unlogged, continuously-varying motor load
correlated with mission class by accident. It is now a discrete, logged, optional action.
See `CONTROL_SENSITIVITY.md` §3.

**4. VR interaction is deliberately easy.** Position-based grabs, generous capture radii,
mild expo, no gestures, no wrist rotation, no precision targets. Every choice in
`CONTROL_SENSITIVITY.md` trades realism for *low and consistent* motor difficulty,
precisely so that motor demand stays small and does not vary with participant skill.

**Bottom line to state in the thesis:** motor demand covaries with mission class; it is
logged at 50 Hz and at event resolution; the matched-quiet-window analysis is the primary
defence; and no classification result should be reported without it.

---

## 2. Modality is a between-subject variable, not a setting

**VR and desktop data must never be pooled.** They differ in:

* visual field, stereopsis and depth perception
* head movement — VR participants move their heads, which is EMG and motion artefact in
  the EEG
* control interface — hand displacement versus keyboard
* physical fatigue over a 75-minute session
* presence and engagement, which affect subjective TLX independently of task demand

Every one of those plausibly shifts both EEG features and TLX ratings. Pooling would mean
a classifier could separate participants by *headset* while appearing to separate them by
workload.

The apparatus enforces the distinction as far as software can:

```json
"input_modality": "vr" | "desktop",
"vr_status": "<full VRRuntime status string>"
```

written to `session.json` and to every trial's `metadata.json`. `VRRuntime.InputModality`
is decided once at session start, so a session cannot silently change modality mid-way.

**Analysis rule: `input_modality` is a grouping variable. Filter on it before anything
else.** If both modalities are collected, either analyse them separately or model modality
explicitly as a between-subject factor — never concatenate.

**Recommendation: pick one modality for the main study.** A within-subject modality
comparison is a different experiment, needs its own counterbalancing, and doubles the
session count. Desktop is the safer choice for a first dataset because it is verified end
to end and has lower artefact risk; VR is the stronger choice for ecological validity if
the hardware and the EEG interaction (§3) are sorted out first.

---

## 3. EEG and a headset on the same head

**This is the largest practical risk in the VR path, and it is a hardware problem the
software cannot solve.**

* **Physical interference.** A Quest 2 head strap crosses exactly where frontal and
  parietal electrodes sit — Fz, Cz, Pz and the frontal sites that carry most workload
  signal. Pressure on an electrode degrades or destroys it.
* **Impedance drift.** Strap pressure changes contact impedance over a session, and it
  drifts differently under the strap than elsewhere, so the drift is spatially
  structured rather than global.
* **Motion artefact.** Head movement is unavoidable in VR and produces artefact
  correlated with looking behaviour — which is itself correlated with workload. That is a
  nastier confound than it first appears: the artefact and the signal share a cause.
* **EMG.** Neck and jaw muscle activity from supporting the headset contaminates
  high-frequency bands, including beta and gamma.

**Before committing to VR data collection, do a hardware pilot**: full cap, headset on,
sit for the full session duration, and check impedances at start and end. If frontal
channels degrade, VR and this EEG setup are incompatible in that combination and the
honest response is to run desktop.

Partial mitigations, none free: a cap with low-profile electrodes; routing the strap
above the electrode rows; a shorter session; more frequent impedance checks;
accelerometer-based artefact rejection.

---

## 4. Simulator sickness

Not currently measured. It should be.

Sickness raises subjective workload ratings, alters EEG (particularly alpha and theta),
and causes dropout — dropout that is **not random**, because susceptibility correlates
with the very individual differences a workload study cares about.

The design already reduces risk: the participant is seated, the cockpit is a stable frame
of reference, and `VRCameraRig` applies **no comfort effects** — no vignetting, no snap
turning, no artificial locomotion — because the aeroplane provides the only motion and
adding effects would alter the visual scene between participants.

**Recommendation if VR is used:** administer the Simulator Sickness Questionnaire before
and after each session, record it alongside TLX, and pre-register an exclusion rule.
Cheap, standard, and it turns a hidden confound into a covariate.

---

## 5. What VR is actually worth here

Stated plainly, because the cost above is real:

* **Ecological validity.** A cockpit at true scale with hands on the controls is much
  closer to the flight deck the results are meant to generalise to.
* **Spatial realism.** Instrument scanning involves real head and eye movement, which is
  the actual visual-attention task rather than a screen-shaped proxy.
* **Presence.** Time pressure and failures land harder, so the workload manipulation is
  likely stronger.
* **Face validity for aviation reviewers**, who will ask why a workload study used a
  keyboard.

**Against:** every item in §3 and §4, plus the fact that VR is not yet hardware-verified.

**The defensible position:** desktop is the verified apparatus and is scientifically sound
for the first dataset. VR is implemented, configured, and ready for hardware verification,
and is the stronger apparatus *if* the EEG-headset pilot passes. Do not run VR
participants before that pilot.

---

## 6. Things that would invalidate a session

* Running participants with `input_modality` mixed within a group.
* Any change to the values in `CONTROL_SENSITIVITY.md` mid-study.
* Skipping the control check — a dead controller produces a plausible-looking but
  meaningless dataset.
* Reporting VR results without stating that the frontal electrode question was tested.
* Pooling the `TEST01` battery output with participant data. It contains synthetic TLX
  values submitted by the harness and is labelled as such in every report.
