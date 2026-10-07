# FlightAssessmentSim — BTP Final

A Cessna 172 glass-cockpit flight simulator built in Unity 6000.0.77f1 to drive an
EEG cognitive-workload experiment (*Automatic Workload Assessment using Brain
Signal Classification*, IIT Kharagpur B.Tech project).

Everything is code-generated at runtime: the terrain, the city and farmland, the
aerodrome, the cockpit (from a GLB model), the displays, the aircraft physics and
all 42 missions. The only scene is `Bootstrap`.

This folder is the merge of two working copies that forked from the same commit:

| Copy | Owned | Now here |
|---|---|---|
| `FlightAssessmentSim - UI` | environment, cockpit, displays, controls | sky, ground, city, PFD/MFD, standby gauges, trim wheel, pedals |
| `FlightAssessmentSim - Level` | experiment flow, missions, data | questionnaire removed, trial integrity flags, dataset exporter, EEG epoch tool |

## Run

Open the folder in Unity Hub and press Play. The flow is: participant ID → menu →
free flight to learn the aeroplane → missions, each 300 s, recorded automatically.
Nothing has to be filled in after a mission.

Mission data lands in `<persistentDataPath>/FlightSimData/experiment/<PID>/<SESSION>/T<nn>_<MISSION>/`
(`telemetry.csv` at 50 Hz, `events.csv`, `trial.json`, `metadata.json`,
`performance.json`). Build the analysis set with:

```bash
python3 tools/build_dataset.py            # index.csv, coverage.csv, VALIDATION.txt
python3 tools/eeg_epochs.py --markers CUE_ONSET
```

Filter on `usable == "true"`. See `DATA_MANAGEMENT.md`.

## Verify

Each battery launches Unity in batch mode (with a graphics device; no `-nographics`
for play-mode runs) and writes a report to the project root:

```bash
U=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -projectPath "$PWD" -executeMethod PlayCapture.RunControlTest -controltest -logFile control.log
$U -batchmode -projectPath "$PWD" -executeMethod PlayCapture.RunPhysicsTest -physicstest -logFile physics.log
$U -batchmode -projectPath "$PWD" -executeMethod PlayCapture.RunWindTest    -windtest    -logFile wind.log
$U -batchmode -projectPath "$PWD" -executeMethod PlayCapture.RunMissionTest -missiontest -logFile mission.log
```

Reports: `control_test_report.txt`, `physics_test_report.txt`, `wind_test_report.txt`,
`mission_test_report.txt`.

## Build

```bash
$U -batchmode -nographics -quit -projectPath "$PWD" -executeMethod BuildTool.BuildWindows -logFile build.log
```

Output: `Builds/Windows/FlightAssessmentSim.exe` (ignored by git). `BuildTool.BuildMac`
does the same for macOS.

## Documents

Start with `FINAL_PROJECT_README.md`, then `FINAL_EXPERIMENT_PROTOCOL.md`,
`MISSION_BANK_DESIGN.md`, `FINAL_TELEMETRY_SCHEMA.md` and `DATA_MANAGEMENT.md`.
`NASA_TLX.md` describes the post-trial questionnaire that this build no longer shows.
