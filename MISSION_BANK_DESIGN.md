# MISSION BANK DESIGN — 42 missions, two axes

Supersedes the 12-mission table in `MissionLibrary.cs`'s header comment. The 12 are
retained unchanged as **variant 1**; nothing that was verified is discarded.

---

## 1. The decision, and why it is not simply "more missions"

The brief asks for 30+ scenarios across take-off, en-route and landing at three load
levels. The naive reading — 36 missions, one participant flies all of them — is not
runnable: 36 × 300 s is three hours of flying inside one EEG session, and fatigue would
dominate every contrast. The naive reading also has a subtler problem, raised in an
external design review and independently true: **a large scenario count is the classic
way an experiment turns into a demonstration.** More cells with one observation each buys
breadth at the cost of the thing that makes the study answerable.

So the count is delivered as a **BANK**, and the **SESSION** stays exactly the size that
was already verified:

| | |
|---|---|
| **Bank** | 42 missions on disk |
| **Session** | 12 missions — one variant index across the crossed 4 × 3 grid |
| **Session design** | unchanged from the verified 12-mission protocol |

The bank is what satisfies the brief and what protects the study; the session is what a
participant actually flies.

---

## 2. TWO AXES, not one scale

This is the most important structural change, and it came out of the design review.

### Axis 1 — COGNITIVE LOAD (the main experiment) · 36 missions
4 phases × 3 classes × 3 variants. Manual/psychomotor demand is **matched within each
phase row**, which is what licenses the claim that an EEG difference between LOW and
HIGH is cognitive and not muscular.

### Axis 2 — PSYCHOMOTOR-INTEGRATED DEMAND (crosswind) · 6 missions
Crosswind take-offs and landings at three crosswind levels.

**Crosswind is deliberately NOT placed on the Low/Medium/High cognitive scale.** It
raises manual demand by construction, so folding it into a motor-matched axis would
destroy the one property that makes that axis interpretable. A crosswind mission and a
working-memory mission can both be "hard" and be hard in ways that no single number
should be asked to represent.

Conditions under which the crosswind set is still defensible, all of which are
implemented:

1. **Control activity is measured, not assumed** — control-input rate, cumulative
   control activity, and control-input variability are logged as covariates, so
   "more EEG activity" can be separated from "more hand movement".
2. **The cognitive component is isolated as a discrete event.** The mission is not
   "fly a crosswind landing"; it is "fly the approach and decide, against a STATED
   crosswind limit, whether to continue or go around". That decision has an onset, a
   response, and a reaction time — it is epochable. The continuous crosswind segment
   is analysed as psychomotor-integrated demand; the decision is analysed as an
   event-related cognitive component.
3. **The conclusion is stated honestly.** If crosswind produces both more EEG activity
   and more control activity, the finding is *increased integrated psychomotor/
   cognitive demand* — not "crosswind increased cognitive workload".

For a novice, crosswind is substantially skill acquisition rather than workload. That
does not make it useless; it makes it a different kind of load, and it is labelled and
analysed as one.

---

## 3. The grid

Variant 1 is the existing, verified mission. Variants 2 and 3 are new, and each uses a
**different cognitive mechanism** — not a re-skin of the same trial.

| Phase | Class | V1 (existing) | V2 | V3 |
|---|---|---|---|---|
| **TAKE-OFF** | LOW | normal departure | normal departure, second stand/route | normal departure + routine readback |
| | MED | traffic + clearance *(visual search + procedure)* | runway change before line-up *(working memory, re-brief)* | conditional line-up clearance + frequency change *(prospective memory)* |
| | HIGH | amended clearance, blocked runway, conflict *(concurrency under time pressure)* | intersection departure, reduced runway, performance decision *(forward reasoning, irreversible commitment)* | low-visibility taxi, hot spot, conditional clearance *(monitoring + prospective memory)* |
| **CLIMB** | LOW | assigned climb | climb + one altitude amendment | departure heading + level-off |
| | MED | cabin door opens *(startle without danger)* | carburettor icing *(gradual, ambiguous cue — diagnosis)* | two re-clearances + squawk/frequency change *(WM + comms)* |
| | HIGH | alternator failure *(forward reasoning about a depleting resource)* | partial power loss after take-off, turn-back decision *(irreversible commitment)* | traffic + terrain alert while re-cleared *(prioritisation)* |
| **CRUISE** | LOW | level hold | level hold + one heading change | single-waypoint navigation |
| | MED | ATC re-clearances *(working memory)* | diversion to an alternate *(re-planning)* | degraded visibility + traffic search *(perception + monitoring)* |
| | HIGH | unreliable instruments *(self-consistent WRONG information)* | fuel imbalance + tank management on a clock *(resource reasoning)* | re-clearance + traffic + annunciation together *(concurrency)* |
| **APPROACH** | LOW | normal landing | normal landing, second initial position | normal landing + configuration call |
| | MED | weather + sidestep *(degraded perception + re-planning)* | late runway change *(re-planning under time pressure)* | traffic on the runway → go-around *(decision + procedure)* |
| | HIGH | engine failure *(irreversible commitment under a clock)* | flap failure on final, re-computed speed *(forward reasoning + configuration)* | go-around at low altitude then re-sequenced *(startle + re-planning, high stakes)* |

**Crosswind set (axis 2):** `XT1/XT2/XT3` take-off and `XL1/XL2/XL3` landing at light /
moderate / near-limit crosswind, each carrying an explicit limit and a continue-or-abort
decision.

---

## 4. Variant assignment, and why it is not a confound

Variant is a **between-subject nuisance factor**, which is acceptable *provided the
variants are interchangeable representations of the same cell*.

Assignment is by Latin square on the participant, **rotated per phase row**:

    variant(participant, phase) = (participantNumber + phaseIndex) % 3

Two properties follow, and both matter:

* **Within a phase row, all three classes share one variant.** So the LOW-MEDIUM-HIGH
  contrast — the contrast the whole study rests on — is always variant-matched. Variant
  can never masquerade as class.
* **Across rows, a participant meets different variants.** So variant is not perfectly
  nested in participant at the sample level, and a variant effect is partly estimable
  within subject.

### The exchangeability evidence the simulator must produce

Assuming exchangeability is not enough, and a non-significant ANOVA is weak evidence for
it. What is required is **effect sizes with confidence intervals for variant → each
workload-relevant variable, within each cell**. So the battery emits, per variant:

1. **Manipulation equivalence** — achieved task-demand parameters, realised event timing
   after jitter, exposure duration, and every scenario-specific parameter.
2. **Flight-performance equivalence** — altitude error, airspeed error, heading /
   cross-track error.
3. **Control-demand equivalence** — control-input rate, cumulative control activity,
   control-input variability.

`variant_equivalence.csv`, written by the mission battery, is that evidence.

### If the variants turn out NOT to be exchangeable

They are not averaged together and hoped away. In order of preference:
1. If the difference traces to an unintended scenario feature, fix the feature and
   re-run — the variants were not equivalent by construction, not by nature.
2. If the difference is real and stable, promote variant to a modelled fixed effect and
   report the class effect within variant.
3. If one variant is an outlier, retire it and document why. The bank is deliberately
   larger than the session needs precisely so a variant can be retired without
   collapsing the design.
