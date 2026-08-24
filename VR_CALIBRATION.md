# VR_CALIBRATION

What has to be set up per participant before a VR session, and why it is kept this short.

---

## 1. The design principle

**The calibration is: sit down, get comfortable, recentre once.**

A long calibration routine would be a second task the participant performs before the
experiment, with its own learning curve and its own fatigue — and any variability it
introduces lands on the first mission, which counterbalancing then smears across
conditions. So the only thing calibrated is the one thing that genuinely varies between
people and sittings: **where their head is**.

Everything else is fixed by construction:

* The **pilot eye point** is measured from the GLB model (`RealCockpit.eyePointInModel`,
  `(0, 0.58, 0.52)` in model space) and is identical for every participant and identical
  to the desktop view. VR does not re-seat the camera; it applies head tracking *around*
  that point. See `VRCameraRig`.
* **Control positions** are fixed in the cockpit. They are not scaled to arm length —
  scaling them would mean different participants operate a different cockpit.
* **Control gearing** (travel distances) is fixed. Per-participant sensitivity would make
  the interface a between-subject variable.

---

## 2. Per-participant procedure (~2 minutes)

1. **Seat the participant** in the chair they will use for the whole session. Height and
   distance matter more than anything the software does — a participant who is too low
   will look at the panel from the wrong angle for an hour.
2. **Fit the headset over the EEG cap** — but see the warning in
   `VR_EXPERIMENT_CONSIDERATIONS.md` before assuming this is acceptable at all.
3. **Press Play → participant gate → CONTROL CHECK.**
4. **Recentre**: hold both grips and both triggers together. A double haptic tick
   confirms. The pilot's eye point is now their eye point.
5. **Touch every control once**, watching the panel readout:
   yoke → pitch/roll move · throttle → number moves · trim wheel → trim moves ·
   flap lever → detent label changes · brake → pressure moves · the four switches → state flips.
6. **Escape** back to the menu and start the session.

Step 5 is the real point of the exercise. It catches a dead controller, a mis-tracked
hand, an unreachable control or a mis-seated participant in about forty seconds —
*before* an hour of EEG is spent, rather than after.

---

## 3. Recentring mid-session

Seated origins drift, and a participant shifts in the chair over 75 minutes. Recentring
is available at any time with **both grips + both triggers**, so nobody has to remove a
headset that is sitting on an EEG cap.

It is deliberately a four-input gesture: it must never fire by accident while flying.

Recentring changes only the head origin. It does **not** move the aeroplane, the
controls, or the eye point relative to the cockpit — so it cannot alter the task, and
using it mid-trial does not invalidate the trial. It is not logged as an event because it
is not a pilot action; if that changes, add a marker.

---

## 4. What is stored

Nothing per-participant is persisted. The recentre offset lives in `VRHeadPose` for the
session and is discarded on exit — deliberately, so a stale calibration from a previous
participant can never be silently applied to the next one.

What *is* recorded, in `session.json` and every trial's `metadata.json`:

```json
"input_modality": "vr",
"vr_status": "VR: active — OpenXR, right controller, left controller"
```

so the modality and the state of the VR chain are attached to the data rather than
remembered.

---

## 5. If calibration ever needs to grow

Two things would justify it, and neither is currently implemented:

* **Arm-length scaling of control positions** — only if participants genuinely cannot
  reach a control. The cost is that the cockpit is then not identical between people,
  which has to be recorded and modelled.
* **Per-participant yoke gearing** — only if a formal pilot study shows the fixed travel
  is too sensitive or too heavy for a substantial fraction of participants. The cost is
  that control gain becomes a between-subject variable.

Both trade experimental control for comfort. Do not add either without deciding, in
writing, which one you are buying.
