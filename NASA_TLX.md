# NASA_TLX

The subjective workload instrument used in this study: what is administered, how it
is scored, what was changed and why, and what it can and cannot support.

Implemented in `WorkloadRating.cs` (data), `ResultsUI.cs` (the form),
`WorkloadLog.cs` and `ExperimentLogger.WriteTlx()` (storage).

---

## 1. What is administered

Immediately after every trial, before the participant sees anything resembling a
score:

**NASA-TLX, raw (RTLX)** — six subscales, each a 21-point scale from 0 to 100 in
steps of 5:

| Subscale | Anchors | Question |
|---|---|---|
| Mental demand | Very low → Very high | How much mental and perceptual activity was required? |
| Physical demand | Very low → Very high | How much physical activity was required? |
| Temporal demand | Very low → Very high | How much time pressure did you feel? |
| Performance | **Perfect → Failure** | How successful were you in doing what you were asked to do? |
| Effort | Very low → Very high | How hard did you have to work? |
| Frustration | Very low → Very high | How insecure, discouraged, irritated, stressed or annoyed were you? |

`RTLX` = the arithmetic mean of the six.

**Bedford Workload Scale** — a single 1–10 hierarchical rating, from "workload
insignificant" to "task abandoned". Kept alongside TLX because it is a decision-tree
rating rather than a set of magnitude judgements, so the two fail in different ways;
agreement between them is evidence, disagreement is a flag.

---

## 2. Raw TLX, not weighted TLX — and why

The original NASA-TLX adds a weighting stage: fifteen pairwise comparisons between
the six subscales, used to compute participant-specific weights. Dropping that stage
gives **Raw TLX (RTLX)**, the arithmetic mean.

RTLX is used here, deliberately:

* It is the most common modification in applied work, and correlates highly with the
  weighted score.
* The pairwise stage costs 1–2 minutes **per trial**. Over twelve trials that is
  15–25 minutes of additional non-flying time inside an EEG session, during which the
  cap dries out and the participant fatigues. That cost is real and it degrades the
  primary measure.
* Comparisons of raw against weighted sensitivity are mixed — some studies find raw
  more sensitive, some find no difference, some find it less. There is no clear
  evidence that weighting would buy accuracy here.

**Say "RTLX" in the write-up, not "NASA-TLX".** They are different instruments and
conflating them makes the numbers non-comparable with papers that did the weighting.

---

## 3. Two corrections that were made to the earlier implementation

Both change the numbers the instrument produces, so they are recorded here rather
than buried in a commit.

### 3.1 The scale is 21-point, not continuous

The NASA TLX manual's scales have 21 tick marks: 0–100 in steps of 5. The earlier
version used a continuous slider, which silently substitutes a different instrument
and produces values that are not comparable with the published literature. Responses
now snap to the nearest 5.

### 3.2 There is no longer a default answer

Every subscale used to start at 50. A pre-set midpoint is an anchor: participants
adjust away from it rather than answering from scratch, which pulls responses toward
the centre and shrinks exactly the between-condition differences the study exists to
detect. It also makes an untouched scale indistinguishable from a genuine "50".

Now: each scale starts unanswered and displays "—", and **SUBMIT is disabled until
all six subscales have been moved and a Bedford statement has been chosen.** An
un-given answer can no longer be recorded as a real one.

---

## 4. Administration rules that protect the data

* **Blind to performance.** The questionnaire appears before any metrics or outcome
  summary. Showing a score first contaminates the Performance and Frustration
  subscales directly, and indirectly all six.
* **Read the definitions aloud before the first trial.** TLX subscale definitions
  are not self-evident — in particular, participants routinely misread Performance.
  Give the standard definitions once, at familiarisation, and make sure the
  participant understands the Performance direction.
* **Performance is reverse-anchored on purpose.** 0 = perfect, 100 = failure, so a
  higher mark means the participant judged their own performance *worse*. It is
  averaged in that direction, per raw-TLX convention. The stored JSON says so
  explicitly in `note_performance_anchor`, because getting this backwards in analysis
  is a common and silent error.
* **Do not prompt or comment** on the ratings. The experimenter's opinion of how the
  trial went is exactly the contamination the blind ordering is protecting against.
* **Time on the form is recorded** and shown, so unusually fast (careless) or
  unusually slow (confused) responses can be identified afterwards.

---

## 5. Where the responses are stored

Per trial, in the trial's own folder:

```
<TRIAL>/nasa_tlx.json
{
  "participant_id": "P07", "session_id": "...", "trial_ordinal": 5,
  "mission_id": "M3", "condition": "MEDIUM",
  "submitted_utc": "...", "trial_duration_s": 300.0,
  "instrument": "NASA-TLX, raw/unweighted (RTLX). 21-point scale, 0-100 in steps of 5, ...",
  "nasa_tlx": { "mental_demand": 65, ..., "rtlx": 48.33 },
  "note_performance_anchor": "...",
  "bedford": 5
}
```

Plus `TLX_START` and `TLX_SUBMIT` markers in that trial's `events.csv`, on the same
clock as the EEG, so the questionnaire period can be excluded from task epochs.

Plus an append-only cross-trial CSV at
`FlightSimData/<PID>/Workload/<PID>_workload_log.csv` for quick inspection.

---

## 6. What TLX can and cannot support here

**It can:**
* Serve as the **manipulation check**. If RTLX does not increase across LOW →
  MEDIUM → HIGH within participants, the missions are not a workload manipulation and
  nothing downstream should be described as workload classification.
* Provide a per-trial continuous target for regression, alongside the class label.
* Show *which* subscale drives a difference — the mental/temporal split is what
  separates H2 (decision complexity) from H4 (time pressure), and the subscale
  profile is a stronger test of the design than RTLX alone.

**It cannot:**
* Serve as ground truth for the EEG. It is a retrospective, subjective, whole-trial
  judgement; the EEG is a continuous physiological measure. They are correlated
  constructs, not the same construct measured twice. Treating RTLX as a label for
  2-second EEG windows assumes load was constant across 300 s, which the event
  markers exist precisely to deny.
* Resolve *within-trial* load. Use the markers and the `segment` column for that.
* Be compared with weighted-TLX numbers from other papers without saying so.

---

## 7. Never fabricated

The simulator collects real participant responses and stores nothing else as a
measurement. Two things in this repository could be mistaken for TLX data and are
not:

* `expected_nasa_tlx` in each `metadata.json` — a **pre-registered prediction** from
  the mission design, written before data collection, carrying an explicit note in
  the file that it must never be substituted for a measured score.
* The `TEST01` participant folder — written by the automated test harness with
  synthetic values, purely to exercise the write path. It is labelled in the test
  report and must never be analysed.

---

## References

Hart & Staveland (1988); Hart (2006), *NASA-TLX; 20 Years Later*; the NASA TLX manual
(21-point scales); Roscoe & Ellis for the Bedford scale. Full list with links in
`RESEARCH_REFERENCES.md`.
