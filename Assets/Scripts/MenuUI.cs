using UnityEngine;

/// <summary>
/// Main menu for the cognitive-load experiment build.
///
///   • RUN FULL SESSION — the actual experiment: two resting baselines, then all
///     twelve missions in the participant's counterbalanced order (each followed by
///     its NASA-TLX), then a closing baseline. This is the button a study session
///     uses; the individual mission buttons below it are for practice and debugging.
///   • The mission list is organised by WORKLOAD CLASS (Low / Medium / High), four
///     missions each. Class is the experimental condition, so it — not flight phase —
///     is the organising principle here.
///   • FREE FLIGHT — no tasks, no logging. For familiarisation before a session.
///
/// The menu shows each mission's Predicted Load Index so the experimenter can see
/// the design's own ordering at a glance. That number is a PREDICTION from the
/// workload model, never a measurement, and it is never shown during a trial.
/// </summary>
public class MenuUI : MonoBehaviour
{
    GUIStyle title, sub, body, btn, btnSel, btnRun, small, cls;

    int classIdx = 0;
    static readonly string[] ClassNames = { "LOW", "MEDIUM", "HIGH" };
    Vector2 scroll;
    bool showDetail;
    string detailId = "";

    void Styles()
    {
        if (title != null) return;
        title  = new GUIStyle(GUI.skin.label)  { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        sub    = new GUIStyle(GUI.skin.label)  { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.72f, 0.82f, 1f) } };
        body   = new GUIStyle(GUI.skin.label)  { fontSize = 13, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
        small  = new GUIStyle(GUI.skin.label)  { fontSize = 11, normal = { textColor = new Color(0.68f, 0.68f, 0.68f) }, wordWrap = true };
        cls    = new GUIStyle(GUI.skin.label)  { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.35f) } };
        btn    = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
        btnSel = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 1f, 0.6f) } };
        btnRun = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.45f, 1f, 0.65f) } };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.ParticipantReady || gm.State != GameState.Menu) return;
        if (gm.ExperimentRunning) return;   // ExperimentUI owns the screen mid-session
        Styles();

        if (showDetail) { DrawDetail(gm); return; }

        float w = 700f, h = 690f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 26, r.y + 18, w - 52, h - 36));

        GUILayout.Label("PILOT COGNITIVE-WORKLOAD EXPERIMENT", title);
        GUILayout.Label("Cessna 172-class trainer  ·  12 missions  ·  4 phases x 3 workload classes  ·  EEG + NASA-TLX", sub);
        GUILayout.Space(12);

        // ---- the actual experiment ----
        if (GUILayout.Button("▶   RUN FULL SESSION   (baselines + 12 counterbalanced trials)", btnRun, GUILayout.Height(44)))
            gm.StartExperimentSession();
        GUILayout.Label("Participant " + (ParticipantManager.IsSet ? ParticipantManager.ID : "—") +
                        ", session " + ParticipantManager.Session +
                        "  ·  order is fixed by a balanced Latin square, so it is reproducible, not random.", small);
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("✈   FREE FLIGHT   (familiarisation — nothing recorded)", btn, GUILayout.Height(30)))
            gm.StartFlight(FlightMode.FreeFlight);
        if (GUILayout.Button("🛠  CONTROL CHECK", btn, GUILayout.Height(30), GUILayout.Width(170)))
            ControlCheckMode.Enter();
        GUILayout.EndHorizontal();
        GUILayout.Label(VRRuntime.StatusLine, small);
        GUILayout.Space(12);

        // ---- single missions, by workload class ----
        GUILayout.Label("PRACTICE / DEBUG — fly one mission on its own", body);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Class:", body, GUILayout.Width(52));
        for (int i = 0; i < 3; i++)
            if (GUILayout.Button(ClassNames[i], i == classIdx ? btnSel : btn, GUILayout.Height(24)))
                classIdx = i;
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        var missions = MissionLibrary.ForClass((WorkloadClass)classIdx);
        GUILayout.Label(ClassNames[classIdx] + " — " + missions.Count + " cognitive-axis missions (" +
                        MissionLibrary.VariantCount + " variants x 4 phases), each " +
                        MissionLibrary.StandardDurationS.ToString("F0") + " s with a " +
                        MissionLibrary.StandardBaselineS.ToString("F0") + " s in-task baseline", small);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(190f));
        foreach (var m in missions)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(m.Id + "   " + m.Phase.ToString().ToUpper() + "   " + m.Name, btn, GUILayout.Height(30)))
                gm.StartSingleMission(m.Id);
            if (GUILayout.Button("?", btn, GUILayout.Width(30), GUILayout.Height(30)))
            { showDetail = true; detailId = m.Id; }
            GUILayout.EndHorizontal();
            GUILayout.Label("      predicted load index " + m.Profile.PLI.ToString("F0") + "/100  ·  " + m.Objective, small);
        }
        GUILayout.EndScrollView();

        // THE DESIGN GRID. Shows every variant of every cell, and marks the one THIS
        // participant is assigned, so the operator can see both the bank and the session
        // at a glance. (The previous version displayed only the last mission matching
        // each cell, which once variants existed meant it silently showed variant 3 and
        // looked as though the bank had one mission per cell.)
        int pnum = ParticipantManager.IsSet
                 ? ExperimentSession.StableNumber(ParticipantManager.ID) + (ParticipantManager.Session - 1) : 0;
        GUILayout.Label("DESIGN GRID — cognitive axis  (" + MissionLibrary.CognitiveAxis().Count +
                        " missions: 4 phases x 3 classes x " + MissionLibrary.VariantCount +
                        " variants).  * = assigned to this participant.", body);
        GUILayout.Label("   " + "PHASE".PadRight(10) + "v".PadRight(3) +
                        "LOW".PadRight(14) + "MEDIUM".PadRight(14) + "HIGH", small);
        foreach (FlightPhase ph in MissionLibrary.Rows)
        {
            int assigned = MissionLibrary.VariantForRow(pnum, ph);
            for (int v = 1; v <= MissionLibrary.VariantCount; v++)
            {
                string row = "   " + (v == 1 ? ph.ToString().ToUpper().PadRight(10) : "".PadRight(10));
                row += ((v == assigned ? "*" : " ") + v).PadRight(3);
                foreach (WorkloadClass wc in new[] { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High })
                {
                    var mm = MissionLibrary.Cell(ph, wc, v);
                    row += (mm == null ? "—" : mm.Id + " " + mm.Profile.PLI.ToString("F0")).PadRight(14);
                }
                GUILayout.Label(row, small);
            }
        }
        GUILayout.Label("   this participant flies: " + ExperimentSessionRowSummary(pnum), small);

        // THE SECOND AXIS, shown separately because it IS separate. Crosswind raises
        // manual demand by construction, so these are never pooled with the cognitive
        // scale and the operator must not be able to mistake them for part of it.
        GUILayout.Space(4);
        GUILayout.Label("SECOND AXIS — psychomotor-integrated (crosswind).  NOT on the Low/Medium/High " +
                        "cognitive scale; analysed separately with the control-activity covariates.", body);
        string xrow = "   ";
        foreach (var mm in MissionLibrary.PsychomotorAxis())
            xrow += mm.Id + " " + mm.CrosswindMs.ToString("F1") + "m/s   ";
        GUILayout.Label(xrow, small);

        GUILayout.Space(6);
        GUILayout.Label(
            "FLIGHT   W/S pitch   A/D roll   Q/E rudder   Shift/Ctrl throttle   F flaps   B brakes   [ ] trim\n" +
            "COCKPIT  LEFT-drag yoke / throttle / trim wheel / flap+brake levers · click switches · RIGHT-drag to look\n" +
            "VR       grip = grab a control · trigger = press a switch · A/X = acknowledge · both grips+triggers = recentre\n" +
            "SYSTEMS  H carb heat   J fuel selector   K shed electrical load   L alternate static\n" +
            "TASK     SPACE acknowledge / checklist item      C view   R restart   Esc menu",
            small);
        GUILayout.EndArea();
    }

    /// <summary>"Takeoff v2 · Climb v3 · Cruise v1 · Approach v2" — the variant this
    /// participant is assigned in each phase row, computed the same way the session
    /// builder computes it so the display cannot drift from what is actually flown.</summary>
    static string ExperimentSessionRowSummary(int pnum)
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < MissionLibrary.Rows.Length; r++)
        {
            if (r > 0) sb.Append(" · ");
            sb.Append(MissionLibrary.Rows[r]).Append(" v").Append(MissionLibrary.VariantForRow(pnum, MissionLibrary.Rows[r]));
        }
        return sb.ToString();
    }

    void DrawDetail(GameManager gm)
    {
        var m = MissionLibrary.Get(detailId);
        if (m == null) { showDetail = false; return; }

        float w = 760f, h = 700f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 26, r.y + 18, w - 52, h - 36));
        GUILayout.Label(m.Id + " — " + m.Name, title);
        GUILayout.Label(m.ClassTag + "  ·  " + m.Phase + "  ·  predicted load index " + m.Profile.PLI.ToString("F1"), cls);
        GUILayout.Space(8);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(h - 130f));
        Field("OBJECTIVE", m.Objective);
        Field("BRIEF", m.Brief);
        Field("WHY THIS CLASS", m.LoadRationale);
        Field("EEG RELEVANCE", m.EegRelevance);
        Field("EXPECTED ERRORS", m.ExpectedErrors);
        Field("SUCCESS", m.SuccessCriteria);
        Field("FAILURE", m.FailureConditions);
        Field("AVIATION BASIS", m.AviationBasis);
        Field("APPROXIMATIONS / NOT MODELLED", m.Approximations);
        GUILayout.Label("DEMAND PROFILE (0-4)", body);
        foreach (var kv in m.Profile.AsDictionary())
            GUILayout.Label("   " + kv.Key.Replace('_', ' ').PadRight(22) + new string('█', kv.Value) + "  " + kv.Value, small);
        GUILayout.Space(6);
        GUILayout.Label("PRE-REGISTERED EXPECTED NASA-TLX (a prediction, never a measurement)", body);
        GUILayout.Label($"   mental {m.Expected.Mental}   physical {m.Expected.Physical}   temporal {m.Expected.Temporal}   " +
                        $"performance {m.Expected.Performance}   effort {m.Expected.Effort}   frustration {m.Expected.Frustration}   " +
                        $"→ RTLX {m.Expected.RTLX:F0}", small);
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("← back", btn, GUILayout.Height(28))) showDetail = false;
        if (GUILayout.Button("FLY " + m.Id, btnSel, GUILayout.Height(28)))
        { showDetail = false; gm.StartSingleMission(m.Id); }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void Field(string k, string v)
    {
        if (string.IsNullOrEmpty(v)) return;
        GUILayout.Label(k, body);
        GUILayout.Label("   " + v, small);
        GUILayout.Space(5);
    }
}
