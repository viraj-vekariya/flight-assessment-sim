# VR COCKPIT & HUMANIZED CONTROLS — PHASE REPORT

What was built, what was fixed, what was measured, and what is still unknown.

---

## 1. What was implemented

**A spec-driven physical control layer.** Nine controls on the real GLB cockpit geometry
— yoke, throttle, trim wheel, flap lever, brake, carb heat, fuel selector, load shed,
alternate static — all built from one `PhysicalControl` implementation driven by a
`ControlSpec` (kind, target, axis, travel, capture radius, time constant, expo, dead
zone, detents). Adding a control is a spec, not a class.

**One interaction model, two hardware paths.** `CockpitInteractorMouse` and
`CockpitInteractorVR` both reduce to `BeginGrab(worldPoint)` / `UpdateGrab(worldPoint)` /
`EndGrab()` / `Click()`. Desktop and VR therefore operate an *identical* cockpit; only
the source of the world point differs.

**Elevator trim**, authority 0.35, on `[` / `]` and on the console wheel — the single
largest workload confound removed (§4).

**Working brakes**, analog and ground-gated (§3, defect 11).

**XR, properly.** OpenXR + XR Management installed, and — the step the previous attempt
missed — the **loader chain actually configured**: `XRSetup.Configure` creates the
settings asset, the per-target manager, assigns `OpenXRLoader`, sets `InitManagerOnStart`
and enables the Oculus Touch and KHR Simple interaction profiles. `XRSetup.Verify`
reports every link.

**`VRRuntime`**, which reports XR module / loader / HMD / controllers as four separate
facts rather than one opaque boolean, plus `InputModality` and haptics.

**`VRCameraRig`**, a seated origin at the measured pilot eye point, no comfort effects,
recentre on both grips + both triggers.

**A control-check bench** (`-controlcheck`, or the menu) showing every control's live
value — the forty-second pre-flight that catches a dead controller before an hour of EEG
is spent.

**An automated control battery** (`-controltest`), 50 checks over all nine controls.

**Instrumentation**: 10 new telemetry columns (`trim`, `elevator_cmd`, `brake_pressure`,
six `ovr_*` flags, `control_held`), 7 new markers (46 → 53 tags), and `input_modality` +
`vr_status` in `session.json` and every `metadata.json`.

---

## 2. Architectural decisions, with reasons

**Core XR input, not the XR Interaction Toolkit.** What this project needs from the XR
stack is small and stable — controller pose, a grip value, two buttons, haptics.
`UnityEngine.XR.InputDevices` supplies exactly that, ships with the engine, and leaves
the cockpit's own interaction model in charge. XRI would have meant either bending its
grab/interactable machinery into that shape or accepting its behaviour instead of the
one the experiment requires. Revisit only if hand-tracking becomes a research need.

**Position-based yoke, not wrist rotation.** Wrist-orientation-to-pitch/roll is the
standard VR shortcut and is wrong here: it couples aircraft control to forearm posture,
drifts as the arm tires across a 300 s trial, and varies between participants. That is a
motor confound in an experiment whose whole purpose is measuring cognitive load. The
yoke is driven by hand displacement, and the visual and the aircraft input are the same
number — 50 % travel is 50 % deflection, verified at 0.41 with the 1.25 expo.

**A control writes only while held.** Otherwise it mirrors the aircraft. This is both
physically right — a real throttle lever *is* the throttle, and moves when anything else
moves it — and necessary, because a lever writing every frame owns its axis permanently
(§3, defect 13).

**Ownership expires rather than latching.** A write owns an axis for that frame plus one.
No release call exists, so none can be forgotten (§3, defect 10).

**Acknowledgement is a controller button.** Response latency is experimental data;
requiring a hand to find a virtual button would add a participant-dependent motor delay
to every measurement.

**Class renamed.** The VR interaction class is `CockpitInteractorVR`, avoiding the
collision with Final's legacy `CockpitInteraction` (now disabled at runtime, retained as
a fallback only).

---

## 3. Defects found and fixed

| # | Defect | How it would have shown up |
|---|---|---|
| 10 | **Override latch with no live caller** — `ClearOverrides()` was never called | first VR/cockpit touch kills the keyboard for that axis for the rest of the session, silently |
| 11 | **Brakes hard-disabled** (`phys.braking = false` every frame) | `B` did nothing; no cockpit brake could work; unnoticed because the scripted pilot never brakes |
| 12 | **A failed flap motor still allowed full retraction** | a participant could silently undo the failure and defeat H2's entire decision, and the trial would look normal |
| 13 | **Levers wrote every frame** | keyboard permanently overridden; `AnyOverride` always true, defeating the expiry model |
| 14 | **`ElevatorCmd` computed inside `if (speed > 1.5f)`** | read 0.00 when parked; wrong value logged at low speed |

Plus 8 orphaned `CockpitControl` trigger colliders destroyed — invisible interaction
targets in the VR world.

**Two of the battery's early "failures" were the test being wrong**, and both are
recorded in `FINAL_TEST_REPORT.md` because they are easy to repeat: waiting *frames*
instead of *time* against time-constant behaviour, and measuring the raw hand position
instead of the smoothed value the aircraft receives — which made the frame-rate test pass
vacuously. A test that cannot fail is not evidence.

---

## 4. Trim, and why the PLI did not change

Before trim, every mission required a sustained elevator force, and **the size of that
force differed between missions** because it depends on speed, flap setting and
configuration — all of which the mission design varies deliberately. That was an
uncontrolled motor load correlated with mission class by accident. Trim converts it into
a discrete, logged, optional pilot action.

Sensitivity analysis: one full point of `ManualControl` demand (weight 0.05 of 12
dimensions) is worth **1.25 PLI points**. The extreme assumption — manual demand down a
full point in *every* mission — is a uniform shift, and a uniform shift cannot change a
difference:

```
              current          if manual −1 everywhere
LOW        12.8 – 29.2            11.5 – 28.0
MEDIUM     49.5 – 57.5            48.2 – 56.2
HIGH       76.2 – 87.0            75.0 – 85.8

class gaps LOW→MED 20.3, MED→HIGH 18.7   —   unchanged
```

**Decision: the published PLI values stand.** Trim removed a confound; it did not alter
the load ordering.

Because trim changes the flight model, the **full 12-mission battery was re-flown** rather
than assumed unaffected: 12/12, 0 problems.

---

## 5. Testing — what was measured

| Battery | Result |
|---|---|
| Compile | 0 errors, **0 warnings** |
| Mission battery (after trim + brakes + flap lock) | **12/12, 0 problems** |
| Mission battery (after the spoiler lockout) | **12/12, 0 problems** |
| Control battery | **0 problems / 50 checks**, 9 controls |
| XR configuration | `Standalone manager=Standalone Providers initOnStart=True loaders=1 [OpenXRLoader]`, 2 interaction profiles |

Selected control results: yoke ±1.00 at full travel on both axes and 0.41 at half; trim
±1.00 with elevator bias ∓0.35; brake 1.00 full, 0.50 partial, 0.00 released, **ignored
airborne**; flap failure freezes the surface where the motor died and restores correctly;
ownership takes *and expires*; per-trial reset clears trim, brake, flaps, three switches
and the selector, leaving `AnyOverride = False`.

### Frame-rate independence — the one claim that was verified rather than asserted

Smoothing uses `k = 1 − exp(−dt/τ)`. Pinning `Time.captureFramerate` to 30, 60 and 90 —
and **asserting the rates were actually simulated** (1, 2 and 3 frames of 0.0333, 0.0167
and 0.0111 s), because the first version of this test silently did nothing — a step input
on the throttle gave:

```
 30 FPS -> 0.614     60 FPS -> 0.614     90 FPS -> 0.614
 analytic 1 - exp(-t/tau) = 0.614        spread 0.000
```

A naive per-frame `Lerp(a, b, k)` would have given roughly 0.05 / 0.10 / 0.15 — a desktop
participant at 60 FPS and a VR participant at 90 FPS flying aircraft with measurably
different control response, i.e. frame rate as a silent experimental variable.

---

## 6. Scientific audit

**Does physical interaction create workload? Yes.** Reaching for a switch consumes
attention and produces motor cortex activity. That is true of any interface.

**Is it constant across missions? No, and it cannot be made so.** HIGH missions require
more hand movement than LOW missions because that is what high-workload flying *is*. This
is stated plainly rather than minimised.

**Can the analysis separate them? Partly, and the apparatus was built to make the attempt
possible.** `control_held` names the held control in every 50 Hz row; seven markers
timestamp discrete actions on the EEG clock; `ctrl_jerk` remains as a continuous
regressor. The primary defence is the **matched-quiet-window analysis** — compare LOW and
HIGH windows in which no control was touched at all. If the workload effect survives, it
is not motor artefact; if it disappears, that is a real finding about the apparatus and
must be reported. Fallbacks (epoch exclusion, covariate regression, a motor-only control
condition) are in `FINAL_EEG_INTEGRATION.md` §5b.

Three further points carried into the documentation:

* **Modality is a grouping variable, not a setting.** VR and desktop differ in visual
  field, head movement, control interface and fatigue. `input_modality` is written to
  `session.json` and every `metadata.json`, and **the two must never be pooled**.
* **EEG under a headset is an open hardware question and a blocker for VR data
  collection** — the strap crosses frontal and central sites, impedance drifts under
  pressure, and head-movement artefact correlates with looking behaviour which correlates
  with workload. Do the pilot before committing.
* **Simulator sickness is not measured.** If VR is used, add the SSQ pre and post and
  pre-register an exclusion rule; susceptibility is not randomly distributed, so
  sickness-driven dropout is not random dropout.

---

## 7. Requirements deliberately not met

* **No spoiler cockpit control** — a 172 has none. The debug key is now additionally
  **locked out while a trial is recording**, so a participant brushing `X` cannot
  silently change the aircraft's drag; the telemetry column stays so its value is
  provably zero rather than assumed.
* **No fake G1000 interaction.** The displays are read-only. Knobs that do not drive a
  simulated system would produce behavioural data unrelated to the flight model.
* **No rudder or toe brakes in VR.** There is no foot tracking on Touch controllers, and
  a hand gesture for rudder would add more noise than realism. Rudder stays on `Q`/`E`.
* **No mixture control.** The engine model has no mixture, and the GLB fuses the throttle
  and mixture knobs into one mesh.
* **No per-participant control gearing.** It would make control gain a between-subject
  variable.

---

## 8. Verified vs implemented — read this before making any claim

| | |
|---|---|
| **Verified** | the desktop path end to end; all 9 controls' wiring, direction, gearing and reset; frame-rate independence at 30/60/90; the 12-mission battery twice; per-trial reset with no state leakage; the XR loader **configuration** |
| **Implemented, not verified** | HMD tracking; controller pose; whether the capture radii are reachable in practice; grip/trigger thresholds against real Touch controllers; haptics; VR frame rate; comfort and seated-origin placement |

**No headset has ever been connected to this project.** macOS has no OpenXR runtime — the
battery log shows `xrCreateInstance: XR_ERROR_RUNTIME_UNAVAILABLE`, which is correct and
expected, and the sim degrades cleanly to desktop and completes all 12 missions anyway.

**The correct sentence for the thesis is: *the VR implementation is complete and
configured; hardware verification is pending.*** Not "Quest 2 verified".

---

## 9. Bringing VR up on hardware

1. Copy the project to the Windows machine; open in Unity 6000.0.77f1.
2. `XRSetup.Verify` — expect the OpenXR loader assigned for Standalone.
3. Quest over Link, Oculus runtime running.
4. Play. The menu's `VRRuntime` status line should read *"VR: active — OpenXR, right
   controller, left controller"*.
5. **CONTROL CHECK first.** Touch all nine controls, watch the numbers move.
6. Fly L3 end to end before any participant.
7. Then the EEG-under-headset pilot (`VR_EXPERIMENT_CONSIDERATIONS.md` §3) — full cap,
   full session duration, impedances at both ends. **This gates VR data collection.**

Expect the capture radii to need adjustment; they are the single most likely thing to
change after the first real session.

---

## 10. Documentation

New: `COCKPIT_CONTROLS.md`, `CONTROL_SENSITIVITY.md`, `VR_INTERACTION.md`,
`VR_CALIBRATION.md`, `VR_EXPERIMENT_CONSIDERATIONS.md`, this report.

Updated: `FINAL_PROJECT_README.md`, `FINAL_EXPERIMENT_PROTOCOL.md`,
`FINAL_EEG_INTEGRATION.md` (new §5b), `DESIGN_CRITIQUE.md`, `FINAL_TEST_REPORT.md`,
`FINAL_VALIDATION_MATRIX.md`. Regenerated from code: `FINAL_TELEMETRY_SCHEMA.md`,
`FINAL_MISSION_DESIGN.md`.

**The 12 missions are unchanged.** Nothing in this phase altered a mission specification,
a class assignment or a PLI value.
