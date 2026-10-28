using UnityEngine;

/// <summary>
/// The page after a flight. For a mission it answers three questions in order:
/// did the trial count (complete and continuous, from trial.json), where am I
/// (n of 42, and what is next), and what do I press. For free flight it just says
/// the flight ended. The session report at the end of the lab protocol is here too.
/// </summary>
public class ResultsUI : MonoBehaviour
{
    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Results) return;

        UiTheme.DimScreen(0.80f);
        UiTheme.Begin();
        if (gm.SessionReportActive) DrawSessionReport(gm);
        else if (gm.ScenarioActive && gm.ScenarioRunner != null && gm.ScenarioRunner.Result != null) DrawMission(gm, gm.ScenarioRunner.Result);
        else DrawFreeFlight(gm);
        UiTheme.End();
    }

    // ── a mission ───────────────────────────────────────────────────────────────
    void DrawMission(GameManager gm, ScenarioResult res)
    {
        var m = gm.ScenarioRunner.Mission;
        var trial = ExperimentLogger.LastTrial;
        bool crashed = res.Outcome == "CRASHED" || gm.Crashed;
        bool usable = trial != null && trial.Usable;
        var col = m != null ? UiTheme.ClassColor(m.Class) : UiTheme.Accent;

        float pw = 980f, ph = 600f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 5f), crashed ? UiTheme.Bad : usable ? UiTheme.Good : UiTheme.Warn);
        float x = p.x + 30f, y = p.y + 22f, w = pw - 60f;

        string head = crashed ? "FLIGHT ENDED — CRASHED" : res.Outcome == "FAILED" ? "MISSION ENDED" : "MISSION COMPLETE";
        UiTheme.Label(new Rect(x, y, w, 36f), head, UiTheme.H1, crashed ? UiTheme.Bad : UiTheme.Text);
        y += 40f;
        if (m != null)
        {
            UiTheme.Label(new Rect(x, y, w, 26f), m.Id + "   " + m.Name, UiTheme.H2);
            y += 30f;
            float cx = x;
            cx += UiTheme.Chip(cx, y, m.ClassTag, col) + 8f;
            cx += UiTheme.Chip(cx, y, m.Phase.ToString().ToUpper(), UiTheme.Line) + 8f;
            if (gm.ExperimentRunning)
                UiTheme.Chip(cx, y, "LAB SESSION  ·  TRIAL " + gm.ExperimentTrialIndex + " OF " + gm.ExperimentTrialTotal, UiTheme.Violet);
            else
                UiTheme.Chip(cx, y, "MISSION " + ParticipantProgress.Position(m.Id) + " OF " + ParticipantProgress.Total, UiTheme.Line, 20f, true);
            y += 32f;
        }
        if (!string.IsNullOrEmpty(res.Headline)) { UiTheme.Label(new Rect(x, y, w, 20f), res.Headline, UiTheme.Body, UiTheme.Dim); y += 24f; }
        y += 6f;

        // left: did it count
        float lw = 470f;
        var lb = new Rect(x, y, lw, 212f);
        UiTheme.Box(lb);
        float ly = y + 12f;
        UiTheme.Label(new Rect(x + 14f, ly, lw, 20f), "RECORDING", UiTheme.H3); ly += 28f;
        if (trial == null)
        {
            UiTheme.Label(new Rect(x + 14f, ly, lw - 28f, 40f), "No trial summary was written for this flight.", UiTheme.Body, UiTheme.Warn);
        }
        else
        {
            float frac = trial.Expected > 0 ? (float)trial.Rows / trial.Expected : 0f;
            UiTheme.Label(new Rect(x + 14f, ly, 120f, 20f), "Telemetry rows", UiTheme.Small, UiTheme.Faint);
            UiTheme.Bar(new Rect(x + 134f, ly + 3f, 200f, 14f), frac, trial.Complete ? UiTheme.Good : UiTheme.Warn);
            UiTheme.Label(new Rect(x + 342f, ly, 120f, 20f), trial.Rows + " / " + trial.Expected, UiTheme.Body); ly += 26f;
            Flag(x + 14f, ly, "Flown to the end", trial.Complete, trial.Complete ? "yes" : "no — stopped at " + Mathf.RoundToInt(frac * 100f) + " %"); ly += 24f;
            Flag(x + 14f, ly, "Continuous", trial.Continuous,
                 trial.Continuous ? (trial.Gaps > 0 ? "yes (" + trial.Gaps + " small hitch" + (trial.Gaps == 1 ? "" : "es") + ")" : "yes")
                                  : "no — gap of " + trial.MaxGapS.ToString("F1") + " s"); ly += 30f;
            UiTheme.Label(new Rect(x + 14f, ly, lw - 28f, 22f),
                usable ? "✓  This trial counts. The mission is marked DONE." :
                crashed ? "✗  Does not count. The data is kept, marked incomplete. Fly it again." :
                          "✗  Does not count — see above. The data is kept. Fly it again.",
                UiTheme.Body, usable ? UiTheme.Good : UiTheme.Warn); ly += 30f;
            UiTheme.Label(new Rect(x + 14f, ly, lw - 28f, 18f), UiTheme.Short(trial.Dir, 70), UiTheme.Small, UiTheme.Faint);
        }

        // right: where am I
        float rx = x + lw + 20f, rw = w - lw - 20f;
        var rb = new Rect(rx, y, rw, 212f);
        UiTheme.Box(rb);
        float ry = y + 12f;
        string next = gm.ExperimentRunning ? null : ParticipantProgress.NextMissionId();
        if (gm.ExperimentRunning)
        {
            UiTheme.Label(new Rect(rx + 14f, ry, rw, 20f), "LAB SESSION", UiTheme.H3); ry += 28f;
            ry += UiTheme.Paragraph(rx + 14f, ry, rw - 28f,
                "Trial " + gm.ExperimentTrialIndex + " of " + gm.ExperimentTrialTotal + " recorded.\n\n" +
                "Enter continues to the next step of the protocol. Take a short pause first if needed — the clock " +
                "between trials is not part of any analysis.", 13, UiTheme.Dim);
        }
        else
        {
            int done = ParticipantProgress.DoneCount;
            UiTheme.Label(new Rect(rx + 14f, ry, rw, 20f), "PROGRESS", UiTheme.H3); ry += 28f;
            UiTheme.Bar(new Rect(rx + 14f, ry, rw - 28f, 16f), (float)done / ParticipantProgress.Total, UiTheme.Good,
                        done + " of " + ParticipantProgress.Total + " done"); ry += 30f;
            if (next != null)
            {
                var nm = MissionLibrary.Get(next);
                UiTheme.Label(new Rect(rx + 14f, ry, rw, 18f), "NEXT  ·  " + ParticipantProgress.Position(next) + " of " + ParticipantProgress.Total, UiTheme.Small, UiTheme.Faint); ry += 22f;
                UiTheme.Label(new Rect(rx + 14f, ry, rw - 28f, 24f), nm.Id + "   " + UiTheme.Short(nm.Name, 30), UiTheme.H3); ry += 26f;
                float cx = rx + 14f;
                cx += UiTheme.Chip(cx, ry, nm.ClassTag, UiTheme.ClassColor(nm.Class), 18f) + 6f;
                UiTheme.Chip(cx, ry, nm.Phase.ToString().ToUpper(), UiTheme.Line, 18f); ry += 26f;
                UiTheme.Label(new Rect(rx + 14f, ry, rw - 28f, 18f), "Its briefing opens first.", UiTheme.Small);
            }
            else
            {
                UiTheme.Label(new Rect(rx + 14f, ry, rw - 28f, 44f), "Every mission is done for this participant.", UiTheme.Body, UiTheme.Good);
            }
        }
        y += 226f;

        // metrics
        UiTheme.Label(new Rect(x, y, w, 20f), "KEY METRICS  (informational — workload is measured from the EEG and the telemetry, not scored here)", UiTheme.Small, UiTheme.Faint);
        y += 22f;
        int i = 0;
        foreach (var kv in res.Metrics)
        {
            if (i >= 8) break;
            float mx = x + (i % 4) * (w / 4f), my = y + (i / 4) * 22f;
            UiTheme.Label(new Rect(mx, my, w / 4f - 10f, 20f), kv.Key.Replace('_', ' ') + "   " + kv.Value.ToString("F1"), UiTheme.Small, UiTheme.Dim);
            i++;
        }

        // footer
        float by = p.yMax - 62f;
        if (gm.ExperimentRunning)
        {
            if (UiTheme.Button(new Rect(p.xMax - 30f - 360f, by, 360f, 44f), "CONTINUE SESSION", UiTheme.Violet, true, true, 16, TextAnchor.MiddleCenter, "Enter"))
                gm.Confirm();
            UiTheme.Label(new Rect(x, by + 12f, 500f, 20f), "R flies this trial again before continuing.", UiTheme.Small, UiTheme.Faint);
        }
        else
        {
            if (UiTheme.Button(new Rect(x, by, 160f, 44f), "HUB", UiTheme.Line, true, false, 14, TextAnchor.MiddleCenter, "Esc"))
                gm.Confirm();
            if (UiTheme.Button(new Rect(x + 172f, by, 200f, 44f), "FLY AGAIN", usable ? UiTheme.Line : UiTheme.Warn, true, !usable, 14, TextAnchor.MiddleCenter, "R"))
                gm.RestartTrial();
            if (next != null)
            {
                var nm = MissionLibrary.Get(next);
                if (UiTheme.Button(new Rect(p.xMax - 30f - 360f, by, 360f, 44f), "NEXT  ·  " + next + "  " + UiTheme.Short(nm.Name, 16),
                                   UiTheme.ClassColor(nm.Class), true, usable, 15, TextAnchor.MiddleCenter, "Enter"))
                    gm.ContinueToMission(next);
            }
        }
    }

    void Flag(float x, float y, string k, bool ok, string v)
    {
        UiTheme.Fill(new Rect(x, y + 6f, 8f, 8f), ok ? UiTheme.Good : UiTheme.Warn);
        UiTheme.Label(new Rect(x + 16f, y, 130f, 20f), k, UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 150f, y, 300f, 20f), v, UiTheme.Body, ok ? UiTheme.Text : UiTheme.Warn);
    }

    // ── free flight ─────────────────────────────────────────────────────────────
    void DrawFreeFlight(GameManager gm)
    {
        float pw = 620f, ph = 300f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 5f), gm.Crashed ? UiTheme.Bad : UiTheme.Accent);
        float x = p.x + 30f, y = p.y + 24f, w = pw - 60f;
        UiTheme.Label(new Rect(x, y, w, 36f), gm.Crashed ? "CRASHED" : "FLIGHT ENDED", UiTheme.H1, gm.Crashed ? UiTheme.Bad : UiTheme.Text); y += 44f;
        string why = gm.Crashed && gm.Aircraft != null && gm.Aircraft.CrashReason != "" ? gm.Aircraft.CrashReason : "";
        y += UiTheme.Paragraph(x, y, w,
            (gm.Crashed ? "The aeroplane was destroyed" + (why != "" ? " (" + why + ")" : "") + ". " : "") +
            "Free flight is practice: nothing was recorded and nothing counts. Fly again, or go to the hub and take a mission.", 14, UiTheme.Dim);
        float by = p.yMax - 64f;
        if (UiTheme.Button(new Rect(x, by, 200f, 44f), "HUB", UiTheme.Line, true, false, 14, TextAnchor.MiddleCenter, "Enter / Esc"))
            gm.Confirm();
        if (UiTheme.Button(new Rect(p.xMax - 30f - 240f, by, 240f, 44f), "FLY AGAIN", UiTheme.Accent, true, true, 15, TextAnchor.MiddleCenter, "R"))
            gm.RestartTrial();
    }

    // ── session report ──────────────────────────────────────────────────────────
    void DrawSessionReport(GameManager gm)
    {
        var results = gm.SessionResults;
        float pw = 900f, ph = 620f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 5f), UiTheme.Violet);
        float x = p.x + 30f, y = p.y + 22f, w = pw - 60f;
        UiTheme.Label(new Rect(x, y, w, 36f), "LAB SESSION COMPLETE", UiTheme.H1); y += 40f;
        UiTheme.Label(new Rect(x, y, w, 20f),
            "Participant " + ParticipantManager.ID + "  ·  visit " + ParticipantManager.Session + "  ·  " + results.Count + " trials recorded",
            UiTheme.Body, UiTheme.Dim); y += 30f;

        float cli = SessionReporter.LastCLI;
        Color cliCol = cli < 33f ? UiTheme.Good : cli < 66f ? UiTheme.Warn : UiTheme.Bad;
        UiTheme.Label(new Rect(x, y, 300f, 20f), "COGNITIVE LOAD INDEX (telemetry-derived, provisional)", UiTheme.Small, UiTheme.Faint); y += 22f;
        UiTheme.Bar(new Rect(x, y, 400f, 18f), cli / 100f, cliCol, cli.ToString("F0") + " / 100  " + (cli < 33f ? "LOW" : cli < 66f ? "MODERATE" : "HIGH")); y += 34f;

        UiTheme.Label(new Rect(x, y, 60f, 20f), "#", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 40f, y, 360f, 20f), "TRIAL", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 420f, y, 100f, 20f), "CLASS", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 520f, y, 140f, 20f), "OUTCOME", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 680f, y, 100f, 20f), "SCORE", UiTheme.Small, UiTheme.Faint); y += 22f;
        float rowH = 24f;
        for (int i = 0; i < results.Count && i < 14; i++)
        {
            var r = results[i];
            if (i % 2 == 1) UiTheme.Fill(new Rect(x, y, w, rowH), new Color(1, 1, 1, 0.03f));
            UiTheme.Label(new Rect(x, y, 40f, rowH), (i + 1).ToString(), UiTheme.Small, UiTheme.Faint);
            UiTheme.Label(new Rect(x + 40f, y, 370f, rowH), UiTheme.Short(r.SessionLabel, 48), UiTheme.Body);
            var cc = r.SessionCond == "HIGH" ? UiTheme.Bad : r.SessionCond == "MEDIUM" ? UiTheme.Warn : UiTheme.Good;
            UiTheme.Label(new Rect(x + 420f, y, 100f, rowH), r.SessionCond, UiTheme.Small, cc);
            UiTheme.Label(new Rect(x + 520f, y, 150f, rowH), r.Outcome, UiTheme.Small, r.Outcome == "CRASHED" ? UiTheme.Bad : UiTheme.Dim);
            UiTheme.Label(new Rect(x + 680f, y, 100f, rowH), r.Score.ToString("F0"), UiTheme.Small);
            y += rowH;
        }
        y += 10f;
        if (!string.IsNullOrEmpty(SessionReporter.LastFilePath))
            UiTheme.Label(new Rect(x, y, w, 18f), "Report: " + UiTheme.Short(SessionReporter.LastFilePath, 90), UiTheme.Small, UiTheme.Faint);

        if (UiTheme.Button(new Rect(p.xMax - 30f - 300f, p.yMax - 62f, 300f, 44f), "BACK TO HUB", UiTheme.Violet, true, true, 15, TextAnchor.MiddleCenter, "Enter"))
            gm.Confirm();
    }
}
