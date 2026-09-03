# MINIMAL COCKPIT — 3 September 2026

The cockpit was reduced, on request, to three things: the **control yoke** and the **two
glass displays**. Everything else was removed. A separate defect — the moving map's
symbology appearing up in the cabin roof — was diagnosed and fixed at the same time.

---

## 1. What is in the cockpit now

| Kept | Notes |
|---|---|
| **Control yoke** | Fully interactive and movable — pitch and roll, mouse and VR, unchanged |
| **PFD** (left display) | Untouched — same position, size, content, code |
| **MFD** (right display) | Untouched — same position, size, content, code |

Removed: throttle, flap lever, brake, trim wheel, carburettor heat, fuel selector, the
sub-panel and systems-bay furniture they sat on, every placard and legend, and the
moulded radio/avionics button stack between the two displays.

The aeroplane's own shell — panel, glareshield, windscreen, seat, rudder pedals — remains,
because the displays have to be mounted in something.

The one thing deliberately left between the displays is the **bezel that frames them**
(`Object_83`, which carries a single moulded knob). It is one mesh with the surround of
both screens: removing it would leave the two displays floating on bare panel, which
counts as modifying them.

## 2. Removing the objects did not remove the inputs

**The aeroplane still has a throttle, flaps, brakes, trim, carburettor heat and a fuel
selector.** They live in `CessnaPhysics` / `AircraftController` / `AircraftSystems`
exactly as before, are still driven by the keyboard, still written to telemetry, and still
set by `ScenarioEngine` at the start of every trial. Only the grabbable cockpit *objects*
are gone. No mission, no checklist and no telemetry column changed.

The control battery asserts this directly (`TestRemovedInputsStillDriveable`): each of
throttle, flaps, brake and trim is driven through the controller and checked to respond.
Without that check, every mission would still pass its own battery — the scripted pilot
drives the same code path — while a human found the aeroplane unflyable.

### The consequence, stated plainly

Those inputs are now **keyboard-only**.

- **Desktop** — unchanged and fully flyable.
- **VR** — a headset has no keyboard, so a participant can move the yoke and nothing else.
  They cannot set power, flaps, brakes or trim by hand, and the systems drills in the HIGH
  missions cannot be performed in that modality.

This is a recorded decision, not an oversight. Every builder is retained and unreferenced
in `CockpitControlRig`, so restoring any single control is one line in the build list.

## 3. The map in the roof — what it actually was

It was not a mirror, not a reflection, and nothing was in the glass.

`LivePFD` and `LiveMFD` each build their symbology — the attitude ball, the compass rose
and its N/NE/E letters, the route, the data readouts — as ordinary **world objects on a
private layer**, parented to their own off-screen camera, and each of those cameras
renders only its own layer into a RenderTexture. That half of the design is sound.

The missing half is that every camera which renders the **world** must exclude those
layers. The cockpit camera's mask was `~(1 << ExteriorLayer)` — everything except the
aeroplane's exterior — so it drew the displays' symbology too. The MFD's camera sits
**500 m directly above the aircraft** looking down, and its symbology is parented to it,
so the map's compass letters and its `GS` / `TRK` readouts hung in the sky above the
cabin. Seen from behind they are mirrored, and framed by the cabin roof they looked like a
reflection in the roof glass.

Measured, from the diagnostic:

```
MFDCam        target=RT      pos=(0.00, 501.16, -280.19)   seesDisplay=YES
CockpitCamera target=SCREEN  pos=(0.00,   1.83, -279.82)   seesDisplay=YES   <-- the bug
```

### The fix

`CockpitBuilder.DisplayOnlyMask` names the three layers (12 reserved, 13 PFD, 14 MFD) and
every world camera now excludes it: the cockpit camera, the external/chase camera
(`ViewManager`), the mission-shot cameras, and the dev yoke shot. **`LivePFD.cs` and
`LiveMFD.cs` were not modified at all** — the displays are untouched, as required.

`ControlTestHarness.TestDisplaysDoNotLeakIntoTheWorld` now asserts the contract on every
camera that renders to the screen, so this cannot come back silently.

## 4. A verification trap worth recording

The first render after the fix still showed the mirrored text, and it was tempting to
conclude the fix had failed. It had not: the file being read (`00_view_up.png`) was a
**stale image** left in the tool's output folder by an older build whose shot list included
it. The current tool never wrote it.

`CockpitDesignShots` now **wipes its output directory** at the start of every run, and the
shot list gained genuine up / down / left / right views — no forward-facing render could
ever have caught a defect that lives above the cabin.

## 5. State

| Battery | Result |
|---|---|
| Cockpit control battery | **0 problems** — 1 control built (the yoke) |
| Mission bank (quick, 12) | **0 problems**, 42-mission bank intact, design checks OK |
| Wind battery | **46 checks, 0 problems** |

---

## 6. Follow-ups, 3 September 2026

### The "camera on the aeroplane" and the "sun on the runway" were Editor gizmos

Three icons were reported: a loudspeaker and a cloud stuck to the aeroplane in the
external view, and a sun-like starburst floating in the cockpit. None of them is an object
in the scene, and nothing in the simulation creates them. They are Unity's built-in
**gizmo icons**, drawn by the Editor on top of everything with no depth test:

| Icon | Component it marks | Where |
|---|---|---|
| Loudspeaker | `AudioSource` | on the aeroplane (engine / wind / ground audio) |
| Cloud | `WindZone` | on the aeroplane |
| Starburst loudspeaker ("the sun") | `AudioListener` | at the cockpit camera |

**They have never appeared in a build and never will** — they exist only in the Editor's
Game view, and only while its Gizmos toggle is on. Deleting scene objects would not have
removed them.

`Assets/Editor/GizmoIcons.cs` now disables the icons themselves through
`UnityEditor.AnnotationUtility`, on every editor load, so they stay gone regardless of the
Gizmos toggle. Verified: *disabled 79 gizmo icons*. Two menu items under
**Tools → Experiment** hide or restore them. The file lives in an Editor folder, so it is
never compiled into a player and cannot affect the build. Every reflection call is guarded:
if a future Unity renames the internal API, it logs once and does nothing.

### The map was lightened

The MFD's dim overlay was 78% opaque over a very dark navy. At dusk, with already-dark
terrain beneath it, that left the map close to black and the ground on it unreadable. It is
now **48% over a slightly lighter, less saturated tone** (`LiveMFD`, the single
`AddTransparentQuad` call).

The reason the overlay exists is preserved: the display is still darker than the symbology
drawn in front of it, so the compass, the route and the readouts remain the brightest
things on screen. That ordering is what makes it read as an instrument rather than a game
minimap — only the amount of dimming changed, not the design.

This is the one change made to a display. It was explicitly requested, and it is confined
to the overlay's colour and alpha: no geometry, no layout, no symbology, no code path.
