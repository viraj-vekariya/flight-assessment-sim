#!/usr/bin/env python3
"""
build_dataset.py — turn the simulator's trial folders into an analysis-ready dataset.

WHAT THIS SOLVES
    The sim writes one self-contained folder per trial. That is the right way to RECORD
    data (nothing can be half-written, nothing depends on anything else) and the wrong
    way to ANALYSE it: to ask "give me every HIGH trial from every participant" you would
    have to walk a thousand folders and hope none of them is a trial somebody abandoned
    after ninety seconds.

    This builds the index that makes the tree queryable, and — more importantly — it
    REFUSES TO LET A BROKEN TRIAL LOOK LIKE A GOOD ONE.

WHY AN INDEX AND NOT ONE BIG CSV
    40 participants x 42 missions x 15,000 rows x 82 columns is about 25 million rows,
    roughly 15 GB. Writing per-class, per-mission AND per-participant copies of that
    triples it to 45 GB of duplicated numbers that immediately drift apart. So the
    default output is a small index (one row per trial) that POINTS at the telemetry,
    plus everything small enough to be worth concatenating (events, QC, coverage).
    Use --materialise when you genuinely want the big per-class files on disk.

USAGE
    python3 tools/build_dataset.py                          # auto-locate, export
    python3 tools/build_dataset.py --root <dir> --out <dir>
    python3 tools/build_dataset.py --materialise LOW,HIGH   # also write big per-class CSVs
    python3 tools/build_dataset.py --include-synthetic      # keep TEST01 etc. (don't)

Stdlib only. No pandas, no Unity, runs on the analysis machine.
"""

import argparse, csv, json, os, sys, statistics
from collections import defaultdict

# Participant ids that are NOT people. The mission battery runs as TEST01 and writes
# folders that are identical in shape to a real participant's — 86 of them on the
# development machine alone. Pooling those into a dataset is not a hypothetical risk.
SYNTHETIC_PIDS = {"TEST01", "SHOTS", "DESIGN", "LEVEL", "CTEST", "UNKN"}

TELEM_HZ = 50.0
EXPECTED_COLS = 82

# A trial with a wall-clock hole bigger than this was PAUSED, not merely hitched. Measured:
# worst engine hitch over 34 clean battery trials 0.641 s; the real participant pause on
# disk 3197 s. Matches ExperimentLogger.DiscontinuityS.
DISCONTINUITY_S = 2.0


def find_root():
    """Locate the experiment root without being told, on macOS or Windows."""
    cands = [
        os.path.expanduser("~/Library/Application Support/DefaultCompany/"
                           "FlightAssessmentSimLevel/FlightSimData/experiment"),
        os.path.expanduser("~/AppData/LocalLow/DefaultCompany/"
                           "FlightAssessmentSimLevel/FlightSimData/experiment"),
    ]
    for c in cands:
        if os.path.isdir(c):
            return c
    return None


def read_json(path):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)
    except Exception:
        return {}


def scan_telemetry(path):
    """Stream a telemetry.csv once and return its quality facts.

    Measures the REALISED sample rate from t_host rather than trusting the configured
    50 Hz. A dropped frame, a garbage-collection pause or a participant alt-tabbing out
    all show up here as a gap, and a gap in the middle of a trial is exactly the thing
    that silently breaks EEG alignment later.
    """
    out = {"rows": 0, "cols": 0, "hz": "", "max_gap_ms": "", "gaps_over_100ms": "", "max_gap_s": 0.0,
           "t_first": "", "t_last": "", "nonfinite": 0}
    if not os.path.exists(path):
        return out
    prev = None
    gaps = []
    maxgap = 0.0
    n = 0
    first = last = None
    nonfinite = 0
    with open(path, newline="", encoding="utf-8-sig") as f:
        rdr = csv.reader(f)
        try:
            header = next(rdr)
        except StopIteration:
            return out
        out["cols"] = len(header)
        try:
            ih = header.index("t_host")
        except ValueError:
            ih = 2
        for row in rdr:
            if not row:
                continue
            n += 1
            try:
                t = float(row[ih])
            except (ValueError, IndexError):
                nonfinite += 1
                continue
            if first is None:
                first = t
            last = t
            if prev is not None:
                d = t - prev
                if d > maxgap:
                    maxgap = d
                if d > 0.100:
                    gaps.append(d)
            prev = t
    out["rows"] = n
    out["nonfinite"] = nonfinite
    out["t_first"] = round(first, 6) if first is not None else ""
    out["t_last"] = round(last, 6) if last is not None else ""
    if first is not None and last is not None and n > 1 and last > first:
        out["hz"] = round((n - 1) / (last - first), 3)
    out["max_gap_ms"] = round(maxgap * 1000.0, 1)
    out["max_gap_s"] = round(maxgap, 3)
    out["gaps_over_100ms"] = len(gaps)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=None, help="experiment root (auto-located if omitted)")
    ap.add_argument("--out", default=None, help="output dir (default: <root>/../export)")
    ap.add_argument("--materialise", default="", help="comma list of LOW,MEDIUM,HIGH to concatenate")
    ap.add_argument("--include-synthetic", action="store_true",
                    help="keep TEST01 and other harness ids (they are not people)")
    ap.add_argument("--include-incomplete", action="store_true",
                    help="put incomplete trials in the materialised files too")
    a = ap.parse_args()

    root = a.root or find_root()
    if not root or not os.path.isdir(root):
        sys.exit("experiment root not found; pass --root")
    out = a.out or os.path.join(os.path.dirname(root.rstrip("/")), "export")
    os.makedirs(out, exist_ok=True)

    problems = []
    rows = []
    all_missions = set()

    pids = sorted(d for d in os.listdir(root) if os.path.isdir(os.path.join(root, d)))
    for pid in pids:
        synthetic = pid.upper() in SYNTHETIC_PIDS
        if synthetic and not a.include_synthetic:
            problems.append(f"SKIPPED {pid}: harness/synthetic id, not a participant "
                            f"(--include-synthetic to override)")
            continue
        pdir = os.path.join(root, pid)
        sessions = sorted(d for d in os.listdir(pdir) if os.path.isdir(os.path.join(pdir, d)))

        # A participant who quits mid-way never used to bump the session counter, so two
        # separate visits could both be written as S01. Report it rather than silently
        # de-duplicating: which trial belongs to which visit is the analyst's call.
        stems = defaultdict(list)
        for s in sessions:
            stems[s.split("_")[0]].append(s)
        for stem, group in stems.items():
            if len(group) > 1:
                problems.append(f"{pid}: {len(group)} session folders share the number "
                                f"'{stem}' ({', '.join(group)}) — visit numbering collided; "
                                f"use session_id (with its timestamp) as the key, not visit.")

        for s in sessions:
            sdir = os.path.join(pdir, s)
            sess_meta = read_json(os.path.join(sdir, "session.json"))
            trials = sorted(d for d in os.listdir(sdir) if os.path.isdir(os.path.join(sdir, d)))
            for t in trials:
                tdir = os.path.join(sdir, t)
                meta = read_json(os.path.join(tdir, "metadata.json"))
                trial = read_json(os.path.join(tdir, "trial.json"))
                perf = read_json(os.path.join(tdir, "performance.json"))
                tel = os.path.join(tdir, "telemetry.csv")
                ev = os.path.join(tdir, "events.csv")
                q = scan_telemetry(tel)

                mid = meta.get("mission_id") or (t.split("_", 1)[1] if "_" in t else "")
                all_missions.add(mid)
                dur = float(meta.get("duration_s") or 0)
                expected = int(round(dur * TELEM_HZ))

                # trial.json is authoritative when present. Older folders predate it, so
                # fall back to the row count — stated, not hidden, in complete_source.
                if "complete" in trial:
                    complete = bool(trial["complete"])
                    csrc = "trial.json"
                else:
                    complete = expected > 0 and q["rows"] >= round(expected * 0.99)
                    csrc = "inferred_from_rows"

                # Continuity is recomputed from the telemetry unless trial.json was written
                # under the SAME threshold we are applying now. An early build logged any
                # gap over 0.5 s as a discontinuity, which marked clean trials (worst real
                # engine hitch: 0.638 s) unusable. Trusting a flag written under a
                # different rule would bake that mistake into the dataset permanently.
                thr = trial.get("discontinuity_threshold_s")
                if thr is not None and float(thr) == DISCONTINUITY_S:
                    cont = bool(trial.get("continuous", True))
                else:
                    cont = q["max_gap_s"] < DISCONTINUITY_S
                    if thr is not None:
                        problems.append(f"{pid}/{s}/{t}: trial.json used a "
                                        f"{thr}s discontinuity threshold; recomputed at "
                                        f"{DISCONTINUITY_S}s from telemetry")

                if not complete:
                    problems.append(f"{pid}/{s}/{t}: INCOMPLETE — {q['rows']} of {expected} "
                                    f"rows ({100.0*q['rows']/expected if expected else 0:.0f}%)")
                if q["cols"] and q["cols"] != EXPECTED_COLS:
                    problems.append(f"{pid}/{s}/{t}: {q['cols']} telemetry columns, expected {EXPECTED_COLS}")
                if q["max_gap_s"] >= DISCONTINUITY_S:
                    problems.append(
                        f"{pid}/{s}/{t}: {q['gaps_over_100ms']} wall-clock gap(s), "
                        f"worst {q['max_gap_ms']/1000.0:.1f} s. The app was paused or "
                        f"backgrounded mid-trial. t_mission does NOT show this — do not "
                        f"align EEG to this trial on t_mission.")
                if q["nonfinite"]:
                    problems.append(f"{pid}/{s}/{t}: {q['nonfinite']} unparseable telemetry row(s)")
                if not os.path.exists(ev):
                    problems.append(f"{pid}/{s}/{t}: events.csv missing")
                if not meta:
                    problems.append(f"{pid}/{s}/{t}: metadata.json missing — condition label unknown")

                rows.append({
                    "participant_id": pid,
                    "session_id": s,
                    "visit": s.split("_")[0],
                    "trial_ordinal": meta.get("trial_ordinal") or trial.get("trial_ordinal") or "",
                    "mission_id": mid,
                    "condition": meta.get("condition") or trial.get("condition") or "",
                    "axis": trial.get("axis") or meta.get("axis") or "Cognitive",
                    "flight_phase": meta.get("flight_phase") or trial.get("flight_phase") or "",
                    "variant": trial.get("variant") or "",
                    "duration_s": int(dur) if dur else "",
                    "in_task_baseline_s": meta.get("in_task_baseline_s") or "",
                    "predicted_load_index": meta.get("predicted_load_index") or "",
                    "outcome": perf.get("outcome") or "",
                    "started_utc": trial.get("started_utc") or meta.get("started_utc") or "",
                    "ended_utc": trial.get("ended_utc") or "",
                    "telemetry_rows": q["rows"],
                    "telemetry_rows_expected": expected,
                    "telemetry_cols": q["cols"],
                    "complete": "true" if complete else "false",
                    "complete_source": csrc,
                    "continuous": "true" if cont else "false",
                    "recording_gaps": trial.get("recording_gaps", q["gaps_over_100ms"]),
                    "paused_total_s": trial.get("paused_total_s", ""),
                    "usable": "true" if (complete and cont) else "false",
                    "measured_hz": q["hz"],
                    "max_gap_ms": q["max_gap_ms"],
                    "max_gap_s": q["max_gap_s"],
                    "gaps_over_100ms": q["gaps_over_100ms"],
                    "t_host_first": q["t_first"],
                    "t_host_last": q["t_last"],
                    "input_modality": sess_meta.get("input_modality") or "",
                    "lsl_available": sess_meta.get("lsl_available") or "",
                    "telemetry_path": os.path.relpath(tel, root),
                    "events_path": os.path.relpath(ev, root) if os.path.exists(ev) else "",
                    "eeg_dir": os.path.relpath(os.path.join(tdir, "eeg"), root),
                })

    if not rows:
        sys.exit("no participant trials found (all ids were synthetic?) — "
                 "run with --include-synthetic to inspect the harness data")

    # ---- index.csv : one row per trial, the spine of everything ----------------
    idx_path = os.path.join(out, "index.csv")
    with open(idx_path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    # ---- participants.csv : who has done what ---------------------------------
    per = defaultdict(lambda: {"trials": 0, "complete": 0, "incomplete": 0,
                               "LOW": 0, "MEDIUM": 0, "HIGH": 0, "missions": set(),
                               "visits": set(), "first": "", "last": ""})
    for r in rows:
        p = per[r["participant_id"]]
        p["trials"] += 1
        p["complete" if r["complete"] == "true" else "incomplete"] += 1
        if r["usable"] == "true":
            if r["condition"] in p:
                p[r["condition"]] += 1
            p["missions"].add(r["mission_id"])
        p["visits"].add(r["session_id"])
        st = r["started_utc"]
        if st:
            p["first"] = min(p["first"], st) if p["first"] else st
            p["last"] = max(p["last"], st) if p["last"] else st

    with open(os.path.join(out, "participants.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["participant_id", "visits", "trials_attempted", "trials_complete",
                    "trials_incomplete", "distinct_missions_complete", "LOW", "MEDIUM",
                    "HIGH", "first_seen_utc", "last_seen_utc", "missions_complete"])
        for pid in sorted(per):
            p = per[pid]
            w.writerow([pid, len(p["visits"]), p["trials"], p["complete"], p["incomplete"],
                        len(p["missions"]), p["LOW"], p["MEDIUM"], p["HIGH"],
                        p["first"], p["last"], " ".join(sorted(p["missions"]))])

    # ---- coverage.csv : participant x mission, the design grid as it stands ----
    missions = sorted(m for m in all_missions if m)
    with open(os.path.join(out, "coverage.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["participant_id"] + missions)
        for pid in sorted(per):
            done = per[pid]["missions"]
            w.writerow([pid] + [(1 if m in done else 0) for m in missions])

    # ---- events_all.csv : small enough to concatenate, and the EEG epochs live here
    ev_out = os.path.join(out, "events_all.csv")
    n_ev = 0
    with open(ev_out, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        head = None
        for r in rows:
            if not r["events_path"]:
                continue
            with open(os.path.join(root, r["events_path"]), newline="", encoding="utf-8-sig") as g:
                rd = csv.reader(g)
                try:
                    h = next(rd)
                except StopIteration:
                    continue
                if head is None:
                    head = ["participant_id", "session_id", "mission_id", "condition"] + h
                    w.writerow(head)
                for row in rd:
                    if row:
                        w.writerow([r["participant_id"], r["session_id"],
                                    r["mission_id"], r["condition"]] + row)
                        n_ev += 1

    # ---- optional: the big per-class files ------------------------------------
    mat = [c.strip().upper() for c in a.materialise.split(",") if c.strip()]
    materialised = {}
    for cls in mat:
        sel = [r for r in rows if r["condition"] == cls
               and (a.include_incomplete or r["usable"] == "true")]
        path = os.path.join(out, f"telemetry_{cls}.csv")
        written = 0
        with open(path, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            head = None
            for r in sel:
                with open(os.path.join(root, r["telemetry_path"]), newline="",
                          encoding="utf-8-sig") as g:
                    rd = csv.reader(g)
                    h = next(rd)
                    if head is None:
                        head = ["participant_id", "session_id", "mission_id",
                                "condition", "flight_phase", "trial_ordinal"] + h
                        w.writerow(head)
                    pre = [r["participant_id"], r["session_id"], r["mission_id"],
                           r["condition"], r["flight_phase"], r["trial_ordinal"]]
                    for row in rd:
                        if row:
                            w.writerow(pre + row)
                            written += 1
        materialised[cls] = {"trials": len(sel), "rows": written, "file": os.path.basename(path)}

    # ---- VALIDATION.txt : the part that stops bad data reaching the model ------
    ncomp = sum(1 for r in rows if r["complete"] == "true")
    nusable = sum(1 for r in rows if r["usable"] == "true")
    hz = [float(r["measured_hz"]) for r in rows if r["measured_hz"] != ""]
    with open(os.path.join(out, "VALIDATION.txt"), "w", encoding="utf-8") as f:
        f.write("DATASET VALIDATION\n==================\n")
        f.write(f"source     : {root}\n")
        f.write(f"participants: {len(per)}\n")
        f.write(f"trials      : {len(rows)}  ({ncomp} complete, {len(rows)-ncomp} incomplete)\n")
        f.write(f"USABLE      : {nusable}   (complete AND continuous — no wall-clock gap)\n")
        if hz:
            f.write(f"measured Hz : min {min(hz):.2f}  median {statistics.median(hz):.2f}  max {max(hz):.2f}  (nominal {TELEM_HZ})\n")
        f.write(f"events rows : {n_ev}\n\n")
        f.write("FILTER ON usable == 'true'.\n")
        f.write("  complete   = the participant flew it to the end.\n")
        f.write("  continuous = no hole in wall-clock time inside the trial.\n")
        f.write("A trial can be complete and still unusable: if the game was paused mid-\n")
        f.write("trial, t_mission runs smoothly across the hole and only t_host records it.\n")
        f.write("EEG aligned to such a trial is matched to the wrong minutes of brain data.\n\n")
        f.write(f"PROBLEMS ({len(problems)})\n" + "-" * 40 + "\n")
        for p in problems:
            f.write("  " + p + "\n")
        if not problems:
            f.write("  (none)\n")

    with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({
            "source_root": root, "participants": len(per),
            "trials_total": len(rows), "trials_complete": ncomp,
            "events_rows": n_ev, "telemetry_hz_nominal": TELEM_HZ,
            "telemetry_columns_expected": EXPECTED_COLS,
            "trials_usable": nusable,
            "materialised": materialised, "problems": len(problems),
            "join_keys": ["participant_id", "session_id", "mission_id", "trial_ordinal"],
            "time_master": "t_host",
            "eeg_join": "align EEG on t_host using each trial's eeg/sync.json; epoch zero "
                        "for a failure trial is the CUE_ONSET marker in events.csv, not "
                        "TRIGGER_ARMED",
        }, f, indent=2)

    print(f"participants {len(per)}   trials {len(rows)}   "
          f"complete {ncomp}   USABLE {nusable}   problems {len(problems)}")
    for k, v in materialised.items():
        print(f"  materialised {k}: {v['trials']} trials, {v['rows']} rows -> {v['file']}")
    print(f"-> {out}")
    print("   index.csv  participants.csv  coverage.csv  events_all.csv  VALIDATION.txt  manifest.json")


if __name__ == "__main__":
    main()
