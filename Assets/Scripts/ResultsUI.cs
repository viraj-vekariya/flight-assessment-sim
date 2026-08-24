using UnityEngine;

/// <summary>
/// Post-level debrief: pass/fail, score, the per-level metrics and the CSV path.
/// Enter = back to menu, R = retry the same level.
/// </summary>
public class ResultsUI : MonoBehaviour
{
    GUIStyle title, row, small, submitBtn, bedfordRow, bedfordRowSel;

    // ---- mental-workload questionnaire (NASA-TLX raw/RTLX + BEDFORD) ------------
    // Two corrections to the earlier version, both of which matter for the numbers
    // this instrument produces:
    //
    //  1. 21-POINT SCALE. The NASA TLX manual's scales are 21 tick marks, 0-100 in
    //     steps of 5. A continuous slider silently changes the instrument, and makes
    //     scores non-comparable with the published literature. Values now snap to 5.
    //
    //  2. NO DEFAULT ANSWER. Every subscale used to start at 50. A pre-set midpoint
    //     is an anchor: participants adjust away from it rather than answering, which
    //     drags responses toward the middle and shrinks exactly the between-condition
    //     differences the study is trying to detect. Subscales now start UNANSWERED,
    //     the handle is hidden until touched, and SUBMIT stays disabled until all six
    //     plus a BEDFORD rating have been given.
    int ratingToken = -1;
    float qMental = 50, qPhysical = 50, qTemporal = 50, qPerformance = 50, qEffort = 50, qFrustration = 50;
    bool tMental, tPhysical, tTemporal, tPerformance, tEffort, tFrustration;
    int qBedford = 0;                 // 0 = not yet answered
    Vector2 bedfordScroll;
    float questionnaireShownAt;

    const float TlxStep = 5f;         // 21-point scale: 0,5,10,...,100
    bool AllAnswered => tMental && tPhysical && tTemporal && tPerformance && tEffort && tFrustration && qBedford > 0;

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        row = new GUIStyle(GUI.skin.label) { fontSize = 17, normal = { textColor = Color.white } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        submitBtn = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        bedfordRow = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
        bedfordRowSel = new GUIStyle(bedfordRow) { normal = { textColor = new Color(1f, 0.85f, 0.2f) }, fontStyle = FontStyle.Bold };
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
        if (gm.WorkloadQuestionnaireActive) { DrawWorkloadQuestionnaire(gm); return; }
        if (gm.SessionReportActive) { DrawSessionReport(gm); return; }

        float w = 620f, h = 500f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 28, r.y + 20, w - 56, h - 40));

        // No scoring / pass-fail this phase — cognitive load is measured from the EEG +
        // the workload self-report, not a game score. Just confirm completion + log data.
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
        GUILayout.Label($"Mean self-reported workload — RTLX: {SessionReporter.LastMeanRTLX:F0}/100" +
                        $"   BEDFORD: {SessionReporter.LastMeanBedford:F1}/10", small);
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

    // ---- Phase 5: mental-workload questionnaire (NASA-TLX RTLX + BEDFORD) --------
    // Shown right after a scenario trial ends, BEFORE the score breakdown, so the
    // self-report isn't biased by having just seen the score. Blocks Enter/R at the
    // GameManager level until SUBMIT is clicked.
    void DrawWorkloadQuestionnaire(GameManager gm)
    {
        if (ratingToken != gm.WorkloadQuestionnaireToken)
        {
            ratingToken = gm.WorkloadQuestionnaireToken;
            qMental = qPhysical = qTemporal = qPerformance = qEffort = qFrustration = 50f;
            tMental = tPhysical = tTemporal = tPerformance = tEffort = tFrustration = false;
            qBedford = 0;
            bedfordScroll = Vector2.zero;
            questionnaireShownAt = Time.realtimeSinceStartup;
        }

        float w = 760f, h = 640f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 26, r.y + 18, w - 52, h - 36));

        GUILayout.Label("WORKLOAD ASSESSMENT", title);
        string sTitle = gm.ScenarioRunner.Current != null ? gm.ScenarioRunner.Current.Title : "";
        GUILayout.Label("Rate the trial you just flew (" + sTitle + ") — score is shown after you submit.", small);
        GUILayout.Space(8);

        GUILayout.Label("NASA-TLX  —  21-point scales, 0-100 in steps of 5", row);
        qMental      = TlxSlider("Mental Demand",   "Very low",  "Very high", qMental, ref tMental);
        qPhysical    = TlxSlider("Physical Demand", "Very low",  "Very high", qPhysical, ref tPhysical);
        qTemporal    = TlxSlider("Temporal Demand", "Very low",  "Very high", qTemporal, ref tTemporal);
        qPerformance = TlxSlider("Performance",     "Perfect",   "Failure",   qPerformance, ref tPerformance);
        qEffort      = TlxSlider("Effort",          "Very low",  "Very high", qEffort, ref tEffort);
        qFrustration = TlxSlider("Frustration",     "Very low",  "Very high", qFrustration, ref tFrustration);
        float rtlx = (qMental + qPhysical + qTemporal + qPerformance + qEffort + qFrustration) / 6f;
        GUILayout.Space(2);
        GUILayout.Label("Performance is anchored PERFECT (0) to FAILURE (100) — a higher mark means you " +
                        "judged your own performance worse. This is the standard raw-TLX direction.", small);
        GUILayout.Label(AllAnswered ? $"RTLX (mean of the 6):  {rtlx:F1} / 100" : "RTLX: —  (answer all six)", small);
        GUILayout.Space(8);

        GUILayout.Label("BEDFORD WORKLOAD SCALE — pick the closest statement", row);
        bedfordScroll = GUILayout.BeginScrollView(bedfordScroll, GUILayout.Height(140));
        for (int i = 0; i < WorkloadRating.BedfordStatements.Length; i++)
        {
            bool sel = qBedford == i + 1;
            if (GUILayout.Button((sel ? "> " : "   ") + WorkloadRating.BedfordStatements[i], sel ? bedfordRowSel : bedfordRow))
                qBedford = i + 1;
        }
        GUILayout.EndScrollView();
        GUILayout.Space(8);

        // SUBMIT is disabled until every subscale has actually been touched, so an
        // untouched midpoint can never be recorded as if it were an answer.
        GUI.enabled = AllAnswered;
        if (GUILayout.Button(AllAnswered ? "SUBMIT" : "SUBMIT  (answer every scale first)", submitBtn, GUILayout.Height(34)))
        {
            gm.SubmitWorkloadRating(new WorkloadRating
            {
                MentalDemand = Snap(qMental), PhysicalDemand = Snap(qPhysical), TemporalDemand = Snap(qTemporal),
                Performance = Snap(qPerformance), Effort = Snap(qEffort), Frustration = Snap(qFrustration),
                Bedford = qBedford
            });
        }
        GUI.enabled = true;
        GUILayout.Label("Time on this form: " + (Time.realtimeSinceStartup - questionnaireShownAt).ToString("F0") + " s", small);
        GUILayout.EndArea();
    }

    static float Snap(float v) => Mathf.Round(Mathf.Clamp(v, 0f, 100f) / TlxStep) * TlxStep;

    /// <summary>One 21-point TLX scale. Reports its value only once the participant
    /// has moved it; until then it shows "—" and is not counted as answered.</summary>
    float TlxSlider(string label, string lo, string hi, float v, ref bool touched)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, row, GUILayout.Width(140));
        GUILayout.Label(lo, small, GUILayout.Width(60));
        float nv = GUILayout.HorizontalSlider(v, 0f, 100f, GUILayout.Width(300));
        GUILayout.Label(hi, small, GUILayout.Width(60));
        if (!Mathf.Approximately(nv, v)) touched = true;
        nv = Snap(nv);
        GUILayout.Label(touched ? nv.ToString("F0") : "—", small, GUILayout.Width(30));
        GUILayout.EndHorizontal();
        return nv;
    }
}
