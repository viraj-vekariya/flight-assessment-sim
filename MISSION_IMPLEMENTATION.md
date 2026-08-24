# MISSION_IMPLEMENTATION

What was actually built, how the pieces fit together, and — the important part —
**every place the simulator is an approximation of the real aeroplane.**

---

## 1. Architecture

```
GameManager                        app state machine; owns the session queue
  ├── ExperimentSession   (static) counterbalanced order, seeds, session folder
  ├── BaselineRunner               the resting-baseline blocks
  ├── ScenarioEngine               runs ONE trial (taxi / flight / approach)
  │     ├── MissionDefinition      the trial's full specification (data)
  │     ├── ChecklistRun           the drill currently being worked
  │     ├── TrafficAircraft[]      scripted visible traffic
  │     └── ExperimentLogger       telemetry.csv / events.csv / *.json / eeg/
  ├── ResultsUI                    NASA-TLX + Bedford form, session report
  ├── ExperimentUI                 baseline screens, experimenter status strip
  └── MenuUI                       session launcher + per-mission practice + design grid

Aircraft GameObject
  ├── CessnaPhysics                flight model (+5 failure hooks, turn coordination,
  │                                 aerodynamic rate damping)
  ├── AircraftController           keyboard + mouse-override input; flap authority
  ├── AircraftSystems              engine / fuel / electrical / pitot-static / airframe
  ├── SystemsInput                 H J K L — the pilot's systems switches
  └── CockpitInteraction           mouse yoke / throttle / levers / pedals / switches

Cockpit (built by CockpitBuilder)
  ├── RealCockpit                  loads cessna_g1000.glb, single-pilot mesh surgery,
  │                                 rigs the model's own yoke to the controls
  ├── LivePFD                      attitude + HDG/SPD/ALT/VS on the left glass
  └── LiveMFD                      heading-up moving map on the right glass

World (WorldBuilder)
  ├── terrain / water / sky / settlements
  ├── runway  (30 x 600 m, take-off toward +Z)
  └── AerodromeBuilder             apron + stand, taxiway A, link B, hold-short, signage

Data (pure, no MonoBehaviours)
  MissionLibrary      the twelve missions (4 phases x 3 classes)
  ChecklistLibrary    the six drills
  WorkloadModel       demand dimensions, weights, PLI, expected TLX
  EventMarkers        the closed marker vocabulary
  Aerodrome           named taxi geometry
```

### Where each piece came from

`MERGE_ARCHITECTURE.md` has the full table. In short: the cockpit stack, the displays,
the mouse interaction and the physics tuning came from the cockpit project; the mission,
telemetry, marker, session, NASA-TLX and EEG apparatus came from the experiment project;
`CessnaPhysics`, `AircraftController` and `WorldBuilder.PadFlatten` were hand-merged
because each source had fixed something the other had not; and the aerodrome, the taxi
goal, the traffic and the mission set itself are new in Final.

### New in Final

| File | Purpose |
|---|---|
| `AerodromeBuilder.cs` | apron, parking stand, taxiways, hold-short markings, signage |
| `TrafficAircraft.cs` | scripted visible other traffic |
| `Aerodrome` (static, in `AerodromeBuilder.cs`) | named taxi geometry shared by missions and the engine |
| `ScenarioGoal.TaxiTakeoff` + the engine's taxi state machine | stand → route → hold short → clearance → roll → climb |

## 2. The aircraft this simulator represents

Fixed for the whole study, and it constrains which abnormalities are legitimate:

**A Cessna 172-class high-wing single** — carburetted engine with carburettor heat,
fixed-pitch propeller, **fixed** tricycle gear, **electric** flaps on the main bus,
one alternator plus a battery, conventional pitot-static instruments, single pilot,
no autopilot.

Three consequences that shaped the mission set:

* **There is no landing-gear mission.** The gear is fixed. "The gear will not extend"
  would be a fabricated abnormality. The nearest honest equivalents — flap failure,
  split flap, no wheel braking — are used instead.
* **Carburettor icing is in scope** because the engine is carburetted. (The cockpit
  ART is a G1000-panel 172S, which is fuel-injected and would have no carb heat. This
  is a **cosmetic inconsistency**: the simulated aircraft is the carburetted variant,
  and the project's own cockpit builder has always had a carb-heat lever. Noted here
  rather than hidden.)
* **There is no autopilot**, so every mission is hand-flown throughout. That raises
  manual demand relative to a real cruise leg, which is why `ManualControl` is scored
  2 rather than 1 even in the quietest mission.

---

## 3. How each abnormality is modelled — and how faithfully

`AircraftSystems.cs` touches the flight model through five hooks and nothing else.
With every system healthy they are `1 / 0 / 0 / 0 / 1` and flight is identical to
before this layer existed.

| Abnormality | What is modelled | What is **not** |
|---|---|---|
| **Carburettor ice** | Accretion 0→1 over ~40–70 s while heat is off; up to 35% power loss; roughness ripple above 35% ice; carb heat clears it in ~8 s and costs ~5% power; the characteristic brief *worsening* as ice melts | Any dependence on OAT, dew point, humidity or power setting. It is a scripted curve, not thermodynamics. |
| **Engine failure** | Power → 0, oil pressure → 0, RPM decays to a windmilling value | Propeller windmilling drag; restart logic beyond the checklist items; a published glide polar (best glide is asserted in the drill) |
| **Fuel starvation** | The selected tank drains; power → 0; a tank change restores it | Fuel-line dynamics, unporting in a slip, restart delay |
| **Alternator failure** | Bus drops to battery; a lumped 24 Ah store with a linear state-of-charge→voltage curve and two load levels (shed / not shed); below 18 V the electric flaps stop working | Individual breakers, per-equipment current draw, real battery chemistry. Drain is tuned to make the decision live inside a 300 s trial — **faster than a real battery**. |
| **Blocked static** | Altimeter lags toward the blockage altitude; VSI reads ~15% of true; ASI errs opposite to the altitude error. Alternate static source restores it. | A pressure-system model. It is a first-order linear approximation of the AFH ch.18 error signature. |
| **Blocked pitot** | ASI behaves like an altimeter (rises in a climb, falls in a descent) | Partial blockage, drain-hole cases, pitot heat effects |
| **Flap motor failure** | Flaps freeze where they are; the *selected* detent still moves, so `flaps_selected` and `flaps_actual` diverge in telemetry | Asymmetric retraction, motor current |
| **Split flap** | Constant roll toward the less-deflected wing + yaw, scaled by flap setting and dynamic pressure. Sized so full asymmetry needs most of the available aileron. | **Not used by any of the twelve missions.** Implemented and available, never flown in a mission — treat the numbers as untested. |
| **Door open** | +13% parasite drag and a small yaw (~0.7 °/s uncorrected) + a loud cue and banner | A dedicated airflow-noise audio asset — so the **acoustic** component of the startle is under-delivered. This is the main weakness of M4's manipulation. |
| **Radio / brake failure** | State flags; brakes are disabled | Anything downstream — no ATC channel is actually lost because there is no voice loop |
| **Turbulence / low visibility** | Band-limited Perlin gusting (vertical, lateral, gentle body-axis rock) + fog density | A spectral (Dryden / von Kármán) turbulence model; cloud; precipitation; airframe icing. **Intensity values are simulator units, not meteorological turbulence categories**, and must be reported as such. |
| **Conflicting traffic** | A visible low-poly aeroplane on a scripted path — holding on the runway, crossing it, taxiing toward the holding point, or crossing the departure path — plus the radio call and, where relevant, a timed response | No collision model, no reaction to the player, no TCAS. Conflict is scripted geometry, not emergent. That is deliberate: emergent conflict would make the trigger instant vary between participants and destroy the event-locked EEG epoching. |

### The checklists

Six drills in `ChecklistLibrary`. Items are either **DO** (complete only when the
aircraft/systems state actually satisfies them — the pilot must perform the action)
or **CHECK** (complete on acknowledgement). A timed-out item logs
`CHECKLIST_TIMEOUT` and the drill advances, so one stuck item can never deadlock a
trial.

Content follows the standard light-single abnormal/emergency flow in FAA-H-8083-3C
ch. 18 and generic C172-class POH section 3 ordering. It is a **teaching-accurate
abstraction, not a transcription of any manufacturer's certified checklist**, and
must not be used for real flight. Not modelled: reading a paper checklist, crew
callouts, challenge-and-response with a pilot monitoring.

---

## 4. Changes made to the world

`WorldBuilder.PadFlatten` was widened along the runway axis from ±380 m (270 m blend)
to ±1600 m (700 m blend).

**Why:** the automated battery found that a straight-out departure from this airfield
was impossible — rolling terrain reaches ~130 m within a few hundred metres of the
runway end and this aeroplane climbs at 3–4 m/s, so every take-off ended in
"Destroyed (terrain impact)" at about 110 m, roughly 25 s after brake release. That
is a pre-existing bug in the world, not something the experiment introduced, and it
also made the protocol's mandatory familiarisation flight unflyable.

The result is an ordinary flat aerodrome plain roughly 3.2 km along the runway axis,
fading into the existing hills. Approaches are unaffected — they come from −Z, over
water beyond ~4 km.

**Still present:** a modelled downtown with buildings up to 130 m tall about 2 km off
the departure end. This is why M4 starts airborne rather than on the runway (see §5).

---

## 4b. Taxi and the aerodrome

`AerodromeBuilder` adds an apron with a parking stand, a parallel taxiway (A), a link to
the runway threshold (B), standard hold-short markings and airfield signage. The taxi
route is about 330 m with two 90-degree turns and one mandatory stop.

`ScenarioGoal.TaxiTakeoff` runs a four-phase state machine — taxi → hold short →
cleared → airborne — each phase emitting a `PHASE_CHANGE` marker. The hold-short line
is enforced: entering the runway before the clearance is logged as a **runway
incursion**, which is the single most meaningful binary error the ground phase can
produce and the exact error the real-world marking exists to prevent.

The take-off clearance is released either by a `ConfigCall` whose text contains
"CLEARED FOR TAKE", or by the pilot *acknowledging* a `Readback` containing it — so in
the missions that require a read-back, the runway cannot be entered until the clearance
has actually been accepted.

**Not modelled:** pushback, engine start, ground crew, wingtip clearance, taxi speed
limits, other aircraft giving way, or ATC responding to what the pilot actually does.

## 5. Design decisions the testing forced

These are changes made *because* the automated battery falsified an assumption.
They are listed because each one would have quietly damaged the study.

| Finding | Change |
|---|---|
| Landing missions ended at touchdown around t ≈ 110 s, so events scheduled at 120–205 s **never fired** — in a real session those missions would simply have been missing their manipulation | All landing missions now share one geometry: 12 km out, level at 500 m, descent clearance at t = 62 s. Touchdown lands around t ≈ 235 s, inside the 300 s ceiling, with the whole schedule in the flying window. |
| M4 began with a take-off roll, so its 60 s in-task baseline was a **different task** from every other mission's baseline | M4 starts airborne. All twelve baselines are now the identical straight-and-level hold, which is what makes a baseline-to-task EEG contrast meaningful. |
| The open-door model applied 220 N·m of constant yaw, which against this airframe's damping settled at ~4 °/s, coupled into bank, and **spiralled the aircraft into the ground every time** | Retuned to 30 N·m (~0.7 °/s) and +13% drag. Without this, the mission designed to be *startling but harmless* would have been the most lethal in the set — destroying the startle-vs-danger dissociation the design depends on. |
| Take-off into rising terrain | `PadFlatten` widened (§4) |
| The scripted test pilot had no airspeed control and reached 300 km/h in descents, diverging | Harness autopilot rewritten: pitch holds the path, throttle holds the speed, with stall and overspeed guards |

---

## 5b. The cruise and climb missions fly out over open water

At 180 km/h a 300 s mission covers **15 km**. The modelled landmass is an island with a
coastline radius of about 4.3 km — roughly 8.6 km across — inside a 12 km terrain. A
straight-line 300 s mission therefore *cannot* stay over land: the cruise and climb rows
cross the coast partway through and spend the rest of the trial over open sea.

**Why this is acceptable rather than a defect.** The transition happens at the same
point, at the same speed, from the same start position, in all three missions of a row
(L2/M2/H2 and L3/M3/H3). The visual scene is therefore *matched within the phase row*,
which is exactly where the LOW-MEDIUM-HIGH contrast is made. It is a constant, not a
confound, for the comparison the experiment actually runs.

**What it does cost.** The external scene in those rows is largely featureless, so the
cruise and climb missions offer little terrain-based situational-awareness demand, and
the requirements document's "mountains / sea / rivers" terrain variety is visible mainly
in the take-off and approach rows, which stay near the airfield. If terrain awareness
becomes a manipulation of interest, the honest fix is a larger landmass (raise the
coastline radius in `WorldBuilder.BaseHeight`), not a longer mission — trial duration
has to stay constant.

**Do not "fix" this by turning L3.** L3's whole purpose is an unchanging
straight-and-level hold; giving it a circuit would change what it measures.

## 6. Known limitations, collected

Repeated from the tables above so nothing has to be hunted for:

1. **No AI traffic.** Traffic conflicts are a call and a cue, not an aircraft.
2. **No voice R/T.** "Readback" is an acknowledge keypress, so it measures acceptance
   *latency*, not readback *accuracy*. Whether the clearance was retained is inferred
   from whether the aircraft was flown to the new targets.
3. **No autopilot**, so automation-surprise manipulations are impossible in this
   simulator, and every mission is hand-flown.
4. **No motion platform**, no g-cues, no vestibular input. Spatial-disorientation
   illusions cannot be simulated and no mission claims to.
5. **Turbulence is not a spectral model**; intensities are simulator units.
6. **The battery drain rate is faster than reality**, tuned so H2's decision is live
   inside 300 s.
7. **The door's acoustic startle is under-delivered** (no dedicated audio asset).
8. **Split flap, radio failure and brake failure are implemented but unused** by the
   twelve missions.
9. **The cockpit art is a G1000 172S** while the simulated aircraft is carburetted.
10. **No EEG hardware has ever been connected.** The LSL path compiles and is
    written, but is untested against a live amplifier.

---

## 7. How to run things

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
PROJ="$HOME/Desktop/BTP Project/FlightAssessmentSim - VR"

# compile check
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" -logFile compile.log

# the automated twelve-mission battery (exit code 0 = all checks passed)
# NOTE: no -quit — PlayCapture exits the editor itself when the harness is done
"$UNITY" -batchmode -nographics -projectPath "$PROJ" \
         -executeMethod PlayCapture.RunMissionTest -missiontest -logFile test.log

# regenerate MISSION_DESIGN.md and TELEMETRY_SCHEMA.md from the code
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
         -executeMethod DocGen.Generate -logFile docgen.log
```

The battery's report is written to
`~/Library/Application Support/DefaultCompany/FlightAssessmentSim/mission_test_report.txt`
and echoed to the log under `[MTEST]`.

**The battery runs as participant `TEST01` and submits synthetic NASA-TLX values to
exercise the write path. That folder must never be analysed as data.**
