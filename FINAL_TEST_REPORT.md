# FINAL_TEST_REPORT

Verification of the merged project and the redesigned twelve-mission experiment.

**Status: 0 compile errors, 0 warnings. Mission battery 12/12, 0 problems. All twelve
missions fly to completion under the scripted pilot.**

Raw output: `mission_test_report.txt`.

---

## 1. What is tested

`Assets/Scripts/MissionTestHarness.cs` flies **all twelve missions end to end** in
headless play mode with a scripted pilot at 8× real time, then reads the files that
landed on disk and asserts against them.

Per mission:

| Check | What would break without it |
|---|---|
| a trial folder is created | nothing is recorded at all |
| `telemetry.csv`, `events.csv`, `metadata.json`, `eeg/sync.json` exist | a missing sidecar is only noticed months later |
| `nasa_tlx.json` written | the self-report path is broken |
| telemetry ≥ 100 rows, no ragged rows, time non-decreasing | corrupt or unusable time series |
| events time non-decreasing | epochs cannot be ordered |
| every marker is in the declared 53-tag vocabulary | an EEG analysis hits an unknown label |
| `MISSION_START` / `MISSION_END` appear **exactly once** | double-counted or truncated trials |
| the mission's own `RequiredMarkers` all appear | the manipulation silently did not happen |
| `TLX_START` + `TLX_SUBMIT` present | the questionnaire period cannot be excluded from task epochs |
| `TRIGGER_ARMED` ⟹ `CUE_ONSET` | **no valid EEG epoch zero** for that failure |
| `CHECKLIST_START` ⟹ ≥1 `CHECKLIST_ITEM` | a drill that never ran |
| checklist timeouts reported | an unreachable item hiding as "completed" |
| in-task baseline ≥ 70% of expected rows | the EEG reference segment is short or absent |

Once per battery: restart re-enters Flying, allocates a **new** trial folder, resets the
mission clock, and leaves no armed failure behind.

### The second battery: cockpit controls

`Assets/Scripts/ControlTestHarness.cs` (`-controltest`) exercises all nine physical
cockpit controls — **50 checks**. It is a separate battery because the mission battery
flies with a scripted pilot writing directly to the controller and would never touch a
lever.

| Group | What would break without it |
|---|---|
| yoke: full aft / forward / left / right, half travel, release | reversed or mis-geared primary control — the worst possible silent defect |
| throttle: idle / mid / full / push-forward-adds-power | power backwards |
| trim: neutral, ±1, elevator bias, direction | trim fighting the pilot instead of helping |
| flaps: three detents, actual extension, **failure freezes travel**, restore | a pilot silently undoing a failure — see defect 13 |
| brake: ground-only, full, partial, release, airborne ignored | brakes that never worked at all — see defect 12 |
| four switches and the 3-position fuel selector | memory items that do nothing |
| **control ownership**: a write takes it, and it EXPIRES | the override latch — see defect 11 |
| **reset**: trim, brake, flaps, all switches, selector, no override owned | state leaking between trials |
| **frame-rate independence at 30 / 60 / 90 FPS** | control response differing by machine |

The frame-rate section deserves a note because it is the one test that verifies a claim
rather than a behaviour. At 30, 60 and 90 FPS (pinned with `Time.captureFramerate`, and
the test asserts the rates were *actually* simulated — 1, 2 and 3 frames of 0.0333,
0.0167 and 0.0111 s) a step input on the throttle produced **0.614, 0.614, 0.614**
against an analytic `1 − exp(−t/τ)` prediction of 0.614, spread 0.000. A naive
per-frame `Lerp(a, b, k)` would have produced roughly 0.05 / 0.10 / 0.15 — i.e. a
desktop participant at 60 FPS and a VR participant at 90 FPS would have been flying
aircraft with measurably different control response.

Plus a **flyability** line per mission — reported, not asserted, because the scripted
pilot is a crude autopilot and not a model of a participant.

---

## 2. Result

```
missions run : 12
problems     : 0
scripted-pilot outcomes:
  L1=SUCCESS  L2=SUCCESS  L3=SUCCESS  L4=SUCCESS
  M1=SUCCESS  M2=SUCCESS  M3=SUCCESS  M4=SUCCESS
  H1=SUCCESS  H2=SUCCESS  H3=SUCCESS  H4=SUCCESS
```

Typical trial: **15,000 telemetry rows × 77 columns** (a true 300 s at 50 Hz),
~3,000 baseline rows, 8–34 event markers depending on the mission.

This is the **re-run mandated after the flight model changed** — trim, analog brakes and
the flap-motor lock all alter the physics, so the whole battery was flown again rather
than assumed unaffected. 12/12, 0 problems, and the column count rose from 67 to 77 as
the control-state columns were added.

The control battery:

```
controls built: 9
problems      : 0        (50 checks)
```

**The cockpit half is verified live, not just present**: the battery log contains
`RealCockpit: centred Object_96+Object_98+Object_100 …`, i.e. the GLB loaded and the
single-pilot mesh surgery ran inside the merged project.

**The taxi and traffic systems are verified live**, e.g. H1:

```
  2.5  ATC_MESSAGE   TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. EXPECT DELAYS.
 31.4  TRAFFIC_ONSET TaxiingParallel | TRAFFIC AHEAD ON ALPHA
 43.7  ATC_MESSAGE   AMENDED CLEARANCE: left heading 320, climb 800 m, CROSS 5 MILES...
 58.5  PHASE_CHANGE  hold_short_reached | taxi_m=215
 64.4  TRAFFIC_ONSET HoldingOnRunway | TRAFFIC HOLDING ON RUNWAY 01
 84.0  ATC_MESSAGE   HOLD POSITION — TRAFFIC ON THE RUNWAY.
118.7  ATC_MESSAGE   CLEARED FOR TAKE-OFF RUNWAY 01 — NO DELAY, traffic 3 miles final.
```

— the amended clearance arriving *while still taxiing*, the runway genuinely blocked,
and the clearance withheld until it clears. `runway_incursion = 0` on all three taxi
missions confirms the hold-short gate is enforced and was respected.

---

## 3. Defects the testing found

Nine so far. The first six were found before the merge; three are new to this project.
Every one was invisible in the design documents.

### New in the merged project

**1. "50 Hz" telemetry was frame-rate bound.**
The sampler could only fire once per frame, so it produced ~30 Hz at 30 fps and
collapsed to ~2 Hz under the accelerated harness. Critically, **the rate fell exactly
where the scene was busiest — the highest-workload missions** — so the sample rate
correlated with the independent variable. That is a confound in the measuring
instrument itself.
*Fix:* mission clock and telemetry moved to `FixedUpdate` (0.02 s = a true 50 Hz),
which also makes event timing deterministic for a given seed on any machine.

**2. Two harnesses drove the controls at once.**
`FlightTest` (from the cockpit project) auto-runs under `-batchmode` and writes
`pitchInput`/`throttle` every frame. It fought `MissionTestHarness` for the controls, so
the aeroplane flew *FlightTest's* scripted climb profile instead of the mission —
climbing away from every assigned altitude — and half the battery was scored INCOMPLETE
for reasons that had nothing to do with the missions. The report looked like a mission
problem and was not.
*Fix:* `FlightTest` stands down for `-missiontest`, `-probe` and `-screens`; the harness
also destroys any rival driver defensively and re-asserts control ownership each frame.

**3. `-nographics` crashes Unity once the real cockpit exists.**
The PFD/MFD render to off-screen cameras; the null graphics device dies in the dynamic
batch renderer. *Fix:* the battery runs `-batchmode` **without** `-nographics`; the
constraint is documented in `PlayCapture.cs` and the README.

### Carried over from the pre-merge experiment project

4. **Late events never fired in the landing missions** — approaches ended at
   t ≈ 110 s so checklists and decision prompts scheduled at 120–205 s never happened.
   Four missions would have run without their manipulation.
5. **The open-door model was ~7× too strong** and spiralled the aircraft into the
   ground every time — the mission designed to be *startling but harmless* was the most
   lethal in the set, which would have destroyed the startle-versus-danger dissociation
   the design depends on.
6. **Banking did not turn the aeroplane** (no turn-coordination term), so no heading
   instruction could be flown.
7. **Aircraft configuration leaked between trials** — flap selected in one mission was
   still down in the next.
8. **Taking off from this airfield was impossible**, and the 12 km straight-in crossed
   hills reaching ~350 m (an approach "landed" on a hilltop at 128 m altitude).
9. **A mission's in-task baseline was a different task** from the others', breaking the
   baseline-to-task contrast for that mission.

### New in the cockpit-control and VR phase

Five real defects, four of which the control battery caught and one of which it was
written to prevent recurring.

**10. The override latch — VR would have killed the keyboard.** `AircraftController`
latched an override on the first external write and cleared it only in
`ClearOverrides()`, **which had no live caller anywhere in the project**. The first time
a VR hand or a cockpit lever touched an axis, the keyboard for that axis died for the
rest of the session, silently. Replaced with frame-expiry ownership: a write owns an
axis for that frame plus one, and ownership lapses on its own, so there is no release
call for anyone to forget. Tested both directions — a write takes ownership, and
ownership expires.

**11. The brakes had never worked.** `phys.braking = false` was hard-assigned every
frame, so the `B` key did nothing and no cockpit brake could have worked either. This
had been true through the whole mission battery without being noticed, because the
scripted pilot never brakes. Restored as an analog pressure (`brakeInput01`), ground-
gated so wheel brakes cannot be applied in flight.

**12. A pilot could undo a flap failure.** With the flap motor failed, flap authority
was recomputed each frame as "wherever the flaps are now" — so selecting UP let the
authority chase the flaps to zero and **fully retract them**. The flaps were stuck
extended in one direction only. This mattered well beyond realism: H2's entire decision
is whether to spend a dying battery on flaps *you cannot take back*, so a participant
who selected UP would have silently defeated the manipulation and the trial would have
looked normal in the data. Fixed with `flapMotorLocked`, which freezes the surface at
the position where the motor died. Caught by the control battery on its third run.

**13. Levers overrode the keyboard permanently.** The first cockpit implementation wrote
each control's position to the aircraft every frame, held or not. A throttle lever
sitting at idle therefore overwrote every `Shift` press, and because something was
always writing, `AnyOverride` was permanently true and the frame-expiry model above was
defeated by the very controls it existed to serve. Fixed by writing **only while held**
and mirroring the aircraft otherwise — which is also what a real cockpit lever does.

**14. `ElevatorCmd` read zero whenever the aircraft was slow.** It was computed inside
`if (speed > 1.5f)`, so trim could not be verified on the ground and the value logged in
telemetry was wrong at low speed. Moved outside the airspeed gate; only the resulting
*torque* is airspeed-dependent, which is the physically correct place for that
dependency.

Two of the control battery's early "failures" were the **test** being wrong, and both are
worth recording because they are easy to repeat:

* The harness waited a fixed number of *frames*. Batchmode frames take about 1.5 ms, so
  "wait 10 frames" was 15 ms — against a 45 ms smoothing constant and a one-second flap
  travel. Every time-based behaviour looked broken. Anything with a time constant must
  be given *time*, not frames.
* The frame-rate test measured `Value` (the raw hand position, which steps instantly)
  instead of the smoothed value the aircraft actually receives, and so read 1.000 at
  every frame rate — passing vacuously. `PhysicalControl.Smoothed` was exposed for the
  purpose. A test that cannot fail is not evidence.

### Also fixed, found by inspection rather than by the battery

* **Both source projects shared a `persistentDataPath`** (`DefaultCompany/FlightAssessmentSim`),
  so Final would have written participant data into the same folder as the old projects
  and their test output. Product name changed to `FlightAssessmentSimFinal`.
* **`time_in_tolerance_pct` read 0% on taxi missions** — tracking error was accumulated
  against a cruise altitude while the aeroplane was still on the ground. Tracking
  metrics are now only accumulated once airborne, and are omitted entirely when no
  tracking occurred, so an inapplicable 0% can never be mistaken for terrible
  performance.
* **Taxi checkpoint accounting stalled at 1/4** because the hold-short trigger zone is
  entered before the waypoint radius. The state machine now credits the checkpoints it
  is authoritative for.

---

## 4. What this testing does **not** establish

* **Nothing about workload.** No human has flown these missions. Whether LOW / MEDIUM /
  HIGH separate on NASA-TLX, on performance, or on EEG is completely open.
* **Nothing about the load model.** The Predicted Load Index is a pre-registered
  prediction; the battery only confirms the missions run as specified.
* **Nothing about EEG.** No amplifier has ever been connected. The LSL path compiles
  and is written but is untested against live hardware.
* **Nothing about difficulty for a person.** That the scripted pilot completes a mission
  says it is *flyable*, not that it is appropriately difficult. Its performance numbers
  are not data.
* **Nothing visual beyond "the cockpit loaded".** The battery confirms the GLB loads and
  the mesh surgery runs. It does not confirm the panel *looks* right, the PFD is
  readable, or the yoke tracks correctly — use `PlayCapture.Run` with `-screens` /
  `-probe`, or simply open the editor and look.
* **Nothing about VR on hardware.** No headset has ever been connected to this project.
  The XR loader chain is configured and `XRSetup.Verify` confirms it, the interaction
  code compiles and the desktop path is fully verified — but HMD tracking, controller
  pose, whether the capture radii are reachable, haptics and VR frame rate are all
  **unverified**. Do not describe this project as "Quest 2 verified".
  `VR_INTERACTION.md` §6 lists exactly what is untested and how to bring it up.
* **Nothing about whether the controls feel right.** The control battery verifies
  wiring, direction, gearing, reset and frame-rate independence. Whether a control is
  comfortable, reachable, or appropriately sensitive for a human needs a person.
* **The `TEST01` folder is synthetic** — fixed NASA-TLX values submitted by the harness
  to exercise the write path. Never analyse it; `build_dataset.py` excludes it by default.

---

## 5. Reproducing

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
PROJ="$HOME/Desktop/BTP Project/FlightAssessmentSim - Final"

# compile
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" -logFile compile.log

# the twelve-mission battery — NOTE: no -nographics, and no -quit
"$UNITY" -batchmode -projectPath "$PROJ" \
         -executeMethod PlayCapture.RunMissionTest -missiontest -logFile test.log
echo "exit code: $?"    # 0 = all checks passed

# the cockpit control battery — same rule: no -nographics, no -quit
"$UNITY" -batchmode -projectPath "$PROJ" \
         -executeMethod PlayCapture.RunControlTest -controltest -logFile ctest.log

# regenerate the generated docs
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
         -executeMethod DocGen.Generate -logFile docgen.log

# confirm the XR loader chain (configuration only — this does not test a headset)
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
         -executeMethod XRSetup.Verify -logFile xr.log
```

Runtime ≈ 9 minutes. Report at
`~/Library/Application Support/DefaultCompany/FlightAssessmentSimFinal/mission_test_report.txt`.

Analysis pipeline (verified on the battery's own output — 26 trials, 7,730 windows,
75 derived columns per trial):

```bash
python3 analysis/build_dataset.py <experiment_root> -o out/
```
