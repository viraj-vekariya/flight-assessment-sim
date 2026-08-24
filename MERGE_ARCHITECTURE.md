# MERGE_ARCHITECTURE

How `FlightAssessmentSim - Final` was assembled from the two source projects, what was
taken from each, what was deliberately left behind, and why.

**Sources (both left untouched and still usable as references):**

| | Path | Role |
|---|---|---|
| **VR** | `FlightAssessmentSim - VR/` | the experimental apparatus |
| **EXTRA** | `FlightAssessmentSim - Extra - Copy/` | the cockpit, displays and interaction |

---

## 1. The one-line summary

Final = **EXTRA's cockpit, displays, interaction, physics tuning and project settings**
+ **VR's entire research apparatus**, with three files hand-merged because each source
had fixed a different thing, and with the VR layer dropped.

---

## 2. System-by-system

| System | VR | EXTRA | Final source | Reason |
|---|---|---|---|---|
| **Mission framework** (`MissionLibrary`, `MissionDefinition`) | ✓ | — | **VR, then redesigned** | Only VR had one. Rewritten in Final — see §5. |
| **Workload model** (`WorkloadModel`) | ✓ | — | VR | Research |
| **Aircraft systems / failures** (`AircraftSystems`, `SystemsInput`) | ✓ | — | VR | Research |
| **Checklists** (`ChecklistSystem`) | ✓ | — | VR | Research |
| **Event markers** (`EventMarkers`, 46 tags — now 53) | ✓ | — | VR | Research |
| **Telemetry** (`ExperimentLogger`) | ✓ | — | **VR, then fixed** | Sampling moved to the physics step — see §6 |
| **Session / Latin square / baselines** (`ExperimentSession`) | ✓ | — | VR | Research |
| **Experiment UI** (`ExperimentUI`) | ✓ | — | VR | Research |
| **NASA-TLX** (`ResultsUI`, `WorkloadRating`, `WorkloadLog`) | ✓ (corrected) | ✓ (older) | VR | VR has the 21-point, no-default-answer version |
| **EEG / LSL** (`LSLSync`) | ✓ (Available/Clock) | ✓ (older) | VR | VR exposes the clock the logger needs |
| **Scenario engine** (`ScenarioEngine`) | ✓ 992 L | ✓ 529 L | **VR, then extended** | VR has jitter, segments, failures, checklists, markers. Taxi + traffic added in Final. |
| **Scenario types** (`ScenarioTypes`) | ✓ | ✓ | **VR, then extended** | VR has the experiment event types; Final adds `TaxiTakeoff` + traffic fields |
| **GameManager** | ✓ research flow | ✗ free-flight stub | **VR** | EXTRA's forces `ParticipantReady = true` and skips the whole experiment |
| **Menu / mission UI** (`MenuUI`) | ✓ | ✗ | **VR, then rewritten** | Rewritten for the crossed design grid |
| **Scenario HUD** (`ScenarioHud`) | ✓ | ✓ (basic) | VR | VR has the checklist panel + systems annunciators |
| **Participant gate** (`ParticipantUI`, `ParticipantManager`) | ✓ | present but bypassed | VR | Restores the gate |
| **Test harness** (`MissionTestHarness`) | ✓ | — | **VR, then extended** | Taxi driver added; timescale retuned for the cockpit |
| **Doc generator** (`Editor/DocGen`) | ✓ | — | VR | Docs generated from code |
| — | | | | |
| **Real GLB cockpit** (`RealCockpit`) | `ModelCockpitRig` (older port) | ✓ | **EXTRA** | More complete node map, verified against renders, better mesh surgery |
| **Live PFD** (`LivePFD`) | 93 L | ✓ 162 L | **EXTRA** | Adds HDG/SPD/ALT/VS readouts, pitch marks, aircraft symbol |
| **Live MFD** (`LiveMFD`) | 76 L | ✓ 178 L | **EXTRA** | Adds compass rose, range ring, aircraft silhouette |
| **Cockpit builder** (`CockpitBuilder`) | 756 L (VR anchors) | ✓ 619 L | **EXTRA** | Wires `RealCockpit` + mouse interaction; VR's wires VR grab anchors |
| **Cockpit interaction** | VR hand-grab (same class name!) | ✓ mouse | **EXTRA** | Study is flatscreen; see §4 |
| **`CockpitControl`, `CockpitTuner`, `ScreenTuner`, `ScreenProbe`, `YokeProbe`, `YokeShot`, `FlightTest`** | — | ✓ | EXTRA | Cockpit tooling and verification |
| **`AircraftAudio`, `YokeAnimator`, `Bootstrap`** | ✓ (+VR spawns) | ✓ | EXTRA | Same code minus the VR spawns |
| — | | | | |
| **Flight physics** (`CessnaPhysics`) | ✓ hooks | ✓ tuning | **HAND-MERGED** | §3 |
| **Aircraft controller** (`AircraftController`) | ✓ flap authority | ✓ mouse overrides | **HAND-MERGED** | §3 |
| **World / terrain** (`WorldBuilder`) | ✓ approach fix | ✓ departure fix | **HAND-MERGED** | §3 |
| **Project settings** | XR + new input | ✓ | EXTRA | Flatscreen build; legacy input is what the code uses |
| **Packages** | gltfast 6.19 + OpenXR | gltfast 6.0.1 | **merged** | gltfast **6.19.0**, XR removed |

---

## 3. The three hand-merges

Each source had independently fixed something the other had not. Taking either file
wholesale would have silently lost work.

### 3.1 `CessnaPhysics.cs`

| Feature | VR | EXTRA | Final |
|---|---|---|---|
| `turnCoordination` (bank → heading) | ✓ (ported from EXTRA) | ✓ origin | ✓ |
| `pitchDamp` / `rollDamp` / `yawDamp` | ✓ (ported) | ✓ origin | ✓ |
| `maxThrust` | 2800 N | 2800 N | 2800 N |
| `cd0` | 0.030 | **0.040** ("raised so top speed is realistic") | **0.040** |
| `powerAvailable01` | ✓ | ✗ | ✓ |
| `extraCd` | ✓ | ✗ | ✓ |
| `extraRollTorque` | ✓ | ✗ | ✓ |
| `extraYawTorque` | ✓ | ✗ | ✓ |
| `flapAuthority01` | ✓ | ✗ | ✓ |
| `ThrustN` (telemetry) | ✓ | ✗ | ✓ |

Without the turn-coordination term, banking tilts the lift vector but the nose never
follows — the aeroplane slips sideways and sinks, and **no heading instruction can be
flown**. Without the failure hooks, `AircraftSystems` has nothing to act on and every
abnormal mission becomes a banner with no consequence. Both halves are required.

### 3.2 `AircraftController.cs`

EXTRA's base (it carries the mouse-override API `SetPitch/SetRoll/SetYaw/SetThrottle/
ClearOverrides` that `CockpitInteraction` drives), plus from VR:
`FlapsSelected`, the `flapAuthority01` clamp, and `ResetConfiguration()` — which fixes
a between-trial carry-over where a flap selection survived into the next mission.

### 3.3 `WorldBuilder.PadFlatten`

Both had widened the flat aerodrome pad, for different reasons:

| | lateral | departure (+Z) | approach (−Z) | blend |
|---|---|---|---|---|
| EXTRA | ±110 m | 1500 m | 450 m | 420 m |
| VR | ±95 m | 1600 m | **10 000 m** | 700 m |
| **Final** | **±260 m** | **1600 m** | **10 000 m** | **700 m** |

Final takes the widest on every axis, and widens laterally further still to carry the
new apron and parallel taxiway. All three constraints are real: terrain reaches ~130 m
just past the runway end (departures used to fly into it), the 12 km straight-in
crosses ground reaching ~350 m (approaches used to "land" on a hilltop at 128 m), and
the taxi layout needs ~260 m of flat ground beside the runway.

---

## 4. Deliberately NOT carried over

| Not copied | Why |
|---|---|
| `VRManager`, `VRHead`, `VRHandControls`, `VRInput`, `VRUIBridge` | Final is the **flatscreen experiment build**. An EEG cap under a Quest 2 head strap sits on exactly the occipital and parietal electrode sites the alpha measure depends on, and VR changes measured workload anyway — so the study cannot be run in VR. Keeping the layer would also have produced a hard class-name clash (below). |
| VR's `CockpitInteraction` | **Same class name, completely different system**: VR's is a proximity hand-grab rig, EXTRA's is the mouse interaction. Only one can exist. Flatscreen → EXTRA's. |
| VR's `ModelCockpitRig` + `ImportedCockpitLoader` | Superseded by EXTRA's `RealCockpit`, which is the more complete implementation they were ported *from*. |
| `Assets/XR/`, `com.unity.xr.openxr`, `com.unity.modules.xr` | Follows from dropping the VR layer. |
| VR's `CockpitBuilder` | Wires VR grab anchors that no longer exist. |
| `MissionSets.cs` | Already superseded in VR by `MissionLibrary`. |
| `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vscode/`, `*.csproj`, `*.slnx`, `play.log`, `CockpitPreview/` | Regenerated by Unity or machine-local. |

The VR project remains on disk and still runs, so the VR demonstration is not lost —
it is just not the experiment.

### Consequences handled

* `ScenarioEngine` dropped its `VRInput.ConsumeAlarmAck()` call; the `ExternalAck()`
  channel already covers programmatic acknowledgement.
* `GameManager.VR_Confirm/VR_Restart/VR_Back` were renamed
  **`Confirm` / `RestartTrial` / `BackToMenu`** — the names were misleading once VR was
  gone, but the methods are still needed: they are the programmatic control API the
  test harness drives.
* `Bootstrap` no longer spawns `VRManager` / `VRUIBridge`.
* `PlayCapture` now carries **both** drivers: `Run()` (EXTRA's timed run, used by the
  cockpit/screen/yoke probes) and `RunMissionTest()` (VR's mission battery).

---

## 5. The mission set was redesigned, not merely copied

The VR mission set was **not** carried across unchanged. Auditing it against the
project's requirements document exposed two problems:

1. **No take-off mission existed.** The requirements name three stages — take-off,
   flight, landing — with the initial focus on **take-off at three levels**. The VR set
   had none: it was 6 cruise, 1 climb, 4 approach, 1 circuit.
2. **Flight phase was confounded with workload class.** HIGH was three approaches plus
   a circuit; LOW was three cruises plus one landing. Any LOW-vs-HIGH difference could
   therefore have been *cruising vs landing* rather than workload.

Final replaces it with a **fully crossed 4 phases × 3 classes grid** — every class
appears exactly once in every phase. See `FINAL_MISSION_DESIGN.md`. This also required
new simulator capability: a taxi/take-off goal, an aerodrome (apron, taxiways,
hold-short, signage) and visible AI traffic.

---

## 6. Defects found and fixed during the merge

| Defect | Fix |
|---|---|
| **"50 Hz" telemetry was frame-rate bound.** The sampler could only fire once per frame, so it produced ~30 Hz at 30 fps — and the busiest, highest-workload missions would have been sampled worst. | Mission clock and telemetry moved to `FixedUpdate` (physics step, 0.02 s = a true 50 Hz), which also makes event timing deterministic for a given seed. |
| `-nographics` crashes Unity in the dynamic batch renderer once the real cockpit's off-screen PFD/MFD cameras exist. | Test battery documented and run as `-batchmode` **without** `-nographics`; warning recorded in `PlayCapture.cs`. |
| Harness ran at 20× real time; with the photoreal cockpit Unity hit `maximumDeltaTime` and silently clamped the physics catch-up. | Reduced to 8×, wall-clock guard raised. |

---

## 7. Verification that the merge is complete

Run from the Final project root:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity

# compiles clean
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" -logFile compile.log

# all twelve missions fly and record correctly  (NOTE: no -nographics)
"$UNITY" -batchmode -projectPath "$PWD" \
         -executeMethod PlayCapture.RunMissionTest -missiontest -logFile test.log
```

The battery log line `RealCockpit: centred Object_96+Object_98+Object_100 ...` confirms
the real GLB cockpit loaded and the single-pilot mesh surgery ran — i.e. the cockpit
half of the merge is live, not just present on disk.
