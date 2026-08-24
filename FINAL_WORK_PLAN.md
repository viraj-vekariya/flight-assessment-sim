# FINAL WORK PLAN — phases, deliverables, checkpoints

Living document. Every phase ends with a **checkpoint that is a measurement, not an
opinion**: a battery that passes, a report file on disk, or a build that exists. A phase
is not "done" because the code compiles.

Status key:  ✅ done and verified · 🔄 in progress · ⬜ not started

---

## PHASE 0 — AUDIT ✅

Inspected: 18 project folders under `~/Desktop/BTP Project/`, 82 runtime scripts +
4 editor tools (17.4k lines) in `FlightAssessmentSim - Final`, all 15 FINAL_* / VR_* /
COGNITIVE_* documents, the mission library, scenario engine, cockpit rig, wind/physics,
logging, EEG sync and session layers.

**Verdict: `FlightAssessmentSim - Final` is the canonical project and is genuinely
strong.** It is a merge of the VR project's research apparatus with the Extra project's
photoreal cockpit, and the merge is documented file by file in `MERGE_ARCHITECTURE.md`.
It compiles clean, the 12-mission battery passes 12/12, and the 50-check control battery
passes. It is NOT a prototype and must not be rebuilt.

**Decision: inspect → consolidate → improve → test. No rewrite, no "Phase 5".**

Baseline captured as a git commit (`git init` in the project root, first commit
`92008e6`) so every later change is reversible and attributable.

---

## PHASE 1 — FLIGHT MODEL: THE MISSING AIR MASS ✅

**Why first.** The brief's highest priority is research validity, and the audit found the
simulator could not stage the single most-cited manipulation in the take-off and landing
literature: there was **no wind**. `MissionDefinition.CrosswindMs` was declared, written
by nothing, and read by nothing. Turbulence existed, but turbulence is a zero-mean
wobble — it does not change where the aeroplane goes, so it does not change the pilot's
task. Every landing mission was flown in dead calm, which is why runway centreline
deviation was a nearly free metric.

**Delivered**
- `WindModel.cs` — steady wind (meteorological from-direction), power-law surface shear,
  optional discrete shear layer, seeded band-limited gusts. Zero wind returns exactly
  `Vector3.zero`, so a calm mission is bit-identical to the pre-wind flight model.
- `CessnaPhysics` — aerodynamics now use AIR-relative velocity; ground handling stays
  ground-relative. New readouts: `GroundSpeedMs`, `WindVel`, `DriftAngleDeg`,
  `SideslipDeg`. New `finVolume` term giving real directional stability.
- Telemetry +5 columns; wind block in `metadata.json`; `SetWind(crosswind, headwind)`
  so missions are authored in the terms the workload argument is actually about.
- `WindTestHarness.cs` (`-windtest`) — 46 checks against closed-form predictions.

**Checkpoint: `wind_test_report.txt` — 46 checks, 0 problems.** ✅

**Four defects the new battery exposed** (none would have been caught by the existing
batteries; details in `FINAL_CHANGE_REPORT.md`):
1. `CrosswindMs` declared but inert — no crosswind ever existed.
2. Turbulence keyed to `UnityEngine.Time.time`, i.e. seconds since the editor launched.
   Seeded but **not reproducible**: two participants with the same seed got different
   turbulence. Now keyed to the mission clock.
3. `CockpitInteraction.ClearOverrides()` ran unconditionally every frame at execution
   order −50, wiping any control override written by a later script. Invisible only
   because the GLB cockpit disables that component ~14 s in; **fatal in the fallback
   path and for any external control-hardware layer**.
4. `FlightTest` auto-spawned on ANY `-batchmode` run unless the command line named one
   of seven opt-out flags, and disabled `AircraftController` behind everyone's back.
   `-windtest` was not on the list, so the entire first wind battery ran with a second
   harness flying the aeroplane. Now opt-in, and `SimDriver` enforces one driver at a
   time so the class of bug cannot recur.

---

## PHASE 2 — REGRESSION ✅

The flight model changed, so every claim resting on it was re-established rather than
assumed. Same discipline `FINAL_TEST_REPORT.md` applied when trim and brakes were added.

- ✅ 12-mission battery re-run — **12/12, 0 problems**
- ✅ 50-check control battery re-run — **0 problems**, and the frame-rate independence
  check still reads 0.614 / 0.614 / 0.614 at 30 / 60 / 90 FPS against an analytic 0.614.

**No regression from the wind model or the fin term.** Expected: with zero wind
configured, `WindModel.Sample()` returns exactly `Vector3.zero`, and the fin term is
proportional to lateral airspeed, which is ~0 in coordinated flight. The batteries
confirm the expectation rather than the expectation excusing the batteries.

---

## PHASE 3 — MISSION BANK: 12 → 42, ON TWO AXES ✅

Design reviewed externally before implementation, because the statistical consequences
of a large bank are not obvious and are the part of this work that cannot be tested into
correctness. Two conclusions changed the design:

1. **Crosswind does not belong on the Low/Medium/High scale.** It raises manual demand
   by construction, and the cognitive axis is interpretable *only* because manual demand
   is matched within each phase row. Crosswind became a second axis with its own
   analysis. See `MISSION_BANK_DESIGN.md` §2.
2. **A large bank is the classic way an experiment becomes a demonstration.** So the
   bank grew and the SESSION did not: a participant still flies the same verified twelve.

**Delivered — 42 missions:**
- **Cognitive axis, 36** — 4 phases × 3 classes × 3 interchangeable variants. The
  original twelve are variant 1, unchanged. Variants 2 and 3 reach the same class by a
  *different cognitive mechanism*, and the battery **fails** any cell whose variants
  repeat a mechanism.
- **Psychomotor-integrated axis, 6** — crosswind take-off and landing at three graded
  crosswind levels, each isolating its cognitive component as a discrete
  continue-or-abandon decision against the C172's 15 kt demonstrated crosswind.
- `ControlActivity.cs` — control travel, rate, per-axis SD, hands-on fraction. A
  manipulation check on the cognitive axis; the covariate an EEG effect must survive on
  the psychomotor one.
- Bank design checks in the battery: grid completeness, unique ids, mechanism
  distinctness, motor matching per row, PLI ordering per column, mandatory metadata, no
  crosswind on the cognitive axis, and every possible session a complete crossed grid.
- `variant_equivalence.csv` — the a-priori half of the exchangeability evidence.

**Checkpoint: design checks OK; quick battery 12/12 at 82 telemetry columns; full
42-mission battery running.** 🔄

---

## PHASE 4 — SESSION / BLOCKING ⬜
No participant can fly 36 × 300 s in one EEG session. The bank needs a counterbalanced
per-session subset with the variant assignment recorded and reproducible.

## PHASE 5 — METRICS & EVALUATION ⬜
Crosswind-aware take-off and landing metrics (crab held, decrab timing, centreline
deviation, drift integral) now that they are measurable.

## PHASE 6 — VR & PHYSICAL CONTROLS ⬜
Validate the XR loader chain, seated origin and reachability; confirm the hardware input
path now that the override-wiping defect is fixed.

## PHASE 7 — WINDOWS BUILD ⬜

## PHASE 8 — DOCUMENTATION & FINAL CHANGE REPORT ⬜
