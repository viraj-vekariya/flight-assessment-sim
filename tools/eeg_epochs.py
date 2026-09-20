#!/usr/bin/env python3
"""
eeg_epochs.py — emit the epoch table you slice your EEG with.

WHY THIS IS SEPARATE FROM YOUR AMPLIFIER
    Every EEG system stores data differently (EDF, BDF, .fif, XDF, a vendor CSV). Rather
    than guess yours, this writes the one thing all of them need: for every event in every
    USABLE trial, the moment it happened expressed in all four clocks at once. Whatever
    reads your recording can then slice on whichever clock it shares with the amplifier.

THE CLOCK RULES
    t_host   monotonic, never jumps, THE MASTER. Use this one.
    t_unix   UTC wall clock. Use only to align across two machines.
    t_lsl    LabStreamingLayer. Use this if you recorded EEG through LSL — it is the
             cleanest path, because both streams then share one clock. -1 means LSL
             was not running, which is currently the case in every session on disk.
    t_mission seconds since MISSION_START. CONVENIENT FOR PLOTS, NEVER FOR ALIGNMENT.
             It advances per frame, so if the game is paused it simply does not tick and
             the pause leaves no trace in it. Trial P002/L3V3 hides a 53-minute hole this
             way. build_dataset.py flags those trials as usable == false; this tool skips
             them unless you pass --include-unusable.

EPOCH ZERO
    For a failure mission, epoch zero is CUE_ONSET — the first moment the pilot could
    perceive the failure. TRIGGER_ARMED is when the simulator scheduled it, which can be
    seconds earlier and is not a perceptual event. Epoching on TRIGGER_ARMED gives you
    pre-stimulus data labelled as post-stimulus.

USAGE
    python3 tools/build_dataset.py            # first: build index.csv
    python3 tools/eeg_epochs.py               # then: epochs.csv beside it
    python3 tools/eeg_epochs.py --markers CUE_ONSET,PROBE_ONSET --pre 2 --post 8
"""

import argparse, csv, json, os, sys

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--export", default=None, help="export dir from build_dataset.py")
    ap.add_argument("--markers", default="", help="comma list; default = all markers")
    ap.add_argument("--pre", type=float, default=2.0, help="seconds before the marker")
    ap.add_argument("--post", type=float, default=8.0, help="seconds after the marker")
    ap.add_argument("--include-unusable", action="store_true",
                    help="also epoch trials with a recording gap or a short run (don't)")
    a = ap.parse_args()

    exp = a.export
    if not exp:
        for c in [os.path.expanduser("~/Library/Application Support/DefaultCompany/"
                                     "FlightAssessmentSimLevel/FlightSimData/export"),
                  os.path.expanduser("~/AppData/LocalLow/DefaultCompany/"
                                     "FlightAssessmentSimLevel/FlightSimData/export")]:
            if os.path.isdir(c):
                exp = c
                break
    if not exp or not os.path.isdir(exp):
        sys.exit("export dir not found — run tools/build_dataset.py first, or pass --export")

    idx_path = os.path.join(exp, "index.csv")
    if not os.path.exists(idx_path):
        sys.exit(f"{idx_path} missing — run tools/build_dataset.py first")

    with open(idx_path, newline="", encoding="utf-8") as f:
        trials = list(csv.DictReader(f))
    manifest = {}
    mpath = os.path.join(exp, "manifest.json")
    if os.path.exists(mpath):
        manifest = json.load(open(mpath, encoding="utf-8"))
    root = manifest.get("source_root", "")

    want = {m.strip().upper() for m in a.markers.split(",") if m.strip()}
    keep = [t for t in trials if a.include_unusable or t.get("usable") == "true"]
    skipped = len(trials) - len(keep)

    out_rows = []
    no_sync = 0
    for t in keep:
        tdir = os.path.join(root, os.path.dirname(t["telemetry_path"]))
        sync = {}
        sp = os.path.join(tdir, "eeg", "sync.json")
        if os.path.exists(sp):
            try:
                sync = json.load(open(sp, encoding="utf-8"))
            except Exception:
                pass
        if not sync:
            no_sync += 1
        evp = os.path.join(root, t["events_path"]) if t["events_path"] else None
        if not evp or not os.path.exists(evp):
            continue
        with open(evp, newline="", encoding="utf-8-sig") as f:
            for e in csv.DictReader(f):
                mk = (e.get("marker") or "").strip()
                if not mk or (want and mk.upper() not in want):
                    continue
                try:
                    th = float(e.get("t_host", "nan"))
                except ValueError:
                    continue
                def fl(k):
                    try:
                        return float(e.get(k, ""))
                    except (TypeError, ValueError):
                        return ""
                out_rows.append({
                    "participant_id": t["participant_id"],
                    "session_id": t["session_id"],
                    "mission_id": t["mission_id"],
                    "condition": t["condition"],
                    "axis": t["axis"],
                    "flight_phase": t["flight_phase"],
                    "trial_ordinal": t["trial_ordinal"],
                    "marker": mk,
                    "detail": e.get("detail", ""),
                    "t_mission": fl("t_mission"),
                    "t_host": th,
                    "t_unix": fl("t_unix"),
                    "t_lsl": fl("t_lsl"),
                    "epoch_start_t_host": round(th - a.pre, 6),
                    "epoch_end_t_host": round(th + a.post, 6),
                    "trial_start_host_s": sync.get("trial_start_host_s", ""),
                    "eeg_dir": t["eeg_dir"],
                })

    outp = os.path.join(exp, "epochs.csv")
    if not out_rows:
        sys.exit("no epochs produced — every trial was unusable, or no marker matched")
    with open(outp, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(out_rows[0].keys()))
        w.writeheader()
        w.writerows(out_rows)

    print(f"epochs {len(out_rows)} from {len(keep)} usable trial(s); "
          f"{skipped} unusable trial(s) skipped")
    if no_sync:
        print(f"WARNING: {no_sync} trial(s) had no eeg/sync.json")
    lsl = sum(1 for r in out_rows if r["t_lsl"] not in ("", -1, -1.0))
    if lsl == 0:
        print("WARNING: t_lsl is absent in every row — LSL was not running, so EEG must be "
              "aligned on t_host/t_unix and the offset measured by hand. Fix this before "
              "collecting: LSL is the only path that shares one clock with the amplifier.")
    print(f"-> {outp}")


if __name__ == "__main__":
    main()
