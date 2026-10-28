# The interface — how a participant moves through the 42 missions

Rebuilt 28 Oct 2026. Four screens, one path. Everything is `OnGUI`, laid out on
a 1280 x 760 canvas and scaled to the window (`UiTheme`), so it looks the same in
the editor, on a lab monitor and in the Windows build.

```
 Participant code ──► HUB ──► Briefing ──► (fly 300 s) ──► Results ──► next Briefing …
       ▲               │                                      │
       └── change ─────┤◄──────────────── hub ────────────────┘
                       ├── Free flight (nothing recorded)
                       ├── Lab session (EEG protocol, 12 trials + rest blocks)
                       ├── Control check
                       └── Help
```

## 1. Participant code (`ParticipantUI`)

Type a study code or click a returning participant in the list on the right.
The list is read from the data folder, with visits, missions done out of 42, and
how many were started but not usable. The line under the field says what the code
means before Continue is pressed: *Returning participant — visit 3, 17 of 42
missions done* or *New participant — visit 1*. Battery IDs (`TEST01` etc.) never
appear in the list. Enter = Continue.

## 2. The hub (`MenuUI`)

**Left — what to do.**
- **NEXT MISSION**: the first mission in this participant's order without a usable
  trial. Shows its position (*18 of 42*), class, phase, variant and name. READ
  BRIEFING (Enter) opens the briefing. When all 42 are done the card says so.
- **FREE FLIGHT** (F): practice; nothing is recorded; Esc comes back here.
- **LAB SESSION**: the EEG protocol. Opens a confirmation listing the four steps
  and this visit's twelve in order before anything starts.
- **CONTROL CHECK**, **HELP & CONTROLS**.
- **RECORDING STATUS**: audio profile, LSL (red/amber if the EEG clock is not
  shared), input hardware, VR, data folder.

**Right — the bank, four ways.**
- **MISSION GRID**: 4 phases x 3 classes, all three variants of every cell, each
  a button. A green edge = DONE, amber = STARTED (flown but not usable), a blue
  frame = NEXT.
- **CROSSWIND**: the six psychomotor-axis missions, kept apart on purpose.
- **MY ORDER**: all 42 numbered in this participant's sequence, with rules at
  12 / 24 / 36. Any row can be flown directly.
- **ATTEMPTS**: every trial folder on disk — visit, trial, rows recorded /
  expected, verdict (USABLE / incomplete / gap > 2 s / not closed), time.

**The order.** Fixed per participant code (`ParticipantProgress.FullOrder`):
block 0 is exactly the twelve `ExperimentSession.Begin` would give this visit
(balanced Latin square, de-clumped); blocks 1 and 2 rotate the variant index so
all 36 cognitive-axis missions appear once; the six crosswind missions come last.
Flying out of order is allowed; NEXT is simply the first not yet DONE.

## 3. Briefing

Opened by every mission button, never skipped. Objective, what will happen, what
counts as success, what ends the mission; at a glance: duration, in-task baseline,
start state, wind, attempts so far; the keys that matter during the mission.
*Show researcher notes* adds the load rationale, EEG relevance, expected errors,
aviation basis, approximations and the predicted load index — never shown during
a trial. START MISSION (Enter) or BACK (Esc).

## 4. Results (`ResultsUI`)

**Recording** — from `trial.json` via `ExperimentLogger.LastTrial`: rows
recorded / expected, flown to the end, continuous (worst gap), and the verdict in
one line: *This trial counts* or *Does not count — fly it again*. The folder path
is printed.

**Progress** — done / 42, and the NEXT card.

**Buttons** — HUB (Esc), FLY AGAIN (R), NEXT · <id> (Enter). NEXT is the
primary button only when this trial counted; after a crash or an abandoned run
FLY AGAIN is. Inside the lab session the page shows *trial n of 12* and a single
CONTINUE SESSION (Enter); Esc does nothing there so a stray key cannot skip a step.

Free flight ends with a plain *flight ended* page. The lab session ends with the
session report.

## What DONE means

A mission is DONE when at least one of its trial folders has `trial.json` with
`complete: true` and `continuous: true` — the same rule as `usable` in
`tools/build_dataset.py`. The hub and the exporter therefore always agree. A
crash, an Esc, or a 53-minute pause mid-trial all leave the data on disk, marked;
the mission stays STARTED until a usable trial exists.

## Dev tool

`UiDesignShots` (`-uishots`) photographs every screen by walking the real state
machine. Batch mode cannot render `OnGUI`, so run it in a player:

```bash
Unity -batchmode -nographics -quit -projectPath "$PWD" -executeMethod BuildTool.BuildMac
Builds/macOS/FlightAssessmentSim.app/Contents/MacOS/FlightAssessmentSim \
  -uishots -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```

Output: `<persistentDataPath>/UiDesign/*.png`.
