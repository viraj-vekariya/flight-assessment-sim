#!/usr/bin/env python3
"""
build_dataset.py — turn a folder of recorded sessions into an ML-ready table, with
the grouping information needed to split it WITHOUT leakage.

    python3 build_dataset.py <experiment_root> [-o out_dir]

    <experiment_root> is .../FlightSimData/experiment

WHAT IT DOES
    * walks every participant / session / trial
    * reads metadata.json, nasa_tlx.json, performance.json, events.csv
    * derives behavioural workload measures from the markers (response latencies,
      miss rates, checklist item times, first-response latency after the cue)
    * derives control-activity measures from telemetry.csv, split by segment, so the
      motor-artifact confound can be regressed out later
    * writes trials.csv (one row per trial) and windows_index.csv (one row per
      analysis window, WITH the group keys)

WHAT IT DOES NOT DO
    * it does not touch EEG. There is no EEG in this repository. windows_index.csv
      gives you the time bounds and the labels; you join your own EEG features onto
      it by (participant_id, session_id, trial_ordinal, t_mission_start).

THE POINT OF windows_index.csv
    It carries `participant_id`, `session_id` and `mission_id` on every row so that
    a split can be made by GROUP. Splitting 2-second EEG windows at random is the
    single most common way these studies overstate their accuracy: adjacent windows
    share raw samples, and windows from one person share that person's anatomy, so a
    random split lets the model identify the participant instead of the workload.
    Use `sklearn.model_selection.LeaveOneGroupOut` with groups=participant_id.
    `demo_split()` below shows it and prints a warning if you ask for anything else.
"""

import argparse
import csv
import json
import math
import os
import sys
from collections import defaultdict

WINDOW_S = 2.0
WINDOW_STEP_S = 1.0          # 50% overlap — legal WITHIN a fold, never across one
TEST_PARTICIPANT_CODE = "TEST01"   # written by the automated harness; synthetic


# ──────────────────────────────────────────────────────────────────────────────
# reading
# ──────────────────────────────────────────────────────────────────────────────

def read_json(path):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)
    except Exception:
        return None


def read_csv_rows(path):
    try:
        with open(path, encoding="utf-8-sig", newline="") as f:
            return list(csv.DictReader(f))
    except Exception:
        return []


def find_trials(root):
    """Yield (participant, session, trial_dir) for every trial folder."""
    if not os.path.isdir(root):
        sys.exit(f"not a directory: {root}")
    for pid in sorted(os.listdir(root)):
        pdir = os.path.join(root, pid)
        if not os.path.isdir(pdir):
            continue
        for sess in sorted(os.listdir(pdir)):
            sdir = os.path.join(pdir, sess)
            if not os.path.isdir(sdir):
                continue
            for trial in sorted(os.listdir(sdir)):
                tdir = os.path.join(sdir, trial)
                if os.path.isdir(tdir) and os.path.exists(os.path.join(tdir, "events.csv")):
                    yield pid, sess, tdir


# ──────────────────────────────────────────────────────────────────────────────
# derived measures
# ──────────────────────────────────────────────────────────────────────────────

def behaviour_from_events(events):
    """Behavioural workload measures that need no EEG and no self-report."""
    out = {
        "probes_presented": 0, "probes_hit": 0, "probes_missed": 0,
        "probe_rt_mean_ms": "", "probe_rt_sd_ms": "",
        "decisions_presented": 0, "decisions_made": 0, "decisions_expired": 0,
        "readbacks_ok": 0, "readbacks_missed": 0,
        "checklist_items": 0, "checklist_timeouts": 0, "checklist_item_mean_s": "",
        "cue_onset_s": "", "trigger_armed_s": "", "cue_lag_s": "",
        "first_response_latency_s": "", "failure_resolved_s": "",
        "config_changes": 0, "go_around_latency_s": "",
        "outcome_marker": "",
    }
    rts, item_dts = [], []
    for e in events:
        m = e.get("marker", "")
        d = e.get("detail", "") or ""
        try:
            t = float(e.get("t_mission", "nan"))
        except ValueError:
            t = float("nan")

        if m == "PROBE_ONSET" or m == "TRAFFIC_ONSET":
            out["probes_presented"] += 1
        elif m == "PROBE_HIT":
            out["probes_hit"] += 1
            for part in d.split(";"):
                if part.startswith("rt_ms="):
                    rts.append(float(part[6:]))
        elif m == "PROBE_MISS":
            out["probes_missed"] += 1
        elif m == "DECISION_PROMPT":
            out["decisions_presented"] += 1
        elif m == "DECISION_MADE":
            out["decisions_made"] += 1
            for part in d.split(";"):
                if part.startswith("rt_ms="):
                    rts.append(float(part[6:]))
        elif m == "DECISION_EXPIRED":
            out["decisions_expired"] += 1
        elif m == "ATC_READBACK_OK":
            out["readbacks_ok"] += 1
        elif m == "ATC_READBACK_MISS":
            out["readbacks_missed"] += 1
        elif m == "CHECKLIST_ITEM":
            out["checklist_items"] += 1
            for part in d.split("|"):
                if part.startswith("dt="):
                    item_dts.append(float(part[3:]))
        elif m == "CHECKLIST_TIMEOUT":
            out["checklist_timeouts"] += 1
        elif m == "TRIGGER_ARMED":
            out["trigger_armed_s"] = t
        elif m == "CUE_ONSET":
            out["cue_onset_s"] = t
        elif m == "PILOT_FIRST_RESPONSE":
            for part in d.split("="):
                pass
            if d.startswith("latency_s="):
                out["first_response_latency_s"] = float(d.split("=")[1])
        elif m == "FAILURE_RESOLVED":
            out["failure_resolved_s"] = t
        elif m == "CONFIGURATION_CHANGE":
            out["config_changes"] += 1
        elif m == "GO_AROUND_INITIATED":
            if d.startswith("latency_s="):
                out["go_around_latency_s"] = float(d.split("=")[1])
        elif m in ("MISSION_SUCCESS", "MISSION_FAILURE"):
            out["outcome_marker"] = m

    if rts:
        mean = sum(rts) / len(rts)
        out["probe_rt_mean_ms"] = round(mean, 1)
        if len(rts) > 1:
            var = sum((r - mean) ** 2 for r in rts) / (len(rts) - 1)
            out["probe_rt_sd_ms"] = round(math.sqrt(var), 1)
    if item_dts:
        out["checklist_item_mean_s"] = round(sum(item_dts) / len(item_dts), 2)
    if out["cue_onset_s"] != "" and out["trigger_armed_s"] != "":
        out["cue_lag_s"] = round(out["cue_onset_s"] - out["trigger_armed_s"], 2)
    return out


def control_activity(telemetry_path):
    """Control-activity and tracking measures per segment.

    ctrl_jerk is the motor-artifact regressor: if an EEG class effect disappears
    once this is controlled for, the effect was movement, not workload.
    """
    per_seg = defaultdict(lambda: {"n": 0, "jerk": 0.0, "alt_err2": 0.0, "hdg_err2": 0.0,
                                   "thr": 0.0, "ias": 0.0})
    try:
        with open(telemetry_path, encoding="utf-8-sig", newline="") as f:
            for row in csv.DictReader(f):
                seg = row.get("segment", "?")
                a = per_seg[seg]
                a["n"] += 1
                for key, col in (("jerk", "ctrl_jerk"), ("thr", "throttle"), ("ias", "airspeed_kmh")):
                    try:
                        a[key] += float(row.get(col, 0) or 0)
                    except ValueError:
                        pass
                for key, col in (("alt_err2", "alt_err_m"), ("hdg_err2", "hdg_err_deg")):
                    try:
                        v = float(row.get(col, 0) or 0)
                        a[key] += v * v
                    except ValueError:
                        pass
    except FileNotFoundError:
        return {}

    out = {}
    for seg, a in per_seg.items():
        n = max(1, a["n"])
        tag = seg.lower()
        out[f"{tag}_samples"] = a["n"]
        out[f"{tag}_ctrl_jerk_mean"] = round(a["jerk"] / n, 4)
        out[f"{tag}_rms_alt_err_m"] = round(math.sqrt(a["alt_err2"] / n), 2)
        out[f"{tag}_rms_hdg_err_deg"] = round(math.sqrt(a["hdg_err2"] / n), 2)
        out[f"{tag}_throttle_mean"] = round(a["thr"] / n, 3)
        out[f"{tag}_ias_mean_kmh"] = round(a["ias"] / n, 1)
    return out


def segment_bounds(events):
    """(baseline_start, baseline_end, task_end) in mission seconds."""
    bs = be = te = None
    for e in events:
        try:
            t = float(e["t_mission"])
        except (KeyError, ValueError):
            continue
        m = e.get("marker", "")
        if m == "BASELINE_START" and bs is None:
            bs = t
        elif m == "BASELINE_END" and be is None:
            be = t
        elif m == "MISSION_END":
            te = t
    return bs, be, te


# ──────────────────────────────────────────────────────────────────────────────
# main
# ──────────────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("root", help="path to FlightSimData/experiment")
    ap.add_argument("-o", "--out", default=".", help="output directory")
    ap.add_argument("--include-test-participant", action="store_true",
                    help="include the TEST01 harness folder (synthetic — do not do this)")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    trial_rows, window_rows, skipped = [], [], []

    for pid, sess, tdir in find_trials(args.root):
        if pid.upper() == TEST_PARTICIPANT_CODE and not args.include_test_participant:
            skipped.append(tdir)
            continue

        meta = read_json(os.path.join(tdir, "metadata.json"))
        if not meta:
            continue
        tlx = read_json(os.path.join(tdir, "nasa_tlx.json"))
        perf = read_json(os.path.join(tdir, "performance.json"))
        events = read_csv_rows(os.path.join(tdir, "events.csv"))

        row = {
            "participant_id": meta.get("participant_id", pid),
            "session_id": meta.get("session_id", sess),
            "trial_ordinal": meta.get("trial_ordinal", ""),
            "mission_id": meta.get("mission_id", ""),
            "condition": meta.get("condition", ""),
            "flight_phase": meta.get("flight_phase", ""),
            "predicted_load_index": meta.get("predicted_load_index", ""),
            "random_seed": meta.get("random_seed", ""),
            "duration_s": meta.get("duration_s", ""),
            "in_task_baseline_s": meta.get("in_task_baseline_s", ""),
            "trial_dir": os.path.relpath(tdir, args.root),
        }
        # measured self-report — note this is the RAW TLX (RTLX)
        if tlx and "nasa_tlx" in tlx:
            for k, v in tlx["nasa_tlx"].items():
                row["tlx_" + k] = v
            row["bedford"] = tlx.get("bedford", "")
        # objective outcome
        if perf:
            row["outcome"] = perf.get("outcome", "")
            for k, v in (perf.get("metrics") or {}).items():
                row["perf_" + k] = v

        row.update(behaviour_from_events(events))
        row.update(control_activity(os.path.join(tdir, "telemetry.csv")))

        # EEG presence — reported, never assumed
        eeg_dir = os.path.join(tdir, "eeg")
        eeg_files = [f for f in os.listdir(eeg_dir)
                     if os.path.isfile(os.path.join(eeg_dir, f))
                     and f not in ("sync.json", "README.txt")] if os.path.isdir(eeg_dir) else []
        row["eeg_files"] = ";".join(sorted(eeg_files))
        row["has_eeg"] = int(bool(eeg_files))

        trial_rows.append(row)

        # window index
        bs, be, te = segment_bounds(events)
        if bs is None:
            bs = 0.0
        if te is None:
            try:
                te = float(meta.get("duration_s", 300))
            except (TypeError, ValueError):
                te = 300.0
        t = bs
        wi = 0
        while t + WINDOW_S <= te:
            seg = "BASELINE" if (be is not None and t + WINDOW_S <= be) else "TASK"
            window_rows.append({
                "participant_id": row["participant_id"],
                "session_id": row["session_id"],
                "trial_ordinal": row["trial_ordinal"],
                "mission_id": row["mission_id"],
                "condition": row["condition"],
                "window_index": wi,
                "t_mission_start": round(t, 3),
                "t_mission_end": round(t + WINDOW_S, 3),
                "segment": seg,
                "cue_onset_s": row.get("cue_onset_s", ""),
                "t_rel_to_cue_s": (round(t - row["cue_onset_s"], 3)
                                   if isinstance(row.get("cue_onset_s"), float) else ""),
            })
            t += WINDOW_STEP_S
            wi += 1

    write_csv(os.path.join(args.out, "trials.csv"), trial_rows)
    write_csv(os.path.join(args.out, "windows_index.csv"), window_rows)

    parts = sorted({r["participant_id"] for r in trial_rows})
    print(f"trials  : {len(trial_rows)} from {len(parts)} participants {parts}")
    print(f"windows : {len(window_rows)}  ({WINDOW_S}s, step {WINDOW_STEP_S}s)")
    if skipped:
        print(f"skipped : {len(skipped)} {TEST_PARTICIPANT_CODE} trial folders "
              f"(synthetic harness output — never analyse these)")
    by_cond = defaultdict(int)
    for r in trial_rows:
        by_cond[r["condition"]] += 1
    print("per condition:", dict(by_cond))
    if trial_rows and not any(r["has_eeg"] for r in trial_rows):
        print("\nNOTE: no EEG files found in any trial's eeg/ folder. windows_index.csv "
              "gives you the labelled time bounds; join your own EEG features onto it "
              "by (participant_id, session_id, trial_ordinal, t_mission_start).")
    demo_split(parts)


def write_csv(path, rows):
    if not rows:
        open(path, "w").close()
        return
    fields = []
    for r in rows:
        for k in r:
            if k not in fields:
                fields.append(k)
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        for r in rows:
            w.writerow(r)
    print("wrote", path)


def demo_split(participants):
    """Print the only split that is defensible for a subject-independent claim."""
    print("""
────────────────────────────────────────────────────────────────────────────
HOW TO SPLIT THIS WITHOUT FOOLING YOURSELF

  Subject-independent (the claim "this works on a NEW pilot"):

      from sklearn.model_selection import LeaveOneGroupOut
      logo = LeaveOneGroupOut()
      for tr, te in logo.split(X, y, groups=df.participant_id):
          ...                       # fit scaler INSIDE the fold, never before

  Within-subject (a different, weaker claim — say which one you are making):
      hold out whole TRIALS, grouped by trial_ordinal. Never split windows from
      one trial across train and test: consecutive windows overlap by 50% and
      share raw samples.

  Never:
      train_test_split(X, y, shuffle=True)          # <- leaks. Always.

  Report: chance level (33% for three balanced classes), the confusion matrix
  (the interesting failure is MEDIUM being absorbed into LOW and HIGH), the
  per-fold scores, and a linear baseline before any deep model.
────────────────────────────────────────────────────────────────────────────""")
    if len(participants) < 5:
        print(f"WARNING: only {len(participants)} participant(s) present. "
              "Leave-one-subject-out with this many folds gives confidence intervals "
              "so wide that a single accuracy number is not meaningful. Report the "
              "per-fold values, or collect more participants.\n")


if __name__ == "__main__":
    main()
