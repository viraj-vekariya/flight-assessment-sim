# COGNITIVE_LOAD_MODEL

How the twelve missions were assigned to LOW / MEDIUM / HIGH, why the assignment is
not "how dangerous does it look", and how the assignment can be proved wrong.

Implemented in `Assets/Scripts/WorkloadModel.cs`.

---

## 1. The problem this model exists to solve

The obvious way to grade flight scenarios is by drama: a normal cruise is easy, an
engine failure is hard. That grading is **wrong for a workload study**, for two
reasons that both matter here.

**First, danger and cognitive demand are different quantities.** A complete engine
failure is dangerous, but for a trained pilot it is also *one well-rehearsed schema*:
speed, field, drill, land. A busy visual circuit with three multi-part re-clearances
and a runway change is not dangerous at all, and it can occupy far more working
memory and force far more attention switching. If the missions are graded by danger,
the "HIGH" class ends up measuring **arousal and threat**, and a classifier trained
on it learns to detect fear, not load — while still reporting a good accuracy.

**Second, an EEG study is unusually exposed to confounds that track drama.** Startle
produces a large, fast autonomic and motor response. If every HIGH mission is also
the only one with violent stick inputs, jaw clenching and a racing heart, then
"workload decoding" is partly EMG decoding. Grading by *demand structure* rather than
by *drama* is the only way to keep that separable.

So the classification is built from an explicit, uniform demand model, and the
missions were then designed to hit target profiles — not the other way round.

---

## 2. The twelve dimensions

Every mission is scored **0–4** on each dimension (0 = essentially absent, 4 =
extreme). The scores are a task analysis, done once, before any data exists.

| Dimension | What it counts | Weight |
|---|---|---|
| **Mental demand** | information processing, diagnosis, computation | 0.16 |
| **Temporal demand** | urgency; how much time the pilot actually has | 0.13 |
| **Decision complexity** | number and interactivity of live alternatives | 0.12 |
| **Working memory** | items that must be held and are not yet discharged | 0.11 |
| **Attention switching** | forced channel changes per unit time | 0.11 |
| **Situation awareness** | effort to build and maintain the picture | 0.08 |
| **Procedural load** | drill / checklist execution | 0.07 |
| **Perception** | scanning and detection demand | 0.06 |
| **Uncertainty** | ambiguity of the cue; surprise | 0.06 |
| **Manual control** | psychomotor tracking difficulty | 0.05 |
| **Communication** | verbal / R-T load | 0.03 |
| **Error consequence** | stakes, i.e. performance pressure | 0.02 |

**Predicted Load Index (PLI)** = weighted sum, rescaled to 0–100.

### Why these dimensions, and why these weights

The dimension list is a merge of three sources, chosen so the model's output is
directly comparable to the instrument used to validate it:

* **NASA-TLX's own six sources** (Hart & Staveland 1988) appear directly — mental,
  physical (as manual control), temporal, effort (distributed across procedural and
  attention), performance pressure (error consequence), frustration (uncertainty).
  Keeping the TLX vocabulary inside the model means a mismatch between prediction and
  measurement is interpretable rather than mysterious.
* **Multiple Resource Theory** (Wickens 2008) is why *attention switching* and
  *working memory* are separate dimensions from raw perceptual and manual demand.
  Total demand is not one pool: two tasks competing for the same resource cost far
  more than two tasks spread across different ones. The missions that score high on
  attention switching are the ones that force verbal/working-memory work at the same
  moment as visual-manual tracking.
* **Startle and surprise research** (Landman et al. 2017; EASA 2018) is why
  *uncertainty* is separate from *temporal demand*. A surprising event is not
  automatically an urgent one, and the design exploits that dissociation explicitly
  (see M4 below).

The weights are a judgement, not a fitted result, and they encode two deliberate
choices:

* **Manual control is weighted lowest (0.05) of the substantive dimensions.** This is
  the confound guard described above. It also means the model cannot promote a
  mission to HIGH just for being physically busy.
* **Error consequence is weighted lowest overall (0.02).** Stakes drive arousal much
  more than they drive information processing. Weighting it heavily would smuggle the
  "danger = hard" assumption back in through the side door.

---

## 3. The resulting classification

| Class | n | PLI range | mean | manual-control range |
|---|---|---|---|---|
| LOW | 4 | 12.8 – 29.2 | 22.0 | 2 – 3 |
| MEDIUM | 4 | 49.5 – 57.5 | 52.4 | 2 – 3 |
| HIGH | 4 | 76.2 – 87.0 | 82.0 | 2 – 4 |

### The grid, not the list

The twelve are a **4 phases × 3 classes crossed grid**, not twelve free-standing
scenarios. That changes what the model has to deliver: it is not enough for the class
means to separate overall, they must separate **inside every phase row**, because that
is where the contrast is actually made.

| Phase | LOW | MEDIUM | HIGH | monotone? | manual (L/M/H) |
|---|---|---|---|---|---|
| Take-off | L1 · 28 | M1 · 53 | H1 · 87 | ✓ | 2 / 2 / 2 |
| Climb | L2 · 18 | M2 · 50 | H2 · 79 | ✓ | 2 / 3 / 2 |
| Cruise | L3 · 13 | M3 · 50 | H3 · 86 | ✓ | 2 / 2 / 3 |
| Approach | L4 · 29 | M4 · 58 | H4 · 76 | ✓ | 3 / 3 / 4 |

Four properties to re-check whenever the mission set changes (`DocGen` regenerates the
tables, so they cannot go stale):

1. **No overlap between classes, with a real gap.** Here: ~20 PLI points at both
   boundaries.
2. **Every phase row is monotone L < M < H.** All four are.
3. **Manual demand matched within each row** (spread ≤ 1 of 4). All four are. This is
   stronger than matching manual demand across the whole set, because the row is where
   the comparison happens.
4. **Mechanism diversity within a class.** No two MEDIUM missions load the same
   dimension hardest, and no two HIGH missions do either — otherwise "MEDIUM" would
   mean one specific kind of difficulty rather than a level of it.

### The requirements document's principle, checked

The project's requirements are explicit that the three levels "should not simply differ
by weather". Auditing the set against that:

| | uses weather | is weather the distinguishing factor? |
|---|---|---|
| M1 | yes (light rain) | no — the defining features are visible traffic and a two-part clearance |
| M4 | yes (turbulence, vis, gusts) | partly — but the late runway change is what raises it, and M4 is the *only* mission where weather is a primary driver |
| H1 | yes (heavy rain) | no — the defining features are an amended clearance, a blocked runway and a conflict |
| H3 | yes (in cloud) | no — cloud only removes the outside horizon that would otherwise resolve the instrument conflict |
| all others | no | — |

Weather appears in 4 of 12 missions and is the primary driver in 1. The classes are
separated by **working memory (M3, H1), visual search and procedure (M1), startle
(M2), degraded input plus re-planning (M4), forward reasoning about a resource (H2),
self-consistent wrong information (H3), and irreversible commitment under a clock (H4)**.

## 4. Where the design deliberately breaks the "danger = hard" intuition

Three cases exist specifically so the model is testable rather than tautological.

**M2 — cabin door opens on the climb-out — is MEDIUM, not HIGH.** It is the loudest,
most startling event in the set. FAA-H-8083-3C is explicit that a door opening in
flight "seldom if ever compromises the airplane's ability to fly" and that the danger
is the *pilot's reaction*. So it scores Uncertainty 3 and Attention switching 3, but
Error consequence 1 — and the correct response is very nearly to do nothing. If
participants' EEG and TLX put M4 up with the HIGH missions, we are measuring arousal.
If they put it in the middle, we are measuring demand. Either result is informative.

**L4 — a normal landing — is LOW, despite landing being the highest-demand phase of a
normal flight.** A landing in nil wind is a continuous, highly practised
perceptual-motor task with almost no diagnosis and no live decision. L4 therefore has
the *highest manual demand in the LOW class* and the lowest weighted-dimension scores.
It is the control for the movement/effort confound, and it is paired with H4 — the
same phase, adjacent manual demand, opposite cognitive demand — for exactly that comparison.

**H4 — engine failure — is the LOWEST-scoring HIGH mission, below H2 (an alternator
failure).** H2 is undramatic; it is also the highest decision-complexity mission in
the set, because the pilot must reason forward about a depleting resource whose
consumption they control while flying an approach. H4 is terrifying and has almost
nothing to diagnose. The model says H2 > H4. That is a falsifiable claim, and it is
one of the more interesting things this experiment can test.

---

## 5. Falsifying the model

The PLI is a **prediction**. It is pre-registered per mission in `metadata.json`
(`predicted_load_index`, `expected_nasa_tlx`) and written before any participant
flies. Three tests, in order of how much weight they should carry:

1. **Ordinal agreement with NASA-TLX.** Does measured RTLX increase monotonically
   across LOW → MEDIUM → HIGH within participants? A Friedman test on the three class
   means, then pairwise comparisons. This is the primary manipulation check — if the
   classes do not separate subjectively, they are not workload classes, whatever the
   EEG says.
2. **Behavioural convergence.** Secondary-task (probe) reaction time and miss rate
   should degrade with class, because a probe measures *spare capacity* and is largely
   independent of the self-report. Tracking error is a weaker measure here because it
   is contaminated by the task differing between missions.
3. **Rank correlation between PLI and measured RTLX across all twelve.** Spearman
   ρ over the twelve mission means. This tests the model at finer grain than the
   three-level classification does, including the deliberate inversions above.

**If the data disagree with the model, the data win.** The concrete revision rule,
fixed in advance so it is not chosen after seeing the results:

* If a mission's measured RTLX sits closer to another class's mean than to its own,
  **relabel that mission** and report both the original and revised classification.
* If the *ordering within* a class is wrong but the classes still separate, leave the
  labels and report the PLI's rank correlation as the (partial) failure it is.
* If the classes do not separate at all, the mission set is not a workload
  manipulation and no classifier trained on the labels should be reported as a
  workload classifier.

---

## 6. Known limitations of the model

* **The scores are one rater's task analysis.** They have not been checked for
  inter-rater reliability. A cheap and worthwhile improvement is to have two or three
  people with flight-training experience score the twelve independently and report
  agreement before data collection.
* **The weights are asserted, not estimated.** They are defensible from the
  literature but no dataset here fitted them. Once real TLX data exists, refitting the
  weights on it would be circular unless done on held-out missions — so the honest
  move is to keep the weights fixed and report how well the original prediction did.
* **Expertise is not in the model.** The whole "an emergency is a rehearsed schema"
  argument depends on training level, and a low-hours participant will not have the
  schema. This is captured in the protocol as a covariate (flight hours, sim
  experience) rather than in the PLI, and it is a genuine threat to the H2 > H4
  prediction specifically.
* **The model predicts a mission-level average.** Load is not constant inside a
  300 s mission — that is why the event markers exist. Within-mission analysis should
  use the markers and the `segment` column, not the mission's PLI.

---

## References

See `RESEARCH_REFERENCES.md` for the full list with links. The load model rests
principally on Hart & Staveland (1988), Hart (2006), Wickens (2008), Landman et al.
(2017), EASA (2018) and the FAA Airplane Flying Handbook (FAA-H-8083-3C) ch. 18.
