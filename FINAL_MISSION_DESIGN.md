# FINAL_MISSION_DESIGN — the twelve cognitive-workload missions

> **This file is GENERATED from `Assets/Scripts/MissionLibrary.cs` by
> `Assets/Editor/DocGen.cs`. Do not edit it by hand — edit the mission set
> and regenerate, so the document and the simulator can never disagree.**

Generated: 2026-08-21 23:30

Every mission is **300 s** long with a **60 s in-task baseline** at its head. Duration is held constant across all three
workload classes on purpose — see `EXPERIMENT_PROTOCOL.md`.

## Final mission table

| ID | Class | Mission | Primary workload mechanism | Main abnormality | Time pressure | Multitasking | Decision complexity | PLI |
|----|-------|---------|----------------------------|------------------|---------------|--------------|---------------------|-----|
| L1 | LOW | Normal departure from the stand | continuous manual tracking (no dominant load driver) | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 28 |
| L2 | LOW | Steady assigned climb | continuous manual tracking (no dominant load driver) | none (normal operations) | ○○○○ | ●○○○ | ○○○○ | 18 |
| L3 | LOW | Straight-and-level cruise hold | continuous manual tracking (no dominant load driver) | none (normal operations) | ○○○○ | ○○○○ | ○○○○ | 13 |
| L4 | LOW | Routine approach and landing | continuous manual tracking (no dominant load driver) | none (normal operations) | ●○○○ | ●○○○ | ●○○○ | 29 |
| M1 | MEDIUM | Departure with traffic and a two-part clearance | attention switching / concurrency | traffic + weather | ●●○○ | ●●●○ | ●●○○ | 53 |
| M2 | MEDIUM | Cabin door opens on the climb-out | attention switching / concurrency | `DoorOpen` | ●●○○ | ●●●○ | ●●○○ | 50 |
| M3 | MEDIUM | Multi-part ATC re-clearances in the cruise | working memory / comms turnover | none (normal operations) | ●●○○ | ●●●○ | ●○○○ | 50 |
| M4 | MEDIUM | Deteriorating weather and a late runway change | working memory / comms turnover | weather | ●●○○ | ●●○○ | ●●○○ | 58 |
| H1 | HIGH | Amended clearance, blocked runway, departure conflict | diagnosis under ambiguity | 3x traffic + weather | ●●●● | ●●●● | ●●●○ | 87 |
| H2 | HIGH | Alternator failure on the departure climb | diagnosis under ambiguity | `AlternatorFailure` | ●●●○ | ●●●○ | ●●●● | 79 |
| H3 | HIGH | Unreliable instruments in the cruise | diagnosis under ambiguity | `StaticBlocked` | ●●●○ | ●●●● | ●●●○ | 86 |
| H4 | HIGH | Engine failure on the approach | time pressure | `EngineFailure` | ●●●● | ●●●○ | ●●●○ | 76 |

`PLI` = Predicted Load Index (0-100) from the weighted demand model in
`COGNITIVE_LOAD_MODEL.md`. It is a **prediction the experiment tests**, not a result.

## The design grid — phase fully crossed with workload class

Every class appears exactly once in every phase, so a class effect can never be
a phase effect, and every mission has a phase-matched LOW baseline.

| Flight phase | LOW | MEDIUM | HIGH | manual demand (L/M/H) |
|---|---|---|---|---|
| **Takeoff** | L1 Normal departure from the stand (PLI 28) | M1 Departure with traffic and a two-part clearance (PLI 53) | H1 Amended clearance, blocked runway, departure conflict (PLI 87) | 2/2/2 |
| **Climb** | L2 Steady assigned climb (PLI 18) | M2 Cabin door opens on the climb-out (PLI 50) | H2 Alternator failure on the departure climb (PLI 79) | 2/3/2 |
| **Cruise** | L3 Straight-and-level cruise hold (PLI 13) | M3 Multi-part ATC re-clearances in the cruise (PLI 50) | H3 Unreliable instruments in the cruise (PLI 86) | 2/2/3 |
| **Approach** | L4 Routine approach and landing (PLI 29) | M4 Deteriorating weather and a late runway change (PLI 58) | H4 Engine failure on the approach (PLI 76) | 3/3/4 |

Manual demand is matched WITHIN each phase row (spread <= 1 on a 0-4 scale), which
is where the class contrast is made — so a difference inside a row cannot be muscle
activity rather than cognitive load.

## Class separation (design check)

| Class | n | PLI min | PLI max | PLI mean | manual-control range |
|-------|---|---------|---------|----------|----------------------|
| LOW | 4 | 12.8 | 29.3 | 22.0 | 2-3 |
| MEDIUM | 4 | 49.5 | 57.5 | 52.4 | 2-3 |
| HIGH | 4 | 76.3 | 87.0 | 82.0 | 2-4 |

The classes must not overlap on PLI, and the manual-control ranges should stay
close together — a large manual-control gap between classes would mean an EEG
difference could be muscle activity rather than cognitive load.

---

## Full mission specifications

### L1 — Normal departure from the stand

| field | value |
|-------|-------|
| Workload class | LOW |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 600 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | LOW |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | LOW |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | LOW |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | MEDIUM |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 700 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.35 |
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
| Workload class | MEDIUM |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | MEDIUM |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | MEDIUM |
| Flight phase | Approach |
| Start | Airborne at (0, 500, -12000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.45 |
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
| Workload class | HIGH |
| Flight phase | Takeoff |
| Start | Runway at (0, 700, -200) |
| Altitude / airspeed / heading | 0 m / 0 km/h / 0° |
| Targets | 800 m ±90, 0° ±15 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.55 |
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
| Workload class | HIGH |
| Flight phase | Climb |
| Start | Airborne at (0, 400, 1500) |
| Altitude / airspeed / heading | 400 m / 180 km/h / 0° |
| Targets | 400 m ±80, 0° ±14 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | HIGH |
| Flight phase | Cruise |
| Start | Airborne at (0, 700, -2000) |
| Altitude / airspeed / heading | 700 m / 180 km/h / 0° |
| Targets | 700 m ±70, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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
| Workload class | HIGH |
| Flight phase | Approach |
| Start | Airborne at (-900, 500, -9000) |
| Altitude / airspeed / heading | 500 m / 180 km/h / 0° |
| Targets | 500 m ±80, 0° ±12 |
| Weather / wind / visibility | turbulence 0.00 ambient, crosswind 0.0 m/s, visibility index 0.00 |
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

