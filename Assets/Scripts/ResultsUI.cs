using UnityEngine;

/// <summary>
/// Post-level debrief: pass/fail, score, the per-level metrics and the CSV path.
/// Enter = back to menu, R = retry the same level.
/// </summary>
public class ResultsUI : MonoBehaviour
{
    GUIStyle title, row, small;

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        row = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Results) return;
        Styles();

        if (gm.ScenarioActive && gm.ScenarioRunner != null && gm.ScenarioRunner.Result != null)
        {
            DrawScenarioResult(gm, gm.ScenarioRunner.Result);
            return;
        }

        var res = gm.Level != null ? gm.Level.Result : null;
        float w = 560f, h = 380f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 28, r.y + 22, w - 56, h - 44));

        if (gm.Crashed)
        {
            GUILayout.Label("CRASHED", title);
            GUILayout.Space(6);
            string why = gm.Aircraft != null && gm.Aircraft.CrashReason != "" ? "  (" + gm.Aircraft.CrashReason + ")" : "";
            GUILayout.Label("Aircraft destroyed" + why, row);
        }
        else if (res == null)
        {
            GUILayout.Label("FLIGHT ENDED", title);
        }
        else
        {
            GUILayout.Label((res.Passed ? "LEVEL PASSED" : "LEVEL FAILED") +
                            $"   —   score {res.Score:F0}/100", title);
            GUILayout.Space(6);
            GUILayout.Label(res.Headline, row);
            GUILayout.Space(10);
            foreach (var kv in res.Metrics)
                GUILayout.Label($"{kv.Key.Replace('_', ' '),-26}: {kv.Value:F1}", row);
            GUILayout.Space(10);
            GUILayout.Label("Data saved to:", row);
            GUILayout.Label(gm.Logger != null ? gm.Logger.FilePath : "(none)", small);
        }

        GUILayout.Space(14);
        GUILayout.Label("[ Enter ] menu      [ R ] retry level", title);
        GUILayout.EndArea();
    }

    void DrawScenarioResult(GameManager gm, ScenarioResult res)
    {
        if (gm.SessionReportActive) { DrawSessionReport(gm); return; }

        float w = 620f, h = 500f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 28, r.y + 20, w - 56, h - 40));

        // No scoring / pass-fail this phase — cognitive load is measured from the EEG
        // and the telemetry, not a game score. Just confirm completion + log data.
        string head = res.Outcome == "CRASHED" ? "FLIGHT ENDED (off-field / crash)" : "SCENARIO COMPLETE";
        GUILayout.Label(head, title);
        GUILayout.Space(4);
        if (gm.ScenarioRunner.Current != null)
            GUILayout.Label(gm.ScenarioRunner.Current.Title + "   —   data logged for analysis", row);
        GUILayout.Space(10);

        // a few key metrics (informational only — not scored)
        GUILayout.Label("KEY METRICS", row);
        int shown = 0;
        foreach (var kv in res.Metrics)
        {
            if (shown++ >= 6) break;
            GUILayout.Label($"   {kv.Key.Replace('_', ' '),-26}: {kv.Value:F1}", small);
        }
        GUILayout.Space(6);
        GUILayout.Label("Data: " + (!string.IsNullOrEmpty(gm.ScenarioRunner.DataFile) ? gm.ScenarioRunner.DataFile : "FlightSimData/Scenarios"), small);

        GUILayout.Space(8);
        GUILayout.Label("[ Enter ] continue / menu      [ R ] retry scenario", title);
        GUILayout.EndArea();
    }

    void DrawSessionReport(GameManager gm)
    {
        var results = gm.SessionResults;
        float w = 680f, h = 520f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 28, r.y + 20, w - 56, h - 40));

        // participant + session badge
        if (ParticipantManager.IsSet)
            GUILayout.Label($"Participant: {ParticipantManager.ID}   Session: {ParticipantManager.Session}", small);

        float total = 0f; foreach (var x in results) total += x.Score;
        float avg = results.Count > 0 ? total / results.Count : 0f;
        GUILayout.Label($"SESSION REPORT   —   overall {avg:F0}/100", title);
        GUILayout.Space(4);

        // CLI gauge
        float cli = SessionReporter.LastCLI;
        Color cliCol = cli < 33f ? new Color(0.3f, 0.9f, 0.4f)
                     : cli < 66f ? new Color(1f, 0.85f, 0.2f)
                                 : new Color(1f, 0.35f, 0.25f);
        var cliStyle = new GUIStyle(row) { normal = { textColor = cliCol } };
        GUILayout.Label($"Cognitive Load Index (CLI):  {cli:F0} / 100   " +
                        (cli < 33f ? "(LOW)" : cli < 66f ? "(MODERATE)" : "(HIGH)"), cliStyle);
        GUILayout.Space(6);

        GUILayout.Label($"{"LEVEL",-26}{"COND",-8}{"OUTCOME",-12}SCORE", row);
        foreach (var x in results)
            GUILayout.Label($"{Trim(x.SessionLabel, 25),-26}{x.SessionCond,-8}{x.Outcome,-12}{x.Score,4:F0}", small);
        GUILayout.Space(6);

        // baseline vs loaded
        if (results.Count >= 2)
        {
            float baseline = results[0].Score, load = 0f;
            for (int i = 1; i < results.Count; i++) load += results[i].Score;
            load /= (results.Count - 1);
            GUILayout.Label($"Baseline {baseline:F0}   vs   loaded mean {load:F0}   (Δ {load - baseline:+0;-0})", row);
        }
        GUILayout.Space(6);

        if (!string.IsNullOrEmpty(SessionReporter.LastFilePath))
            GUILayout.Label("Report: " + SessionReporter.LastFilePath, small);

        GUILayout.Space(8);
        GUILayout.Label("[ Enter ] back to menu", title);
        GUILayout.EndArea();
    }

    static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));
}
