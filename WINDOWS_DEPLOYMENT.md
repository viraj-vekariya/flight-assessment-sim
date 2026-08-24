# WINDOWS_DEPLOYMENT — taking the simulator to the laboratory PC

## 1. Status, stated plainly

**Windows Build Support (Mono) was missing and has been installed.** The development
machine originally had only `MacStandaloneSupport` under `PlaybackEngines/`, and Unity
cannot cross-compile to a target whose module is absent — so no Windows build was
possible at all. `WindowsStandaloneSupport` is now present and
`modules.json` records `windows-mono` as selected.

Also done, and independent of the module:

* a full static portability audit of the codebase (§2), which is **clean**;
* verification that no runtime script has an editor-only dependency;
* a reproducible build script (§3) that fails with the exact install command rather
  than an opaque error if the module is ever missing again.

The build's own status is recorded in `FINAL_CHANGE_REPORT.md` §10 — for the same reason
the VR documentation records what needs a headset: an unverified claim in a deployment
document is worse than an admitted gap, because someone will rely on it on the day.

## 2. Portability audit — what was checked and what was found

| Check | Result |
|---|---|
| Absolute macOS paths in code (`/Users`, `/Applications`, `~`) | **None.** Only in comments giving the editor's own path. |
| Path construction | `Path.Combine` throughout. The `"/"` literals that exist are display strings and `Resources.Load` paths, which use `/` on every platform by definition. |
| Data root | `Application.persistentDataPath` — resolves correctly and writably on Windows (`%USERPROFILE%\AppData\LocalLow\<company>\<product>`). |
| StreamingAssets (GLB cockpit, sky, terrain, voice) | `Path.Combine(Application.streamingAssetsPath, …)` — correct on Windows. |
| `using UnityEditor` in runtime scripts | **None.** The four editor tools live under `Assets/Editor/` and are excluded from a build automatically. |
| Shell / process invocation | **None.** |
| Case sensitivity | No collisions; Windows is case-insensitive, so a macOS-clean project cannot regress here. |
| Line endings in the data files | `StreamWriter` uses `Environment.NewLine`, so CSVs written on Windows are CRLF and on macOS LF. Both `pandas` and Python's `csv` handle either. **Noted, not a defect** — but a checksum comparison across platforms will differ, so do not use one as an integrity check. |

## 3. Producing the build

If the module is ever missing (a fresh machine, a new editor version), install it once:

```bash
"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless \
    install-modules --version 6000.0.77f1 --module windows-mono --childModules
```

Then, from the project root:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" \
         -executeMethod BuildTool.BuildWindows -logFile build_win.log
```

`Assets/Editor/BuildTool.cs` sets the target, the output path and the player options, so
the build is reproducible from a command line rather than from remembered dialog
settings.

## 4. On the laboratory PC

1. **Copy the whole build folder**, not just the `.exe`. `FlightAssessmentSim_Data/` and
   `StreamingAssets/` must travel with it — the cockpit GLB, the sky, the terrain
   textures and the voice callouts all load from `StreamingAssets` at runtime. If the
   GLB is missing the simulator still runs, on the code-built fallback cockpit, and it
   is **visibly different** so the failure cannot pass unnoticed.
2. **Check the write path.** Data goes to
   `%USERPROFILE%\AppData\LocalLow\DefaultCompany\FlightAssessmentSimFinal\FlightSimData\experiment`.
   On a locked-down institutional machine, confirm this is writable *before* a
   participant is in the chair.
3. **Bind the controls** — see `HARDWARE_CONTROLS.md` §5.
4. **Check XR** if running in VR — see `VR_CALIBRATION.md`. The OpenXR loader chain is
   configured in `Assets/XR/`; `XRSetup.Verify` reports every link.
5. **Run the control-check bench** (`-controlcheck`) and confirm every axis moves.
6. **Run one free flight** to confirm the world, cockpit and displays load.

## 5. Command-line flags

| Flag | Purpose |
|---|---|
| `-controlcheck` | control/calibration bench |
| `-missiontest` | the full mission battery (add `-bankquick` for one variant index) |
| `-controltest` | the 50-check cockpit control battery |
| `-windtest` | the 46-check wind-model battery |
| `-flighttest` | the scripted flight verifier — **opt-in only**, and it takes the controls |

Only one of these may be used at a time. `SimDriver` enforces it: the second driver to
claim control stands down and says so in the log, because two harnesses flying at once
does not crash, it quietly changes every number the run reports.

## 6. What remains hardware-dependent

* The Windows build itself (module not installed).
* Meta Quest: headset detection, seated-origin comfort, control reachability, frame
  timing under the real cockpit load.
* Yoke/throttle/pedal axis numbering and travel.
* EEG: the LSL outlet is implemented and marked, but end-to-end synchronisation with an
  actual amplifier has never been measured. See `FINAL_EEG_INTEGRATION.md`.
