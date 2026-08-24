# FINAL_MISSION_DESIGN — the mission bank

> **This file is GENERATED from `Assets/Scripts/MissionLibrary.cs` by
> `Assets/Editor/DocGen.cs`. Do not edit it by hand — edit the mission set
> and regenerate, so the document and the simulator can never disagree.**

Generated: 2026-08-24 15:08

Every mission is **300 s** long with a **60 s in-task baseline** at its head. Duration is held constant across all three
workload classes on purpose — see `FINAL_EXPERIMENT_PROTOCOL.md`.

## The bank, and the session

The bank holds **42 missions** on **two experimental axes**:

* **Cognitive axis — 36 missions.** 4 flight phases x 3 workload
  classes x 3 interchangeable variants. Manual demand is MATCHED within each
  phase row, which is what licenses reading a class difference as cognitive rather
  than muscular.
* **Psychomotor-integrated axis — 6 missions.** Crosswind take-offs and
  landings at graded crosswind levels. Deliberately NOT on the Low/Medium/High
  scale: crosswind raises manual demand by construction, so putting it there would
  break the matching the cognitive axis depends on. Analysed separately, with the
  control-activity covariates, and its cognitive component isolated as a discrete
  continue-or-abandon decision against a stated crosswind limit.

**A participant flies TWELVE**, not 42: one variant index per phase row,
rotated by Latin square. 42 x 300 s is over three hours of flying inside one
EEG session, and fatigue would dominate every contrast the study exists to measure.
Within a phase row all three classes share one variant, so the Low-Medium-High
contrast is always variant-matched; across rows the participant meets different
variants, so variant is not perfectly nested in participant. See `MISSION_BANK_DESIGN.md`.

## Final mission table

| ID | Axis | Phase | Class | v | Mission | Cognitive mechanism | Main abnormality | Time pressure | Multitasking | Decision complexity | PLI |
|----|------|-------|-------|---|---------|---------------------|------------------|---------------|--------------|---------------------|-----|
| L1 | cog | Takeoff | LOW | 1 | Normal departure from the stand | reference - single-threaded procedure | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 28 |
| L2 | cog | Climb | LOW | 1 | Steady assigned climb | reference - steady-state tracking | none (normal operations) | ○○○○ | ●○○○ | ○○○○ | 18 |
| L3 | cog | Cruise | LOW | 1 | Straight-and-level cruise hold | reference - steady-state tracking | none (normal operations) | ○○○○ | ○○○○ | ○○○○ | 13 |
| L4 | cog | Approach | LOW | 1 | Routine approach and landing | reference - rehearsed approach | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 29 |
| M1 | cog | Takeoff | MEDIUM | 1 | Departure with traffic and a two-part clearance | visual search + procedure | traffic + weather | ●●○○ | ●●●○ | ●●○○ | 53 |
| M2 | cog | Climb | MEDIUM | 1 | Cabin door opens on the climb-out | startle without danger | `DoorOpen` | ●●○○ | ●●●○ | ●●○○ | 50 |
| M3 | cog | Cruise | MEDIUM | 1 | Multi-part ATC re-clearances in the cruise | working memory (clearance turnover) | none (normal operations) | ●●○○ | ●●●○ | ●○○○ | 50 |
| M4 | cog | Approach | MEDIUM | 1 | Deteriorating weather and a late runway change | degraded perception + re-planning | weather | ●●○○ | ●●○○ | ●●○○ | 58 |
| H1 | cog | Takeoff | HIGH | 1 | Amended clearance, blocked runway, departure conflict | concurrency under time pressure | 3x traffic + weather | ●●●● | ●●●● | ●●●○ | 87 |
| H2 | cog | Climb | HIGH | 1 | Alternator failure on the departure climb | forward reasoning about a depleting resource | `AlternatorFailure` | ●●●○ | ●●●○ | ●●●● | 79 |
| H3 | cog | Cruise | HIGH | 1 | Unreliable instruments in the cruise | self-consistent wrong information | `StaticBlocked` | ●●●○ | ●●●● | ●●●○ | 86 |
| H4 | cog | Approach | HIGH | 1 | Engine failure on the approach | irreversible commitment under a clock | `EngineFailure` | ●●●● | ●●●○ | ●●●○ | 76 |
| L1V2 | cog | Takeoff | LOW | 2 | Normal departure with a routine readback | reference - single-threaded procedure with one acknowledgement | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 28 |
| L2V2 | cog | Climb | LOW | 2 | Assigned climb with one amendment | reference - steady-state tracking with one target change | none (normal operations) | ○○○○ | ○○○○ | ○○○○ | 13 |
| L3V2 | cog | Cruise | LOW | 2 | Level cruise with one heading change | reference - steady-state tracking with one turn | none (normal operations) | ○○○○ | ○○○○ | ○○○○ | 13 |
| L4V2 | cog | Approach | LOW | 2 | Normal landing with a configuration call | reference - rehearsed approach with a routine configuration change | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 32 |
| M1V2 | cog | Takeoff | MEDIUM | 2 | Runway change before line-up | working memory - replacing a briefed plan | none (normal operations) | ●●○○ | ●●○○ | ●●○○ | 54 |
| M2V2 | cog | Climb | MEDIUM | 2 | Carburettor ice on the climb | diagnosis of a gradual, ambiguous cue | `CarbIce` | ●●○○ | ●●●○ | ●●○○ | 52 |
| M3V2 | cog | Cruise | MEDIUM | 2 | Diversion to an alternate | re-planning against competing constraints | none (normal operations) | ●●○○ | ●●○○ | ●●●● | 54 |
| M4V2 | cog | Approach | MEDIUM | 2 | Late runway change on final | re-planning under time pressure late in a committed approach | none (normal operations) | ●●●● | ●●○○ | ●●○○ | 61 |
| H1V2 | cog | Takeoff | HIGH | 2 | Intersection departure with a performance decision | forward reasoning about a physical margin under a clock | traffic | ●●●● | ●●●○ | ●●●● | 87 |
| H2V2 | cog | Climb | HIGH | 2 | Partial power loss after take-off — the turn-back decision | irreversible commitment against a shrinking margin | `EngineRoughness` | ●●●● | ●●●○ | ●●●● | 84 |
| H3V2 | cog | Cruise | HIGH | 2 | Fuel imbalance and tank management | resource management with a self-inflicted cure | `FuelStarvation` | ●●●○ | ●●●● | ●●●● | 84 |
| H4V2 | cog | Approach | HIGH | 2 | Flap failure on final — the no-flap approach | re-computing a procedure whose parameters have changed | `FlapMotorFailure` | ●●●○ | ●●●○ | ●●●○ | 79 |
| L1V3 | cog | Takeoff | LOW | 3 | Normal departure to a higher level-off | reference - single-threaded procedure to a higher level-off | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 28 |
| L2V3 | cog | Climb | LOW | 3 | Departure heading and level-off | reference - steady-state tracking with a turn and a level-off | none (normal operations) | ○○○○ | ○○○○ | ○○○○ | 13 |
| L3V3 | cog | Cruise | LOW | 3 | Waypoint navigation | reference - steady-state tracking to displayed targets | none (normal operations) | ○○○○ | ○○○○ | ○○○○ | 15 |
| L4V3 | cog | Approach | LOW | 3 | Normal landing from a closer final | reference - rehearsed approach from a shorter final | none (normal operations) | ●●○○ | ●○○○ | ●○○○ | 35 |
| M1V3 | cog | Takeoff | MEDIUM | 3 | Conditional line-up clearance | prospective memory - holding an instruction until its condition occurs | traffic | ●●○○ | ●●●○ | ●●○○ | 56 |
| M2V3 | cog | Climb | MEDIUM | 3 | Re-clearances and a frequency change | working memory turnover across mixed verbal and numeric items | none (normal operations) | ●●○○ | ●●●○ | ●○○○ | 52 |
| M3V3 | cog | Cruise | MEDIUM | 3 | Traffic search in reduced visibility | visual search and sustained monitoring under degraded input | 3x traffic + weather | ●●○○ | ●●●● | ●○○○ | 53 |
| M4V3 | cog | Approach | MEDIUM | 3 | Traffic on the runway — the go-around | abandoning a committed procedure and executing a rehearsed alternative | traffic | ●●○○ | ●●○○ | ●●○○ | 58 |
| H1V3 | cog | Takeoff | HIGH | 3 | Low-visibility taxi past a hot spot | sustained monitoring under degraded perception + prospective memory | 2x traffic + weather | ●●●○ | ●●●● | ●●●○ | 89 |
| H2V3 | cog | Climb | HIGH | 3 | Traffic and terrain while being re-cleared | prioritisation among simultaneous conflicting demands | traffic | ●●●● | ●●●● | ●●●○ | 85 |
| H3V3 | cog | Cruise | HIGH | 3 | Everything at once in the cruise | concurrency - competing demands with no gaps between them | `EngineRoughness` | ●●●● | ●●●● | ●●●○ | 90 |
| H4V3 | cog | Approach | HIGH | 3 | Late go-around and re-sequence | startle followed immediately by re-planning at high stakes | 2x traffic | ●●●● | ●●●○ | ●●●○ | 80 |
| XT1 | psy | Takeoff | LOW | 1 | Crosswind take-off — light | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision | none (normal operations) | ●●○○ | ●●○○ | ●●○○ | 49 |
| XL1 | psy | Approach | LOW | 1 | Crosswind landing — light | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision | none (normal operations) | ●●●○ | ●●○○ | ●●○○ | 56 |
| XT2 | psy | Takeoff | MEDIUM | 2 | Crosswind take-off — moderate | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision | none (normal operations) | ●●○○ | ●●●○ | ●●●○ | 65 |
| XL2 | psy | Approach | MEDIUM | 2 | Crosswind landing — moderate | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision | none (normal operations) | ●●●○ | ●●●○ | ●●●○ | 70 |
| XT3 | psy | Takeoff | HIGH | 3 | Crosswind take-off — near-limit | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision | none (normal operations) | ●●●○ | ●●●● | ●●●○ | 82 |
| XL3 | psy | Approach | HIGH | 3 | Crosswind landing — near-limit | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision | none (normal operations) | ●●●● | ●●●● | ●●●○ | 85 |

`PLI` = Predicted Load Index (0-100) from the weighted demand model in
`COGNITIVE_LOAD_MODEL.md`. It is a **prediction the experiment tests**, not a result.

## The design grid — phase fully crossed with workload class

Every class appears exactly once in every phase, so a class effect can never be
a phase effect, and every mission has a phase-matched LOW baseline.

| Flight phase | v | LOW | MEDIUM | HIGH | manual demand (L/M/H) |
|---|---|---|---|---|---|
| **Takeoff** | 1 | L1 Normal departure from the stand (PLI 28) | M1 Departure with traffic and a two-part clearance (PLI 53) | H1 Amended clearance, blocked runway, departure conflict (PLI 87) | 2/2/2 |
|  | 2 | L1V2 Normal departure with a routine readback (PLI 28) | M1V2 Runway change before line-up (PLI 54) | H1V2 Intersection departure with a performance decision (PLI 87) | 2/2/2 |
|  | 3 | L1V3 Normal departure to a higher level-off (PLI 28) | M1V3 Conditional line-up clearance (PLI 56) | H1V3 Low-visibility taxi past a hot spot (PLI 89) | 2/2/2 |
| **Climb** | 1 | L2 Steady assigned climb (PLI 18) | M2 Cabin door opens on the climb-out (PLI 50) | H2 Alternator failure on the departure climb (PLI 79) | 2/3/2 |
|  | 2 | L2V2 Assigned climb with one amendment (PLI 13) | M2V2 Carburettor ice on the climb (PLI 52) | H2V2 Partial power loss after take-off — the turn-back decision (PLI 84) | 2/2/3 |
|  | 3 | L2V3 Departure heading and level-off (PLI 13) | M2V3 Re-clearances and a frequency change (PLI 52) | H2V3 Traffic and terrain while being re-cleared (PLI 85) | 2/2/2 |
| **Cruise** | 1 | L3 Straight-and-level cruise hold (PLI 13) | M3 Multi-part ATC re-clearances in the cruise (PLI 50) | H3 Unreliable instruments in the cruise (PLI 86) | 2/2/3 |
|  | 2 | L3V2 Level cruise with one heading change (PLI 13) | M3V2 Diversion to an alternate (PLI 54) | H3V2 Fuel imbalance and tank management (PLI 84) | 2/2/2 |
|  | 3 | L3V3 Waypoint navigation (PLI 15) | M3V3 Traffic search in reduced visibility (PLI 53) | H3V3 Everything at once in the cruise (PLI 90) | 2/2/2 |
| **Approach** | 1 | L4 Routine approach and landing (PLI 29) | M4 Deteriorating weather and a late runway change (PLI 58) | H4 Engine failure on the approach (PLI 76) | 3/3/4 |
|  | 2 | L4V2 Normal landing with a configuration call (PLI 32) | M4V2 Late runway change on final (PLI 61) | H4V2 Flap failure on final — the no-flap approach (PLI 79) | 3/3/4 |
|  | 3 | L4V3 Normal landing from a closer final (PLI 35) | M4V3 Traffic on the runway — the go-around (PLI 58) | H4V3 Late go-around and re-sequence (PLI 80) | 3/3/3 |

Manual demand is matched WITHIN each phase row (spread <= 1 on a 0-4 scale), which
is where the class contrast is made — so a difference inside a row cannot be muscle
activity rather than cognitive load.

## Variant exchangeability (design check)

The variants of a cell are supposed to be interchangeable realisations of it, so
their PREDICTED loads must agree. A variant scoring well above its siblings is not
a second version of that class — it is drifting toward the next one, and since
variant is assigned by participant, a participant's effective class would then
depend on which variant they were given. Tolerance: **8 points on the 0-100 PLI**,
enforced by the mission battery.

| Cell | variant 1 | variant 2 | variant 3 | spread | within tolerance |
|---|---|---|---|---|---||
| Takeoff/LOW | L1 28.0 | L1V2 28.0 | L1V3 28.0 | 0.0 | yes |
| Takeoff/MEDIUM | M1 52.8 | M1V2 54.0 | M1V3 55.5 | 2.8 | yes |
| Takeoff/HIGH | H1 87.0 | H1V2 86.5 | H1V3 88.8 | 2.3 | yes |
| Climb/LOW | L2 18.0 | L2V2 12.8 | L2V3 12.8 | 5.3 | yes |
| Climb/MEDIUM | M2 49.8 | M2V2 51.8 | M2V3 52.3 | 2.5 | yes |
| Climb/HIGH | H2 78.8 | H2V2 84.3 | H2V3 84.5 | 5.8 | yes |
| Cruise/LOW | L3 12.8 | L3V2 12.8 | L3V3 14.8 | 2.0 | yes |
| Cruise/MEDIUM | M3 49.5 | M3V2 54.3 | M3V3 53.3 | 4.8 | yes |
| Cruise/HIGH | H3 86.0 | H3V2 84.0 | H3V3 89.5 | 5.5 | yes |
| Approach/LOW | L4 29.3 | L4V2 31.8 | L4V3 35.0 | 5.8 | yes |
| Approach/MEDIUM | M4 57.5 | M4V2 61.0 | M4V3 58.3 | 3.5 | yes |
| Approach/HIGH | H4 76.3 | H4V2 78.8 | H4V3 80.3 | 4.0 | yes |

## The psychomotor-integrated axis (crosswind)

| ID | Mission | Crosswind | Headwind | Gust | Surface wind | Manual demand | PLI |
|---|---|---|---|---|---|---|---|
| XT1 | Crosswind take-off — light | 2.0 m/s | 3.0 m/s | 0.0 m/s | 034/07 kt | 2 | 49 |
| XL1 | Crosswind landing — light | 2.0 m/s | 3.0 m/s | 0.0 m/s | 034/07 kt | 3 | 56 |
| XT2 | Crosswind take-off — moderate | 4.6 m/s | 3.0 m/s | 0.0 m/s | 057/11 kt | 3 | 65 |
| XL2 | Crosswind landing — moderate | 4.6 m/s | 3.0 m/s | 0.0 m/s | 057/11 kt | 4 | 70 |
| XT3 | Crosswind take-off — near-limit | 7.3 m/s | 3.0 m/s | 2.4 m/s | 068/15G18 kt | 4 | 82 |
| XL3 | Crosswind landing — near-limit | 7.3 m/s | 3.0 m/s | 2.4 m/s | 068/15G18 kt | 4 | 85 |

These PLI values are **not comparable with the cognitive axis's**: they come from
the same weighted model, but the model deliberately weights ManualControl LOW, and
ManualControl is precisely what these missions manipulate. A finding from this axis
is stated as *increased integrated psychomotor/cognitive demand*, never as
*crosswind increased cognitive workload*.

## Class separation (design check)

| Class | n | PLI min | PLI max | PLI mean | manual-control range |
|-------|---|---------|---------|----------|----------------------|
| LOW | 12 | 12.8 | 35.0 | 22.0 | 2-3 |
| MEDIUM | 12 | 49.5 | 61.0 | 54.1 | 2-3 |
| HIGH | 12 | 76.3 | 89.5 | 83.7 | 2-4 |

The classes must not overlap on PLI, and the manual-control ranges should stay
close together — a large manual-control gap between classes would mean an EEG
difference could be muscle activity rather than cognitive load.

---

## Full mission specifications

### L1 — Normal departure from the stand

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 1 of 3 |
| Cognitive mechanism | reference - single-threaded procedure |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 28.0 / 100 |

**Objective** — Taxi to runway 01, hold short, take off and climb to 600 m on runway heading.

**Brief to the participant** — You are on stand 1, engine running. Taxi via taxiway A to the holding point for runway 01, hold short, and wait for your take-off clearance. Then line up, take off, and climb straight ahead to 600 m. Clear day, no other traffic.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. |
| 88 | ±6 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — climb runway heading to 600 m. |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand | █ 1 |
| decision complexity | █ 1 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | █ 1 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty |  0 |
| communication | █ 1 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 30, physical 25, temporal 20, performance 25, effort 30, frustration 15 → RTLX 24. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The reference condition for the take-off phase, and the mission that establishes what a take-off costs before anything is added. There is real work — follow a taxi route by sign and marking, respect a hold-short line, wait for a clearance, then fly an accurate climb — but it is all single-threaded, unhurried and fully specified in advance. Procedural load is 2 because the sequence genuinely has to be executed in order; everything the model weights heavily is at 0 or 1.

**EEG relevance** — The phase-matched baseline for M1 and H1. Its first 60 s is taxiing, which is the same baseline activity as the other two missions in this row, so the class contrast within the row is clean. Taxi is also the lowest-motion segment of any non-cruise mission, which makes it a useful low-artifact reference.

**Expected errors** — Taxiing past the hold-short line; taking off without waiting for the clearance; drifting off the runway heading in the climb.

**Success criteria** — Hold short respected, take-off clearance received before entering the runway, airborne, and the assigned climb held.

**Failure conditions** — Runway incursion (entering the runway uncleared); crash; not airborne.

**Aviation basis** — Normal taxi and departure. The hold-short line is the standard mandatory runway-holding position, and crossing it uncleared is a runway incursion — the surface-movement error category the marking exists to prevent.

**Approximations / not modelled** — Taxi is about 330 m and roughly a minute, not the 10-15 minutes of an airline gate-to-runway sequence. The stand is placed close to the runway (a small-field layout) rather than the aeroplane being sped up, so taxi speed, steering and braking stay realistic. See the duration analysis in FINAL_EXPERIMENT_PROTOCOL.md.

**Required event markers** — `MISSION_START`, `PHASE_CHANGE`, `MISSION_END`

---

### L2 — Steady assigned climb

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 1 of 3 |
| Cognitive mechanism | reference - steady-state tracking |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 18.0 / 100 |

**Objective** — Hold the departure leg, then fly one assigned climb accurately.

**Brief to the participant** — Departure leg, 400 m, runway heading. Hold height and heading until ATC clears you to climb, then climb and level off accurately. Clear day.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 62 | ±0 | AltitudeChange | altitude → 900 m |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand |  0 |
| decision complexity |  0 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | █ 1 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load | █ 1 |
| uncertainty |  0 |
| communication | █ 1 |
| error consequence |  0 |

**Pre-registered expected NASA-TLX** — mental 25, physical 25, temporal 15, performance 25, effort 30, frustration 12 → RTLX 22. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — A single continuous tracking task with one unhurried, single-item instruction. The phase-matched reference for M2 and H2. Kept non-trivial by a tight altitude tolerance (±80 m) and a real level-off, so it measures low workload rather than disengagement.

**EEG relevance** — The quietest airborne mission after L3. One isolated, time-stamped target change gives a clean within-mission before/after contrast at the lowest possible background load.

**Expected errors** — Overshooting the level-off; heading drift during the climb.

**Success criteria** — The new level acquired and held; heading maintained; no crash.

**Failure conditions** — Crash; failure to start the climb within 60 s of the instruction.

**Aviation basis** — Routine departure climb under ATC.

**Approximations / not modelled** — No autopilot exists in this simulator, so the climb is hand-flown. That raises manual demand relative to a real departure and is why ManualControl is scored 2 rather than 1.

**Required event markers** — `MISSION_START`, `TARGET_CHANGE`, `MISSION_END`

---

### L3 — Straight-and-level cruise hold

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 1 of 3 |
| Cognitive mechanism | reference - steady-state tracking |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 12.8 / 100 |

**Objective** — Hold 700 m and heading 000° in calm air for the whole run.

**Brief to the participant** — Cruise, calm air, no traffic, no radio. Fly it accurately — altitude within 70 m and heading within 12° — and nothing else will be asked of you.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CRUISE — maintain 700 m, heading 000°. Calm air. |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand |  0 |
| decision complexity |  0 |
| working memory | █ 1 |
| attention switching |  0 |
| situation awareness | █ 1 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load |  0 |
| uncertainty |  0 |
| communication |  0 |
| error consequence |  0 |

**Pre-registered expected NASA-TLX** — mental 20, physical 20, temporal 10, performance 25, effort 25, frustration 10 → RTLX 18. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The floor of the whole design: one continuous two-axis tracking task with a fixed target, no secondary task, no communication and no decisions. Demand is almost entirely in the manual/perceptual loop, which is precisely the demand that must NOT differ between classes. Tolerances are the tightest in the set on purpose — that keeps the pilot engaged and guards against this drifting into underload, where workload indices invert and the class label would be meaningless.

**EEG relevance** — The cleanest within-subject reference epoch: 300 s of stationary demand with no discrete events at all. Frontal-midline theta should be lowest and parietal alpha highest of the twelve. Also the correct normalisation reference for the cruise row.

**Expected errors** — Slow altitude drift; heading wander after inattention; over-controlling.

**Success criteria** — Runs 300 s without a crash. Performance is continuous (time in tolerance, RMS altitude and heading error), not pass/fail.

**Failure conditions** — Crash, or more than 30 s beyond ±300 m of the assigned altitude.

**Aviation basis** — Normal cruise. Cruise is the documented workload trough of a normal flight.

**Approximations / not modelled** — Hand-flown throughout (no autopilot), which raises manual demand relative to a real cruise leg.

**Required event markers** — `MISSION_START`, `MISSION_END`

---

### L4 — Routine approach and landing

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 1 of 3 |
| Cognitive mechanism | reference - rehearsed approach |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 29.3 / 100 |

**Objective** — Hold the inbound leg, then fly a stable approach and land on the centreline.

**Brief to the participant** — You are 12 km out, lined up with the runway at 500 m. Hold height and heading until cleared to descend, then fly a normal approach and landing. Calm wind, unlimited visibility, no traffic.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | INBOUND — maintain 500 m, heading 000°. Expect a visual approach. |
| 62 | ±0 | Message | CLEARED TO LAND RUNWAY 01 — descend at your discretion, wind calm. |
| 150 | ±0 | ConfigCall | APPROACH FLAPS — as required   [F] |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand | █ 1 |
| decision complexity | █ 1 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ███ 3 |
| procedural load | █ 1 |
| uncertainty |  0 |
| communication |  0 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 30, physical 32, temporal 25, performance 32, effort 38, frustration 15 → RTLX 29. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The honest treatment of a landing at the LOW class, and the control that makes the whole design testable. A landing is an intrinsic workload peak — physiologically the highest-demand phase of a normal flight — so labelling it LOW would be wrong if the demand were cognitive. It is not: a clean visual approach in nil wind is a continuous, highly practised PERCEPTUAL-MOTOR task with no diagnosis, no ambiguity, no concurrent task, and one decision that never becomes live. L4 therefore has the highest manual demand in the LOW class and the lowest scores on everything the model weights heavily.

**EEG relevance** — The critical control for the movement/effort confound, and the phase-matched baseline for M4 and H4. If the workload indices separate L4 from H4 despite closely matched psychomotor demand (3 vs 4), the separation is cognitive rather than muscular.

**Expected errors** — High or low on profile; late flare; drifting off centreline; floating.

**Success criteria** — Touchdown on the runway within ±16 m of the centreline, sink < 3.5 m/s.

**Failure conditions** — Crash; landing off the runway; not landed within 300 s.

**Aviation basis** — Normal visual approach and landing.

**Approximations / not modelled** — No runway lighting or PAPI, no ATC sequencing; the approach is visual and unaided. Note `alt_err_m` is only meaningful during the level segment of this mission — see TELEMETRY_SCHEMA.

**Required event markers** — `MISSION_START`, `TOUCHDOWN`, `MISSION_END`

---

### M1 — Departure with traffic and a two-part clearance

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 1 of 3 |
| Cognitive mechanism | visual search + procedure |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 700 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.35 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 52.8 / 100 |

**Objective** — Taxi with other traffic on the move, absorb a two-part departure clearance, and depart on the assigned heading and level.

**Brief to the participant** — Stand 1, engine running, light rain and reducing visibility. Taxi via alpha to the holding point for runway 01. There is other traffic moving on the airfield. ATC will pass a departure clearance you must read back before you are cleared to go.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. TRAFFIC IS MOVING. |
| 6 | ±0 | Weather | intensity 0.30 for 250 s |
| 40 | ±10 | Traffic | TRAFFIC CROSSING RUNWAY 01 AHEAD |
| 78 | ±8 | Readback | DEPARTURE CLEARANCE: right heading 040°, climb 700 m — ACKNOWLEDGE  *(respond within 7.0 s)* |
| 96 | ±8 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — no delay, traffic 4 miles final. |
| 150 | ±0 | HeadingChange | heading → 40 |
| 158 | ±0 | AltitudeChange | altitude → 700 m |
| 230 | ±12 | Probe | OPS CHECK — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | ██ 2 |
| attention switching | ███ 3 |
| situation awareness | ██ 2 |
| perception | ███ 3 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty | █ 1 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 55, physical 30, temporal 50, performance 45, effort 55, frustration 35 → RTLX 45. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Adds two things to L1 and nothing else: something to LOOK FOR outside, and a clearance with two items to hold. The traffic is a real aeroplane on a scripted path, so the pilot's scan must leave the instruments and the taxi route to find it, and reducing visibility makes that search cost more. The departure clearance must be read back before the take-off clearance is issued, and its two items are not flown until after rotation — so they sit in working memory across the busiest part of the mission. Manual demand is identical to L1, so the step up is attentional and mnemonic.

**EEG relevance** — Two well-separated discrete onsets (the traffic sighting, the clearance) against a phase-matched baseline shared with L1 and H1. The interval between the read-back and flying the two items is a working-memory maintenance window with no other event in it.

**Expected errors** — Missing the crossing traffic; entering the runway before the read-back; flying only the heading and forgetting the level, or vice versa.

**Success criteria** — Hold short respected, clearance acknowledged, airborne, and both clearance items flown.

**Failure conditions** — Runway incursion; crash; clearance never acknowledged.

**Aviation basis** — Departure with surface traffic and a read-back-required clearance. Read-back/hear-back is a recognised error source, and channel load rises with the number of elements per transmission.

**Approximations / not modelled** — The crossing aircraft follows a fixed path and cannot collide — conflict is scripted geometry, not emergent, so every participant meets it at the same (jittered) instant and the EEG epoch is comparable. 'Read-back' is a single acknowledgement keypress, so it measures acceptance LATENCY, not read-back accuracy; whether the items were retained is inferred from whether the aircraft was actually flown to them.

**Required event markers** — `MISSION_START`, `TRAFFIC_ONSET`, `ATC_MESSAGE`, `MISSION_END`

---

### M2 — Cabin door opens on the climb-out

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 1 of 3 |
| Cognitive mechanism | startle without danger |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 49.8 / 100 |

**Objective** — Fly the assigned climb accurately, and keep flying it when startled.

**Brief to the participant** — Departure leg, 400 m, runway heading. ATC will clear you to climb. Whatever happens, fly the aeroplane first.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 62 | ±0 | AltitudeChange | altitude → 900 m |
| 105 | ±22 | SystemFailure | `DoorOpen` severity 1.00 |
| 165 | ±10 | Probe | OPS CHECK — respond  *(respond within 5.0 s)* |
| 205 | ±12 | HeadingChange | heading → 330 |
| 250 | ±10 | Probe | OPS CHECK — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | █ 1 |
| attention switching | ███ 3 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ███ 3 |
| procedural load | █ 1 |
| uncertainty | ███ 3 |
| communication | █ 1 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 50, physical 40, temporal 45, performance 40, effort 55, frustration 50 → RTLX 47. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The set's deliberate dissociation of STARTLE from DANGER, and the strongest single argument that this experiment measures workload rather than fear. A door popping open in the climb is sudden, loud and physically distracting, yet FAA-H-8083-3C is explicit that it 'seldom if ever compromises the airplane's ability to fly' and that the hazard is the PILOT'S REACTION. So Uncertainty and AttentionSwitching are high, ErrorConsequence is 1, and the correct response is very nearly to do nothing. If participants' EEG and TLX put M2 up with the HIGH missions, we are measuring arousal; if they put it in the middle, we are measuring demand. Either result is worth having.

**EEG relevance** — The cleanest startle probe available: one abrupt onset with a large unpredictable jitter (±22 s) against an ordinary climb. The 30-60 s after CUE_ONSET is the startle refractory window the literature says to measure, and the two probes fall inside and outside it, giving a within-mission spare-capacity contrast.

**Expected errors** — Reaching for the door and losing the climb; a large heading excursion at the bang; abandoning the climb to return immediately.

**Success criteria** — Assigned climb continued and held; heading held within 15°; probes answered.

**Failure conditions** — Crash; loss of control; abandoning the assigned climb after the event.

**Aviation basis** — FAA-H-8083-3C ch.18 'Door Opening In-Flight' — concentrate on flying, do not rush to land, do not release the harness to reach the door.

**Approximations / not modelled** — The door is a small drag and yaw increment (about 0.7°/s of uncorrected yaw) plus a loud cue and a banner. There is no dedicated airflow-noise asset, so the ACOUSTIC component of the startle is under-delivered — the main known weakness of this manipulation.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `PROBE_ONSET`, `MISSION_END`

---

### M3 — Multi-part ATC re-clearances in the cruise

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 1 of 3 |
| Cognitive mechanism | working memory (clearance turnover) |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 49.5 / 100 |

**Objective** — Absorb, acknowledge and fly a stream of multi-item clearances.

**Brief to the participant** — Busy sector, calm air. ATC will pass multi-part clearances — heading, altitude and a report — in single transmissions. Acknowledge each in the window and fly it.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CRUISE — maintain 700 m, heading 000°. Expect re-routing. |
| 72 | ±8 | Readback | CLEARANCE: right heading 050°, climb 850 m — ACKNOWLEDGE  *(respond within 6.0 s)* |
| 76 | ±0 | HeadingChange | heading → 50 |
| 80 | ±0 | AltitudeChange | altitude → 850 m |
| 150 | ±8 | Readback | CLEARANCE: left heading 340°, descend 600 m — ACKNOWLEDGE  *(respond within 5.0 s)* |
| 154 | ±0 | HeadingChange | heading → 340 |
| 158 | ±0 | AltitudeChange | altitude → 600 m |
| 228 | ±8 | Readback | CLEARANCE: heading 020°, climb 780 m, report level — ACKNOWLEDGE  *(respond within 4.5 s)* |
| 232 | ±0 | HeadingChange | heading → 20 |
| 236 | ±0 | AltitudeChange | altitude → 780 m |
| 262 | ±6 | Probe | REPORT LEVEL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | █ 1 |
| working memory | ███ 3 |
| attention switching | ███ 3 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | █ 1 |
| uncertainty | █ 1 |
| communication | ███ 3 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 55, physical 25, temporal 50, performance 45, effort 55, frustration 35 → RTLX 44. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The pure working-memory and channel-switching manipulation. Same aircraft, same calm air and the same tracking task as L3 — the ONLY change is that instructions now arrive in bundles of two to three items, must be acknowledged inside a shrinking window, and one is a deferred report held across 30 s of other flying. That is textbook multiple-resource competition: verbal working memory and visual-manual tracking loaded together. Manual demand is identical to L3, so the step up is unambiguously cognitive.

**EEG relevance** — Should give the clearest frontal-midline theta increase of the MEDIUM set, since fm-theta tracks working-memory load specifically. Each clearance is an isolated, jittered, time-stamped onset suitable for event-locked averaging, and the deferred report creates a sustained maintenance interval.

**Expected errors** — Acknowledging but flying only the first item; forgetting the deferred report; reversing left/right on the 340° clearance.

**Success criteria** — All three clearances acknowledged in window, both items of each flown, deferred report made.

**Failure conditions** — Crash; more than one clearance missed entirely.

**Aviation basis** — Multi-element re-clearance in congested airspace; channel load rises with elements per transmission.

**Approximations / not modelled** — 'Read-back' is a single acknowledgement keypress — acceptance latency, not read-back accuracy. Retention is inferred from whether the aircraft was flown to the new targets.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `TARGET_CHANGE`, `MISSION_END`

---

### M4 — Deteriorating weather and a late runway change

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 1 of 3 |
| Cognitive mechanism | degraded perception + re-planning |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.45 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 57.5 / 100 |

**Objective** — Fly an approach in worsening weather and re-plan it when the runway changes late.

**Brief to the participant** — Inbound, 500 m, 12 km out, weather deteriorating. Hold height and heading until cleared, then fly the approach. Expect changes on final.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | INBOUND — maintain 500 m, heading 000°. Weather deteriorating. |
| 30 | ±6 | Weather | intensity 0.45 for 240 s |
| 62 | ±0 | Message | CLEARED TO LAND RUNWAY 01 — descend at your discretion. |
| 120 | ±10 | Probe | OPS CHECK — respond  *(respond within 5.0 s)* |
| 168 | ±12 | Readback | SIDE-STEP: displaced threshold, aim 200 m LONG, wind now 070° gusting — ACKNOWLEDGE  *(respond within 6.0 s)* |
| 176 | ±0 | Weather | intensity 0.55 for 90 s |
| 226 | ±10 | Probe | OPS CHECK — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | ███ 3 |
| attention switching | ██ 2 |
| situation awareness | ███ 3 |
| perception | ███ 3 |
| manual control | ███ 3 |
| procedural load | ██ 2 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 58, physical 48, temporal 55, performance 55, effort 65, frustration 45 → RTLX 54. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Raises load two ways that are deliberately different from each other: it degrades the INPUT (visibility falls, so the external horizon and the runway picture get harder to read, and turbulence makes the tracking task noisier) and then it invalidates the PLAN late, when the pilot is already committed. The side-step arrives on final and has to be held and applied while flying — the aiming point moves and the wind changes at the same time. It is MEDIUM rather than HIGH because nothing is ambiguous and nothing is irreversible: a go-around is always available and the aeroplane is serviceable.

**EEG relevance** — The mission most exposed to the motor-artifact confound in the whole set, because turbulence provokes corrective inputs. It is instrumented for exactly that check: `ctrl_jerk` is logged at 50 Hz so band power can be regressed on motor activity, and probe reaction time is the motor-light workload measure to fall back on.

**Expected errors** — Chasing the altimeter in turbulence; flying the original aiming point after the side-step; missing a probe while fighting a gust; landing long.

**Success criteria** — Side-step acknowledged, approach flown to a landing or a deliberate go-around, probes answered.

**Failure conditions** — Crash; landing off the runway; loss of control not recovered in 10 s.

**Aviation basis** — Deteriorating VMC, crosswind, and a late runway/threshold change. Turbulence and crosswind raise measured workload and degrade tracking; late changes on final are a recognised destabilising factor.

**Approximations / not modelled** — Turbulence is band-limited Perlin gusting, not a spectral (Dryden/von Karman) model; visibility is fog density. Intensities are SIMULATOR UNITS, not meteorological turbulence categories, and must be reported as such. The 'displaced threshold' is an instruction, not new runway geometry.

**Required event markers** — `MISSION_START`, `WEATHER_ONSET`, `ATC_MESSAGE`, `PROBE_ONSET`, `MISSION_END`

---

### H1 — Amended clearance, blocked runway, departure conflict

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 1 of 3 |
| Cognitive mechanism | concurrency under time pressure |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 800 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.55 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 87.0 / 100 |

**Objective** — Absorb an amended departure clearance while taxiing, wait out a blocked runway, depart without delay, and resolve a conflict after rotation.

**Brief to the participant** — Stand 1, engine running, heavy rain and poor visibility. Taxi via alpha to the holding point for runway 01. The airfield is busy. Expect changes.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. EXPECT DELAYS. |
| 4 | ±0 | Weather | intensity 0.55 for 280 s |
| 30 | ±8 | Traffic | TRAFFIC AHEAD ON ALPHA |
| 48 | ±6 | Readback | AMENDED CLEARANCE: left heading 320°, climb 800 m, CROSS 5 MILES AT OR ABOVE 500 M — ACKNOWLEDGE  *(respond within 6.0 s)* |
| 72 | ±8 | Traffic | TRAFFIC HOLDING ON RUNWAY 01 |
| 84 | ±0 | Message | HOLD POSITION — TRAFFIC ON THE RUNWAY. |
| 120 | ±10 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — NO DELAY, traffic 3 miles final. |
| 190 | ±0 | HeadingChange | heading → 320 |
| 196 | ±0 | AltitudeChange | altitude → 800 m |
| 222 | ±14 | Traffic | TRAFFIC 1 O'CLOCK CROSSING, SAME LEVEL — respond  *(respond within 6.0 s)* |
| 248 | ±10 | Decision | TURN OR CLIMB? — decide  *(respond within 7.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ████ 4 |
| decision complexity | ███ 3 |
| working memory | ████ 4 |
| attention switching | ████ 4 |
| situation awareness | ███ 3 |
| perception | ███ 3 |
| manual control | ██ 2 |
| procedural load | ███ 3 |
| uncertainty | ███ 3 |
| communication | ███ 3 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 85, physical 40, temporal 82, performance 70, effort 85, frustration 70 → RTLX 72. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The highest working-memory load in the set, and the clearest case of load emerging from INTERACTION rather than accumulation. The pilot is given a three-item amended clearance while still taxiing in poor visibility with traffic ahead — so the clearance has to be held while a surface-navigation task is running. One item (the crossing restriction) is not actionable for another two minutes, so it must survive the take-off. Then the expected clearance does NOT come, because the runway is blocked, which is a specific and under-appreciated load: waiting while primed to act. When it finally comes it comes with 'no delay', converting a patience task into a time-pressure task in one transmission. Note manual demand is 2 — identical to L1 and M1. Nothing about this mission is physically harder than a normal departure.

**EEG relevance** — Expected to show the largest sustained frontal-midline theta rise of the take-off row, with a distinct step at the amended clearance. The blocked-runway wait is a rare clean example of high cognitive load with almost NO motor activity — the aeroplane is stationary — which makes it the strongest available test of whether the workload measures are tracking cognition rather than movement.

**Expected errors** — Dropping the crossing restriction; entering the runway while it is occupied; rushing the line-up after the 'no delay'; missing the crossing traffic while reconfiguring.

**Success criteria** — Hold short respected while the runway is occupied, amended clearance acknowledged and all three items honoured, conflict acknowledged, a deliberate decision made.

**Failure conditions** — Runway incursion; crash; no response to the conflict.

**Aviation basis** — Amended departure clearances, blocked-runway holds and departure conflicts are routine at a busy field. The 'no delay' instruction after an extended hold is a recognised rush-inducing pattern.

**Approximations / not modelled** — Traffic is scripted and cannot collide. Weather is fog density plus turbulence, not a meteorological model, and its intensity is in simulator units. The crossing restriction is not independently checked by the sim — compliance is read from the telemetry afterwards.

**Required event markers** — `MISSION_START`, `TRAFFIC_ONSET`, `ATC_MESSAGE`, `DECISION_PROMPT`, `MISSION_END`

---

### H2 — Alternator failure on the departure climb

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 1 of 3 |
| Cognitive mechanism | forward reasoning about a depleting resource |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 78.8 / 100 |

**Objective** — Manage a draining battery while flying an assigned climb and a level restriction, and decide whether to continue or return.

**Brief to the participant** — Departure leg, 400 m, runway heading. ATC will clear you to climb and will give you a restriction. Fly the departure.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 62 | ±5 | Readback | CLEARANCE: climb 900 m, cross 10 miles AT OR ABOVE 700 M — ACKNOWLEDGE  *(respond within 7.0 s)* |
| 68 | ±0 | AltitudeChange | altitude → 900 m |
| 96 | ±15 | SystemFailure | `AlternatorFailure` severity 1.00 |
| 150 | ±0 | Checklist | `ELEC_ALT` — ALTERNATOR FAILURE DRILL |
| 212 | ±12 | Decision | CONTINUE THE DEPARTURE OR RETURN? — decide  *(respond within 8.0 s)* |
| 258 | ±10 | Probe | REPORT LEVEL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ███ 3 |
| decision complexity | ████ 4 |
| working memory | ███ 3 |
| attention switching | ███ 3 |
| situation awareness | ███ 3 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ████ 4 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 80, physical 35, temporal 62, performance 60, effort 78, frustration 58 → RTLX 62. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The RESOURCE-BUDGETING mission and the highest decision complexity in the set. Nothing here is startling and nothing is instantly dangerous — the load is entirely in having to reason FORWARD about a depleting resource whose consumption the pilot controls, while flying a climb, honouring a crossing restriction, and working a drill. The decision has real branches with different failure modes: shed load early and the battery lasts; continue outbound on a draining bus and the return leg gets worse; turn back early and the clearance is abandoned. That forward reasoning under a moving constraint is the kind of element interactivity that raises intrinsic cognitive load, and it is why H2 outranks H4 on the model despite H4 being far more dramatic. Manual demand is 2, the same as L2.

**EEG relevance** — A SUSTAINED elevation rather than a transient spike — valuable because most of the set is event-locked. Between LOW VOLTS and the decision there should be a persistent theta/alpha shift with no single dominant onset, which is the pattern a windowed classifier has to detect and an ERP analysis cannot.

**Expected errors** — Missing the low-volts indication; never shedding load; dropping the crossing restriction while working the drill; deciding by default (continuing because no decision was made).

**Success criteria** — Low volts recognised, load shed, restriction honoured, an explicit continue/return decision made in window.

**Failure conditions** — Crash; no decision made; loss of control.

**Aviation basis** — FAA-H-8083-3C ch.18 'Electrical System': shed non-essential loads immediately, land at the nearest suitable airport, and note that a 40 A load can flatten the battery in 10-15 minutes.

**Approximations / not modelled** — The battery is a single lumped 24 Ah store with a linear state-of-charge-to-voltage curve and two load levels (shed / not shed). Individual breakers and per-equipment draw are not modelled, and the drain is tuned so the decision is live inside a 300 s trial — faster than a real battery would go flat.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `DECISION_PROMPT`, `MISSION_END`

---

### H3 — Unreliable instruments in the cruise

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 1 of 3 |
| Cognitive mechanism | self-consistent wrong information |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 86.0 / 100 |

**Objective** — Recognise that the instruments are lying, keep flying accurately, and decide what to do about it.

**Brief to the participant** — Cruise, 700 m, heading 000°, weather deteriorating ahead. Fly the assigned level and heading. If anything changes, handle it as you would in the aircraft.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CRUISE — maintain 700 m, heading 000°. |
| 66 | ±6 | Weather | intensity 0.45 for 210 s |
| 92 | ±18 | SystemFailure | `StaticBlocked` severity 1.00 |
| 150 | ±0 | Checklist | `PITOT_STATIC` — PITOT-STATIC DRILL |
| 196 | ±10 | Readback | CLEARANCE: descend 500 m, right heading 060° — ACKNOWLEDGE  *(respond within 5.0 s)* |
| 202 | ±0 | AltitudeChange | altitude → 500 m |
| 206 | ±0 | HeadingChange | heading → 60 |
| 240 | ±10 | Decision | CONTINUE OR DIVERT? — decide  *(respond within 8.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ███ 3 |
| decision complexity | ███ 3 |
| working memory | ███ 3 |
| attention switching | ████ 4 |
| situation awareness | ████ 4 |
| perception | ████ 4 |
| manual control | ███ 3 |
| procedural load | ███ 3 |
| uncertainty | ████ 4 |
| communication | ██ 2 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 85, physical 40, temporal 68, performance 70, effort 85, frustration 75 → RTLX 71. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The highest-uncertainty mission in the set. A partial static blockage is singled out by FAA-H-8083-3C as 'insidious' precisely because it is SELF-CONSISTENT: the altimeter, the airspeed indicator and the VSI all corroborate a picture that is wrong, so the normal cross-check — the thing a pilot falls back on — actively confirms the error. Resolving it means distrusting the primary instruments and flying attitude and power instead, which is expensive in working memory and attention. Being in cloud removes the outside horizon that would otherwise settle the argument, and an ATC re-clearance arrives while the picture is still unresolved, forcing a channel switch at the worst moment. The load is emergent, not additive: none of these three things alone is a HIGH mission.

**EEG relevance** — Expected to show the largest sustained frontal theta rise and the poorest secondary-task performance of the cruise row. Because the phase and the manual task are matched to L3 and M3, the L3 -> M3 -> H3 progression is the cleanest three-level dose-response contrast the design offers, with visual scene, aircraft and tracking task all held constant.

**Expected errors** — Chasing the false altimeter; never opening the alternate static source; flying the re-clearance using the lying instruments; deciding nothing.

**Success criteria** — Alternate static opened OR the aircraft flown on attitude and power; clearance acknowledged; a deliberate decision made.

**Failure conditions** — Crash; loss of control; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.18 'Pitot-Static System': with a restricted static source the altimeter, ASI and VSI mislead together, and the confirmation is to open the alternate static source while climbing or descending.

**Approximations / not modelled** — Pitot-static errors are a first-order linear approximation of the handbook's error signature, not a pressure-system model. 'In cloud' is fog density; there is no cloud layer, precipitation or airframe icing.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `DECISION_PROMPT`, `MISSION_END`

---

### H4 — Engine failure on the approach

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 1 of 3 |
| Cognitive mechanism | irreversible commitment under a clock |
| Flight phase | Approach |
| Start | Airborne at (-900, 500, -9000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 76.3 / 100 |

**Objective** — Lose the engine with the runway not quite made, and put the aeroplane down under control.

**Brief to the participant** — Inbound, 500 m, positioning for runway 01. Hold height and heading until cleared. Fly the aeroplane.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | INBOUND — maintain 500 m, heading 000°. |
| 62 | ±0 | Message | CLEARED TO LAND RUNWAY 01 — descend at your discretion. |
| 96 | ±14 | SystemFailure | `EngineFailure` severity 1.00 |
| 106 | ±0 | Checklist | `ENG_FAIL` — ENGINE FAILURE DRILL |
| 128 | ±6 | Decision | RUNWAY OR FIELD AHEAD? — decide  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ███ 3 |
| temporal demand | ████ 4 |
| decision complexity | ███ 3 |
| working memory | ██ 2 |
| attention switching | ███ 3 |
| situation awareness | ████ 4 |
| perception | ███ 3 |
| manual control | ████ 4 |
| procedural load | ███ 3 |
| uncertainty | ██ 2 |
| communication | █ 1 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 75, physical 60, temporal 88, performance 70, effort 85, frustration 70 → RTLX 75. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The time-pressure pole of the HIGH class. Note the profile is deliberately a DIFFERENT SHAPE from H2's: temporal demand and error consequence are at 4 while uncertainty is only 2, because there is nothing to diagnose — the engine has stopped and the pilot knows it instantly. The demand is energy management under an irreversible clock: glide speed, site selection and a committed turn inside about 90 seconds, with the memory drill competing for the same attention. It is scored slightly BELOW H2 and H3 precisely because a trained pilot has a rehearsed schema for it — the expertise effect this design is meant to expose rather than assume away. That prediction is the most fragile in the study and depends on the participant actually having the schema, which is why flight hours are a covariate.

**EEG relevance** — The strongest startle onset in the set and the closest thing to a step change in demand: silence, then nothing but glide. Expect the largest transient theta burst at CUE_ONSET, and degraded checklist and decision performance in the 30-60 s startle window after it. Also the mission most at risk of motor-artifact contamination, which is why it is paired with L4 (same phase, matched manual demand, minimal cognitive demand).

**Expected errors** — Holding the nose up and decaying toward the stall; stretching the glide to a runway that cannot be reached; starting the drill before establishing the glide; freezing for several seconds (cognitive lock-up).

**Success criteria** — Best-glide speed established, a landing site committed to, and a survivable arrival — on the runway or under control on the ground.

**Failure conditions** — Stall/spin; uncontrolled ground impact; no decision before 150 m.

**Aviation basis** — FAA-H-8083-3C ch.18, engine failure and emergency approach. The handbook's worked example (300 ft AGL, a 4-second reaction time, ~1,000 fpm power-off descent) is why the turn back usually is not made, and it names the psychological hazards directly — reluctance to accept the emergency, and the desire to save the aeroplane leading to a stretched glide.

**Approximations / not modelled** — No propeller windmilling-drag model and no restart logic beyond the checklist items. The surrounding terrain has no prepared off-field landing sites, so 'field ahead' is judged only by whether the arrival is survivable under the existing crash model. Best-glide speed is asserted in the drill rather than derived from a published polar.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `DECISION_PROMPT`, `MISSION_END`

---

### L1V2 — Normal departure with a routine readback

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 2 of 3 |
| Cognitive mechanism | reference - single-threaded procedure with one acknowledgement |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 28.0 / 100 |

**Objective** — Taxi to runway 01, hold short, read back the departure clearance, take off and climb to 600 m on runway heading.

**Brief to the participant** — You are on stand 1, engine running. Taxi via taxiway A to the holding point for runway 01 and hold short. Read back your departure clearance when it is given. Then line up, take off, and climb straight ahead to 600 m. Clear day, no other traffic.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. |
| 70 | ±4 | Readback | CLEARANCE: runway heading, climb 600 m — ACKNOWLEDGE  *(respond within 8.0 s)* |
| 92 | ±6 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — climb runway heading to 600 m. |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand | █ 1 |
| decision complexity | █ 1 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | █ 1 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty |  0 |
| communication | █ 1 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 32, physical 25, temporal 22, performance 25, effort 32, frustration 16 → RTLX 25. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The take-off row's reference condition, realised a second way. The single acknowledgement is deliberately trivial — one item, no readback content to hold, ample window — and exists so that the LOW cell contains the same KIND of activity as its MEDIUM and HIGH neighbours (a radio call happens) without any of the demand that makes them what they are. Without it, a participant could distinguish LOW from the rest purely by the radio being silent, and 'the radio was quiet' is a cue about the condition rather than a property of it.

**EEG relevance** — Phase-matched baseline for M1V2 and H1V2. Its first 60 s is taxiing, the same baseline activity as the other two missions in the row, so the within-row class contrast is clean.

**Expected errors** — Taxiing past the hold-short line; taking off without the clearance; drifting off runway heading in the climb.

**Success criteria** — Hold short respected, clearance acknowledged, airborne and established at 600 m +/- 90 m on runway heading.

**Failure conditions** — Runway incursion; crash; failure to get airborne inside the trial.

**Aviation basis** — FAA-H-8083-3C ch.2 (airport operations) and the standard departure clearance readback required by ICAO Annex 10 vol. II.

**Approximations / not modelled** — Read-back is a single keypress acknowledgement, not speech: the simulator cannot check that the pilot read the clearance back CORRECTLY, only that they responded and how quickly.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `MISSION_END`

---

### L2V2 — Assigned climb with one amendment

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 2 of 3 |
| Cognitive mechanism | reference - steady-state tracking with one target change |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 12.8 / 100 |

**Objective** — Fly the departure climb and accept one routine altitude amendment.

**Brief to the participant** — Departure leg, 400 m, runway heading. ATC will clear you to climb. Fly the departure accurately. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 66 | ±0 | AltitudeChange | altitude → 700 m |
| 196 | ±0 | AltitudeChange | altitude → 800 m |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand |  0 |
| decision complexity |  0 |
| working memory | █ 1 |
| attention switching |  0 |
| situation awareness | █ 1 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load |  0 |
| uncertainty |  0 |
| communication |  0 |
| error consequence |  0 |

**Pre-registered expected NASA-TLX** — mental 28, physical 26, temporal 20, performance 24, effort 30, frustration 14 → RTLX 24. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The climb row's reference. Two target changes across 300 s is well inside what a single channel absorbs without competition: each arrives alone, with no other task running, and the pilot has minutes to settle on it. It is included rather than a pure level-off so that ALTITUDE CAPTURE — the manual activity that MEDIUM and HIGH in this row also perform — is present in the reference, keeping manual demand matched at 2 across the row.

**EEG relevance** — Phase-matched baseline for M2V2 and H2V2, and one of the two cleanest low-artifact airborne segments in the bank.

**Expected errors** — Overshooting the new altitude; drifting off heading during the level-off.

**Success criteria** — Both assigned altitudes captured and held within +/- 80 m, heading within +/- 14°.

**Failure conditions** — Crash; loss of control.

**Aviation basis** — Routine departure vectoring; FAA-H-8083-3C ch.3 (basic flight manoeuvres).

**Approximations / not modelled** — No SID; ATC is a scripted sequence with no dialogue.

**Required event markers** — `MISSION_START`, `TARGET_CHANGE`, `MISSION_END`

---

### L3V2 — Level cruise with one heading change

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 2 of 3 |
| Cognitive mechanism | reference - steady-state tracking with one turn |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 12.8 / 100 |

**Objective** — Hold 700 m and fly one assigned heading change.

**Brief to the participant** — Straight and level at 700 m, heading 000°. Hold altitude and heading accurately. ATC may give you a turn. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | MAINTAIN 700 M, HEADING 000°. |
| 82 | ±0 | HeadingChange | heading → 20 |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand |  0 |
| decision complexity |  0 |
| working memory | █ 1 |
| attention switching |  0 |
| situation awareness | █ 1 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load |  0 |
| uncertainty |  0 |
| communication |  0 |
| error consequence |  0 |

**Pre-registered expected NASA-TLX** — mental 26, physical 24, temporal 18, performance 24, effort 28, frustration 13 → RTLX 22. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The cruise row's reference, with a single 20° turn so that the reference contains the same manual activity — a coordinated turn and a roll-out onto a target — that MEDIUM and HIGH in this row require. A pure wings-level hold would have left manual demand lower in LOW than in its own row's other cells, which is precisely the confound the row matching exists to prevent.

**EEG relevance** — Phase-matched baseline for M3V2 and H3V2. Cruise is the lowest-artifact condition in the bank and the natural reference for the sustained-load analyses.

**Expected errors** — Losing altitude in the turn; overshooting the assigned heading.

**Success criteria** — Altitude within +/- 70 m and heading within +/- 12° for the majority of the trial.

**Failure conditions** — Crash; loss of control.

**Aviation basis** — Straight-and-level and medium turns, FAA-H-8083-3C ch.3.

**Approximations / not modelled** — No other traffic and no weather; ATC is a scripted sequence.

**Required event markers** — `MISSION_START`, `TARGET_CHANGE`, `MISSION_END`

---

### L4V2 — Normal landing with a configuration call

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 2 of 3 |
| Cognitive mechanism | reference - rehearsed approach with a routine configuration change |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 31.8 / 100 |

**Objective** — Fly a straight-in approach and land on runway 01.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Descend, configure and land. Clear day, light air, no other traffic.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND CALM. |
| 92 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 114 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand | █ 1 |
| decision complexity | █ 1 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ███ 3 |
| procedural load | ██ 2 |
| uncertainty |  0 |
| communication | █ 1 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 34, physical 40, temporal 26, performance 34, effort 40, frustration 20 → RTLX 32. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The approach row's reference. Manual demand is 3 rather than 2 because an approach IS harder to fly than cruise — and that is exactly why the row is matched INTERNALLY at 3 and never compared across rows without the phase term. The configuration call and the checklist are present so that the reference contains the same procedural furniture as M4V2 and H4V2, differing only in what the pilot has to think about.

**EEG relevance** — Phase-matched baseline for M4V2 and H4V2 — the reference against which the approach row's class contrasts are read. Note that the approach row has the highest movement artifact in the bank; the row-internal comparison is what controls for it.

**Expected errors** — High or fast on final; late configuration; long or firm touchdown.

**Success criteria** — Touchdown on the runway, wings level, sink rate inside the acceptable band.

**Failure conditions** — Crash; runway excursion; not landed inside the trial.

**Aviation basis** — Normal approach and landing, FAA-H-8083-3C ch.8.

**Approximations / not modelled** — No glideslope guidance and no PAPI: the approach is flown visually.

**Required event markers** — `MISSION_START`, `CHECKLIST_START`, `MISSION_END`

---

### M1V2 — Runway change before line-up

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 2 of 3 |
| Cognitive mechanism | working memory - replacing a briefed plan |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 750 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 54.0 / 100 |

**Objective** — Taxi for runway 01, absorb a late runway and departure change at the holding point, and fly the amended departure.

**Brief to the participant** — You are on stand 1, engine running. Taxi via taxiway A for runway 01 and hold short. Expect a straight-ahead departure to 600 m. Clear day.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. EXPECT RUNWAY 01, RUNWAY HEADING. |
| 70 | ±6 | Readback | CHANGE OF DEPARTURE: after airborne turn LEFT heading 340, climb 750 m. READ BACK  *(respond within 9.0 s)* |
| 104 | ±7 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — left heading 340, climb 750 m. |
| 146 | ±0 | AltitudeChange | altitude → 750 m |
| 186 | ±0 | HeadingChange | heading → 340 |
| 238 | ±10 | Probe | CONFIRM ASSIGNED HEADING — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | ████ 4 |
| attention switching | ██ 2 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty | █ 1 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 60, physical 30, temporal 45, performance 45, effort 58, frustration 38 → RTLX 46. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by working memory, and specifically by INTERFERENCE rather than by volume. The pilot is not given two items to remember; they are given one item that REPLACES an item they have already committed to, at a moment when the original plan is about to be executed. Replacing an active plan is measurably more costly than forming one, and the characteristic error is not forgetting the new clearance but reverting to the old one — which is exactly what the confirm-heading probe at 232 s is there to catch. Manual demand stays at 2, matched to L1V2 and H1V2: a left turn after take-off is not harder to fly than a straight climb.

**EEG relevance** — A clean event-related contrast at the clearance change against the mission's own quiet taxi baseline, plus a sustained maintenance period between the change and its execution during which the amended clearance must be held.

**Expected errors** — Flying the ORIGINAL runway heading after take-off; climbing to 600 m instead of 750 m; answering the probe with the briefed rather than the amended heading.

**Success criteria** — Amended clearance acknowledged, hold short respected, established on 340 at 750 m.

**Failure conditions** — Runway incursion; crash; established on the original clearance at the end of the trial.

**Aviation basis** — Amended departure clearances at the holding point are routine and are a recognised source of read-back/hear-back error (ICAO Doc 9683 human-factors training manual, communication chapter).

**Approximations / not modelled** — No speech recognition: the read-back is acknowledged, not verified, so a pilot who mis-read-back but flew correctly is scored as correct.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `TARGET_CHANGE`, `PROBE_ONSET`, `MISSION_END`

---

### M2V2 — Carburettor ice on the climb

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 2 of 3 |
| Cognitive mechanism | diagnosis of a gradual, ambiguous cue |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.05 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.25 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 51.8 / 100 |

**Objective** — Fly the assigned climb, notice a gradual power loss, diagnose it and cure it.

**Brief to the participant** — Departure leg, 400 m, runway heading. ATC will clear you to climb. Fly the departure. Cool, damp, hazy air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 66 | ±0 | AltitudeChange | altitude → 800 m |
| 104 | ±14 | SystemFailure | `CarbIce` severity 0.75 |
| 196 | ±0 | Checklist | `ENG_ROUGH` — ROUGH RUNNING / PARTIAL POWER |
| 254 | ±10 | Probe | REPORT LEVEL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | █ 1 |
| attention switching | ███ 3 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty | ████ 4 |
| communication | █ 1 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 62, physical 32, temporal 40, performance 52, effort 62, frustration 48 → RTLX 49. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by UNCERTAINTY, which is the opposite pole from variant 1's mechanism. M2 is a cabin door: instantaneous, unmistakable, and startling, with almost nothing to work out. Carburettor ice is the reverse — nothing happens suddenly, there is no annunciation, and the only evidence is a slow decay in manifold pressure and climb rate that could equally be a mis-set throttle, a heavier aeroplane, or nothing at all. The demand is in NOTICING an absence and then attributing it, which is why Uncertainty is the only 4 in the profile while Temporal Demand stays at 2. Manual demand 2, matched to the row.

**EEG relevance** — The bank's clearest case for epoching on CUE_ONSET rather than TRIGGER_ARMED. The abnormality is armed at ~104 s but is not perceptible for tens of seconds, so a naive analysis time-locked to the injection would average across a window in which nothing had yet happened to the pilot. The interval between the two markers is itself a measure of how long detection took.

**Expected errors** — Not noticing the decay at all; attributing it to the throttle and pushing power up instead of applying carburettor heat; applying carb heat and removing it too early; losing the altitude assignment while diagnosing.

**Success criteria** — Power loss detected, carburettor heat applied, climb re-established, assigned altitude held.

**Failure conditions** — Crash; complete power loss through untreated icing.

**Aviation basis** — FAA-H-8083-3C ch.7 and AC 20-113: carburettor icing is most likely in humid air between about -7 and 21 °C, develops gradually, and presents as an unexplained loss of power with rough running.

**Approximations / not modelled** — Icing severity is a scripted ramp rather than a function of modelled humidity and carburettor temperature, and carburettor heat clears it at a fixed rate. There is no carburettor air-temperature gauge.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `MISSION_END`

---

### M3V2 — Diversion to an alternate

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 2 of 3 |
| Cognitive mechanism | re-planning against competing constraints |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 54.3 / 100 |

**Objective** — Cruise en route, then re-plan to an alternate when the destination closes.

**Brief to the participant** — En route at 700 m, heading 000° for your destination. Hold altitude and heading. Clear day.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | MAINTAIN 700 M, HEADING 000° — 18 MILES TO RUN. |
| 68 | ±0 | Message | DESTINATION IS NOW CLOSED — RUNWAY OBSTRUCTED. ALTERNATE IS 40 MILES NORTH-WEST, OR 25 MILES EAST WITH LOWER CLOUD. |
| 76 | ±8 | Decision | SELECT AN ALTERNATE — decide  *(respond within 12.0 s)* |
| 102 | ±6 | Readback | CLEARED TO THE ALTERNATE: heading 315, climb 900 m. READ BACK  *(respond within 9.0 s)* |
| 114 | ±0 | HeadingChange | heading → 315 |
| 120 | ±0 | AltitudeChange | altitude → 900 m |
| 238 | ±12 | Probe | CONFIRM FUEL ENDURANCE — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ████ 4 |
| working memory | ███ 3 |
| attention switching | ██ 2 |
| situation awareness | ██ 2 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load | █ 1 |
| uncertainty | █ 1 |
| communication | ███ 3 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 66, physical 28, temporal 44, performance 50, effort 64, frustration 44 → RTLX 49. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by RE-PLANNING rather than by retention. Variant 1 of this cell loads working memory with a turnover of clearances; here there is only one clearance, but before it arrives the pilot has to construct and compare two options that trade against each other on different dimensions — distance against weather — with no dominant answer. Decision complexity is therefore 4 while working memory stays at 3 and temporal demand at 2: it is a hard question asked calmly, which is a different load profile from an easy question asked repeatedly. Manual demand 2, matched to the row.

**EEG relevance** — A long DECISION_PROMPT window (12 s) makes this one of the few epochs in the bank where a slow, deliberative decision process can be observed rather than a reflexive response. Contrast the reaction-time distribution here against H1V2's, which is the same marker under a clock.

**Expected errors** — Choosing by proximity without considering the weather; not deciding inside the window; continuing toward the closed destination; losing altitude during the re-plan.

**Success criteria** — An explicit alternate chosen in window, clearance acknowledged, established on 315 at 900 m.

**Failure conditions** — Crash; no decision made.

**Aviation basis** — In-flight diversion decision-making, FAA-H-8083-25 ch.17 (aeronautical decision-making); plan-continuation bias is the documented failure mode.

**Approximations / not modelled** — The two alternates are described in the radio call only — neither exists in the world, and the trial ends before either could be reached. What is measured is the decision and the subsequent tracking, not an arrival.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `TARGET_CHANGE`, `MISSION_END`

---

### M4V2 — Late runway change on final

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 2 of 3 |
| Cognitive mechanism | re-planning under time pressure late in a committed approach |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 61.0 / 100 |

**Objective** — Fly the approach, absorb a late runway change, and land.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Descend, configure and land. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND CALM. |
| 82 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 124 | ±7 | Readback | CHANGE OF RUNWAY: SIDESTEP AND LAND RUNWAY 01 RIGHT, DISPLACED THRESHOLD 200 M. READ BACK  *(respond within 9.0 s)* |
| 138 | ±0 | Message | TOUCH DOWN BEYOND THE DISPLACED THRESHOLD. |
| 158 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |
| 194 | ±10 | Probe | CONFIRM LANDING RUNWAY — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ████ 4 |
| decision complexity | ██ 2 |
| working memory | ███ 3 |
| attention switching | ██ 2 |
| situation awareness | ███ 3 |
| perception | ██ 2 |
| manual control | ███ 3 |
| procedural load | ██ 2 |
| uncertainty | █ 1 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 66, physical 46, temporal 74, performance 56, effort 68, frustration 52 → RTLX 60. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by TIME PRESSURE on a committed plan, where variant 1 of this cell is degraded perception. Nothing here is hard to see and nothing is ambiguous — the pilot is told clearly what to do. The cost is entirely that the instruction arrives after the approach is established and must be executed inside a window that is closing at the aircraft's own descent rate, with a displaced threshold that changes the aiming point as well as the runway. Temporal demand is the only 4. Manual demand 3, matched to the row.

**EEG relevance** — An event-related response at a moment of high existing task load, which is the condition under which spare-capacity measures are most diagnostic. Compare the probe response time here against L4V2's baseline: the same probe under a heavier load is the cleanest secondary-task contrast the approach row offers.

**Expected errors** — Landing on the original runway; touching down short of the displaced threshold; destabilising the approach during the sidestep; going around unnecessarily.

**Success criteria** — Change acknowledged, touchdown beyond the displaced threshold on the nominated runway, wings level.

**Failure conditions** — Crash; runway excursion; not landed inside the trial.

**Aviation basis** — Late landing-clearance and runway changes are a recognised destabilising factor on approach; FAA-H-8083-3C ch.8 and the Flight Safety Foundation's stabilised-approach criteria.

**Approximations / not modelled** — There is one physical runway. The 'right' runway and its displaced threshold are represented by a lateral offset and a shifted aiming point on the same strip, so the geometry of the sidestep is real but the second runway is not separately modelled.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `PROBE_ONSET`, `MISSION_END`

---

### H1V2 — Intersection departure with a performance decision

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 2 of 3 |
| Cognitive mechanism | forward reasoning about a physical margin under a clock |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 86.5 / 100 |

**Objective** — Taxi for runway 01, evaluate an offered intersection departure against the runway actually available, decide, and fly the departure.

**Brief to the participant** — You are on stand 1, engine running. Taxi via taxiway A for runway 01. The full runway is 600 m. ATC may offer you an intersection departure to save time — an intersection departure means less runway ahead of you. Departure is runway heading, climb 600 m.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA FOR RUNWAY 01. TRAFFIC IS 4 MILES FINAL. |
| 66 | ±0 | Message | ARE YOU ABLE INTERSECTION BRAVO? 380 M AVAILABLE. TRAFFIC 3 MILES FINAL — IF UNABLE, EXPECT A 4 MINUTE DELAY. |
| 72 | ±6 | Decision | ACCEPT INTERSECTION BRAVO OR REQUEST FULL LENGTH? — decide  *(respond within 10.0 s)* |
| 88 | ±5 | Traffic | TRAFFIC 3 MILES FINAL RUNWAY 01 |
| 106 | ±6 | Readback | IF ACCEPTING: LINE UP BRAVO, NO DELAY. IF NOT: HOLD SHORT. ACKNOWLEDGE  *(respond within 8.0 s)* |
| 126 | ±8 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — no delay, traffic 2 miles final. |
| 190 | ±14 | Probe | REPORT PASSING 300 M — respond  *(respond within 5.0 s)* |
| 214 | ±0 | HeadingChange | heading → 25 |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ████ 4 |
| decision complexity | ████ 4 |
| working memory | ███ 3 |
| attention switching | ███ 3 |
| situation awareness | ████ 4 |
| perception | ███ 3 |
| manual control | ██ 2 |
| procedural load | ███ 3 |
| uncertainty | ███ 3 |
| communication | ███ 3 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 82, physical 35, temporal 80, performance 62, effort 80, frustration 60 → RTLX 67. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by a mechanism variant 1 does not use. H1 is CONCURRENCY — several things at once. This is a single question that has to be ANSWERED CORRECTLY, under a clock, where both answers cost something and the wrong one is unrecoverable: accept and you commit to a take-off from 380 m of runway; decline and you take a delay with traffic on final. The pilot has to reason forward from a number they were given in the brief to a physical margin, while a converging aeroplane makes the clock visible rather than merely stated. The subsequent departure is deliberately ordinary — the load is front-loaded into the decision so the EEG contrast has a clean onset. Manual demand 2, matched to the row.

**EEG relevance** — The strongest single DECISION_PROMPT -> DECISION_MADE epoch in the take-off row, with a decision that is genuinely effortful rather than a button press. Reaction time here is a primary behavioural measure and should correlate with the EEG index if the manipulation works. The visible converging traffic gives an independent, non-verbal time cue whose onset is separately marked.

**Expected errors** — Accepting the intersection without evaluating it (compliance bias); deciding by default (letting the window expire); accepting and then rotating late; forgetting the traffic-avoidance turn at 214 s.

**Success criteria** — An explicit decision made inside the window, hold short respected until cleared, airborne and established at 600 m on the assigned heading.

**Failure conditions** — Runway incursion; crash; no decision made; departing without clearance.

**Aviation basis** — Intersection departures and the associated 'runway remaining' judgement are treated in FAA-H-8083-3C ch.2, and accepting an intersection departure without computing the remaining distance is a documented contributor to runway-overrun events (FAA InFO 07004).

**Approximations / not modelled** — The aeroplane's take-off performance is the flight model's, not a certified performance chart: 380 m is genuinely marginal in this model but the pilot cannot consult a real TODA/TORA table, only the figure in the brief. The intersection is represented by the existing link taxiway.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `TRAFFIC_ONSET`, `PROBE_ONSET`, `MISSION_END`

---

### H2V2 — Partial power loss after take-off — the turn-back decision

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 2 of 3 |
| Cognitive mechanism | irreversible commitment against a shrinking margin |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 84.3 / 100 |

**Objective** — Fly the departure, handle a partial power loss at low altitude, and decide between continuing ahead and turning back.

**Brief to the participant** — Departure leg, 400 m, runway heading. ATC will clear you to climb. The aerodrome is behind you. Terrain ahead is flat and open. Fly the departure.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 66 | ±0 | AltitudeChange | altitude → 900 m |
| 98 | ±12 | SystemFailure | `EngineRoughness` severity 0.55 |
| 126 | ±0 | Checklist | `ENG_ROUGH` — PARTIAL POWER LOSS DRILL |
| 158 | ±10 | Decision | CONTINUE AHEAD OR TURN BACK TO THE FIELD? — decide  *(respond within 10.0 s)* |
| 176 | ±0 | Message | NO OTHER TRAFFIC. FIELD IS 4 KM BEHIND YOU. SURFACE WIND CALM. |
| 244 | ±12 | Probe | STATE YOUR INTENTIONS — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ████ 4 |
| decision complexity | ████ 4 |
| working memory | ██ 2 |
| attention switching | ███ 3 |
| situation awareness | ████ 4 |
| perception | ███ 3 |
| manual control | ███ 3 |
| procedural load | ███ 3 |
| uncertainty | ███ 3 |
| communication | ██ 2 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 84, physical 45, temporal 82, performance 66, effort 84, frustration 66 → RTLX 71. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by a mechanism the row's variant 1 does not use. H2 is resource budgeting over minutes; this is a commitment made in seconds whose margin is SHRINKING while it is being made. Partial rather than total power is the point: with the engine dead there is no decision worth measuring, whereas with partial power the aeroplane will fly but not climb, both options remain nominally available, and the option set degrades continuously. This is the classic turn-back trade, and the correct answer for a low-time pilot at this height is usually to continue ahead — so a participant who turns back has made a recognisable, analysable error rather than merely a different choice. Manual demand is 3, one above the row's other two; that is the maximum spread the design permits and it is declared rather than hidden, because a degraded aeroplane cannot be flown with the same hands as a healthy one. Control-activity covariates are logged for exactly this reason.

**EEG relevance** — A sharp CUE_ONSET at the power loss followed by a sustained high-load segment through the drill and the decision. The DECISION_PROMPT epoch is the primary contrast; the interval from CUE_ONSET to PILOT_FIRST_RESPONSE indexes startle recovery.

**Expected errors** — Turning back (the documented fatal error); leaving the throttle where it is; stalling the turn; running the drill while allowing the speed to decay.

**Success criteria** — Power loss handled, an explicit decision made in window, aircraft under control at the end of the trial.

**Failure conditions** — Crash; stall/spin; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.18 and the AOPA Air Safety Institute's work on the 'impossible turn': the turn-back after a power loss on departure is among the most consistently fatal manoeuvres in light aviation, and the accident record turns on altitude available rather than pilot skill.

**Approximations / not modelled** — Partial power is a fixed multiplier on available thrust, not a modelled cylinder or induction fault, and it neither worsens nor recovers during the trial. The aerodrome behind the aircraft is reachable in the model but the trial does not require a landing.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `DECISION_PROMPT`, `MISSION_END`

---

### H3V2 — Fuel imbalance and tank management

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 2 of 3 |
| Cognitive mechanism | resource management with a self-inflicted cure |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 92 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 84.0 / 100 |

**Objective** — Cruise en route while managing an asymmetric fuel state against a deadline.

**Brief to the participant** — En route at 700 m, heading 000°. Fuel is in two tanks, left and right, selected by the fuel selector on the console. Hold altitude and heading. Clear day.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | MAINTAIN 700 M, HEADING 000°. |
| 92 | ±12 | SystemFailure | `FuelStarvation` severity 1.00 |
| 132 | ±0 | Checklist | `ENG_ROUGH` — ENGINE ROUGHNESS / FUEL |
| 168 | ±6 | Readback | CLEARANCE: cross the boundary AT OR ABOVE 850 M WITHIN 6 MINUTES. READ BACK  *(respond within 9.0 s)* |
| 178 | ±0 | AltitudeChange | altitude → 850 m |
| 226 | ±10 | Decision | CONTINUE OR DIVERT ON REMAINING FUEL? — decide  *(respond within 9.0 s)* |
| 268 | ±10 | Probe | REPORT FUEL REMAINING — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ███ 3 |
| decision complexity | ████ 4 |
| working memory | ████ 4 |
| attention switching | ████ 4 |
| situation awareness | ███ 3 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ███ 3 |
| uncertainty | ███ 3 |
| communication | ██ 2 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 82, physical 32, temporal 66, performance 62, effort 80, frustration 62 → RTLX 64. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by a mechanism distinct from variant 1's. H3 gives the pilot instruments that lie consistently; this gives them instruments that tell the truth about a state they must actively manage. The cure — switching tanks — restores power immediately and then starts draining the only remaining supply, so the action that solves the emergency creates the next constraint, and the pilot must hold that forward consequence while also holding a crossing restriction with a time limit. Working memory and attention switching are both 4 because the fuel state, the restriction and the clock are three independent things that must all stay live. Manual demand 2, matched to the row.

**EEG relevance** — Like H2, a sustained elevation rather than a single transient, but with a discrete recovery point (the tank change) that splits the trial into a pre-cure and post-cure segment on the same clock. Those two segments are matched for visuals and manual demand and differ in what the pilot has to keep in mind, which makes them a useful within-trial contrast independent of the between-mission one.

**Expected errors** — Not identifying the selector as the cure; switching to the empty tank; switching and then forgetting the remaining tank is finite; dropping the crossing restriction while troubleshooting.

**Success criteria** — Power restored by tank selection, restriction honoured, an explicit continue/divert decision made in window.

**Failure conditions** — Crash; fuel exhaustion; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.18 and the NTSB's long-standing finding that fuel starvation — fuel aboard but not reaching the engine — remains a leading cause of light-aircraft power loss, and is usually cured by the selector.

**Approximations / not modelled** — Two lumped tanks with a linear burn split and no cross-feed, unporting or attitude dependence. Fuel quantity is shown as a gauge value, not a totaliser, and burn is scaled so the constraint is live inside 300 s.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `DECISION_PROMPT`, `MISSION_END`

---

### H4V2 — Flap failure on final — the no-flap approach

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 2 of 3 |
| Cognitive mechanism | re-computing a procedure whose parameters have changed |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 78.8 / 100 |

**Objective** — Fly the approach, discover the flaps will not extend, re-compute the approach and land without them.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Descend, configure and land. Runway 01 is 600 m long. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND CALM. RUNWAY 600 M. |
| 78 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 84 | ±8 | SystemFailure | `FlapMotorFailure` severity 1.00 |
| 110 | ±0 | Checklist | `FLAP_FAIL` — FLAP FAILURE — NO-FLAP APPROACH |
| 128 | ±0 | Message | NO-FLAP APPROACH SPEED IS 15 KM/H HIGHER. LANDING DISTANCE INCREASES BY ABOUT HALF. |
| 150 | ±8 | Decision | LAND ON 600 M WITHOUT FLAPS, OR GO AROUND? — decide  *(respond within 10.0 s)* |
| 204 | ±10 | Probe | CONFIRM APPROACH SPEED — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ███ 3 |
| decision complexity | ███ 3 |
| working memory | ███ 3 |
| attention switching | ███ 3 |
| situation awareness | ███ 3 |
| perception | ██ 2 |
| manual control | ████ 4 |
| procedural load | ████ 4 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 78, physical 58, temporal 68, performance 68, effort 82, frustration 64 → RTLX 70. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by RE-COMPUTATION, where variant 1 of this cell is irreversible commitment under a clock. The engine failure in H4 removes options; this one keeps every option and changes all their numbers. A rehearsed procedure the pilot already knows — the approach — must be executed with two of its parameters altered at once (a higher speed and a longer roll) onto a runway short enough that the alteration matters. Procedural load is 4 because the drill is real and the recomputed approach must then actually be flown. Manual demand is 4, one above the row's other two, which is the permitted maximum spread: a flapless approach is genuinely harder to fly and that is declared, not concealed. The control-activity covariates exist to let an analysis account for it.

**EEG relevance** — A discovery event (selecting flaps and getting nothing) that is neither loud nor sudden, so CUE_ONSET is driven by the pilot's own action rather than by an annunciation — the only mission in the bank where the pilot effectively creates their own cue onset. Reaction time from selection to first response is therefore a purer detection measure than in the annunciated failures.

**Expected errors** — Flying the normal approach speed and floating; landing long and running off the end; continuing to cycle the flap selector; not deciding.

**Success criteria** — Failure recognised, drill run, an explicit decision made in window, touchdown on the runway with the aircraft stopped on it.

**Failure conditions** — Crash; runway excursion; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.18 (flap malfunction) and ch.8: a no-flap approach is flown faster and flatter and lengthens the landing roll substantially.

**Approximations / not modelled** — The flap motor fails to a fixed position and cannot be recovered; there is no manual extension and no split-flap asymmetry in this mission. The quoted speed and distance penalties are given to the pilot in a radio call rather than being looked up in a POH.

**Required event markers** — `MISSION_START`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `DECISION_PROMPT`, `MISSION_END`

---

### L1V3 — Normal departure to a higher level-off

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 3 of 3 |
| Cognitive mechanism | reference - single-threaded procedure to a higher level-off |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 800 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 28.0 / 100 |

**Objective** — Taxi to runway 01, hold short, take off and climb to 800 m on runway heading.

**Brief to the participant** — You are on stand 1, engine running. Taxi via taxiway A to the holding point for runway 01 and hold short. Wait for your take-off clearance, then line up, take off and climb straight ahead to 800 m. Clear day, no other traffic.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. |
| 86 | ±6 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — climb runway heading to 800 m. |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand | █ 1 |
| decision complexity | █ 1 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | █ 1 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty |  0 |
| communication | █ 1 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 30, physical 26, temporal 20, performance 26, effort 31, frustration 15 → RTLX 25. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The take-off row's reference, realised a third way. The only difference from variant 1 is the level-off altitude, which is deliberate: a LOW cell's job is to establish what the phase costs before anything is added, and inventing demand to make the three LOW variants look different would defeat that. What the variants buy in the LOW cells is protection against a participant flying the identical trial twice, not mechanism diversity — the mechanism diversity that matters is in MEDIUM and HIGH.

**EEG relevance** — Phase-matched baseline for M1V3 and H1V3; taxi baseline identical to the row.

**Expected errors** — Taxiing past the hold-short line; departing without clearance; levelling at 600 m out of habit if the participant has flown another variant.

**Success criteria** — Hold short respected, airborne and established at 800 m +/- 90 m on runway heading.

**Failure conditions** — Runway incursion; crash; failure to get airborne inside the trial.

**Aviation basis** — FAA-H-8083-3C ch.2 and ch.5 (normal take-off and climb).

**Approximations / not modelled** — Same as L1: a small-field taxi layout, and ATC is a scripted sequence.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `MISSION_END`

---

### L2V3 — Departure heading and level-off

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 3 of 3 |
| Cognitive mechanism | reference - steady-state tracking with a turn and a level-off |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 12.8 / 100 |

**Objective** — Fly an assigned departure heading and level off at the assigned altitude.

**Brief to the participant** — Departure leg, 400 m. ATC will give you a heading and a climb. Fly them accurately. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 68 | ±0 | HeadingChange | heading → 340 |
| 76 | ±0 | AltitudeChange | altitude → 700 m |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand |  0 |
| decision complexity |  0 |
| working memory | █ 1 |
| attention switching |  0 |
| situation awareness | █ 1 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load |  0 |
| uncertainty |  0 |
| communication |  0 |
| error consequence |  0 |

**Pre-registered expected NASA-TLX** — mental 29, physical 26, temporal 21, performance 25, effort 30, frustration 15 → RTLX 24. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The climb row's reference. Two instructions arrive close together and then nothing else happens for four minutes — enough activity to match the row's manual demand, far too little to compete for any cognitive resource.

**EEG relevance** — Phase-matched baseline for M2V3 and H2V3.

**Expected errors** — Overshooting the heading; climbing through the assigned altitude.

**Success criteria** — Heading and altitude captured and held within tolerance.

**Failure conditions** — Crash; loss of control.

**Aviation basis** — Routine departure vectoring; FAA-H-8083-3C ch.3.

**Approximations / not modelled** — No SID; ATC is a scripted sequence with no dialogue.

**Required event markers** — `MISSION_START`, `TARGET_CHANGE`, `MISSION_END`

---

### L3V3 — Waypoint navigation

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 3 of 3 |
| Cognitive mechanism | reference - steady-state tracking to displayed targets |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Navigate |
| Predicted Load Index | 14.8 / 100 |

**Objective** — Track a four-waypoint route at 700 m.

**Brief to the participant** — Straight and level at 700 m. A four-point route is displayed on the navigation display. Fly it, holding altitude. There is no time limit and no other traffic. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | MAINTAIN 700 M. CLEARED ALPHA, BRAVO, CHARLIE, DELTA. |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand |  0 |
| decision complexity |  0 |
| working memory | █ 1 |
| attention switching |  0 |
| situation awareness | ██ 2 |
| perception | █ 1 |
| manual control | ██ 2 |
| procedural load |  0 |
| uncertainty |  0 |
| communication |  0 |
| error consequence |  0 |

**Pre-registered expected NASA-TLX** — mental 30, physical 25, temporal 20, performance 26, effort 30, frustration 15 → RTLX 24. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The cruise row's reference, realised as navigation rather than as a heading hold. Situation awareness is 2 rather than 1 because the pilot must relate the display to the aeroplane, which is the same relating that M3V3 and H3V3 require — the reference has to contain the activity, only without the competition. One waypoint, displayed continuously, no time limit.

**EEG relevance** — Phase-matched baseline for M3V3 and H3V3, with the navigation display in active use so display scanning is present in the reference too.

**Expected errors** — Wandering off track; losing altitude while looking at the display; turning early or late at a waypoint.

**Success criteria** — All four waypoints reached, altitude held within +/- 70 m.

**Failure conditions** — Crash; loss of control.

**Aviation basis** — Pilotage and basic navigation display use; FAA-H-8083-25 ch.16.

**Approximations / not modelled** — The waypoints are markers in the world and symbols on the MFD; there is no VOR, GPS receiver or flight plan to program.

**Required event markers** — `MISSION_START`, `MISSION_END`

---

### L4V3 — Normal landing from a closer final

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | LOW |
| Variant | 3 of 3 |
| Cognitive mechanism | reference - rehearsed approach from a shorter final |
| Flight phase | Approach |
| Start | Airborne at (0, 400, -9000) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 35.0 / 100 |

**Objective** — Fly a straight-in approach and land on runway 01.

**Brief to the participant** — You are 9 km on final for runway 01 at 400 m. Descend, configure and land. Clear day, light air, no other traffic.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND CALM. |
| 102 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | █ 1 |
| temporal demand | ██ 2 |
| decision complexity | █ 1 |
| working memory | █ 1 |
| attention switching | █ 1 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ███ 3 |
| procedural load | ██ 2 |
| uncertainty |  0 |
| communication | █ 1 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 35, physical 42, temporal 30, performance 35, effort 42, frustration 22 → RTLX 34. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — The approach row's reference, from a closer starting point so that a participant who has flown another variant does not simply repeat a memorised profile. Temporal demand is 2 rather than 1 because there is less track distance to lose the height in; it remains an unhurried, single-threaded approach with nothing to decide.

**EEG relevance** — Phase-matched baseline for M4V3 and H4V3.

**Expected errors** — High on final because the descent was started late; long touchdown.

**Success criteria** — Touchdown on the runway, wings level, sink rate inside the acceptable band.

**Failure conditions** — Crash; runway excursion; not landed inside the trial.

**Aviation basis** — Normal approach and landing, FAA-H-8083-3C ch.8.

**Approximations / not modelled** — No glideslope guidance and no PAPI; the approach is flown visually.

**Required event markers** — `MISSION_START`, `CHECKLIST_START`, `MISSION_END`

---

### M1V3 — Conditional line-up clearance

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 3 of 3 |
| Cognitive mechanism | prospective memory - holding an instruction until its condition occurs |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 55.5 / 100 |

**Objective** — Taxi for runway 01, hold a conditional clearance until its condition is met, then line up and depart.

**Brief to the participant** — You are on stand 1, engine running. Taxi via taxiway A for runway 01 and hold short. There is arriving traffic. Departure is runway heading, climb 600 m. Clear day.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. ONE ARRIVAL. |
| 68 | ±5 | Readback | BEHIND THE LANDING AIRCRAFT, LINE UP RUNWAY 01 BEHIND. READ BACK  *(respond within 9.0 s)* |
| 82 | ±6 | Traffic | TRAFFIC ON SHORT FINAL RUNWAY 01 |
| 110 | ±0 | Message | CONTACT TOWER 118.7 AFTER THE ARRIVAL HAS PASSED. |
| 122 | ±8 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — the arrival is clear, no delay. |
| 206 | ±12 | Probe | CONFIRM AIRBORNE AND CLIMBING — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | ███ 3 |
| attention switching | ███ 3 |
| situation awareness | ██ 2 |
| perception | ███ 3 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty | █ 1 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 60, physical 30, temporal 44, performance 48, effort 60, frustration 42 → RTLX 47. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by PROSPECTIVE MEMORY — remembering to do something later, on a cue that is not a reminder. This is the mechanism neither of the other two variants of this cell uses: M1 is visual search against a present scene, M1V2 is replacing a plan already held, and this is holding an intention across a delay while attending to something else. It is also the one mechanism here with a documented aviation failure mode of its own: conditional clearances are a recognised runway-incursion risk precisely because the pilot must both identify the right aircraft and inhibit acting until it has passed. Manual demand 2, matched to the row.

**EEG relevance** — The interval between the conditional clearance and its execution is a maintenance period of a different kind from M1V2's: nothing must be recalled on demand, but an intention must be kept live against a competing task. The frequency-change call at T0+48 is a deliberate secondary intention layered on the first.

**Expected errors** — Lining up before the arrival has passed (the incursion); forgetting the clearance entirely and waiting for a fresh one; missing the frequency change.

**Success criteria** — Conditional clearance acknowledged and NOT acted on early, hold short respected, airborne and established at 600 m.

**Failure conditions** — Runway incursion; crash; failure to get airborne inside the trial.

**Aviation basis** — ICAO Doc 4444 conditional clearances, and the EUROCONTROL/FAA runway-safety literature identifying mis-executed conditional line-up clearances as a recurring incursion cause.

**Approximations / not modelled** — The arriving aircraft is a scripted visual object on a fixed path; it cannot be talked to, and the hold-short gate is released by the scripted clearance rather than by the pilot's judgement that the runway is clear.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `TRAFFIC_ONSET`, `PROBE_ONSET`, `MISSION_END`

---

### M2V3 — Re-clearances and a frequency change

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 3 of 3 |
| Cognitive mechanism | working memory turnover across mixed verbal and numeric items |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 52.3 / 100 |

**Objective** — Fly the departure while absorbing a sequence of amended clearances and an administrative change.

**Brief to the participant** — Departure leg, 400 m, heading 000°. ATC is busy this morning. Fly the departure and comply with what you are given. Clear day.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. |
| 66 | ±4 | Readback | CLEARANCE: climb 700 m, heading 020. READ BACK  *(respond within 8.0 s)* |
| 76 | ±0 | AltitudeChange | altitude → 700 m |
| 82 | ±0 | HeadingChange | heading → 20 |
| 108 | ±0 | Message | SQUAWK 4271. CONTACT DEPARTURE 124.35. |
| 132 | ±6 | Readback | AMENDED: climb 850 m, heading 350. READ BACK  *(respond within 8.0 s)* |
| 142 | ±0 | AltitudeChange | altitude → 850 m |
| 148 | ±0 | HeadingChange | heading → 350 |
| 194 | ±10 | Probe | CONFIRM ASSIGNED SQUAWK — respond  *(respond within 6.0 s)* |
| 238 | ±10 | Probe | CONFIRM ASSIGNED ALTITUDE — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | █ 1 |
| working memory | ████ 4 |
| attention switching | ███ 3 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | █ 1 |
| uncertainty | █ 1 |
| communication | ███ 3 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 64, physical 30, temporal 56, performance 50, effort 62, frustration 46 → RTLX 51. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by VOLUME of items rather than by their difficulty, which distinguishes it from both siblings: M2 is one startling event with nothing to remember, M2V2 is one ambiguous event with nothing to remember, and this has nothing surprising at all and six things to hold. The items are deliberately of mixed type — two altitudes, two headings, a squawk and a frequency — because same-type items interfere with each other in a way mixed items do not, and the design wants turnover, not a memory-span test. Communication is 4, the highest in the bank. Manual demand 2, matched to the row.

**EEG relevance** — The most regular event structure in the climb row: paired clearance/execution events at predictable spacing, which supports averaging across repetitions within a single trial rather than relying on the between-mission contrast alone.

**Expected errors** — Flying the first clearance after the second has been issued; confusing the squawk with the frequency; answering a probe with a superseded value.

**Success criteria** — Final assigned altitude and heading held, both probes answered correctly in window.

**Failure conditions** — Crash; loss of control.

**Aviation basis** — Clearance read-back/hear-back load; ICAO Doc 9683 human-factors training manual, communication chapter.

**Approximations / not modelled** — Squawk and frequency are spoken and acknowledged but there is no transponder or radio panel to set them on, so the items are held in memory rather than actioned — which is the load being manipulated, but it is not the full task a real pilot would perform.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `TARGET_CHANGE`, `PROBE_ONSET`, `MISSION_END`

---

### M3V3 — Traffic search in reduced visibility

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 3 of 3 |
| Cognitive mechanism | visual search and sustained monitoring under degraded input |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.08 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.40 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 53.3 / 100 |

**Objective** — Hold altitude and heading while searching for and acquiring called traffic in poor visibility.

**Brief to the participant** — Straight and level at 700 m, heading 000°. Visibility is reduced in haze. ATC will call traffic to you. Hold altitude and heading.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | MAINTAIN 700 M, HEADING 000°. HAZE, VISIBILITY 4 KM. |
| 74 | ±6 | Traffic | TRAFFIC 11 O'CLOCK, 3 MILES, OPPOSITE DIRECTION — REPORT IN SIGHT  *(respond within 9.0 s)* |
| 102 | ±7 | Traffic | TRAFFIC 1 O'CLOCK, CROSSING LEFT TO RIGHT  *(respond within 9.0 s)* |
| 132 | ±8 | Weather | intensity 0.45 for 100 s |
| 170 | ±8 | Traffic | SECOND TRAFFIC 2 O'CLOCK, 2 MILES — REPORT IN SIGHT  *(respond within 9.0 s)* |
| 214 | ±10 | Probe | CONFIRM LEVEL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | █ 1 |
| working memory | ██ 2 |
| attention switching | ████ 4 |
| situation awareness | ██ 2 |
| perception | ████ 4 |
| manual control | ██ 2 |
| procedural load | █ 1 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | █ 1 |

**Pre-registered expected NASA-TLX** — mental 60, physical 30, temporal 42, performance 52, effort 62, frustration 50 → RTLX 49. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by PERCEPTUAL load, the third distinct mechanism in this cell alongside M3's working memory and M3V2's re-planning. The pilot is asked to do something easy to describe and hard to do — find a small object against a low-contrast background while holding altitude on instruments — so Perception and Attention Switching are 4 while Decision Complexity and Working Memory stay at 2. The searches are deliberately not all successful: one target is genuinely difficult to acquire, so a participant who reports everything in sight immediately is displaying a response bias worth measuring. Manual demand 2, matched to the row.

**EEG relevance** — Alpha-band effects of visual search and the switching between an outside scan and an instrument scan are among the better-established EEG workload signatures, which makes this the mission most likely to produce a positive result if the recording chain is sound — and therefore a useful pipeline check as well as a condition.

**Expected errors** — Losing altitude while looking outside; reporting traffic in sight that has not been acquired; missing the second call while still searching for the first.

**Success criteria** — Both traffic calls responded to in window, altitude within +/- 70 m and heading within +/- 12° for the majority of the trial.

**Failure conditions** — Crash; loss of control; mid-air collision.

**Aviation basis** — See-and-avoid limitations and the empty-field problem; FAA-H-8083-25 ch.2 and AC 90-48. The see-and-avoid literature is explicit that acquisition rates in haze are poor even when the traffic is called.

**Approximations / not modelled** — Traffic is a small number of scripted aircraft on fixed paths; 'report in sight' is a keypress and the simulator cannot verify the pilot actually saw the aeroplane, only that they responded and when.

**Required event markers** — `MISSION_START`, `TRAFFIC_ONSET`, `WEATHER_ONSET`, `PROBE_ONSET`, `MISSION_END`

---

### M4V3 — Traffic on the runway — the go-around

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | MEDIUM |
| Variant | 3 of 3 |
| Cognitive mechanism | abandoning a committed procedure and executing a rehearsed alternative |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 58.3 / 100 |

**Objective** — Fly the approach, recognise that the runway is occupied, go around, and re-position for a second approach.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Descend, configure and land. There is departing traffic ahead of you. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CONTINUE APPROACH RUNWAY 01. ONE DEPARTURE AHEAD. |
| 86 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 106 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |
| 138 | ±6 | Traffic | TRAFFIC STILL ON THE RUNWAY |
| 154 | ±6 | GoAround | traffic on the runway — climb runway heading 500 m |
| 180 | ±0 | Message | CLIMB 500 M RUNWAY HEADING. EXPECT A LEFT CIRCUIT FOR A SECOND APPROACH. |
| 222 | ±10 | Probe | REPORT DOWNWIND — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | ██ 2 |
| attention switching | ██ 2 |
| situation awareness | ███ 3 |
| perception | ███ 3 |
| manual control | ███ 3 |
| procedural load | ████ 4 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 62, physical 50, temporal 66, performance 56, effort 68, frustration 50 → RTLX 59. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — MEDIUM by PROCEDURAL EXECUTION under the reluctance to abandon a plan. The go-around is a rehearsed drill, so decision complexity is only 2 — the pilot is told to go around and there is nothing to work out. What makes it demanding is that it must be executed promptly, in the correct order (power, attitude, configuration), at the exact moment the pilot is most committed to landing. Procedural load is 4, the highest in this row's MEDIUM cells. That is a different mechanism from M4's degraded perception and M4V2's time-pressured re-plan, and it is the one with the best-documented failure mode: plan-continuation bias. Manual demand 3, matched to the row.

**EEG relevance** — GO_AROUND_COMMANDED to GO_AROUND_INITIATED is a directly measured response latency at a moment of maximum commitment, and is the cleanest behavioural index of plan-continuation bias the bank produces. It pairs with the EEG epoch at the same instant.

**Expected errors** — Continuing to land anyway; going around but retracting flap before establishing a climb; losing runway heading during the go-around; missing the downwind report while re-configuring.

**Success criteria** — Go-around initiated promptly, climb established on runway heading at 500 m, aircraft under control at the end of the trial.

**Failure conditions** — Crash; landing on an occupied runway; loss of control.

**Aviation basis** — Go-around technique and the decision to go around; FAA-H-8083-3C ch.8, and the Flight Safety Foundation's finding that the commonest go-around error is not executing one.

**Approximations / not modelled** — The trial ends in the climb-out rather than at a second touchdown — 300 s does not contain an approach, a go-around and a full circuit — so the second approach is briefed but not flown.

**Required event markers** — `MISSION_START`, `TRAFFIC_ONSET`, `GO_AROUND_COMMANDED`, `MISSION_END`

---

### H1V3 — Low-visibility taxi past a hot spot

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 3 of 3 |
| Cognitive mechanism | sustained monitoring under degraded perception + prospective memory |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.62 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 88.8 / 100 |

**Objective** — Taxi to runway 01 in poor visibility, navigate a known hot spot by signs and markings alone, hold a conditional clearance, and depart.

**Brief to the participant** — You are on stand 1, engine running. Visibility is poor. Taxi via taxiway A for runway 01. There is a hot spot where taxiway A meets the link — the geometry there is confusing and aircraft have entered the runway from it by mistake. Hold short of runway 01. Departure is runway heading, climb 600 m.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. RVR 550 METRES. CAUTION HOT SPOT AT THE LINK. |
| 46 | ±6 | Traffic | TRAFFIC AHEAD ON ALPHA |
| 72 | ±6 | Readback | BEHIND THE DEPARTING AIRCRAFT, LINE UP RUNWAY 01 BEHIND. READ BACK  *(respond within 9.0 s)* |
| 86 | ±6 | Traffic | TRAFFIC DEPARTING RUNWAY 01 |
| 102 | ±8 | Probe | CONFIRM POSITION ON ALPHA — respond  *(respond within 6.0 s)* |
| 120 | ±0 | Message | REPORT WHEN YOU ARE HOLDING SHORT. TRAFFIC 5 MILES FINAL. |
| 140 | ±8 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — no delay, traffic 3 miles final. |
| 152 | ±8 | Weather | intensity 0.50 for 90 s |
| 232 | ±12 | Probe | REPORT PASSING 300 M — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ███ 3 |
| decision complexity | ███ 3 |
| working memory | ████ 4 |
| attention switching | ████ 4 |
| situation awareness | ████ 4 |
| perception | ████ 4 |
| manual control | ██ 2 |
| procedural load | ███ 3 |
| uncertainty | ████ 4 |
| communication | ███ 3 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 84, physical 36, temporal 70, performance 66, effort 84, frustration 68 → RTLX 68. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by SUSTAINED MONITORING under degraded input, combined with an intention held across it — a third distinct mechanism for this cell. H1 is concurrency (many things at once) and H1V2 is a single hard decision under a clock; this is neither. Nothing here is individually difficult and there is no moment of crisis. The load is that the pilot can never stop checking where they are, because the usual cue — being able to see the layout — has been removed, and they must simultaneously keep a conditional clearance alive. Perception and Uncertainty are both 4, which no other take-off mission has. Manual demand stays at 2: taxiing slowly in poor visibility is not physically harder, which is exactly why this mission is a clean high-load condition for an EEG contrast.

**EEG relevance** — The bank's best candidate for a SUSTAINED rather than event-locked effect in the take-off row, and therefore the natural partner to H2 and H3V2 in a windowed-classifier analysis. The two position-confirmation probes are spare-capacity measures taken under continuous monitoring load rather than after a discrete event.

**Expected errors** — Taking a wrong turn at the hot spot; crossing the hold-short line while looking for signs; lining up before the departing aircraft has gone; missing a probe while navigating.

**Success criteria** — Correct route flown, hold short respected, conditional clearance not acted on early, airborne and established at 600 m.

**Failure conditions** — Runway incursion; crash; failure to get airborne inside the trial.

**Aviation basis** — Low-visibility ground operations and published hot spots; FAA-H-8083-25 ch.14 and the FAA/ICAO runway-safety programmes, which identify hot spots precisely because layout confusion under reduced visibility is a leading incursion cause.

**Approximations / not modelled** — Reduced visibility is fog density and sky severity, not a modelled RVR: the quoted 550 m is a briefing figure, not a measured one. The hot spot is the existing link-taxiway junction, briefed as confusing rather than physically redesigned to be so.

**Required event markers** — `MISSION_START`, `ATC_MESSAGE`, `TRAFFIC_ONSET`, `WEATHER_ONSET`, `PROBE_ONSET`, `MISSION_END`

---

### H2V3 — Traffic and terrain while being re-cleared

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 3 of 3 |
| Cognitive mechanism | prioritisation among simultaneous conflicting demands |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.06 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.28 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 84.5 / 100 |

**Objective** — Fly the departure and prioritise correctly when traffic, terrain and a new clearance all demand attention at once.

**Brief to the participant** — Departure leg, 400 m, heading 000°. Rising ground to the north-east. ATC will work you. Fly the departure. Hazy.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | DEPARTURE — maintain 400 m, heading 000°. TERRAIN NORTH-EAST. |
| 66 | ±0 | AltitudeChange | altitude → 800 m |
| 106 | ±6 | Traffic | TRAFFIC 2 O'CLOCK, CROSSING, SAME LEVEL  *(respond within 7.0 s)* |
| 114 | ±5 | Readback | AMENDED: right heading 050 for spacing. READ BACK  *(respond within 8.0 s)* |
| 122 | ±0 | HeadingChange | heading → 50 |
| 128 | ±0 | Alarm | TERRAIN AHEAD — CHECK ALTITUDE |
| 140 | ±8 | Decision | ACCEPT THE TURN, OR REFUSE IT FOR TERRAIN? — decide  *(respond within 9.0 s)* |
| 212 | ±10 | Probe | REPORT LEVEL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ████ 4 |
| decision complexity | ███ 3 |
| working memory | ███ 3 |
| attention switching | ████ 4 |
| situation awareness | ████ 4 |
| perception | ███ 3 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty | ███ 3 |
| communication | ███ 3 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 82, physical 34, temporal 80, performance 64, effort 82, frustration 66 → RTLX 68. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by PRIORITISATION, which is not the same as concurrency and is the third distinct mechanism in this cell. H2 is one slow problem; H2V2 is one fast commitment; this is three demands arriving together that CANNOT all be satisfied — the clearance turns the aeroplane toward the terrain and toward the traffic, so complying immediately is wrong and refusing outright is also wrong. Attention switching and situation awareness are both 4. The correct behaviour is the standard aviate-navigate-communicate ordering, which gives a defensible right answer to score against rather than a matter of taste. Manual demand 2, matched to the row.

**EEG relevance** — Three separately-marked onsets inside about twenty seconds — TRAFFIC_ONSET, a clearance, and a WARNING_APPEARS — which makes this the bank's best test of whether an EEG workload index tracks demand at a resolution finer than the mission. If the index only separates missions and not the segments inside this one, that is an informative negative result.

**Expected errors** — Turning into the terrain because ATC said so (compliance bias); fixating on the traffic and losing the altitude; refusing the turn without saying so; letting the decision window expire.

**Success criteria** — Terrain avoided, traffic acknowledged, an explicit decision made in window, altitude held.

**Failure conditions** — Crash; terrain impact; loss of control; no decision made.

**Aviation basis** — Aviate-navigate-communicate prioritisation and the handling of conflicting ATC instructions; FAA-H-8083-25 ch.2 and ch.17. Accepting a clearance that the pilot can see is unsafe is a documented crew-resource-management failure.

**Approximations / not modelled** — The terrain warning is a scripted call, not a modelled TAWS with a real terrain database lookup — though the rising ground it refers to genuinely exists in the world and can genuinely be hit.

**Required event markers** — `MISSION_START`, `TRAFFIC_ONSET`, `WARNING_APPEARS`, `DECISION_PROMPT`, `MISSION_END`

---

### H3V3 — Everything at once in the cruise

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 3 of 3 |
| Cognitive mechanism | concurrency - competing demands with no gaps between them |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.10 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.30 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | HoldTargets |
| Predicted Load Index | 89.5 / 100 |

**Objective** — Hold the cruise while a clearance, traffic and a systems annunciation compete for attention simultaneously.

**Brief to the participant** — Straight and level at 700 m, heading 000°. Busy sector. Hold altitude and heading and comply with what you are given. Hazy.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | MAINTAIN 700 M, HEADING 000°. |
| 70 | ±5 | Readback | CLEARANCE: descend 500 m, heading 030, cross the boundary WITHIN 4 MINUTES. READ BACK  *(respond within 9.0 s)* |
| 80 | ±0 | AltitudeChange | altitude → 500 m |
| 86 | ±0 | HeadingChange | heading → 30 |
| 96 | ±5 | Traffic | TRAFFIC 12 O'CLOCK, 2 MILES, CONVERGING  *(respond within 7.0 s)* |
| 106 | ±7 | SystemFailure | `EngineRoughness` severity 0.30 |
| 138 | ±0 | Checklist | `ENG_ROUGH` — ROUGH RUNNING DRILL |
| 180 | ±6 | Readback | AMENDED: maintain 600 m, heading 010. READ BACK  *(respond within 8.0 s)* |
| 190 | ±0 | AltitudeChange | altitude → 600 m |
| 196 | ±0 | HeadingChange | heading → 10 |
| 238 | ±10 | Probe | CONFIRM ASSIGNED ALTITUDE — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ████ 4 |
| decision complexity | ███ 3 |
| working memory | ████ 4 |
| attention switching | ████ 4 |
| situation awareness | ████ 4 |
| perception | ███ 3 |
| manual control | ██ 2 |
| procedural load | ████ 4 |
| uncertainty | ██ 2 |
| communication | ████ 4 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 86, physical 36, temporal 84, performance 68, effort 86, frustration 70 → RTLX 72. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by CONCURRENCY — the highest predicted load in the cruise row and the mission with the least idle time in the bank. Each individual demand here is one a MEDIUM mission would carry alone; the manipulation is that they overlap, so that no task can be finished before the next begins and the pilot is forced to interleave rather than queue. That is the distinction Wickens' multiple-resource account is about, and it is a different claim from H3's (information that is wrong) or H3V2's (a resource that is running out). Communication, working memory and attention switching are all 4. Manual demand stays at 2 — nothing about the aeroplane is degraded — which is what makes this a clean high-load condition despite being the busiest.

**EEG relevance** — The saturation case. If an EEG workload index does not separate this from L3V3, the index is not measuring workload in this paradigm at all, so it functions as the positive control for the whole cruise row. The overlapping events also mean epochs here will contaminate each other, and the analysis should use the sustained window rather than event-related averaging.

**Expected errors** — Dropping the crossing time; flying a superseded clearance; abandoning the drill part-way; missing the traffic; answering the probe with the old altitude.

**Success criteria** — Final assigned altitude and heading held, drill run, traffic acknowledged, probe answered in window.

**Failure conditions** — Crash; loss of control.

**Aviation basis** — Task saturation and interleaving in single-pilot operations; FAA-H-8083-25 ch.2 (workload management) and Wickens' multiple-resource account of concurrent-task interference.

**Approximations / not modelled** — Roughness here is a mild 0.3 severity that does not threaten the flight — it is present as a demand on attention rather than as an emergency, which is a deliberate difference from H2V2 where the same failure kind is severe.

**Required event markers** — `MISSION_START`, `TARGET_CHANGE`, `TRAFFIC_ONSET`, `TRIGGER_ARMED`, `CUE_ONSET`, `CHECKLIST_START`, `PROBE_ONSET`, `MISSION_END`

---

### H4V3 — Late go-around and re-sequence

| field | value |
|-------|-------|
| Experimental axis | cognitive (manual demand matched within the phase row) |
| Workload class | HIGH |
| Variant | 3 of 3 |
| Cognitive mechanism | startle followed immediately by re-planning at high stakes |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, wind CALM (crosswind 0.0 m/s, headwind 0.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 80.3 / 100 |

**Objective** — Fly the approach, execute a go-around ordered at low altitude, and immediately absorb a re-sequencing clearance while re-configuring.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Descend, configure and land. Moderate traffic. Clear day, light air.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CONTINUE APPROACH RUNWAY 01. |
| 82 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 102 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |
| 146 | ±5 | Traffic | VEHICLE CROSSING THE RUNWAY |
| 166 | ±7 | GoAround | GO AROUND, GO AROUND — vehicle on the runway |
| 178 | ±5 | Readback | CLIMB 600 M, LEFT HEADING 300, NUMBER THREE IN SEQUENCE. READ BACK  *(respond within 9.0 s)* |
| 188 | ±0 | AltitudeChange | altitude → 600 m |
| 194 | ±0 | HeadingChange | heading → 300 |
| 208 | ±6 | Traffic | TRAFFIC 10 O'CLOCK, SAME LEVEL, JOINING  *(respond within 7.0 s)* |
| 252 | ±10 | Probe | CONFIRM YOUR SEQUENCE NUMBER — respond  *(respond within 6.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ███ 3 |
| temporal demand | ████ 4 |
| decision complexity | ███ 3 |
| working memory | ███ 3 |
| attention switching | ███ 3 |
| situation awareness | ████ 4 |
| perception | ███ 3 |
| manual control | ███ 3 |
| procedural load | ████ 4 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 86, physical 54, temporal 86, performance 70, effort 88, frustration 72 → RTLX 76. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — HIGH by STARTLE FOLLOWED BY LOAD, which is the sequence the startle literature identifies as the dangerous one and which neither sibling reproduces. H4 is a committed emergency with a clock; H4V2 is a recomputation with time to do it. Here the pilot is startled at the worst moment — low, slow, configured to land — and then, before the go-around is stabilised, is given three new items and a joining aircraft. The manipulation is the OVERLAP between the startle-recovery period and the arrival of new information, which is why working memory and attention switching are 4 despite the go-around itself being a rehearsed drill. Manual demand 3, matched to the row.

**EEG relevance** — The bank's cleanest startle epoch, with an unambiguous onset (GO_AROUND_COMMANDED) at a known aircraft state, followed by a measurable recovery interval before the clearance arrives. The contrast of interest is not just load versus baseline but whether the response to the clearance differs from the same clearance delivered in a calm state, which M2V3 provides.

**Expected errors** — Landing anyway; going around but mishandling the configuration; flying the runway heading instead of 300; losing the sequence number; missing the joining traffic while re-configuring.

**Success criteria** — Go-around initiated promptly, climb established, assigned altitude and heading captured, traffic acknowledged, probe answered in window.

**Failure conditions** — Crash; landing on an occupied runway; loss of control.

**Aviation basis** — Startle and surprise in flight operations (EASA startle-effect research; Landman et al. on surprise and the 'freeze' response), combined with go-around technique from FAA-H-8083-3C ch.8.

**Approximations / not modelled** — The crossing vehicle is a scripted traffic object rather than a ground vehicle model. The trial ends on the re-sequencing leg; there is no second approach inside 300 s.

**Required event markers** — `MISSION_START`, `TRAFFIC_ONSET`, `GO_AROUND_COMMANDED`, `TARGET_CHANGE`, `PROBE_ONSET`, `MISSION_END`

---

### XT1 — Crosswind take-off — light

| field | value |
|-------|-------|
| Experimental axis | psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale) |
| Workload class | LOW |
| Variant | 1 of 3 |
| Cognitive mechanism | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.04 ambient, wind 034/07 kt (crosswind 2.0 m/s, headwind 3.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 48.5 / 100 |

**Objective** — Taxi to runway 01, assess the crosswind against the aircraft's demonstrated limit, decide whether to depart, and if so fly an accurate crosswind take-off.

**Brief to the participant** — You are on stand 1, engine running. Surface wind is 034/07 kt — a crosswind of about 4 knots on runway 01. This aircraft's DEMONSTRATED CROSSWIND is 15 knots; above that the manufacturer makes no promises and you should not depart. Taxi via taxiway A, hold short, and you will be asked for your decision. If you depart: aileron into the wind on the roll, keep straight with rudder, and climb runway heading to 600 m.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. WIND 034/07 kt. |
| 66 | ±0 | Message | WIND CHECK: 034/07 kt. CROSSWIND COMPONENT 4 KNOTS. |
| 84 | ±6 | Decision | CROSSWIND 4 KT AGAINST A 15 KT LIMIT — DEPART OR HOLD? — decide  *(respond within 10.0 s)* |
| 112 | ±6 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — wind 034/07 kt, climb runway heading to 600 m. |
| 212 | ±12 | Probe | REPORT PASSING 300 M — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ██ 2 |
| decision complexity | ██ 2 |
| working memory | ██ 2 |
| attention switching | ██ 2 |
| situation awareness | ██ 2 |
| perception | ██ 2 |
| manual control | ██ 2 |
| procedural load | ██ 2 |
| uncertainty | █ 1 |
| communication | ██ 2 |
| error consequence | ██ 2 |

**Pre-registered expected NASA-TLX** — mental 45, physical 40, temporal 38, performance 42, effort 50, frustration 32 → RTLX 41. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Level 1 of three on the PSYCHOMOTOR-INTEGRATED axis: a 4 kt crosswind, about 26% of the demonstrated component. ManualControl rises with the level BY DESIGN — that is what is being manipulated — which is exactly why this mission is not on the cognitive axis, where manual demand is held constant. What must not be claimed from this series is that crosswind raised cognitive workload; what can be claimed is that integrated psychomotor/cognitive demand rose, and the logged control-activity covariates are what let those be told apart.

**EEG relevance** — Two separable segments on one clock. The DECISION_PROMPT epoch at ~84 s happens with the aeroplane STATIONARY at the holding point — no control activity, no movement artifact — so it is a clean event-related cognitive measure taken under the crosswind condition. The take-off roll and climb are the continuous psychomotor segment, analysed against the control-activity covariates rather than as cognitive load. Comparing the decision epoch across the three levels is the one contrast on this axis that is NOT motor-contaminated.

**Expected errors** — Departing above the demonstrated crosswind (the scored error at level 3); no aileron into wind on the roll; drifting downwind of the centreline; rotating early and being blown off the extended centreline.

**Success criteria** — An explicit decision made in window; if departing, the centreline held within the runway width during the roll and the climb flown on runway heading to 600 m.

**Failure conditions** — Runway excursion; crash; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.5 (crosswind take-off: aileron into the wind, directional control with rudder) and the C172 POH's 15 kt demonstrated crosswind, which is a demonstrated value rather than a certified limitation — a distinction the brief states honestly rather than presenting it as a hard limit.

**Approximations / not modelled** — The wind is a horizontal field with a power-law surface profile and band-limited gusts; there is no terrain-induced rotor, no mechanical turbulence from buildings, and no wind gradient below 3 m. Weathervaning is modelled as a fin yawing moment plus tyre grip, not as a full landing-gear side-force model.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `PROBE_ONSET`, `MISSION_END`

---

### XL1 — Crosswind landing — light

| field | value |
|-------|-------|
| Experimental axis | psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale) |
| Workload class | LOW |
| Variant | 1 of 3 |
| Cognitive mechanism | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.04 ambient, wind 034/07 kt (crosswind 2.0 m/s, headwind 3.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 55.5 / 100 |

**Objective** — Fly a crosswind approach, assess the crosswind against the demonstrated limit on short final, decide to land or go around, and carry out the decision.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Surface wind is 034/07 kt — a crosswind of about 4 knots. This aircraft's DEMONSTRATED CROSSWIND is 15 knots. Fly the approach: crab into the wind to hold the centreline, and straighten the aircraft with rudder before touchdown. You will be asked on short final whether you are continuing.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND 034/07 kt. |
| 80 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 100 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |
| 128 | ±0 | Message | WIND CHECK: 034/07 kt. CROSSWIND COMPONENT 4 KNOTS. |
| 144 | ±6 | Decision | CROSSWIND 4 KT AGAINST A 15 KT LIMIT — CONTINUE OR GO AROUND? — decide  *(respond within 9.0 s)* |
| 190 | ±10 | Probe | REPORT SHORT FINAL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ██ 2 |
| temporal demand | ███ 3 |
| decision complexity | ██ 2 |
| working memory | ██ 2 |
| attention switching | ██ 2 |
| situation awareness | ███ 3 |
| perception | ██ 2 |
| manual control | ███ 3 |
| procedural load | ██ 2 |
| uncertainty | █ 1 |
| communication | ██ 2 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 50, physical 46, temporal 46, performance 48, effort 56, frustration 36 → RTLX 47. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Level 1 of three on the psychomotor-integrated axis, and the harder half of the pair: a crosswind take-off is over in seconds whereas a crosswind approach is three minutes of continuous correction ending in a decrab that has to be timed. ManualControl reaches 3 at this level, the highest value anywhere in the bank — declared, not concealed, and the reason this mission is on its own axis. Note that the wind the aircraft is in DECREASES on the way down through the surface layer, so the crab that was correct at 500 m is too much at 50 m and the correction has to be continuously revised rather than set once.

**EEG relevance** — The continuous segment is the axis's psychomotor measure and must be read together with the control-activity covariates. The DECISION_PROMPT epoch on short final is the isolated cognitive component — but unlike the take-off pair it occurs while the pilot IS flying, so it carries motor activity that the take-off decision does not. That difference is deliberate and useful: the take-off decision and the landing decision are the same judgement made with and without concurrent manual control, which is a within-axis control for exactly the contamination this axis exists to handle.

**Expected errors** — Landing crabbed (side-loading the gear); drifting downwind of the centreline; over-controlling in the gusts at level 3; continuing above the demonstrated crosswind; deciding by default.

**Success criteria** — An explicit decision made in window; if continuing, touchdown on the runway, aligned with the centreline, wings level or into wind, sink rate inside the acceptable band.

**Failure conditions** — Crash; runway excursion; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.8 (crosswind approach and landing: the crab and the sideslip methods, and the decrab before touchdown) and the C172 POH's 15 kt demonstrated crosswind component.

**Approximations / not modelled** — As for the crosswind take-off. In addition, touchdown scoring judges sink rate, bank and alignment but does not model gear side-load, so landing crabbed is penalised through alignment and excursion rather than through a modelled undercarriage failure.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `PROBE_ONSET`, `MISSION_END`

---

### XT2 — Crosswind take-off — moderate

| field | value |
|-------|-------|
| Experimental axis | psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale) |
| Workload class | MEDIUM |
| Variant | 2 of 3 |
| Cognitive mechanism | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.07 ambient, wind 057/11 kt (crosswind 4.6 m/s, headwind 3.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 65.0 / 100 |

**Objective** — Taxi to runway 01, assess the crosswind against the aircraft's demonstrated limit, decide whether to depart, and if so fly an accurate crosswind take-off.

**Brief to the participant** — You are on stand 1, engine running. Surface wind is 057/11 kt — a crosswind of about 9 knots on runway 01. This aircraft's DEMONSTRATED CROSSWIND is 15 knots; above that the manufacturer makes no promises and you should not depart. Taxi via taxiway A, hold short, and you will be asked for your decision. If you depart: aileron into the wind on the roll, keep straight with rudder, and climb runway heading to 600 m.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. WIND 057/11 kt. |
| 66 | ±0 | Message | WIND CHECK: 057/11 kt. CROSSWIND COMPONENT 9 KNOTS. |
| 84 | ±6 | Decision | CROSSWIND 9 KT AGAINST A 15 KT LIMIT — DEPART OR HOLD? — decide  *(respond within 10.0 s)* |
| 112 | ±6 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — wind 057/11 kt, climb runway heading to 600 m. |
| 212 | ±12 | Probe | REPORT PASSING 300 M — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ███ 3 |
| temporal demand | ██ 2 |
| decision complexity | ███ 3 |
| working memory | ██ 2 |
| attention switching | ███ 3 |
| situation awareness | ███ 3 |
| perception | ███ 3 |
| manual control | ███ 3 |
| procedural load | ██ 2 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 59, physical 58, temporal 50, performance 55, effort 66, frustration 48 → RTLX 56. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Level 2 of three on the PSYCHOMOTOR-INTEGRATED axis: a 9 kt crosswind, about 60% of the demonstrated component. ManualControl rises with the level BY DESIGN — that is what is being manipulated — which is exactly why this mission is not on the cognitive axis, where manual demand is held constant. What must not be claimed from this series is that crosswind raised cognitive workload; what can be claimed is that integrated psychomotor/cognitive demand rose, and the logged control-activity covariates are what let those be told apart.

**EEG relevance** — Two separable segments on one clock. The DECISION_PROMPT epoch at ~84 s happens with the aeroplane STATIONARY at the holding point — no control activity, no movement artifact — so it is a clean event-related cognitive measure taken under the crosswind condition. The take-off roll and climb are the continuous psychomotor segment, analysed against the control-activity covariates rather than as cognitive load. Comparing the decision epoch across the three levels is the one contrast on this axis that is NOT motor-contaminated.

**Expected errors** — Departing above the demonstrated crosswind (the scored error at level 3); no aileron into wind on the roll; drifting downwind of the centreline; rotating early and being blown off the extended centreline.

**Success criteria** — An explicit decision made in window; if departing, the centreline held within the runway width during the roll and the climb flown on runway heading to 600 m.

**Failure conditions** — Runway excursion; crash; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.5 (crosswind take-off: aileron into the wind, directional control with rudder) and the C172 POH's 15 kt demonstrated crosswind, which is a demonstrated value rather than a certified limitation — a distinction the brief states honestly rather than presenting it as a hard limit.

**Approximations / not modelled** — The wind is a horizontal field with a power-law surface profile and band-limited gusts; there is no terrain-induced rotor, no mechanical turbulence from buildings, and no wind gradient below 3 m. Weathervaning is modelled as a fin yawing moment plus tyre grip, not as a full landing-gear side-force model.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `PROBE_ONSET`, `MISSION_END`

---

### XL2 — Crosswind landing — moderate

| field | value |
|-------|-------|
| Experimental axis | psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale) |
| Workload class | MEDIUM |
| Variant | 2 of 3 |
| Cognitive mechanism | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.07 ambient, wind 057/11 kt (crosswind 4.6 m/s, headwind 3.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 69.5 / 100 |

**Objective** — Fly a crosswind approach, assess the crosswind against the demonstrated limit on short final, decide to land or go around, and carry out the decision.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Surface wind is 057/11 kt — a crosswind of about 9 knots. This aircraft's DEMONSTRATED CROSSWIND is 15 knots. Fly the approach: crab into the wind to hold the centreline, and straighten the aircraft with rudder before touchdown. You will be asked on short final whether you are continuing.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND 057/11 kt. |
| 80 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 100 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |
| 128 | ±0 | Message | WIND CHECK: 057/11 kt. CROSSWIND COMPONENT 9 KNOTS. |
| 144 | ±6 | Decision | CROSSWIND 9 KT AGAINST A 15 KT LIMIT — CONTINUE OR GO AROUND? — decide  *(respond within 9.0 s)* |
| 190 | ±10 | Probe | REPORT SHORT FINAL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ███ 3 |
| temporal demand | ███ 3 |
| decision complexity | ███ 3 |
| working memory | ██ 2 |
| attention switching | ███ 3 |
| situation awareness | ███ 3 |
| perception | ███ 3 |
| manual control | ████ 4 |
| procedural load | ██ 2 |
| uncertainty | ██ 2 |
| communication | ██ 2 |
| error consequence | ███ 3 |

**Pre-registered expected NASA-TLX** — mental 64, physical 64, temporal 60, performance 62, effort 71, frustration 54 → RTLX 63. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Level 2 of three on the psychomotor-integrated axis, and the harder half of the pair: a crosswind take-off is over in seconds whereas a crosswind approach is three minutes of continuous correction ending in a decrab that has to be timed. ManualControl reaches 4 at this level, the highest value anywhere in the bank — declared, not concealed, and the reason this mission is on its own axis. Note that the wind the aircraft is in DECREASES on the way down through the surface layer, so the crab that was correct at 500 m is too much at 50 m and the correction has to be continuously revised rather than set once.

**EEG relevance** — The continuous segment is the axis's psychomotor measure and must be read together with the control-activity covariates. The DECISION_PROMPT epoch on short final is the isolated cognitive component — but unlike the take-off pair it occurs while the pilot IS flying, so it carries motor activity that the take-off decision does not. That difference is deliberate and useful: the take-off decision and the landing decision are the same judgement made with and without concurrent manual control, which is a within-axis control for exactly the contamination this axis exists to handle.

**Expected errors** — Landing crabbed (side-loading the gear); drifting downwind of the centreline; over-controlling in the gusts at level 3; continuing above the demonstrated crosswind; deciding by default.

**Success criteria** — An explicit decision made in window; if continuing, touchdown on the runway, aligned with the centreline, wings level or into wind, sink rate inside the acceptable band.

**Failure conditions** — Crash; runway excursion; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.8 (crosswind approach and landing: the crab and the sideslip methods, and the decrab before touchdown) and the C172 POH's 15 kt demonstrated crosswind component.

**Approximations / not modelled** — As for the crosswind take-off. In addition, touchdown scoring judges sink rate, bank and alignment but does not model gear side-load, so landing crabbed is penalised through alignment and excursion rather than through a modelled undercarriage failure.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `PROBE_ONSET`, `MISSION_END`

---

### XT3 — Crosswind take-off — near-limit

| field | value |
|-------|-------|
| Experimental axis | psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale) |
| Workload class | HIGH |
| Variant | 3 of 3 |
| Cognitive mechanism | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.10 ambient, wind 068/15G18 kt (crosswind 7.3 m/s, headwind 3.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | TaxiTakeoff |
| Predicted Load Index | 81.8 / 100 |

**Objective** — Taxi to runway 01, assess the crosswind against the aircraft's demonstrated limit, decide whether to depart, and if so fly an accurate crosswind take-off.

**Brief to the participant** — You are on stand 1, engine running. Surface wind is 068/15G18 kt — a crosswind of about 14 knots on runway 01. This aircraft's DEMONSTRATED CROSSWIND is 15 knots; above that the manufacturer makes no promises and you should not depart. Taxi via taxiway A, hold short, and you will be asked for your decision. If you depart: aileron into the wind on the roll, keep straight with rudder, and climb runway heading to 600 m.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. WIND 068/15G18 kt. |
| 66 | ±0 | Message | WIND CHECK: 068/15G18 kt. CROSSWIND COMPONENT 14 KNOTS. |
| 84 | ±6 | Decision | CROSSWIND 14 KT AGAINST A 15 KT LIMIT — DEPART OR HOLD? — decide  *(respond within 10.0 s)* |
| 112 | ±6 | ConfigCall | CLEARED FOR TAKE-OFF RUNWAY 01 — wind 068/15G18 kt, climb runway heading to 600 m. |
| 212 | ±12 | Probe | REPORT PASSING 300 M — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ███ 3 |
| decision complexity | ███ 3 |
| working memory | ██ 2 |
| attention switching | ████ 4 |
| situation awareness | ████ 4 |
| perception | ████ 4 |
| manual control | ████ 4 |
| procedural load | ██ 2 |
| uncertainty | ███ 3 |
| communication | ██ 2 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 73, physical 76, temporal 62, performance 68, effort 82, frustration 64 → RTLX 71. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Level 3 of three on the PSYCHOMOTOR-INTEGRATED axis: a 14 kt crosswind, about 95% of the demonstrated component. ManualControl rises with the level BY DESIGN — that is what is being manipulated — which is exactly why this mission is not on the cognitive axis, where manual demand is held constant. What must not be claimed from this series is that crosswind raised cognitive workload; what can be claimed is that integrated psychomotor/cognitive demand rose, and the logged control-activity covariates are what let those be told apart.

**EEG relevance** — Two separable segments on one clock. The DECISION_PROMPT epoch at ~84 s happens with the aeroplane STATIONARY at the holding point — no control activity, no movement artifact — so it is a clean event-related cognitive measure taken under the crosswind condition. The take-off roll and climb are the continuous psychomotor segment, analysed against the control-activity covariates rather than as cognitive load. Comparing the decision epoch across the three levels is the one contrast on this axis that is NOT motor-contaminated.

**Expected errors** — Departing above the demonstrated crosswind (the scored error at level 3); no aileron into wind on the roll; drifting downwind of the centreline; rotating early and being blown off the extended centreline.

**Success criteria** — An explicit decision made in window; if departing, the centreline held within the runway width during the roll and the climb flown on runway heading to 600 m.

**Failure conditions** — Runway excursion; crash; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.5 (crosswind take-off: aileron into the wind, directional control with rudder) and the C172 POH's 15 kt demonstrated crosswind, which is a demonstrated value rather than a certified limitation — a distinction the brief states honestly rather than presenting it as a hard limit.

**Approximations / not modelled** — The wind is a horizontal field with a power-law surface profile and band-limited gusts; there is no terrain-induced rotor, no mechanical turbulence from buildings, and no wind gradient below 3 m. Weathervaning is modelled as a fin yawing moment plus tyre grip, not as a full landing-gear side-force model.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `PROBE_ONSET`, `MISSION_END`

---

### XL3 — Crosswind landing — near-limit

| field | value |
|-------|-------|
| Experimental axis | psychomotor-integrated (manual demand is the manipulation — NOT on the L/M/H scale) |
| Workload class | HIGH |
| Variant | 3 of 3 |
| Cognitive mechanism | psychomotor-integrated demand at a graded crosswind, with an isolated limit decision |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.10 ambient, wind 068/15G18 kt (crosswind 7.3 m/s, headwind 3.0 m/s), visibility index 0.00 |
| Configuration / fuel | flaps 0.00, 180 L |
| Duration / in-task baseline | 300 s / 60 s |
| Goal | Land |
| Predicted Load Index | 85.0 / 100 |

**Objective** — Fly a crosswind approach, assess the crosswind against the demonstrated limit on short final, decide to land or go around, and carry out the decision.

**Brief to the participant** — You are 12 km on final for runway 01 at 500 m. Surface wind is 068/15G18 kt — a crosswind of about 14 knots. This aircraft's DEMONSTRATED CROSSWIND is 15 knots. Fly the approach: crab into the wind to hold the centreline, and straighten the aircraft with rudder before touchdown. You will be asked on short final whether you are continuing.

**Event schedule** (nominal time; ± jitter is drawn per trial from the logged seed)

| t (s) | jitter | type | detail |
|-------|--------|------|--------|
| 1 | ±0 | Message | CLEARED TO LAND RUNWAY 01. WIND 068/15G18 kt. |
| 80 | ±0 | ConfigCall | FLAPS 10 — configure for the approach |
| 100 | ±0 | Checklist | `BEFORE_LANDING` — BEFORE LANDING |
| 128 | ±0 | Message | WIND CHECK: 068/15G18 kt. CROSSWIND COMPONENT 14 KNOTS. |
| 144 | ±6 | Decision | CROSSWIND 14 KT AGAINST A 15 KT LIMIT — CONTINUE OR GO AROUND? — decide  *(respond within 9.0 s)* |
| 190 | ±10 | Probe | REPORT SHORT FINAL — respond  *(respond within 5.0 s)* |

**Demand profile** (0-4 on each dimension)

| dimension | rating |
|-----------|--------|
| mental demand | ████ 4 |
| temporal demand | ████ 4 |
| decision complexity | ███ 3 |
| working memory | ██ 2 |
| attention switching | ████ 4 |
| situation awareness | ████ 4 |
| perception | ████ 4 |
| manual control | ████ 4 |
| procedural load | ██ 2 |
| uncertainty | ███ 3 |
| communication | ██ 2 |
| error consequence | ████ 4 |

**Pre-registered expected NASA-TLX** — mental 78, physical 82, temporal 74, performance 76, effort 86, frustration 72 → RTLX 78. *A prediction recorded before data collection; never a measurement.*

**Cognitive-load analysis** — Level 3 of three on the psychomotor-integrated axis, and the harder half of the pair: a crosswind take-off is over in seconds whereas a crosswind approach is three minutes of continuous correction ending in a decrab that has to be timed. ManualControl reaches 5 at this level, the highest value anywhere in the bank — declared, not concealed, and the reason this mission is on its own axis. Note that the wind the aircraft is in DECREASES on the way down through the surface layer, so the crab that was correct at 500 m is too much at 50 m and the correction has to be continuously revised rather than set once.

**EEG relevance** — The continuous segment is the axis's psychomotor measure and must be read together with the control-activity covariates. The DECISION_PROMPT epoch on short final is the isolated cognitive component — but unlike the take-off pair it occurs while the pilot IS flying, so it carries motor activity that the take-off decision does not. That difference is deliberate and useful: the take-off decision and the landing decision are the same judgement made with and without concurrent manual control, which is a within-axis control for exactly the contamination this axis exists to handle.

**Expected errors** — Landing crabbed (side-loading the gear); drifting downwind of the centreline; over-controlling in the gusts at level 3; continuing above the demonstrated crosswind; deciding by default.

**Success criteria** — An explicit decision made in window; if continuing, touchdown on the runway, aligned with the centreline, wings level or into wind, sink rate inside the acceptable band.

**Failure conditions** — Crash; runway excursion; no decision made.

**Aviation basis** — FAA-H-8083-3C ch.8 (crosswind approach and landing: the crab and the sideslip methods, and the decrab before touchdown) and the C172 POH's 15 kt demonstrated crosswind component.

**Approximations / not modelled** — As for the crosswind take-off. In addition, touchdown scoring judges sink rate, bank and alignment but does not model gear side-load, so landing crabbed is penalised through alignment and excursion rather than through a modelled undercarriage failure.

**Required event markers** — `MISSION_START`, `DECISION_PROMPT`, `PROBE_ONSET`, `MISSION_END`

---

---

## Drills used by the missions

### ENGINE ROUGHNESS / PARTIAL POWER LOSS  (`ENG_ROUGH`)

- **DO** — CARBURETTOR HEAT — ON            [H]  *(timeout 25 s)*
- **DO** — FUEL SELECTOR — SWITCH TANK      [J]  *(timeout 25 s)*
- **CHECK** — MIXTURE — RICH  *(timeout 20 s)*
- **CHECK** — ENGINE GAUGES — CHECK  *(timeout 20 s)*
- **CHECK** — LAND AS SOON AS PRACTICABLE  *(timeout 20 s)*

### ENGINE FAILURE IN FLIGHT  (`ENG_FAIL`)

- **DO** — AIRSPEED — BEST GLIDE (~120 km/h)  *(timeout 30 s)*
- **CHECK** — LANDING SITE — SELECT  *(timeout 20 s)*
- **DO** — CARBURETTOR HEAT — ON            [H]  *(timeout 20 s)*
- **DO** — FUEL SELECTOR — SWITCH TANK      [J]  *(timeout 20 s)*
- **CHECK** — MIXTURE — RICH, IGNITION — BOTH  *(timeout 20 s)*
- **CHECK** — IF NO RESTART — SECURE & LAND  *(timeout 20 s)*

### ALTERNATOR FAILURE / LOW VOLTS  (`ELEC_ALT`)

- **CHECK** — AMMETER / VOLTS — CONFIRM  *(timeout 20 s)*
- **DO** — NON-ESSENTIAL LOADS — SHED       [K]  *(timeout 25 s)*
- **CHECK** — ALTERNATOR — RESET ATTEMPT  *(timeout 20 s)*
- **CHECK** — LAND AT NEAREST SUITABLE AIRPORT  *(timeout 20 s)*
- **CHECK** — PLAN FOR NO ELECTRIC FLAPS  *(timeout 20 s)*

### SUSPECTED PITOT-STATIC BLOCKAGE  (`PITOT_STATIC`)

- **CHECK** — CROSS-CHECK — ATTITUDE vs ASI vs ALT  *(timeout 20 s)*
- **DO** — ALTERNATE STATIC SOURCE — OPEN   [L]  *(timeout 30 s)*
- **CHECK** — PITOT HEAT — ON  *(timeout 20 s)*
- **CHECK** — FLY ATTITUDE + POWER, NOT THE NEEDLES  *(timeout 20 s)*

### FLAP FAILURE — NO-FLAP APPROACH  (`FLAP_FAIL`)

- **CHECK** — FLAP POSITION — CONFIRM  *(timeout 20 s)*
- **CHECK** — APPROACH SPEED — NORMAL, FLATTER PATH  *(timeout 20 s)*
- **CHECK** — LANDING DISTANCE — UP TO 50% GREATER  *(timeout 20 s)*
- **CHECK** — GO-AROUND — BRIEFED  *(timeout 20 s)*

### BEFORE LANDING  (`BEFORE_LANDING`)

- **CHECK** — SEATBELTS / HARNESS — SECURE  *(timeout 20 s)*
- **DO** — CARBURETTOR HEAT — ON            [H]  *(timeout 25 s)*
- **CHECK** — MIXTURE — RICH  *(timeout 20 s)*
- **DO** — FLAPS — AS REQUIRED              [F]  *(timeout 30 s)*

`DO` items complete only when the aircraft/systems state actually satisfies them;
`CHECK` items complete on the participant's acknowledgement. A timed-out item is
logged as `CHECKLIST_TIMEOUT` and the drill advances, so one stuck item can never
deadlock a trial.

## Event-marker vocabulary

Closed set of 53 tags (`Assets/Scripts/EventMarkers.cs`):

```
SESSION_START           SESSION_END             BASELINE_START          
BASELINE_END            BRIEF_START             BRIEF_END               
MISSION_START           MISSION_END             MISSION_SUCCESS         
MISSION_FAILURE         MISSION_ABORT           MISSION_RESTART         
PHASE_CHANGE            TRIGGER_ARMED           CUE_ONSET               
WARNING_APPEARS         PILOT_FIRST_RESPONSE    FAILURE_RESOLVED        
RECOVERY_START          CHECKLIST_START         CHECKLIST_ITEM          
CHECKLIST_COMPLETE      CHECKLIST_TIMEOUT       ATC_MESSAGE             
ATC_READBACK_OK         ATC_READBACK_MISS       TARGET_CHANGE           
WAYPOINT                PROBE_ONSET             PROBE_HIT               
PROBE_MISS              PROBE_FALSE_ALARM       DECISION_PROMPT         
DECISION_MADE           DECISION_EXPIRED        CONTROL_GRAB            
CONTROL_RELEASE         FLAP_SELECTED           TRIM_CHANGED            
THROTTLE_CHANGED        BRAKE_APPLIED           BRAKE_RELEASED          
CONFIGURATION_CHANGE    WEATHER_ONSET           WEATHER_END             
TRAFFIC_ONSET           GO_AROUND_COMMANDED     GO_AROUND_INITIATED     
TOUCHDOWN               CRASH                   STALL                   
TLX_START               TLX_SUBMIT              
```

