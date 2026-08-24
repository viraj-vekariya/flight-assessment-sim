# COCKPIT_CONTROLS

Every control the pilot can operate, what it does, how it behaves, and the numbers
behind it. Implemented in `PhysicalControl.cs` (behaviour) and `CockpitControlRig.cs`
(placement and configuration).

---

## 1. The control set

**Rebuilt 25 August 2026.** The layout, the shapes and the sizes below all changed; the
behaviour, the telemetry and the input mapping did not. See `COCKPIT_REDESIGN.md` for why.

### Scale

The GLB is **0.352x life size** — its cabin is 352 mm across where a 172's is a metre —
and the cockpit camera sits inside that same small model, which is why it looks correct
from the seat. Four independent features agree on the figure (panel width, eye-to-panel
distance, eye height above the floor, rudder-pedal span), and it is `CockpitHardware.ModelScale`.

Every piece of hardware is therefore authored in **real millimetres** and multiplied by
`CockpitHardware.MM`, so any dimension in the source can be checked against a 172 drawing.
Before this, dimensions were written directly in model units, which made every control
about three times life size — the single reason the old throttle ball and flap paddle
looked like toys glued to the panel.

### Layout

Measured surfaces the layout is built against (design probe, drawn-triangle bounds):

| Surface | Where |
|---|---|
| Upper panel (`Object_50`) | z = 0.755, y 0.470–0.560, full width |
| **Lower panel** (`Object_81`) | z = 0.760, y 0.270–0.470, x ±0.176 |
| **Centre pedestal** (`Object_58`) | z = 0.667, y 0.250–0.450, x ±0.047 — stands 93 mm proud |
| Yoke wheel | z = 0.710, 94 mm across, centred on the eye |
| Rudder pedals (`Object_52`) | z = 0.755, y 0.270–0.310, x ±0.110 |
| PFD / MFD glass | (−0.0709, 0.4921, 0.7535) and (0.0461, 0.4921, 0.7535), 85 × 56 |

The pedestal drives everything: it occupies the centre strip and stands 93 mm closer to
the pilot than the panel, so anything mounted on the lower panel behind it is invisible.
The panel is therefore used as the aeroplane uses it — gauges and brake to the LEFT of the
pedestal, trim and fuel ON it, engine controls and flaps to the RIGHT.

| Control | Shape | Position (model) | Capture radius | Notes |
|---|---|---|---|---|
| **Yoke** | ram's-horn control wheel, 300 mm | (0, 0.436, 0.742) | 55 mm | grips raked aft, PTT on the left horn |
| **Carb heat** | push-pull plunger, 24 mm knob | (0.072, 0.430, 0.7585) | 15 mm | pull OUT for heat ON |
| **Throttle** | push-pull plunger, 32 mm knurled knob | (0.106, 0.430, 0.7585) | 16 mm | push IN for power; 55 mm travel |
| **Mixture** | plunger, 28 mm red knob | (0.140, 0.430, 0.7585) | — | **geometry only, not interactive** |
| **Flap lever** | gated lever, 3 gates | (0.154, 0.394, 0.7585) | 18 mm | UP / 10 / FULL, 52 mm travel |
| **Brake** | parking-brake T-pull | (−0.072, 0.394, 0.7585) | 20 mm | spring-loaded; releases to 0 |
| **Trim wheel** | 128 mm wheel, edge-on in the pedestal | (0, 0.393, 0.6655) | 28 mm | roll the rim DOWN for nose up |
| **Fuel selector** | red rotary valve, bar handle | (0, 0.310, 0.6655) | 24 mm | L / BOTH / R |
| **Load shed** | panel toggle | (−0.150, 0.332, 0.7585) | 9 mm | restored — see below |
| **Alternate static** | panel toggle | (−0.128, 0.332, 0.7585) | 9 mm | restored — see below |

**Shape is identity.** No two controls with different jobs share a shape: the throttle is
the only knurled black plunger, the flaps the only gated lever, the trim the only wheel,
the fuel valve the only rotary bar. That is what lets a pilot find a control without
looking, and building every control out of the same slider vocabulary — which is what the
previous version did — destroys precisely the property the cockpit exists to have.

**Mixture is deliberately dead.** A 172 has three engine controls and a cockpit with two
looks wrong. But the flight model has no mixture, so an interactive one would either do
nothing (a control that lies) or would have to be invented, and inventing one adds a
variable to a workload experiment that nothing in the design controls for. It is built, in
the right place, shape and colour, and it cannot be grabbed.

**Carb heat is black, not orange.** Powerplant controls are colour-coded — black throttle,
red mixture — and inventing a third colour throws away the one cue that transfers. Carb
heat is told from the throttle by size (24 mm vs 32 mm knob) and position, and the control
battery asserts that difference rather than trusting it.

**Load shed and alternate static are back.** They were removed on 23 Aug as panel clutter.
The criterion is whether the experiment needs them, and it does: `ChecklistLibrary` gates
Electrical/H2 on `LoadShed` and StaticBlock/H3 on `AlternateStaticOpen`. Keyboard K and L
cover the desktop, but a headset has no keyboard, so without cockpit objects those drills
were unperformable in the modality the study is actually run in.

**Brakes.** The aeroplane brakes with toe pads on the rudder pedals, and those now exist:
they carry the tread, they are on the pedals, and they tilt with applied pressure. A hand
control is kept as well, and that is an accessibility decision rather than an oversight —
no headset in this lab has rudder pedals and a participant in VR has no keyboard, so with
toe brakes alone there would be no way to stop the aeroplane. It is shaped and placed as
the thing a 172 really has within reach of the left hand: the small black parking-brake
T-pull under the left panel edge.

### Reach and visibility, both now checked automatically

Two properties used to be assumed and are now asserted by the control battery:

- **No two capture volumes overlap.** Checked pairwise across the whole cockpit. This
  caught the yoke's 130 mm capture radius — chosen when the engine controls were somewhere
  else — swallowing the carb-heat knob 64 mm away, so a hand reaching for carb heat would
  have taken the yoke. The yoke is now 55 mm and the tightest pair has 3 mm to spare.
- **Primary controls are inside the seated frame.** The camera is 78° vertical with a 4°
  down-tilt, so the bottom of the frame meets the panel at **y = 0.358**. Yoke, throttle,
  flaps and brake are all above it. The memory items (trim, fuel, the two switches) sit
  below it deliberately: they are a glance or a keystroke away and that is where the
  aeroplane keeps them.

The second check exists because the flap lever, the brake and the switch bank were once
placed on perfectly good panel, correctly wired, and all three were below the bottom edge
of the frame. Every test passed while a participant could not have seen any of them.

---

## 2. Position-based, not rate-based

Every continuous control maps **hand displacement → control position**:

```
hand moves half the control's travel  →  control shows ~50%  →  aircraft gets ~50%
```

It is *not* "hold your hand off-centre and the value keeps winding". Three reasons:

1. A real lever is a position. Rate mapping is what makes VR flight controls feel
   disconnected from the aeroplane.
2. Rate mapping makes the control value depend on **how long** the hand was held there —
   i.e. on the participant's reaction time, which this experiment measures separately
   and must not have leaking into the control input.
3. The travel figures above *are* the gearing. Changing sensitivity means changing a
   physical distance, which is inspectable and reportable, rather than a magic constant.

**Visual and aircraft agree by construction.** The yoke's visual is driven by
`phys.pitchInput` / `phys.rollInput` (in `RealCockpit`), and the grab writes those same
values — so a yoke that *looks* half-deflected *is* half-deflected. Levers move their own
visual from the same normalised value the aircraft receives.

---

## 3. Release behaviour — deliberately different per control

| Control | On release |
|---|---|
| Yoke | **holds its position** |
| Throttle, trim, flaps, fuel selector, switches | hold position (they are settings) |
| Brake handle | springs to zero |

The yoke is the interesting one. A real yoke does not spring to neutral — it settles
where aerodynamic force and trim balance. Modelling that needs a control-force model the
flight model does not have, so the closest defensible abstraction is: **the yoke holds
where you left it, and you use TRIM to make that the position you want.**

Snapping it to neutral would make the yoke behave like a keyboard key and would remove
the entire reason trim exists. This is an approximation and is listed as one.

While the yoke is *not* held, pitch and roll fall back to the keyboard — so releasing it
hands control back rather than freezing the aeroplane.

---

## 4. Elevator trim — new, and it changes the flight model

`CessnaPhysics.trim`, pilot convention **+1 = nose up**, authority 0.35 of full elevator.

```
ElevatorCmd = clamp(pitchInput − trim × 0.35, −1, +1)
```

**Why it was added.** Without trim the pilot holds a sustained elevator force for the
whole of a 300 s mission. That force is (a) a real continuous physical effort that
differs between missions purely by how far the aeroplane is from its hands-off trim
speed, and (b) exactly the motor activity that contaminates an EEG workload measure.
A real pilot trims within seconds of levelling off; not offering that made every mission
carry an uncontrolled, mission-dependent motor load.

**Authority choice.** 0.35 trims out cruise, climb and approach comfortably but cannot
fly the aeroplane on trim alone — trim is a relief control, not a second elevator.

**Keyboard:** `[` nose down, `]` nose up, at 0.25/s — slow, like the real wheel.

`trim` and `ElevatorCmd` are both logged at 50 Hz. `ElevatorCmd` separates *how much the
pilot is holding* from *how much they are moving*, which is what makes trim a workload
measure rather than just another control.

---

## 5. Wheel brakes — restored

They were hard-disabled (`phys.braking = false` at the end of every `Update`). That meant
the aeroplane could not be stopped, taxi speed could not be controlled, and the
`BrakeFailure` abnormality was unobservable because brakes never worked anyway.

Now: analog `brakeInput01` 0–1 scales `brakeAccel`; `braking` stays as the on/off flag
existing callers use. **Brakes are gated to the ground** in `AircraftController`, so an
airborne brake input can never bleed speed.

A failed brake system still records the pilot's *input* while producing no deceleration —
so "did they try to brake?" is answerable even when braking did nothing.

**Keyboard:** `B`, ramped at 4/s so it is not instantly full pressure.

**VR:** the pull brake handle under the panel. A 172 has toe brakes, but there are no
feet in this simulation and foot tracking is not available on the target hardware —
inventing a foot gesture would add noise, not realism. The handle is the aeroplane's
*other* real brake control, so nothing is fabricated.

---

## 6. Controls deliberately NOT offered

| Not implemented | Why |
|---|---|
| **Mixture** | The engine model has no mixture. `Object_107` fuses the throttle and mixture knobs and cannot be split. A knob that did nothing would be worse than no knob. |
| **Spoilers** | Not a C172 control. `phys.spoiler` remains as a simulation capability on the `X` key for testing, and is not exposed in the cockpit. |
| **Rudder pedals as pedals** | `Object_52` is the pedal strip, but there is no foot tracking on the target hardware. Rudder is on `Q`/`E` and available to the override API; a hand gesture for rudder would produce more noise than realism. See §7. |
| **G1000 knobs, autopilot, radios, flight plan** | The displays are read-only because the systems behind them are not simulated. Fake knobs would generate behavioural data unrelated to the flight model. |
| **Magnetos, primer, master switch** | Not modelled by `AircraftSystems`. |

---

## 7. Rudder — the honest position

Rudder input exists (`SetYaw`, keys `Q`/`E`) and is used by the taxi missions for ground
steering. What does **not** exist is a physical rudder control in VR.

The options were: foot tracking (not available on Quest 2 controllers), a hand-operated
rudder gesture (imprecise, and it would occupy a hand that should be on the yoke), or a
controller thumbstick axis. The thumbstick is the reliable one and is what the VR
interactor will map when it is wired to hardware; documented as a departure from
physical realism made deliberately, because the alternative adds motor noise to a
measurement that is trying to isolate cognitive load.

---

## 8. Sensitivity pipeline

Every analog control runs the same chain:

```
hand displacement
  → projected onto the control's own local axis
  → ÷ travel                       (the gearing)
  → + value at grab                (relative, so a grab never snaps the control)
  → clamp                          (0..1, or −1..+1 if centred)
  → dead zone                      (only the yoke, 0.02)
  → response curve                 (only the yoke, exponent 1.25)
  → exponential smoothing          (per-control τ, see the table)
  → aircraft variable
```

**Dead zones are near-absent on purpose.** On a *position* control a dead zone is a lie
about where the control is. The yoke gets 0.02 only so hand tremor at neutral is not
continuous small aileron.

**Smoothing is frame-rate independent:** `k = 1 − exp(−dt/τ)`, not a constant `Lerp`
factor. A constant factor gives different response at 30, 60 and 90 fps — which would
make the controls feel different on different machines and become an uncontrolled
between-participant variable. τ values are 30–60 ms: enough to remove tracking jitter,
short enough not to read as lag.

---

## 9. Desktop keys

| | |
|---|---|
| Flight | `W`/`S` pitch · `A`/`D` roll · `Q`/`E` rudder · `Shift`/`Ctrl` throttle |
| Configuration | `F` flaps · `[` / `]` trim · `B` brakes |
| Systems | `H` carb heat · `J` fuel selector · `K` load shed · `L` alternate static |
| Task | `SPACE` acknowledge |
| Debug | `X` spoiler (not a pilot control) |
| Cockpit mouse | LEFT-drag yoke / throttle / trim / levers · click switches · RIGHT-drag to look |

The keyboard is always live and is the fallback for every axis. A cockpit control only
owns an axis while it is being operated — see `AircraftController`'s ownership model.

---

## 10. Control ownership — the latch bug this design prevents

The override API used to **latch**: `SetPitch()` set a flag that only `ClearOverrides()`
could unset. Safe with one caller that cleared every frame; unsafe the moment a second
caller exists — a VR component that called `SetPitch()` once would own pitch forever and
the keyboard would go dead with no visible cause.

Ownership now **expires**. Each axis records the frame it was last written and is
honoured only for that frame (plus one, to absorb script-order differences). A source
that wants continuous control simply keeps writing, which is what a held grab does
anyway; a source that stops writing releases the axis automatically, and a crashed or
disabled component cannot strand the aeroplane.

`ovr_pitch … ovr_brake` are logged at 50 Hz, so an analysis can tell hand-flown segments
from keyboard ones instead of inferring it.

---

## 11. Reset between trials

Both `AircraftController.ResetConfiguration()` and `CockpitControlRig.ResetAll()` run at
the start of every trial and every baseline block. The physical levers reset too — not
just the aircraft variables — because a position-based control would otherwise write its
stale position straight back to the aeroplane on the first frame, silently re-applying
the previous trial's configuration.

Verified by the mission battery's restart check and by `ControlCheckMode`.

---

## 12. Visual pass — placards, furniture and the throttle (22 Aug 2026)

The controls were wired correctly before this pass but presented badly: they hung in
space with caption-sized text beside them. This section records what changed and the
numbers it was judged against.

### The measurement, not the impression

`Assets/Scripts/CockpitAudit.cs` (dev-only, `-cockpitaudit`) renders the pilot's view and
reports every control's screen-space bounding box **normalised to the render target**, so
before/after is comparable rather than a matter of taste. The reference is the PFD, whose
box is unchanged throughout at **w = 0.167**.

| Label | before | after | vs PFD before → after |
|---|---|---|---|
| CARB HEAT | 0.449 | **0.069** | 2.7× → 0.41× |
| TRIM | 0.410 | **0.034** | 2.5× → 0.20× |
| BRAKE | 0.401 | **0.048** | 2.4× → 0.29× |
| FLAPS | 0.337 | **0.036** | 2.0× → 0.22× |
| LOAD SHED | 0.301 | **0.066** | 1.8× → 0.40× |
| ALT STATIC | 0.297 | **0.065** | 1.8× → 0.39× |

Every placard was **wider than the primary flight display**; none is now more than
0.41× of it. PFD, MFD and yoke boxes are byte-identical before and after — the screens
were used as fixed anchors and never moved.

### Labels are placards, sized in millimetres

The old helper set `characterSize = size * 0.35` against `fontSize 72`, which produced
roughly 5 cm lettering. `Label()` now takes a **cap height in metres** and derives
`characterSize = capHeight * 10 / fontSize`, so the size is expressed in the unit the
cockpit actually cares about and cannot drift again:

* `PlacardText` = **6.0 mm** — control identification (THROTTLE, CARB HEAT, FLAPS …)
* `DetentText` = **4.8 mm** — detent legends (UP / 10 / FULL, L / BOTH / R, NOSE UP/DN)

Real placard lettering on a 172 is 4–6 mm, so these are life-size rather than merely
"smaller". Each placard is a child of the control it names, so it tracks that control
when the camera or the aircraft moves and can never drift independently.

### Controls are mounted, not floating

`BuildStructure()` creates the cockpit furniture **before** any control is built, so every
control lands on something:

* **SubPanel** — the lower panel bay carrying the plungers and switches
* **QuadrantPlate** — the escutcheon the throttle and carb-heat plungers pass through
* **Pedestal / PedestalLower** — the centre pedestal carrying trim and the fuel selector

Furniture is a realistic 172 mid-grey. A near-black first attempt swallowed both the black
knobs and the white placards; grey lets both read against it.

### Throttle — floating cylinder → panel plunger

Was a bare cylinder at the quadrant with nothing behind it. It is now a 172-style
**push-pull plunger**: a fixed boss sunk into the escutcheon, with a shaft and knob that
slide through it. Only the plunger is the animated `visual`, so the boss stays in the
panel and the lever visibly emerges from the console. Push toward the panel (+Z) is more
power, matching the aeroplane.

Verified by `ControlTestHarness`: **idle 0.00, midpoint 0.50, full 1.00** — physical
position, visual position and `phys.throttle` agree by construction.

### Positions corrected against the model

* **Brake** moved from x −0.175 to **−0.138**. The left door skin occupies x −0.158 …
  −0.181, so the old position buried the handle inside the door and it rendered as a red
  speck. It now clears the trim wheel by 89 mm (combined capture radii 84 mm) and the
  load-shed switch by 101 mm (combined 68 mm).
* **Trim** moved onto the pedestal's **left face** as a vertical wheel, which is where a
  172's trim wheel is. An intermediate pass had it inside the pedestal box, invisible.
* **Fuel selector** moved onto the pedestal's lower front face so its legend faces the
  pilot.

### The camera and speaker icons

They are **Unity Editor gizmos, not runtime objects**. The audit's renderer scan finds no
icon, marker or gizmo renderer anywhere in the running scene, and no rendered capture
contains either icon — the `AudioSource` and `Camera` components carry no `Renderer` at
all. No runtime change was made. To clear them from the Editor's Game view, use its
**Gizmos** toggle; a build never showed them.

---

## 13. Simplification pass (22 Aug 2026)

The controls were correct but the cockpit had grown a caption for everything. This pass
cut the presentation back to two tiers and rebuilt the throttle.

### What the aircraft actually supports — the audit that drove this

| Control | Implemented? | Aircraft variable | Required by | Physical control? |
|---|---|---|---|---|
| Pitch / roll | yes | `pitchInput` / `rollInput` | every mission | **yes** — GLB yoke |
| Throttle | yes | `phys.throttle` | every mission | **yes** — quadrant lever |
| Flaps | yes (3 detents) | `phys.flaps`, capped by `flapAuthority01` | L4 M4 H4 | **yes** — gated lever |
| Wheel brakes | yes (analog, ground only) | `phys.brakePressure` | taxi missions L1 M1 H1 | **yes** — pull handle |
| Elevator trim | yes | `phys.trim` | continuous hand-flying | yes — pedestal wheel |
| Carb heat | yes | `AircraftSystems.CarbHeatOn` | **H4** drill DO item | yes — small knob |
| Fuel selector | yes (L/BOTH/R) | `AircraftSystems.Selector` | **H4** drill DO item | yes — small rotary |
| Load shed | yes | `AircraftSystems.LoadShed` | **H2** drill DO item | yes — toggle |
| Alternate static | yes | `AircraftSystems.AlternateStaticOpen` | **H3** drill DO item | yes — toggle |
| Spoilers | variable exists | `phys.spoiler` | **nothing** | **no** — keyboard `X` only |
| Rudder | yes | `yawInput` | taxi, crosswind | no — `Q`/`E`, no pedal mesh |
| Mixture, magnetos, radio, autopilot | **not simulated** | — | — | **no** |

Two conclusions came out of this and both changed the build:

* **Spoilers are not a Cessna control and no mission or checklist references them.** They
  stay a keyboard/debug variable and get no cockpit object. Verified by grep over
  `MissionLibrary` and `ChecklistSystem`: zero hits.
* **The four "systems" controls cannot be dropped.** `ChecklistLibrary` gates real DO
  items on them — `Electrical`/H2 on `LoadShed`, `StaticBlock`/H3 on
  `AlternateStaticOpen`, `EngineFailure`/H4 on `CarbHeatOn` **and** `Selector != Both`.
  Delete any one and that drill can never be completed, the item times out, and the
  mission's manipulation is broken. They are kept — but demoted to one tidy block.

### Two tiers, not nine scattered objects

**Primary** (touched continuously, found without looking): yoke · throttle quadrant ·
flap lever · brake handle.
**Systems** (memory items, three missions): carb heat · fuel selector · load shed ·
alternate static — grouped into a single block on the lower-left panel so they read as a
switch panel rather than as controls strewn round the cabin.
**Pedestal**: trim wheel.

### Throttle — plunger → quadrant lever

The previous pass built a 172-style push-pull plunger. Type-correct, but from the pilot's
eye a plunger points *at you*: it projects to a small dark disc with no visible direction
of travel, which is exactly why it kept reading as "a floating cylinder".

It is now a **quadrant lever**: a fixed housing bolted to the pedestal, a slot, and a
lever that pivots inside it through 44° — aft at idle, forward at full — with a ball grip.
Its whole length is presented side-on, so the travel axis is obvious at a glance and the
lever's angle *is* the power setting. For participants who are not pilots that legibility
beats type-fidelity.

Mechanically: hand displacement forward (+Z) = more power, position-based, `travel`
0.075 m, `smoothingTau` 0.035 s, capture radius 0.042 m. Verified idle **0.00** /
midpoint **0.50** / full **1.00**. The flap lever uses the same idiom with a white grip
and three gates, so the pair reads as one control group.

### Labels: 16 → 9, all 4.8 mm

**Removed** — the physical object already says it: `THROTTLE`, `FLAPS`, `BRAKE`, `TRIM`,
`NOSE UP`, `NOSE DN`, `FUEL`.

**Kept**, because the shape genuinely cannot carry the meaning:

* `UP` / `10` / `FULL` — flap gates. A detent's *value* is not visible from its shape.
* `L` / `BOTH` / `R` — selector positions. A pointer cannot say which tank it points to.
* `CARB HEAT` — a plain knob is ambiguous, and it is a DO item in H4's drill.
* `LOAD SHED`, `ALT STATIC` — two identical toggles, distinguishable only by legend.

Every survivor is 4.8 mm cap height and sits within 25 mm of the control it names.

---

## 14. Control quadrant design pass (23 Aug 2026)

A pass over the **four levered controls only** — brake, spoiler, throttle, flaps. The
yoke, the PFD/MFD, the camera and the four systems switches were deliberately not
touched. No control was added or removed.

### What was actually wrong

Rendered from the pilot's eye and measured, not judged by eye:

| Defect | Evidence |
|---|---|
| The levers were untextured primitives | a pure-blue cube (spoiler), a pure-white cube (flaps), a black sphere on a pale wedge (throttle) |
| Nothing was mounted | flat black rectangles painted on a flat deck stood in for slots; no casing, no cheeks, no faceplate |
| Legends floated | "SPOILER" printed across the handle; italic "UP / 10 / FULL" hanging in mid-air beside the lever |
| The whole quadrant was **outside the pilot's view** | every lever measured `ymax = 0.00` — the bottom edge of the screen — so a participant saw no engine controls at all |
| The outermost lever ran off the right edge | `xmax = 1.24` |

### What it is now

One `ControlQuadrant` casing — body, raked faceplate, front placard lip, two end cheeks —
with three separate levers mounted in it. Each lever is its own object with its own
collider, slot, pivot boss, blade, collar and handle, so they animate, capture and log
independently:

```
ControlQuadrant
 ├── Ctl_spoiler   slot · boss · blade · collar · ribbed grip   (dark navy)
 ├── Ctl_throttle  slot · boss · blade · collar · ball knob     (black)
 └── Ctl_flaps     slot · boss · blade · collar · flat paddle   (off-white)
```

### Geometry, and where the numbers came from

| | Value | Source |
|---|---|---|
| Lever spacing | 52 → **46 mm** centre-to-centre | compact GA quadrants run 40–50 mm. MIL-STD-1472F Fig.18 wants 50 mm minimum for one-hand *random* access, but 5.4.3.2.1.2 requires handle **coding** when levers are grouped — and coding is what buys the separation back |
| Quadrant centre | x = **0.115** | set by what the yoke occludes. Took three passes: at 0.072 *and* 0.092 the inboard (spoiler) lever rendered **behind the right yoke horn** and could not be seen or reached |
| Pivot height | y = **0.384** | set by the MFD. The binding case is the flap/spoiler levers at their RETRACTED stop, where 23.779 puts them fully forward and their handles reach highest: at y = 0.398 the flap paddle rose to −26.4° and clipped the MFD's lower edge (−26.8°). At 0.384 the worst case is −30.5° and the throttle at full power is −28.2°, both clear |
| Pivot depth | z = **0.722** | set by the swing: at the full-forward stop the handle reaches z = 0.748, just short of the sub-panel face |
| Lever swing | **±22°** (44° total) | 2.5 in of handle travel is the GA quadrant norm, ≈30–45° of arc for a lever this length |
| Throttle knob | **26 mm**, slightly oblate | the genuine Cessna A-820 throttle knob measures ≈34 mm; 14 CFR 23.781(b) shows the powerplant knob as plain round and flat-topped, and a true sphere reads as a gearstick |

### Shape and colour coding — the reason the levers look different

**14 CFR 23.781** requires flap and powerplant control knobs to conform to published
shapes. That is what makes the three handles identifiable *by touch, in a headset,
without looking* — which matters far more here than colour:

* **Throttle** — round ball knob, black. The largest handle, and the only spherical one.
* **Flaps** — flat paddle, thick leading edge tapering to a thin trailing edge. 23.781(a)
  depicts the flap knob as flap-shaped, exactly as the landing-gear knob is wheel-shaped.
* **Spoiler** — upright ribbed grip, dark navy. Neither round nor flat.

On colour: **14 CFR 23.1555(e)(2) reserves red for emergency controls and nothing else.**
Black throttle / blue prop is industry convention, not regulation — so nothing here
claims regulatory colour compliance. The navy spoiler follows glider airbrake practice
and is deliberately desaturated; the previous pure blue was the brightest object in the
cockpit.

### Travel directions — all four verified against 14 CFR 23.779

| Control | Rule | Implemented |
|---|---|---|
| Throttle | "forward to increase forward thrust" | forward = more power ✅ |
| Flaps | "rearward/down to extend" | pull aft through the gates ✅ |
| Spoiler | "speed brakes — aft to extend" | pull aft ✅ |
| Brake | — | press pedal tops forward ✅ |

**A real defect was found doing this.** The flap and spoiler specs animate about
`Vector3.left` while the throttle animates about `Vector3.right`, so a shared −22° static
arm bias sent those two from −22° to **−66°** — a 66° sweep that laid the handles flat
across the front of the quadrant and over their own placards. The travel *directions* had
been right all along; the *rest position* was wrong. `LeverArm` now takes an explicit bias
whose sign must oppose the animation axis.

### Labels

Three placards — `SPOILER`, `THROTTLE`, `FLAPS` — engraved on the quadrant's front lip at
**4.5 mm** cap height (down from 6.0 mm), in a single row. That is it.

The `UP / 10 / FULL` gate legends were **deleted**. Three milled teeth beside the slot say
"three positions" without a word, and the lever resting against one says which is
selected. Floating italic text beside a lever was the single most UI-looking thing in the
cockpit, and anything a participant genuinely needs to be told is in the drill text, not
painted on the aeroplane.

### Flap gate — a deliberate simplification

A real 172S flap lever must be **jogged sideways to clear the mechanical stops** at 10°
and 20°. That is not implemented, on purpose: a two-axis gate turns flap selection into a
precision motor task, which is exactly the confound this apparatus is built to keep out of
the EEG. The three detents are honest to the simulation's three flap states; the aircraft
has four (0/10/20/FULL) and the simulation has three, and the cockpit shows the
simulation's.

### Brake

**The visual is the aeroplane's own pedal assembly (`Object_52`) and stays that way** —
no invented handle, no label, no panel. Two changes:

* The pivot moved to the pedal's **lower edge** and the tilt went from 12° to **20°**. The
  172 maintenance manual puts the brake master cylinders "immediately forward of the
  pilot's rudder pedals", with the pedal face hinged low so pressing the *top* strokes the
  cylinder — pivoting about the centre tipped the whole assembly like a see-saw. At 12° the
  applied-vs-released difference was **0.1 % of rendered pixels**: the pilot could not see
  whether the brakes were on.
* **VR now brakes on the left controller's trigger**, as an analog axis. The pedals are
  measured at **45° below the eye line** and largely tucked under the panel; there is no
  foot tracking on Touch controllers, so working them as a grabbed control means reaching a
  hand into the footwell mid-taxi — awkward, unreliable, and a motor confound. The trigger
  is a genuine analog axis, so braking stays *progressive*. It stands down whenever that
  hand is holding a control, so one input can never do two things at once. The pedals still
  tilt with applied pressure, so the cockpit continues to tell the truth.

### Where the quadrant sits relative to the eye — measured

Measured azimuth spread, which is what guarantees the three levers never overlap from the
seat: **spoiler 13–21°, throttle 28–33°, flaps 34–42°.** Elevation runs about **−28° to
−49°**, so the handle tops sit just under the bottom edge of the seated forward view and
the rest of the quadrant is below it. That is correct rather than a compromise: in a real 172 the throttle sits
roughly 26° below the eye line and is not in the flying scan either.

**The two constraints pull against each other, and that is why the quadrant moved
outboard rather than up.** High enough to clear the right yoke horn is high enough to clip
the MFD's lower edge, and the displays were not to be moved — so the fix was lateral.

The verification renders are produced by `CockpitDesignShots` (`-designshots`), which also
writes `pilot_view_occupancy.txt` — every cockpit object's screen box and its elevation and
azimuth from the pilot's eye. That file is how "the levers are invisible" was found, and
it is the check to re-run after any layout change.


---

## 15. Slide controls, brake handle, left bay and MFD (23 Aug 2026)

A second pass over the same four levered controls, plus two things that were not controls
at all. The yoke, PFD, camera, missions, telemetry and markers were not touched.

### 15.1 The three levers became SLIDE controls

The pivoting quadrant levers were replaced with three vertical slide controls let into the
lower panel, in one row: **SPOILER | THROTTLE | FLAPS**.

Each is a separate object with its own escutcheon, recessed channel, machined side rails,
end stops, carriage and shape-coded grip:

```
ControlArea                       (naming root only — no geometry, nothing parented to it)
 ├── Ctl_spoiler   escutcheon · channel · rails · stops · carriage · ribbed bar   (navy)
 ├── Ctl_throttle  escutcheon · channel · rails · stops · carriage · ball knob    (black)
 └── Ctl_flaps     escutcheon · channel · rails · stops · carriage · flat paddle  (off-white)
```

The carriage **translates and never rotates**, and its position along the channel *is* the
value — 0 % of travel = 0.0, 50 % = 0.5, 100 % = 1.0. There is no second animation path
that could disagree with the aeroplane. Travel is 72 mm.

Directions follow **14 CFR 23.779**: throttle slides **up** for power; flaps and spoiler
slide **down** to extend. Flaps keep three detents because the simulation has exactly three
flap states — giving the handle continuous travel would let the cockpit lie about the
aeroplane.

**No backing panel.** The first attempt put a 168 × 130 mm plate behind the group to
"group" it; from the seat that read as a large blank panel bolted to the aircraft. The
three controls are grouped by being 45 mm apart in a row with matching hardware.

### 15.2 Position was set by two hard edges, both found by rendering

| Constraint | What happened |
|---|---|
| **Inboard — the yoke** | at earlier positions the right yoke horn occluded the inboard control entirely |
| **Outboard — the panel ends** | at x = 0.163 the flap control rendered hanging over the cabin sidewall with nothing behind it |

That leaves a window about 90 mm wide. Final: **x = 0.095, spacing 45 mm**, which is inside
the 40–50 mm band compact GA quadrants use. MIL-STD-1472F Fig.18 asks 50 mm for one-hand
*random* access, but 5.4.3.2.1.2 requires handle **coding** when controls are grouped, and
all three grips are shape-coded per 14 CFR 23.781 (round ball / flat paddle / ribbed bar) —
distinguishable by touch alone. Capture radius is **21 mm**, under half the spacing, so one
reach can never take two controls.

**A sign error worth recording:** the first build placed every part at *positive* local Z.
The pilot's eye is at z = 0.52 and the panel at 0.755, so +Z points **into** the panel — the
whole control area was built inside the panel and was invisible from the seat while passing
every functional test. Anything that should stand proud of this panel is at negative local Z.

### 15.3 Brake — its own control, decoupled from the pedals

The brake was the pedal assembly itself, which was unusable: 45° below the eye line, tucked
under the panel, reachable only by putting a hand into the footwell. Worse, `Object_52` was
**reparented under the control**, so an interactable and an unrelated piece of airframe
moved and hid together.

Now:

* **`Ctl_brake` is a pull handle on the lower-left panel** — escutcheon, barrel, shaft and
  T-grip, all created by and parented to the control. It comes aft toward the pilot as
  pressure goes on, and springs back on release. This is where a 172's brake handle actually
  is ("under the left side of the instrument panel", POH Fig. 7-2).
* **The pedals are driven by `PedalBrakeVisual`**, a separate component that only *reads*
  `phys.brakeInput01` and tilts the pedal mesh about a low pivot (20°, the top of the
  15–20° band for a GA toe brake). It writes nothing and holds no reference to the control.
  **Delete it and the brake still works; delete the brake and the pedals simply stop
  tilting.** That independence is the point.
* Capture radius **30 mm**: the nearest neighbour is the carb-heat knob 62 mm away with a
  26 mm radius, so 45 mm would have overlapped it and a reach for carb heat could have taken
  the brake.
* VR also brakes on the **left controller trigger** as an analog axis, so nobody has to
  reach into the footwell. It stands down whenever that hand holds a control.

### 15.4 Left bay — the two black switches removed

`LOAD SHED` and `ALTERNATE STATIC` are gone from the cockpit. The **systems are untouched**
and still driven by `K` and `L`.

There was **no hierarchy bug to fix**: every control on this panel was already its own
GameObject with its own transform, renderers and collider, and the bay behind them is a
backing plate nothing is parented to. Rather than assert that, the control battery now
proves it — `TestOrangeControlIndependence` checks that the two switches are absent and
that the orange carb-heat control is still present, at the same cockpit-local position,
visible, sharing no hierarchy with another control, interactable, and still toggling, with
**0.000 m of drift**.

> **Consequence you must know about.** `ChecklistSystem` gates a real DO item on
> `LoadShed` (H2, alternator failure) and another on `AlternateStaticOpen` (H3, static
> blockage). With no cockpit object, those two items can only be completed from the
> keyboard — so **H2 and H3 are desktop-only until a VR route is chosen for them.** The
> desktop path is unaffected and the checklist text already names the keys.

### 15.5 MFD compass — restored

Nothing was broken, hidden, mis-layered or clipped. The eight labels were **deleted on
22 Aug** as screen clutter: `compassLabels` was left a zero-length array and `compassRadius`
zero, so the orbit code in `LateUpdate` had nothing to orbit.

They are rebuilt the same way as the rest of the map — plain `TextMesh` on the symbol
layer, parented to the map camera, drawn through it into the render texture. That is what
makes them *part of the MFD's rendering* rather than text floating in front of the glass.
Radius is 0.80 of the camera's vertical half-extent: outside the reference ring (0.50) and
inside the frame.

Each label is drawn three times — two dark offset copies behind, one light copy in front.
A single flat colour was unreadable: the map renders light-green terrain most of the time,
and the first pass used light grey, which made the **"E" indistinguishable from an "F"**.
Outlining is the same treatment the own-ship symbol already uses.


---

## 16. The measurement bug that made three passes look like nothing changed

**`CockpitDesignShots` was rendering the "pilot view" at 55° vertical FOV. The real cockpit
camera is 78°** (`CockpitBuilder.cs`). The verification tool was not reproducing the thing
it was verifying.

The consequence was not cosmetic. At 55° the frame is a narrow tube: the entire lower panel
falls off the bottom edge, so every control I built measured `viewport-y −0.18 … −0.41` and
I concluded — and wrote down — that *"a three-control quadrant cannot be visible in the
forward view without covering the displays."* That conclusion was an artifact of the tool.
At the FOV the participant actually gets, there is plenty of visible panel below the
displays, and the controls had been sitting just off the bottom of a frame that was too
narrow.

The tool now takes the FOV from the cockpit camera itself (`fov <= 0` means "use the real
one"), and the occupancy probe measures at that FOV too.

**Everything measured against the real view:**

| | |
|---|---|
| yoke horns span | x −0.052 … +0.048 |
| panel's right edge | x 0.159 |
| MFD lower edge | viewport y 0.24 |
| PFD / MFD boxes | x 0.28…0.45 and 0.51…0.67, y 0.24…0.39 |

That gives a 111 mm window outboard of the yoke, which takes three controls at 45 mm
centres comfortably.

### Final geometry

| | |
|---|---|
| Group centre | x = 0.105, spacing 45 mm |
| Height | y = 0.432, travel 44 mm |
| Plane | z = 0.7485 (6 mm proud of the panel) |
| Capture radius | 20 mm — under half the spacing |
| Brake handle | x = −0.130, y = 0.435 (was 0.345, below the frame edge) |

Two further defects fell out of measuring properly:

* **The escutcheon, not the travel, is the tallest part of a control.** Sizing the group
  against its travel put the spoiler's mounting plate through the bottom of the MFD. The
  spoiler sits directly under the MFD in x, so it is the one that has to clear it.
* **Dropping the group to clear the MFD pushed the placards off the bottom of the frame**,
  which is why `SPOILER / THROTTLE / FLAPS` now sit *above* their controls rather than
  below.

### Verified from the pilot's seat

An automated check now asserts that **no control's screen box intersects either display
box** — it passes clean. Handle movement is confirmed by pixel diff between the two travel
extremes (6.94 % of the frame changes between idle and full).

The forward view now shows, without leaning or looking down: PFD, MFD with its compass,
the yoke, three separate labelled sliders, the brake handle, and the orange carb-heat
control — with the two black switches gone.
