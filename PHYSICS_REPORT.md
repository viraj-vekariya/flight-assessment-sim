# PHYSICS REPORT — 3 September 2026

Asked: has the flight model been gutted since Phase 4, and can it be made the best it can be?

**Short answer: it was not gutted.** The current model is Phase 4's, plus a wind layer, trim,
an analog brake and one new aerodynamic term. Two things in it *were* wrong, both in the
lateral axis — which is exactly where it felt wrong — and both are now corrected from
published Cessna 172 stability derivatives rather than tuned by feel.

---

## 1. Is it Phase 4's physics?

Diffed `CessnaPhysics.cs` against `FlightAssessmentSim copy - Phase 4`. Everything Phase 4
had is still there and unchanged: the lift and drag build-up, the stall model, the control
torques, the rate damping, the ground suspension and tyre model. What was **added**:

| Addition | Effect when unused |
|---|---|
| Wind (`WindModel`) | returns exactly zero with no wind configured — bit-identical |
| Elevator trim | zero unless trimmed |
| Analog brake (`brakeInput01`) | replaces an on/off brake |
| Readouts: ground speed, drift, sideslip | no forces |
| **Vertical-fin yaw stiffness (`finVolume`)** | **the one real aerodynamic change** |

So with no wind and no trim, the *only* thing that can make it fly differently from Phase 4
is the fin term — which is measured directly below rather than argued about.

## 2. Why Phase 4 felt like it "went sideways" and this does not

Measured, same aileron input, same speed:

| | bank | heading change | **sideslip** |
|---|---|---|---|
| `finVolume = 0` (Phase 4) | 25° | 28° | **−15.3°** |
| `finVolume = 17` (as found) | 40° | 20° | **−0.9°** |

**Phase 4 flew its turns skidding sideways with 15° of sideslip.** It had no directional
stability at all, so the aeroplane slid through the air with the nose well off the flight
path. That slide is almost certainly the "going side to side" that is missed. It was not a
feature — an aeroplane that skids 15° through every turn is wrong — but removing it removed
a sensation, and nothing replaced it because the fin was set too stiff (see below).

## 3. What the aeroplane actually does now

A new battery, `PhysicsTestHarness` (`-physicstest`), flies defined manoeuvres and judges
them against published C172S figures and closed-form flight mechanics. **30 checks, 0 problems.**

| Measured | Sim | Real C172S |
|---|---|---|
| Stall, 1g clean | **23.9 m/s (46 kt)** | 48 kt |
| Stall, full flap (from coefficients) | **21.3 m/s (41 kt)** | 40 kt |
| Climb, full power | **4.4 m/s (866 fpm)** | ~730 fpm |
| Lift-off | **55 kt after 241 m** | ~55 kt, ~300 m |
| Roll rate, full aileron | **53 °/s**, helix pb/2V = 0.094 | 45–60 °/s, ~0.07 |
| Turn rate vs `g·tan φ / V` | **0.82 – 1.30 ×** | 1.0 × |
| Sideslip, full rudder | **24°**, washes to 0.2° in 4 s | ~20–25° |

Bank produces a turn in both directions, at the right rate, and the ground track curves the
correct way. Stall occurs within 0.5% of the 1g theoretical speed and recovers hands-off.

## 4. The two things that were wrong, and the fixes

### Fin stiffness was 40% too high — `finVolume` 17 → 12.2

The term is applied as `torque = q · sin β · finVolume`, so `finVolume` has units of S·b and
is the directional stability derivative scaled by the aeroplane's own geometry:

```
finVolume = wingArea × span × Cn_β = 16.2 × 11.0 × Cn_β
```

A 172's `Cn_β` is about **0.069 /rad**. The value found, 17, implies 0.095 — a much stiffer
fin than the real aeroplane, which is why it held a turn almost perfectly coordinated with
no rudder and killed sideslip faster than a 172 does. Now **12.2**, derived.

Effect: full-rudder sideslip went from 16° to **24°** — the aeroplane can be slipped
sideways again, properly, which is the sensation that was missing.

### There was no dihedral effect at all — `dihedralVolume` added

The model had **no roll-from-sideslip coupling whatsoever**. Same construction:

```
dihedralVolume = wingArea × span × |Cl_β| = 16.2 × 11.0 × 0.089 = 15.9
```

A 172 is a high-wing aeroplane and its `Cl_β` is strongly negative. Measured, with full
right rudder held for 3.5 seconds:

| | resulting bank | sideslip |
|---|---|---|
| Without dihedral | **1.1°** | −20.0° |
| With dihedral | **64.0°** | −21.3° |

Before, 20° of sideslip could be held with the wings dead level. That is not an aeroplane.
With it:

* a slip needs opposite aileron, so it feels like a slip;
* a crosswind landing needs the wing-low technique;
* the aeroplane has its two lateral modes — hands-off after a disturbance it now shows a
  **slow spiral divergence**, 20° → 29° of bank over 25 s, which is exactly how a 172
  behaves and is what makes it need flying rather than pointing.

## 5. What was NOT wrong — and the mistake I nearly made

The first roll measurement said **27 °/s**, which is sluggish for a 172, and the obvious
move was to raise roll power. That number was wrong: it let the bank grow past 60° inside
the sample window, where gravity and sideslip change the answer. Measured properly, from a
body-axis rate while the bank is small, it is **53 °/s** — correct.

Had I tuned to the bad measurement I would have roughly doubled the roll power of an
aeroplane that was already right. **The roll authority is unchanged.**

## 6. Why the yoke feels dead on the runway

Control authority scales with the square of airspeed, which is correct — a parked aeroplane's
ailerons do nothing. In numbers:

| speed | control power |
|---|---|
| 0 kt | 0% |
| 19 kt | 7% |
| 29 kt | 16% |
| 49 kt | 43% |
| 74 kt | 100% |

So moving the yoke while stopped or taxiing *should* do nothing to the aeroplane. That is
not a fault, and it is the most likely reason it feels unresponsive on the ground.

## 7. Known limitations, recorded not hidden

**Thrust does not fall off with airspeed.** `ThrustN = maxThrust × throttle`, constant at any
speed. A real propeller's thrust lapses roughly as power/speed, which is what makes a 172
run out of acceleration near 124 kt. With constant thrust it keeps accelerating until drag
alone catches it, well past anything a 172 can do.

Not fixed here, deliberately: a thrust model changes take-off roll, climb rate and cruise
speed in **every mission**, which would invalidate the timing of the whole verified bank and
the workload model built on it. It is a real improvement and it is a decision to be taken
knowingly, not a change to slip in alongside a lateral-axis fix.

**Ground crosswind handling is too forgiving.** A 39 kt crosswind still holds the centreline
to within 2 m of a 15 m half-width. The airborne crab behaviour the crosswind missions
actually measure is unaffected.

## 8. Measurement traps hit while doing this

Three, all of which produced confident wrong answers:

1. **Inputs applied in `FixedUpdate`.** The controller's override expires after one rendered
   frame; batch mode runs many frames per physics step, so every input silently read zero
   and the first report was a page of `0.0` that looked like an aeroplane with no
   aerodynamics. Inputs are asserted in `Update`.
2. **The controller only runs in the flying state.** Outside it, `AircraftController` zeroes
   every input and returns. A battery that never enters that state measures nothing.
3. **A stale report read as a current result.** The wind battery's 300 s hard timeout fired
   mid-run on a cold project, leaving the *previous* run's report on disk looking current —
   the same trap as the stale render earlier in this project. Timeouts raised to 900 s.

## 9. Consequence for the experiment

Both corrections change lateral handling, which the crosswind axis measures. The mission
bank and the wind battery were re-run in full afterwards. **The crosswind missions should be
re-piloted before their workload predictions are treated as calibrated** — the aeroplane now
requires aileron in a slip where it previously did not, which is a change in the motor
demand of exactly those trials.
