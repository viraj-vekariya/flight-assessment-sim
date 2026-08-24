# FINAL_VALIDATION_MATRIX

Every requirement, its status, and the evidence. Nothing here is asserted without a
place to go and check it.

Verified on the final build: **compile exit 0, 0 errors, 0 warnings; mission battery
exit 0, 12/12, 0 problems.**

---

## A. Deliverables

| Requirement | Status | Evidence |
|---|---|---|
| Final project created | ✅ | `~/Desktop/BTP Project/FlightAssessmentSim - Final/` |
| Source projects preserved untouched | ✅ | `FlightAssessmentSim - VR/` and `FlightAssessmentSim - Extra - Copy/` unmodified; Final was built as a fresh copy, never a rename |
| Real GLB cockpit | ✅ | `Assets/StreamingAssets/Cockpit/cessna_g1000.glb` (28.7 MB) + `RealCockpit.cs`; battery log shows `RealCockpit: centred Object_96+Object_98+Object_100 …` — it loads and the single-pilot mesh surgery runs |
| Live PFD | ✅ | `LivePFD.cs` (cockpit project's 162-line version: attitude + HDG/SPD/ALT/VS, pitch marks, aircraft symbol) |
| Live MFD | ✅ | `LiveMFD.cs` (178-line version: heading-up moving map, compass rose, range ring, silhouette) |
| Cockpit interaction | ✅ | `CockpitInteraction.cs` + `CockpitControl.cs` — mouse yoke / throttle / levers / pedals / brake / switches, wired by `CockpitBuilder` |
| Cockpit tuning tools | ✅ | `CockpitTuner`, `ScreenTuner`, `ScreenProbe`, `YokeProbe`, `YokeShot`, `FlightTest`, `PlayCapture.Run` |

## B. Experiment

| Requirement | Status | Evidence |
|---|---|---|
| 12 missions | ✅ | `MissionLibrary.All()` returns 12; `FINAL_MISSION_DESIGN.md` table |
| 4 LOW | ✅ | L1 L2 L3 L4 — PLI 13–29 |
| 4 MEDIUM | ✅ | M1 M2 M3 M4 — PLI 50–58 |
| 4 HIGH | ✅ | H1 H2 H3 H4 — PLI 76–87 |
| **Phase crossed with class** | ✅ **new** | 4 phases × 3 classes, one per cell; every phase row monotone L<M<H; `FINAL_MISSION_DESIGN.md` design grid |
| Take-off missions at all three levels | ✅ **new** | L1 / M1 / H1, with a real taxi from a parking stand — the requirements' stated initial focus |
| Not distinguished by weather alone | ✅ | Weather in 4 of 12, primary driver in 1 (M4). Audit table in `COGNITIVE_LOAD_MODEL.md` §3 |
| Workload model | ✅ | `WorkloadModel.cs` — 12 dimensions, weighted PLI, per-mission profile |
| Aircraft failure system | ✅ | `AircraftSystems.cs` — engine, fuel, electrical, pitot-static, avionics, airframe; 14 `FailureKind`s |
| Checklist system | ✅ | `ChecklistSystem.cs` — 6 drills, DO vs CHECK items, timeout handling |
| 50 Hz telemetry | ✅ | 15,000 rows × 77 columns per 300 s trial, sampled on the **physics step** (frame-rate independent) |
| 46 event markers | ✅ | `EventMarkers.cs` — 46 `public const string`; harness rejects any marker outside the vocabulary |
| EEG synchronisation | ✅ | 4 clocks per row (`t_mission`, `t_host`, `t_unix`, `t_lsl`), `eeg/sync.json`, LSL outlet in `LSLSync.cs` |
| NASA-TLX | ✅ | 21-point scale, **no default answer**, SUBMIT disabled until all six + Bedford answered; `nasa_tlx.json` per trial |
| Latin-square ordering | ✅ | `ExperimentSession.BalancedLatinSquareRow` (Williams) + class de-clumping; recorded in `session.json` |
| Participant flow restored | ✅ | `ParticipantReady` set **only** by `ParticipantUI` → `SetParticipantReady()`; the free-flight bypass is gone |
| Results screen | ✅ | `ResultsUI` — no score before the questionnaire; session report at the end |
| Mission test harness | ✅ | `MissionTestHarness.cs`, 12/12, exit 0 |
| Traffic (requirements) | ✅ **new** | `TrafficAircraft.cs` — visible scripted aircraft; M1 crossing the runway, H1 taxiing + holding on the runway + crossing the departure |
| Taxiways / signage / checkpoints (requirements) | ✅ **new** | `AerodromeBuilder.cs` — apron, stand, taxiway A, link B, hold-short markings, mandatory + direction signs, 4 named checkpoints |

## C. Physics merge — the explicitly required items

| Feature | Source | In Final | Evidence |
|---|---|---|---|
| `turnCoordination` | Extra | ✅ | `CessnaPhysics.cs` field + torque application |
| rate damping (`pitchDamp`/`rollDamp`/`yawDamp`) | Extra | ✅ | field + `AddRelativeTorque` block |
| `powerAvailable01` | Experiment | ✅ | scales `ThrustN` |
| `extraCd` | Experiment | ✅ | added into the drag coefficient |
| `extraRollTorque` | Experiment | ✅ | q-scaled body-axis torque |
| `extraYawTorque` | Experiment | ✅ | q-scaled body-axis torque |
| `flapAuthority01` | Experiment | ✅ | clamps the flap command in `AircraftController` |
| `PadFlatten` | **both, merged** | ✅ | widest of both on every axis, plus lateral room for the taxiways |
| `cd0` = 0.040, `maxThrust` = 2800 N | Extra (calibrated) | ✅ | kept over the experiment project's older values |

## D. Testing

| Test | Result |
|---|---|
| Compile | **exit 0 — 0 errors, 0 warnings** |
| Mission battery | **exit 0 — 12/12, 0 problems** |
| Startup / participant gate | ✅ gate is the only path to `ParticipantReady` |
| Mission selection + start | ✅ every mission started and ran to completion |
| Baseline | ✅ ~3,000 rows (60 s at 50 Hz) in every mission |
| Trigger / failure injection | ✅ `TRIGGER_ARMED` + `CUE_ONSET` in all failure missions |
| Taxi + hold short | ✅ `runway_incursion = 0`, 4/4 checkpoints, `taxi_phase_reached = 3` |
| Traffic | ✅ `TRAFFIC_ONSET` for all scripted traffic, runway genuinely blocked in H1 |
| Telemetry | ✅ 15,000 × 77, monotonic, no ragged rows |
| Event markers | ✅ all within the 53-tag vocabulary; `MISSION_START`/`END` exactly once |
| NASA-TLX | ✅ `nasa_tlx.json` written per trial; `TLX_START`/`TLX_SUBMIT` bracket it |
| Reset / restart | ✅ new trial folder, clock reset, no failure survives |
| Next mission / session flow | ✅ 12 trials advanced automatically |
| Analysis pipeline | ✅ 26 trials → `trials.csv` (75 cols) + `windows_index.csv` (7,730 windows) |
| Cockpit loads in the merged project | ✅ `RealCockpit` log line in the battery |
| **Mission battery re-run after the flight model changed** | **exit 0 — 12/12, 0 problems** (trim, analog brakes, flap lock) |
| **Cockpit control battery** | **0 problems / 50 checks** — 9 controls |
| Yoke direction + gearing | ✅ ±1.00 at full travel each axis, 0.41 at half (1.25 expo) |
| Trim | ✅ ±1.00, elevator bias ∓0.35, correct sense |
| Brakes | ✅ analog 0.50/1.00, releases to 0, **ignored airborne** |
| Flap failure | ✅ selection moves, surface frozen where the motor died, restores |
| Control ownership | ✅ a write takes it and it **expires** (the latch regression) |
| Per-trial reset | ✅ trim, brake, flaps, 3 switches, selector, `AnyOverride = False` |
| **Frame-rate independence** | ✅ 30/60/90 FPS → 0.614/0.614/0.614, **spread 0.000**, analytic 0.614 |
| XR loader chain configured | ✅ `XRSetup.Verify`: Standalone, `initOnStart=True`, `[OpenXRLoader]`, 2 interaction profiles |
| VR on a headset | ❌ **not tested — no headset has been connected.** See G |

## E. Documentation

| Document | Status |
|---|---|
| `FINAL_PROJECT_README.md` | ✅ how to open, run, add a mission, debug |
| `FINAL_ARCHITECTURE.md` | ✅ structure + the four contracts |
| `MERGE_ARCHITECTURE.md` | ✅ full source-by-source table |
| `FINAL_EXPERIMENT_PROTOCOL.md` | ✅ incl. the duration decision |
| `FINAL_MISSION_DESIGN.md` | ✅ **generated from code** |
| `FINAL_TELEMETRY_SCHEMA.md` | ✅ **generated from code** |
| `FINAL_EEG_INTEGRATION.md` | ✅ incl. the leakage rules |
| `FINAL_TEST_REPORT.md` | ✅ incl. the fourteen defects found |
| `COCKPIT_CONTROLS.md` | ✅ all nine controls, wiring and anchors |
| `CONTROL_SENSITIVITY.md` | ✅ every travel/radius/τ with its reason; the trim–PLI analysis |
| `VR_INTERACTION.md` | ✅ incl. an explicit *not verified on hardware* section |
| `VR_CALIBRATION.md` | ✅ the two-minute per-participant procedure |
| `VR_EXPERIMENT_CONSIDERATIONS.md` | ✅ the scientific audit of adding VR |
| `FINAL_VALIDATION_MATRIX.md` | ✅ this file |
| `COGNITIVE_LOAD_MODEL.md`, `DESIGN_CRITIQUE.md`, `NASA_TLX.md`, `MISSION_IMPLEMENTATION.md`, `RESEARCH_REFERENCES.md` | ✅ updated for the new set |

---

## F. Deliberately NOT done, with reasons

| | Why |
|---|---|
| VR layer not carried over | Final is the flatscreen experiment build. An EEG cap under a Quest 2 strap fouls the occipital/parietal sites the alpha measure needs. The VR project still exists separately. |
| 10–15 minute take-off tasks | Twelve × 12 min ≈ 3 h of EEG per session; gel dries and fatigue would dominate. Task *structure* preserved, *distance* compressed. Argued in `FINAL_EXPERIMENT_PROTOCOL.md` §2.1. |
| Emergent/collidable AI traffic | Would make the trigger instant vary between participants and destroy event-locked epoching. Scripted geometry is the deliberate trade. |
| Voice R/T loop | Not modelled; "read-back" is an acknowledgement keypress measuring acceptance latency, not read-back accuracy. Documented everywhere it matters. |
| Carb ice, split flap, radio/brake failure as missions | Implemented in `AircraftSystems` and available, but not used by the twelve — mechanism diversity was prioritised over coverage. Flagged as untested in `MISSION_IMPLEMENTATION.md`. |

## G. Not verified — be clear about this

* **No participant has flown it. No EEG has ever been recorded.** Every workload claim
  in these documents is a prediction from the literature, labelled as such.
* **The LSL path has never met an amplifier.** It compiles; that is all that is known.
* **No power analysis** — there is no pilot data to estimate an effect size from.
* **No visual sign-off.** The battery proves the cockpit loads; it does not prove the
  panel reads well. Open the editor and look, or use `-screens` / `-probe`.
* **The demand scores are one rater's task analysis** with no inter-rater reliability.
* **VR has never run on a headset.** The XR loader chain is configured and verified as
  *configuration*; HMD tracking, controller pose, whether the capture radii are actually
  reachable, haptics and VR frame rate are all untested. macOS has no OpenXR runtime, so
  this cannot be tested on the development machine — it needs the Windows/Quest setup.
  **Do not describe this project as "Quest 2 verified".**
* **The EEG-under-headset question is open**, and it is a blocker for VR data collection
  rather than a caveat. `VR_EXPERIMENT_CONSIDERATIONS.md` §3.
* **Whether the controls feel right to a human is unknown.** The control battery verifies
  wiring, direction, gearing, reset and frame-rate independence — not comfort, reach or
  appropriate sensitivity.
