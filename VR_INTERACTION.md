# VR_INTERACTION

How VR is wired into this project, what runs where, and — stated plainly — what has and
has not been verified on hardware.

---

## 1. Status

| | |
|---|---|
| XR packages installed | ✅ `com.unity.xr.openxr` 1.14.3, `com.unity.xr.management` 4.5.0, engine XR/VR modules |
| XR loader configured | ✅ OpenXR loader assigned for **Standalone (Windows)**; `InitManagerOnStart` on |
| Interaction profiles | ✅ Oculus Touch + KHR Simple, Standalone |
| Runtime detection | ✅ `VRRuntime` — reports loader / HMD / controllers separately |
| Camera rig | ✅ `VRCameraRig` — seated origin at the measured pilot eye point |
| Cockpit interaction | ✅ `CockpitInteractorVR` — grip to grab, trigger to press |
| Acknowledge button | ✅ A/X → `ScenarioEngine.ExternalAck()` |
| Desktop fallback | ✅ verified — the whole battery and control tests run desktop |
| **Verified on a headset** | ❌ **NOT DONE.** See §6. |

**The honest summary: the VR chain is implemented and configured, and nothing has ever
been run through a physical headset.** Everything below describes what the code does,
not what has been observed.

---

## 2. The mistake this build was written to avoid

The previous VR attempt shipped the OpenXR package and a set of VR scripts, and had **no
XR loader configuration**. The scripts compiled, looked complete, and never executed a
single line at runtime — `XRSettings.enabled` was false, no HMD was ever acquired, and
nothing said so.

Two things prevent a repeat:

**`Assets/Editor/XRSetup.cs`** creates the whole chain in code — the
`XRGeneralSettingsPerBuildTarget` asset, an `XRManagerSettings` per target, the OpenXR
loader assigned to it, the asset registered under
`com.unity.xr.management.loader_settings`, and the interaction profiles enabled. Run it
with:

```bash
Unity -batchmode -nographics -quit -projectPath <p> -executeMethod XRSetup.Configure
Unity -batchmode -nographics -quit -projectPath <p> -executeMethod XRSetup.Verify
```

`Verify` prints the state of every link, so a VR failure is diagnosable rather than
mysterious. Current output:

```
settings asset registered : True
Standalone   manager=Standalone Providers  initOnStart=True  loaders=1 [OpenXRLoader]
Standalone   enabled OpenXR features: KHRSimpleControllerProfile, OculusTouchControllerProfile
```

**`VRRuntime`** reports four separate facts at runtime — XR module present, loader
active, HMD present, VR selected — instead of one opaque boolean. Its status string is
shown on the main menu and in the control-check bench, and is written into every
session's `session.json`. On this Mac it currently reads:

> `VR: no XR loader active (desktop). Expected on macOS — OpenXR is Windows/Android only.`

which is the correct and expected state on a Mac.

---

## 3. Platform reality

**OpenXR runs on Windows and Android, not macOS.** This project is developed on a Mac,
so on the development machine VR will always report inactive and the sim runs desktop.
That is not a bug and must not be "fixed".

The intended lab setup is the one already in this project's history: a **Windows PC
driving a Quest 2 over Link**. Android (Quest standalone) has the loader assigned too,
but its OpenXR settings could not be created on this machine because the Android build
module is not installed — configure it on a machine that has it if standalone Quest is
ever wanted.

---

## 4. Architecture

```
   controller pose / grip / trigger        (UnityEngine.XR.InputDevices)
                    │
              VRRuntime                    detection + polling + haptics
                    │
          CockpitInteractorVR              which control, grab or press
                    │
            PhysicalControl                position → normalised value
                    │
           AircraftController               ← the ONLY writer of aircraft input
                    │
             CessnaPhysics
```

The mouse interactor sits in exactly the same slot. **There is one aircraft simulation
and one control pipeline**; VR and desktop differ only in how a world point is obtained.
Nothing in the VR layer touches the Rigidbody, and nothing bypasses
`AircraftController`.

### Why core XR input, not the XR Interaction Toolkit

XRI supplies grab interaction, interactor/interactable state machines and a large
behaviour surface this project does not want. The cockpit already has its own
interaction model, shaped around *experimental* requirements — position-based,
spec-driven, identical between mouse and VR. Adopting XRI would mean either bending it
into that shape or accepting its own.

What is actually needed from the XR stack is small and stable: controller pose, a grip
value, two buttons, and haptics. `UnityEngine.XR.InputDevices` provides exactly that, is
part of the engine rather than a package that churns, and keeps the dependency at the XR
module plus a loader.

**Revisit only if** hand-tracking meshes or complex grab affordances become a research
requirement.

---

## 5. Interaction model

| Action | Input |
|---|---|
| Grab a continuous control (yoke, throttle, trim, flap lever, brake) | **grip** ≥ 0.65, release below 0.35 |
| Press a discrete control (carb heat, fuel selector, load shed, alt static) | **trigger** ≥ 0.7 |
| Acknowledge a prompt / checklist item | **A or X button** |
| Recentre the seated origin | both grips + both triggers |

**Both hands work independently.** One hand can fly while the other sets power — which is
how the aeroplane is actually flown — but two hands cannot fight over one control.

**Wrist rotation is deliberately not used for the yoke.** Mapping wrist orientation to
pitch/roll is the standard VR shortcut and it is wrong for this experiment: it couples
aircraft control to forearm posture, drifts as the arm tires over a 300 s trial, and adds
a motor difficulty that varies between participants — exactly the confound the design is
trying to keep out of the EEG. The yoke is driven by **hand displacement**.

**Acknowledgement is a button, on purpose.** Response latency to probes, decisions and
checklist items is experimental data. Requiring a hand to find and press a small virtual
button would add a variable, participant-dependent motor delay to every one of those
measurements.

Haptics: a short tick on grab, on a discrete press, and on recentre. Silently absent on
hardware without haptic support — no caller checks.

---

## 6. What "not verified" means, concretely

Compiled and configured, never executed against hardware:

* HMD initialisation and tracking
* controller pose accuracy and whether the cockpit controls are physically reachable
* capture radii in practice — whether reaching for the trim wheel grabs the fuel selector
* grip/trigger thresholds against real Touch controllers
* haptic feel
* frame rate with the GLB cockpit at VR resolutions and refresh rates
* comfort, and whether the seated origin lands the participant correctly in the seat

**Do not report this project as "Quest 2 verified".** The correct statement is: *the VR
implementation is complete and configured; hardware verification is pending.*

### Bringing it up on the Windows machine

1. Copy the project. Open in Unity 6000.0.77f1.
2. Run `XRSetup.Verify` (Tools ▸ Experiment ▸ Verify XR configuration). It should report
   the loader assigned for Standalone.
3. Connect the Quest over Link, start the Oculus runtime.
4. Press Play. The main menu shows the `VRRuntime` status line — it should now read
   *"VR: active — OpenXR, right controller, left controller"*.
5. **Go to CONTROL CHECK before anything else.** Reach for each control in turn and
   confirm the number on the panel moves. That is the whole pre-flight for VR.
6. Then fly `L3` (the quietest mission) end to end before running a participant.

If the status line says *"loader active but no HMD detected"*, the chain is configured
and the headset is the problem. If it says *"no XR loader active"*, run
`XRSetup.Configure`.

---

## 7. Known limitations

* **No rudder pedals in VR.** `Object_52` is the pedal geometry, but there is no foot
  tracking on Touch controllers. Rudder is on `Q`/`E` and available through the override
  API; a hand gesture for rudder would add more noise than realism. See
  `COCKPIT_CONTROLS.md` §7.
* **Brakes are a pull handle, not toe brakes** — same reason.
* **Mixture is not offered** because the engine model has no mixture, and the GLB fuses
  the throttle and mixture knobs into one mesh.
* **The G1000 displays are read-only.** Adding knobs that do not drive a simulated system
  would generate behavioural data unrelated to the flight model.
* **Hand tracking is not supported**, only controllers. `VRRuntime` degrades gracefully
  if controllers are absent, but there is no bare-hand path.
* **VR and desktop data must never be pooled.** See `VR_EXPERIMENT_CONSIDERATIONS.md`.
