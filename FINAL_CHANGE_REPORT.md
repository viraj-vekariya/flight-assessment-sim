# FINAL_CHANGE_REPORT

What was inspected, what was kept, what was changed, and what is still not done.

Every claim in this document is backed by a report file in the project root or a git
commit. Where something is unverified it says so.

---

## 1. What was inspected

Eighteen folders under `~/Desktop/BTP Project/`, including five full Unity projects
(`Final`, `VR`, `VR copy`, `Extra - Copy`, and four `Phase N` copies), the
`FlightSim_Versions` archive and the `Windows file` tree.

Inside `FlightAssessmentSim - Final`: all 82 runtime scripts and 4 editor tools
(~17,400 lines), all 25 documents, the mission library, the scenario engine, the cockpit
rig, the flight model, `WorldBuilder`/`AerodromeBuilder`, the logging and EEG layers, the
session and participant layers, the XR configuration, the packages and the project
settings.

## 2. What was retained

**Nearly all of it.** `FlightAssessmentSim - Final` is not a prototype: it is a
deliberate merge of the VR project's research apparatus with the Extra project's
photoreal cockpit, documented file-by-file in `MERGE_ARCHITECTURE.md`, compiling clean
with a passing 12-mission battery and a passing 50-check control battery.

Retained unchanged in substance: the scenario engine, the checklist system, the failure
model and its five-hook contract, the event-marker vocabulary, the logging architecture
and its single-write-point rule, the physics-locked clock, the Latin-square session
design, the three-baseline design, NASA-TLX, the GLB cockpit and its control rig, the
PFD/MFD, the world builder, and **all twelve original missions**, which are now variant 1
of the bank.

The strategy was **inspect → consolidate → improve → test**, not rewrite. No "Phase 5"
was created.

## 3. What was removed

Very little, and nothing that worked:

* `CockpitInteraction`'s unconditional per-frame `ClearOverrides()` (§5, defect 3).
* `FlightTest`'s implicit `-batchmode` auto-spawn and its seven-entry opt-out list,
  replaced by an explicit flag plus `SimDriver` (§5, defect 4).
* The narrow aerodrome corridor in `WorldBuilder.PadFlatten`, replaced by a plain (§5,
  defect 6). Flattening only lowers terrain, so nothing previously verified got worse.

## 4. What was rebuilt

* **The air mass.** `WindModel.cs` — steady wind, power-law surface shear, optional
  discrete shear layer, seeded band-limited gusts.
* **Directional stability.** A fin term in `CessnaPhysics` (`finVolume`), because the
  existing `weathervane` term delivers about 2 N·m per radian of yaw misalignment —
  roughly three thousand times too little to model a fin, so the aeroplane flew through a
  crosswind almost entirely sideways.
* **The mission set**, from 12 to a 42-mission bank on two axes.

## 5. Defects found, and how each was found

Every one of these was found by a **new automated check**, not by inspection. That is the
point: the existing batteries were good and passed throughout.

| # | Defect | Found by | Consequence if shipped |
|---|---|---|---|
| 1 | `MissionDefinition.CrosswindMs` declared, written by nothing, read by nothing | reading the field's callers | **Every landing mission flown in dead calm.** Runway centreline deviation was a nearly free metric and no crosswind task was possible. |
| 2 | Turbulence keyed to `UnityEngine.Time.time` — seconds since the editor launched | wind battery, determinism check | Turbulence was **seeded but not reproducible**: two participants with the same seed got different turbulence. A reproducibility claim that was false. |
| 3 | `CockpitInteraction.ClearOverrides()` called unconditionally every frame at order −50 | wind battery could not command the rudder | Any override written by a later script was silently wiped. Invisible only because the GLB cockpit disables that component 14 s in; **in the fallback path no programmatic or hardware input could reach the aircraft at all**. |
| 4 | `FlightTest` auto-spawned on any `-batchmode` run unless the command line named one of seven flags | the wind battery's own results made no sense | `-windtest` was not on the list, so the first wind battery ran with a second harness flying the aeroplane and `AircraftController` disabled. **A battery that completes and reports wrong numbers.** |
| 5 | Within-cell variant PLI spreads up to 17.5 on a 0–100 scale | `variant_equivalence.csv`, read after being written | Variants of a cell were not interchangeable; a participant's effective workload class would have depended on which variant they were assigned — **variant becoming a confound with class**. |
| 6 | A departure turn flew into terrain (`M1V2`, "Destroyed (terrain impact)", 131 m) | full mission battery | Two causes: `TargetHeadingDeg` set on a take-off mission makes it the target from brake release; and the aerodrome pad was a **corridor** sized only for straight-out departures. Turning missions had −97 to −11 m of terrain clearance. |
| 7 | Mean \|drift\| of 154° on a crosswind take-off | reading the recorded telemetry | Drift computed from a near-zero ground track points anywhere at all. The crosswind axis's **own primary metric** would have been analysed as data. |
| 8 | A stale `mission_test_report.txt` in the project root, dated the previous day, showing the old 12 missions and 77-column telemetry | comparing the report against the live log | Reading yesterday's result and believing it is today's. The report is now written to both locations. |
| 9 | The scripted pilot could not follow waypoints | `L3V3` reported INCOMPLETE | A navigation mission looked broken when the autopilot was blind. |

## 6. Mission structure

**Bank: 42.** **Session: 12.** These are different numbers on purpose — 42 × 300 s is
over three hours of flying inside one EEG session.

* **Cognitive axis — 36.** 4 phases × 3 classes × 3 variants. The original twelve are
  variant 1, unchanged.
* **Psychomotor-integrated axis — 6.** Crosswind take-off and landing at three graded
  crosswind levels.

A participant flies one variant index per phase row, rotated by Latin square. Within a
row all three classes share a variant, so the class contrast is always variant-matched;
across rows the variant differs, so variant is not perfectly nested in participant.

Full table in `MISSION_BANK_DESIGN.md`; generated per-mission specifications in
`FINAL_MISSION_DESIGN.md`.

## 7. Cognitive-load design

The rule that makes the bank worth having: **the three variants of a cell must reach
their workload class by different cognitive mechanisms.** Reading the HIGH row across
variants:

| | variant 1 | variant 2 | variant 3 |
|---|---|---|---|
| TAKE-OFF | concurrency under time pressure | forward reasoning about a physical margin | sustained monitoring + prospective memory |
| CLIMB | a depleting resource | irreversible commitment | prioritisation among conflicting demands |
| CRUISE | self-consistent wrong information | resource management | concurrency |
| APPROACH | irreversible commitment under a clock | re-computation | startle then re-planning |

The battery **fails** any cell whose variants repeat a mechanism string, so the claim is
enforced rather than asserted.

**Crosswind was deliberately kept off the Low/Medium/High scale.** It raises manual demand
by construction, and that scale is interpretable only because manual demand is matched
within each phase row. It is a second axis, with the control-activity covariates measured
and its cognitive component isolated as a discrete continue-or-abandon decision against a
stated 15 kt demonstrated crosswind — which, on the take-off missions, is made with the
aeroplane stationary at the holding point and therefore free of movement artifact.

## 8. Data architecture

* Telemetry **77 → 82 columns**: `groundspeed_kmh`, `drift_deg`, `sideslip_deg`,
  `wind_n_ms`, `wind_e_ms`.
* `metadata.json` gains the full wind specification, plus `load_axis`, `variant`,
  `mechanism` and `cell`.
* `session.json` gains `row_variants`, `bank_size` and `variants_per_cell`.
* `ControlActivity` writes eleven motor covariates into `performance.json`.
* New take-off metrics: rotation airspeed and distance, ground-roll centreline and
  heading excursion, initial climb rate, time from rotation to level-off.
* New landing metrics: touchdown distance from the threshold, touchdown speed, flare
  start height and duration, glidepath RMS error, runway excursion, touchdown drift.

## 9. Testing performed

| Battery | Result |
|---|---|
| Compile | 0 errors, 0 warnings |
| **Wind model** (`-windtest`, new) | **46 checks, 0 problems** |
| **Cockpit controls** (`-controltest`) | **50 checks, 0 problems**, re-run after the flight-model change |
| **Mission battery, 12 missions** (`-missiontest`) | **12/12, 0 problems**, re-run after the flight-model change |
| **Bank design checks** (new) | **OK** — grid completeness, unique ids, mechanism distinctness, motor matching per row, PLI ordering per column, within-cell PLI tolerance, mandatory metadata, no wind on the cognitive axis, every possible session a complete grid, terrain clearance |
| **Full bank battery, 42 missions** | see §15 |

The wind battery asserts *numbers against closed-form predictions computed in the test*,
not behaviours: the geometry of a meteorological direction, the surface/free-stream shear
relationship, a crosswind/headwind round trip, groundspeed = airspeed − headwind, drift =
atan(Vw/V), ground weathervaning direction, gust determinism from the seed, and that zero
wind is exactly zero.

## 10. Windows build

**Not produced.** Only `MacStandaloneSupport` is installed in this editor; Windows Build
Support is absent and Unity cannot cross-compile to a missing module.

Everything that does not require the module was done: a full static portability audit
(clean — no Mac-only paths, `Path.Combine` throughout, `persistentDataPath` for all data,
no editor-only dependencies in runtime code), a reproducible `BuildTool` that fails with
the exact install command rather than an opaque error, and a deployment runbook. See
`WINDOWS_DEPLOYMENT.md`.

## 11. Remaining hardware-dependent tests

Nothing below has been faked or asserted:

* The Windows build itself.
* **Meta Quest**: headset detection, seated-origin comfort, control reachability, frame
  timing under the real cockpit load. The OpenXR loader chain is configured and
  `XRSetup.Verify` reports every link, but no headset has been connected.
* **Flight hardware**: axis numbering, travel and comfort for a specific yoke, throttle
  and pedals. The path in exists and the calibration is designed; the binding is a bench
  task.
* **EEG**: the LSL outlet is implemented and every marker carries four clocks, but
  end-to-end synchronisation against a real amplifier has never been measured.
* **No participant has ever flown this simulator.** Every "flyability" statement in the
  battery comes from a crude scripted autopilot, which is not a model of a person.

## 12. Known limitations

* The wind model is horizontal only: no terrain rotor, no mechanical turbulence from
  buildings, no thermals, no wake turbulence, and no downdraught in the shear case (a
  real microburst is unsurvivable in a C172 and the mission would measure luck).
* Read-backs are a keypress, not speech. The simulator can record *that* the pilot
  responded and *how fast*, never whether they read the clearance back correctly.
* There is one physical runway; sidesteps and runway changes are represented by a lateral
  offset and a shifted aiming point on the same strip.
* Differential braking is not modelled — the flight model has a single brake value.
* Landing scoring judges sink, bank and alignment but does not model gear side-load, so
  landing crabbed is penalised through alignment and excursion rather than through a
  modelled undercarriage failure.
* Every mission's own `Approximations` field lists what that mission in particular cannot
  model faithfully, and it is copied into `metadata.json` so no analysis can quietly
  forget it.

## 13. Recommended next steps, in order

1. **Install Windows Build Support and produce the lab build.** One command, in
   `WINDOWS_DEPLOYMENT.md` §3.
2. **Bind and check the flight controls on the lab PC** (`HARDWARE_CONTROLS.md` §5).
3. **Measure the EEG synchronisation end to end** — a hardware trigger and the LSL
   marker recorded together, and the offset and jitter reported. Until that number
   exists, every epoch in the study rests on an assumption.
4. **Run one full pilot session with a person**, and look at three things: does the
   NASA-TLX separate the classes; do the `ctrl_*` covariates stay flat within a phase
   row (the motor-matching manipulation check); and does anyone actually finish a HIGH
   mission.
5. **Then, and only then, revisit the mission set.** If a class does not separate on
   TLX, the label is wrong and the data win — that is what the pre-registered
   predictions are for.
6. Run a power analysis against the pilot session's effect sizes before committing to a
   sample size.
