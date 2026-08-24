# COCKPIT REDESIGN — 25 August 2026

What changed in the cockpit, why, and what is measured rather than asserted.

The brief was blunt and correct: *"the cockpit structure is not good yet"*. It was not.
Every control worked, every battery passed, and from the seat it read as a set of Unity
sliders arranged in front of an aeroplane-shaped shell. This is what was done about it,
and what is still wrong.

---

## 1. The two defects that were reported

**The pedals went through the floor when the brake was applied.** Confirmed, and it was a
pivot-placement error rather than anything to do with braking. `AttachPedalFollower` put
the hinge at `pedals.localPosition`; in a glTF the mesh transform is usually identity with
the geometry baked into the vertices, so that "pivot" sat at the model datum, roughly a
metre from the pedals and well below them. Rotating 20° about a point a metre from the part
throws it about 350 mm. The hinge now comes from the pedal's own renderer bounds — its
bottom edge, which is where a 172's pedal is hinged — and the rig **asserts at build time**
that a full-brake application cannot move the pedal further than 120 mm. It reports 33 mm.

**The flap control was in a bad position and looked wrong.** Two separate causes:

- `PhysicalControl.visualBase` was captured in `Awake()`, which runs the instant
  `AddComponent` is called — before the builder had created any geometry or set the spec.
  It was therefore always `Vector3.zero`, so a handle built at the top of its channel was
  snapped to the channel's *centre* on the first frame and driven to a full travel *beyond*
  the end stop at full deflection. The handle hung outside its own slot at every position
  except the middle. The rest datum is now captured explicitly, after the spec exists.
- The lever sat at x = 0.150 with a 30 mm escutcheon on a panel whose edge is x = 0.159, so
  it overhung the aeroplane.

---

## 2. The measurement that changed everything

The model is **not in real-world metres**. Its cabin is 352 mm across and its wings span
3.7 m — about a third of a 172 — and the cockpit camera sits inside that same small model,
which is why it looks right from the seat. Every dimension had been authored directly in
model units, which made all the hardware roughly three times life size.

The scale is measured, not assumed, and four independent features agree:

| Feature | Model | Real | Ratio |
|---|---|---|---|
| Panel width | 0.352 | 1.00 m | 0.352 |
| Eye to panel | 0.235 | 0.67 m | 0.351 |
| Eye above floor | 0.320 | 0.91 m | 0.352 |
| Rudder pedal span | 0.254 | 0.72 m | 0.353 |

Everything is now written in real millimetres and scaled by `CockpitHardware.MM`.

A **depth map** of the panel was also built (design probe → `cockpit_geometry.txt`): for
each point of a grid, which mesh a hand would meet first and at what depth. That found the
fact the whole layout turns on — the **centre pedestal stands 93 mm proud of the panel
across x ±0.047** — so the engine controls could never live at centre. They had been placed
there, behind it, which is why they rendered as knobs floating in the footwell.

The probe had to be fixed twice before it could be trusted, and both failures were the kind
that look like success:

- It measured `sharedMesh.bounds`, which spans *all* vertices. `RealCockpit` makes this
  twin-seat model single-pilot by deleting triangles, not vertices, so the bounds still
  covered both halves of the cabin. That produced the confident and wrong conclusion that
  the yoke sat 63 mm right of the pilot and covered the right-hand panel. It does not.
- The occupancy table was computed from the world origin, because the close-up camera
  detaches itself and nothing put it back. It reported one visible renderer and looked like
  a working tool.

---

## 3. What the cockpit is now

Hardware built to the shapes a 172 actually has, because **shape is identity** — a pilot
finds the throttle without looking because it is the only knurled black plunger, and
building every control out of one slider vocabulary destroys exactly that.

- **Control wheel** — ram's horn: hub, arms, grips raked aft, PTT on the left horn. The
  GLB's own yoke is a flat U with two straight prongs; it is the largest object in the
  participant's view and read as a bracket. The mesh is disabled and generated geometry
  goes in its place *under the same rig*, so the verified pitch/roll animation is untouched.
- **Engine cluster** — push-pull plungers for carb heat, throttle and a red non-interactive
  mixture, in a row on a sub-panel right of the pedestal. The throttle ends up 284 mm right
  of the pilot's centreline at full scale, which is where a 172's is.
- **Flap selector** — a gated lever with three physical gates and UP / 10 / FULL legends,
  outboard of and below the engine row so it cannot be mistaken for the throttle.
- **Trim wheel** — 128 mm, edge-on, protruding through a slot in the pedestal, with a
  separate absolute-position pointer beside it (the wheel turns 220° per unit and cannot
  itself be a reading).
- **Fuel valve** — red rotary with a bar handle on the pedestal below the trim, L/BOTH/R.
- **Toe brakes** — tread pads on the rudder pedals, tilting with brake pressure.
- **Six live gauges** — RPM, FUEL, OIL P, OIL T, VOLTS, AMPS, driven by `AircraftSystems`.
- **Switch bank and circuit breakers** — including the two restored system switches.
- **Cabin trim** — carpet, seat rails, kick panels, sidewall trim, panel underside, and the
  large interior shells toned down and desaturated.

### One change that is not neutral, and is flagged rather than buried

The six engine and electrical gauges are **new information**. Three HIGH missions turn on
diagnosing a systems failure — a rough engine, a failing alternator, a blocked static port —
and until now the cockpit displayed none of the evidence a pilot would use. A participant
could only follow the checklist text and hope, which turns a diagnostic task into a reading
task. With the gauges present the diagnosis becomes a real perceptual task, which is what
the mission was designed to be.

It changes what those three missions measure. **They should be re-piloted before their
workload predictions are treated as calibrated.** Every value shown already existed in
`AircraftSystems` and was already being logged; nothing here invents a number.

---

## 4. Two approximations, stated as approximations

**A cabin fill light that is not really there.** The scene is lit by one directional sun and
a flat ambient term. Flat ambient gives every surface the same contribution regardless of
its surroundings, so it cannot produce the light that in a real cabin bounces off the
windscreen and glareshield and fills the panel. The panel faces aft, away from the sun, so
with no bounce it rendered at ambient only and every control below the glareshield was a
dark silhouette. A weak, warm, shadowless point light at the pilot's head stands in for
that bounce. Its culling mask is the cockpit layer alone, so it cannot touch the terrain or
the aeroplane's exterior. It is a cheat; leaving the panel unreadable would have been a
larger distortion of the task.

**A propeller disc.** The prop was driven by throttle, so at idle — which is how every
mission starts and how the aeroplane sits through every briefing and baseline — the engine
was running, the cockpit was full of engine noise, and the propeller was stopped dead with
one blade lying diagonally across the windscreen. It is now driven by RPM, and above a few
hundred RPM the blades fade out and a faint translucent disc fades in, because no frame rate
can sample forty revolutions a second without strobing.

---

## 5. Checks that did not exist before

Each of these was added because its absence had already cost something.

| Check | Catches |
|---|---|
| **Pedal hinge sanity** (rig, build time) | a hinge placed off the part it hinges |
| **Reach ambiguity** (control battery) | any two capture volumes overlapping |
| **Seated visibility** (control battery, and the rig logs it) | a primary control below the bottom of the frame |
| **Carb heat vs throttle size** (control battery) | two controls in one cluster becoming indistinguishable by feel |
| **Trim wheel rolls with the hand** (control battery) | a direct-manipulation visual moving against the fingers on it |
| **`DocGen.CompileOnly`** | a compile check that cannot pass by accident |

The seated-visibility check is the important one. The flap lever, the brake and the switch
bank were all once placed on good panel, correctly wired and correctly spaced — and all
three sat below the bottom edge of the frame. **Every existing test passed while a
participant could not have seen any of them.** "Is it wired correctly" and "can the person
see it" are different questions, and only one of them was being asked.

---

## 6. State

| Battery | Result |
|---|---|
| Compile | COMPILE OK |
| Cockpit control battery | **0 problems**, 9 controls |
| Mission bank (quick, 12 missions) | **0 problems**, design checks OK |
| Wind battery | **46 checks, 0 problems** |

## 7. Still wrong, or still unknown

- The cabin interior is better but still low-detail: no upholstery texture, no ambient
  occlusion, no contact shadows. Deliberately stopped there — beyond this point it is
  scenery, and the participant's attention belongs on the instruments.
- The trim wheel and fuel valve sit below the seated frame (−48° and −58°). That is correct
  for the aeroplane and fine in a headset, but a desktop participant must hold right-mouse
  and look down, or use the keyboard.
- The aircraft is a hybrid: a glass panel with a carburetted engine's controls. A real
  C172S with a G1000 has a fuel-injected IO-360 and no carb heat. Carb heat is kept because
  a HIGH mission's drill depends on it. This is an intentional experimental configuration,
  not a claim of fidelity to a specific airframe.
- **No participant has flown any of this.** None of it is validated against a person.
- Nothing here is hardware-verified: no headset connected, no yoke plugged in, and the
  Windows build compiled but never launched on Windows.
