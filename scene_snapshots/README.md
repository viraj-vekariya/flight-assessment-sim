# Mission scene snapshots

Rendered from the Final project itself, not mocked up. Regenerate any time with:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "<project>" \
         -executeMethod PlayCapture.RunMissionShots -shots -logFile shots.log
```

> No `-nographics` — the cockpit's PFD/MFD render to off-screen cameras and Unity's
> null graphics device crashes on them.

Output lands in `<persistentDataPath>/shots/` and is copied here.

## Files

| File | What it is |
|---|---|
| `CONTACT_SHEET_cockpit.png` | all 12 pilot views, laid out as the design grid (phase × class) |
| `CONTACT_SHEET_external.png` | all 12 external views, same layout |
| `NN_<id>_<CLASS>_cockpit.png` | full-resolution pilot view, 1600 × 1000 |
| `NN_<id>_<CLASS>_external.png` | full-resolution external view, 1600 × 1000 |

## How the moment is chosen

Each frame is captured at that mission's **defining moment**, not at t = 0 — otherwise
all twelve would be near-identical pictures of a Cessna panel. The capture times live in
`MissionShots.ShowcaseAt` and are derived from each mission's event schedule:

| | captured at | showing |
|---|---|---|
| L1 | 48 s | taxiing alpha, hold-short bars and signage ahead |
| M1 | 50 s | traffic crossing the runway, light rain |
| H1 | 92 s | holding short with traffic **on** the runway, heavy rain |
| L2 / M2 / H2 | 110 / 135 / 165 s | established climb · after the door opens · low volts |
| L3 / M3 / H3 | 150 / 160 / 175 s | level hold · mid re-clearance turn · in cloud |
| L4 / M4 / H4 | 215 / 205 / 150 s | short final · approach in weather · gliding, engine out |

## What to look for

* **Weather is a real visual variable.** LOW missions are clear blue; M1/H1/M4/H3 are
  visibly overcast and hazier. Before this the bundled HDRI made every mission look
  equally grey, so the weather manipulation did not exist for the participant.
* **The PFD and MFD are live**, not textures: attitude, HDG, SPD, ALT, VS, and a
  heading-up moving map that shows the taxiway layout on the ground missions.
* **The aerodrome** — apron, taxiway centreline, hold-short bars, red mandatory
  "01" signs and yellow direction signs — is what the take-off missions navigate.
* **The cruise and climb rows are over open water** for much of the trial. That is a
  known consequence of an 8.6 km island and a 300 s mission; it is matched across the
  three missions of each row, so it is a constant rather than a confound. See
  `MISSION_IMPLEMENTATION.md` §5b.

The scripted pilot flying these captures is a crude autopilot, not a participant. Its
flying is not data.
