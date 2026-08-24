# CONTROL_SENSITIVITY

Every number that decides how a hand movement becomes an aircraft input, and why it is
that number. These values are experimental parameters, not cosmetics — changing one
changes the motor demand of every mission, so treat this file as part of the protocol.

Authoritative source: `CockpitControlRig.cs` (specs) and `PhysicalControl.cs` (the law).

---

## 1. The tuning target

Two failure modes bracket the design:

* **Too sensitive** — the participant fights the aeroplane, control becomes a precision
  task, and the EEG records motor effort that the mission design never asked for.
* **Too heavy** — the participant cannot achieve the tolerances the missions score
  against, and failure stops measuring cognition.

The controls are tuned toward **predictable, forgiving and repeatable**, because the
independent variable in this experiment is cognitive demand, not stick-and-rudder skill.
Where realism and measurement conflicted, measurement won, and the compromise is
documented.

---

## 2. The numbers

| Control | Kind | Travel | Capture radius | τ (smoothing) | Centred | Notes |
|---|---|---|---|---|---|---|
| Yoke | Yoke (2-axis) | 0.16 m fore/aft, 0.18 m lateral | 0.13 m | 0.045 s | yes | exponent 1.25, dead zone 0.02 |
| Throttle | Lever | 0.085 m | 0.055 m | 0.035 s | no | idle→full in one push |
| Trim wheel | TrimWheel | 0.12 m | 0.055 m | 0.060 s | yes | long travel on purpose |
| Flap lever | DetentLever | 0.075 m | 0.050 m | 0.050 s | no | detents 0 / 0.5 / 1.0 |
| Brake | SpringLever | 0.055 m | 0.050 m | 0.030 s | no | springs to zero on release |
| Carb heat | Toggle | 0.03 m | 0.042 m | 0.050 s | no | discrete |
| Fuel selector | Rotary | 0.09 m | 0.048 m | 0.060 s | no | 3 positions |
| Load shed | Toggle | 0.03 m | 0.036 m | 0.040 s | no | switches sit close |
| Alt static | Toggle | 0.03 m | 0.036 m | 0.040 s | no | switches sit close |

### Travel

**Yoke — 16 cm fore/aft, 18 cm lateral.** Close to a real 172's yoke throw. Long enough
that a 1 cm hand tremor is ~6 % of travel rather than a full deflection; short enough to
reach the stops without leaning. Verified: half travel produces 0.41 deflection (the 1.25
exponent, see below), full travel produces exactly ±1.00.

**Trim — 12 cm, the longest travel of any lever.** Deliberate. Trim is a fine adjustment,
and a short travel would turn it into a precision task — which is exactly the motor
workload this design is trying to remove. Long travel makes trim easy, which is the
point of adding it at all.

**Throttle — 8.5 cm.** Idle to full in one comfortable push, no repositioning.

**Brake — 5.5 cm.** Short, because it springs back and is held under tension.

**Toggles — 3 cm.** Just enough for the visual to read as "moved".

### Capture radius

The radius within which a grab claims that control. It trades reach-comfort against
mis-grabs:

* **Yoke, 13 cm** — the biggest control and the one reached for without looking, because
  the eyes are outside or on the panel. Generous on purpose.
* **Levers, 5–5.5 cm** — comfortable but not overlapping.
* **Switches, 3.6–4.2 cm** — small, because load shed and alt static sit close together
  and grabbing the wrong one is a silent data error.

When two controls are in range the **nearest** wins, and one control can be held by only
one hand at a time.

> **Unverified in VR.** These radii were chosen from the model geometry and tested with
> the mouse. Whether they feel right with a tracked controller needs a headset — this is
> the single most likely thing to need adjustment after the first hardware session.

### Smoothing (τ)

Applied as a frame-rate-independent exponential filter:

```csharp
float k = 1f - Mathf.Exp(-Time.deltaTime / spec.smoothingTau);
smoothed = Mathf.Lerp(smoothed, raw, k);
```

**This form is required, not stylistic.** The naive `Lerp(a, b, 0.1f)` per frame gives a
different response at 30, 60 and 90 FPS — so control feel would differ between a
desktop participant at 60 FPS and a VR participant at 90 FPS, and between a machine
under load and one that is not. Frame rate would silently become an experimental
variable. With the exponential form, τ is a time constant and the response is identical
at any frame rate.

Values are small — 30 to 60 ms — enough to remove tracking jitter, too short to feel like
lag. Brakes are the shortest (30 ms) because they must feel immediate; trim and the
rotary are the longest (60 ms) because they are deliberate, unhurried actions.

### Response exponent — yoke only, 1.25

```
output = sign(x) · |x|^1.25
```

A mild expo. Near centre the yoke is slightly less sensitive, which is where fine
attitude holding happens; full travel still reaches exactly ±1.0. At half travel the
output is 0.5^1.25 = 0.42 (measured 0.41 after the dead zone).

Chosen at 1.25 rather than the 1.5–2.0 typical of game controllers because a large expo
makes the control **non-linear in a way the participant must learn**, and learning is a
between-trial confound. 1.25 takes the edge off centre without creating a skill to
acquire.

### Dead zone — yoke only, 0.02

2 % of travel. Removes tracking jitter and hand tremor at the neutral position so the
aeroplane does not wander while the participant holds the yoke still. Small enough not to
be felt as a notch.

---

## 3. Trim authority — 0.35

```csharp
ElevatorCmd = clamp(pitchInput − trim × 0.35, −1, +1)
```

Full nose-up trim biases the elevator by 0.35 of full deflection. Enough to trim off
cruise stick force at any of the speeds the missions fly, not enough for trim alone to
fly the aeroplane — so trim is a workload reducer, not an autopilot.

Verified: trim +1 → `elevatorCmd = −0.35`; trim −1 → `elevatorCmd = +0.35`.

### Why the workload argument matters more than the realism argument

Before trim existed, every mission required a sustained elevator force for its whole
duration, and **the size of that force differed between missions** — because it depends on
speed, flap setting and configuration, which the mission design varies deliberately.
That is an uncontrolled motor load that correlated with mission class by accident.

Trim lets a participant remove it in any mission. What was an uncontrolled between-mission
difference becomes a controllable, logged pilot action.

**Effect on the Predicted Load Index:** one full point of `ManualControl` demand (weight
0.05) is worth **1.25 PLI points**. Even the extreme assumption — that trim reduces manual
demand by a full point in *every* mission — is a uniform shift, and a uniform shift cannot
change a difference:

```
                 current        if manual −1 everywhere
LOW           12.8 – 29.2          11.5 – 28.0
MEDIUM        49.5 – 57.5          48.2 – 56.2
HIGH          76.2 – 87.0          75.0 – 85.8

class gaps:   LOW→MED 20.3   MED→HIGH 18.7      unchanged
```

**Decision: the PLI values stand as published.** Trim removed a confound; it did not
alter the load ordering. Recorded here and in `DESIGN_CRITIQUE.md`.

---

## 4. Release behaviour

Two rules, and the first is the one that matters:

**A control writes to the aircraft only while it is held.** When released it calls
`FollowAircraft()` and tracks whatever the aircraft input actually is. Without this, a
lever left alone would keep writing its last value every frame, the keyboard would be
permanently overridden, and ownership would never lapse. (This was a real bug, caught by
the control tests — see `FINAL_TEST_REPORT.md`.)

**The yoke holds its position on release.** A real yoke returns toward neutral under
aerodynamic force; here it stays where it was left. This is a deliberate approximation:

* The alternative — snapping to neutral — makes releasing the yoke *itself* a large
  control input, so the participant can never let go, and the interface becomes a
  physical endurance task.
* Holding position means release is neutral with respect to the aircraft, which is what
  trim is for and what a trimmed aeroplane does.

Documented rather than hidden, because it is a departure from the real aircraft.

**The brake is the exception**: it is a `SpringLever` and does return to zero on release,
because a brake that stayed applied would be both unrealistic and dangerous to the task.
Verified: releases to 0.00.

---

## 5. Control ownership

`AircraftController` is the only writer of aircraft input, and ownership **expires by
frame** rather than latching:

```csharp
const int GraceFrames = 1;
bool Live(int frame) => Time.frameCount - frame <= GraceFrames;
```

Write to an axis and you own it for that frame plus one. Stop writing and the keyboard
takes over on the next frame — no explicit release call, so there is no `ClearOverrides()`
that someone can forget to call. (The previous build had exactly that bug: a latch with
no live caller, so the first VR touch killed the keyboard for the rest of the session.)

Verified both directions: a single write takes ownership; ownership expires without
further writes.

---

## 6. Changing these values

If you change anything in the table:

1. Run the control tests: `-executeMethod ControlTestHarness.Run -controltest`. Target 0 problems.
2. Run the 12-mission battery: `-executeMethod PlayCapture.RunMissionTest -missiontest`. Target 12/12.
3. **Re-examine the PLI** if the change plausibly alters manual demand — use the 1.25
   points-per-manual-point figure in §3.
4. Update this file and `COCKPIT_CONTROLS.md`.
5. **Do not change these values between participants.** Control gearing is part of the
   apparatus. If it changes mid-study, data collected before and after are not comparable
   and must be analysed as separate groups.
