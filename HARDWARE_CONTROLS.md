# HARDWARE_CONTROLS — physical yoke, throttle, rudder pedals and toe brakes

## 1. What was there before, and why it could not work

The simulator had three ways into the flight model — the keyboard, the mouse-driven
cockpit, and the VR hand grab — and **no path for physical flight controls at all**.

Two things were in the way, and both are now fixed:

**The Input Manager defined no flight axes.** Unity's default map has `Horizontal` and
`Vertical` bound to a gamepad's left stick and nothing else. There was no rudder axis,
no throttle axis and no toe brakes, so a yoke plugged into the lab PC would have moved
the ailerons and elevator at best and nothing at all at worst.

**Any override written by a hardware layer was wiped before it arrived.**
`CockpitInteraction.Update()` called `AircraftController.ClearOverrides()`
unconditionally, every frame, at execution order −50 — i.e. after every input source has
written and before the controller reads. Anything running at order ≥ −50 was silently
discarded. It was invisible in normal use only because `RealCockpit` disables that
component once the GLB cockpit finishes loading, about fourteen seconds in. In the
fallback path, where the GLB fails to load and the component stays enabled for the whole
session, **no programmatic or hardware input could reach the aircraft at all.**

That call was also vestigial: it dated from an older model in which an override *latched*
until explicitly cleared. Ownership now expires by itself after one frame, so nothing
needs clearing except this component's own grab, on release.

## 2. Why the modality is recorded, and must not be pooled

Control response is an **uncontrolled between-subject variable** if some participants fly
with a keyboard and others with a proportional yoke. A keyboard cannot hold 30% aileron;
it can only hold 0% or 100% and ramp between them. So keyboard tracking error, control
jerk and the `ctrl_*` covariates are **not comparable** with hardware ones, and pooling
the two would put an interface difference into the middle of a workload contrast.

`session.json` therefore records `input_modality`, `vr_status`, the connected device
names and the full per-axis calibration. Analysis must treat modality as a factor, not as
noise.

## 3. The two rules

**It stands down when it is not wanted.** No joystick connected, an axis not explicitly
enabled, or a headless harness currently driving (`SimDriver`) → `HardwareInput` writes
nothing. This matters more than it sounds: many devices report a constant non-zero value
on an axis that is unplugged or uncalibrated, and an axis that wrote unconditionally
would fly the aeroplane into the ground during a battery run while the log showed a
perfectly innocent `in_pitch`.

**A present axis writes every frame.** This is the opposite of the rule for the cockpit's
virtual levers — `PhysicalControl` writes only while grabbed and otherwise *mirrors* the
aircraft — and the difference is not arbitrary. A virtual lever is a picture of the
throttle; a real lever **is** the throttle. Its physical position is the commanded value
whether or not a hand is on it, so it must write continuously or the pilot could never
command idle. Enabling a hardware axis therefore takes that axis away from the keyboard,
deliberately and visibly.

## 4. The axes

| Role | Input Manager axis | Kind | Default shaping |
|---|---|---|---|
| Yoke pitch | `HwPitch` | centred | dead zone 0.04, expo 1.4 |
| Yoke roll | `HwRoll` | centred | dead zone 0.04, expo 1.4 |
| Rudder pedals | `HwYaw` | centred | dead zone 0.08, expo 1.2 |
| Throttle lever | `HwThrottle` | slider | rescaled −1..1 → 0..1 |
| Left toe brake | `HwBrakeLeft` | slider | rescaled −1..1 → 0..1 |
| Right toe brake | `HwBrakeRight` | slider | rescaled −1..1 → 0..1 |

**Dead zone** exists because real hardware does not return exactly to centre; without it
the aeroplane creeps. **Expo** gives finer control near centre, which a yoke wants and a
throttle does not. Both are applied in `HwAxis.Read()` rather than in the Input Manager,
so the calibration lives in one place and is written into the session record.

Toe brakes are reduced to `max(left, right)`: the flight model has a single brake value,
so **differential braking is not modelled**, and pretending to steer with the pedals
would be a fiction.

The *axis numbers* in `ProjectSettings/InputManager.asset` are a guess about the device
and are meant to be re-bound on the lab PC. What matters is that the **names** exist —
`Input.GetAxisRaw` throws on an undefined name, which would take the whole hardware path
down.

## 5. Setting it up on the lab PC

1. Plug in the hardware **before** launching.
2. Launch and open **CONTROL CHECK** from the menu (or run with `-controlcheck`).
3. Watch the live axis values while moving each control through its full travel. Note
   which Unity axis number responds; if it is not the default, change it in the Input
   Manager for that named axis.
4. Enable the axes you actually have. An axis that has never been calibrated stays
   **disabled** rather than being assumed to be centred.
5. Set inversion so that: pull back = nose up, right yoke = roll right, forward lever =
   more power, right pedal = nose right.
6. The calibration is stored per machine in `PlayerPrefs` and echoed into every
   `session.json`.

**Do this once per lab PC, and re-check it at the start of every session** — a dead axis
discovered after an hour of EEG is an hour of EEG wasted. That forty-second check is the
entire reason the control-check bench exists.

## 6. What is verified, and what is not

**Verified by the automated control battery (50 checks):** every cockpit control's
wiring, direction, gearing, detents, ownership expiry, reset, and frame-rate
independence (a step input produces 0.614 at 30, 60 and 90 FPS against an analytic
0.614; a naive per-frame `Lerp` would have produced roughly 0.05 / 0.10 / 0.15).

**Not verified, and cannot be without the hardware:** that a particular yoke's axis
numbering matches the defaults; that its travel maps comfortably; that the pedals are
reachable; that the throttle detent lines up. All of that needs the device on the desk.
The architecture is ready; the binding is a bench task.
