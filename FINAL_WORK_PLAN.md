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

## PHASE 4 — SESSION / BLOCKING ✅
`MissionLibrary.SessionMissions()` returns the twelve a given participant flies — one
variant index per phase row, rotated by Latin square. `session.json` records
`row_variants`, `bank_size` and `variants_per_cell`; every trial's `metadata.json`
records its axis, variant, mechanism and cell. The operator grid shows all three variants
and marks the assigned one.

**Checkpoint:** a design check verifies that *every* possible participant's session is a
complete crossed grid with no mixed variants inside a phase row. ✅

## PHASE 5 — METRICS & EVALUATION ✅
* **Motor covariates** (`ControlActivity`): travel, rate, per-axis SD, hands-on fraction.
  A manipulation check on the cognitive axis; the covariate an EEG effect must survive on
  the psychomotor one.
* **Take-off:** rotation airspeed and distance, ground-roll centreline and heading
  excursion, initial climb rate, rotate-to-level time.
* **Landing:** touchdown distance from the threshold, touchdown speed, flare start height
  and duration, glidepath RMS error, runway excursion, touchdown drift and bank, max
  centreline deviation, mean |drift| below 60 m.

Two defects found here rather than assumed away: a completed navigation mission was
scored `MISSION_FAILURE`, and mean |drift| read 154° because it was being integrated at a
standstill.

## PHASE 6 — VR & PHYSICAL CONTROLS ✅ *(configuration only — see limitations)*
OpenXR loader chain verified in the asset files; Quest 3 / Quest Pro interaction profiles
enabled alongside Oculus Touch and KHR Simple; seated origin reviewed and left unchanged
(it is correct — it puts the headset at the measured pilot eye point and adds no comfort
effects). `HardwareInput` gives physical yoke/throttle/pedals/toe-brakes a path in, which
only became possible once the override-wiping defect was fixed.

**Nothing here is hardware-verified.** No headset has been connected and no yoke plugged
in. See `FINAL_CHANGE_REPORT.md` §11.

## PHASE 7 — WINDOWS BUILD ✅
`Builds/Windows/`, 127 MB, 0 errors. Required installing Windows Build Support *and*
discovering that **the project had never been built into a player on any platform** — it
has no scenes by design, and Unity refuses an empty scene list.

**Not yet run on Windows.** No macOS host can do that.

## PHASE 8 — DOCUMENTATION & FINAL CHANGE REPORT ✅
`FINAL_CHANGE_REPORT.md`, `MISSION_BANK_DESIGN.md`, `HARDWARE_CONTROLS.md`,
`WINDOWS_DEPLOYMENT.md` written; `FINAL_ARCHITECTURE.md`, `FINAL_EXPERIMENT_PROTOCOL.md`,
`DESIGN_CRITIQUE.md`, `FINAL_PROJECT_README.md` updated; `FINAL_MISSION_DESIGN.md` and
`FINAL_TELEMETRY_SCHEMA.md` regenerated from code.
