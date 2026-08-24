using UnityEngine;

/// <summary>
/// Overlay for an active scenario: the status bar, event banners, the big
/// "acknowledge" alarm prompt, weather/fault flags, and a waypoint bearing +
/// distance readout. Draws on top of the normal Hud2D while flying a scenario.
/// </summary>
public class ScenarioHud : MonoBehaviour
{
    GUIStyle banner, alarm, flag, status, wpt, chkTitle, chkItem, chkDone, brief, annun;

    void Init()
    {
        if (banner != null) return;
        banner = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.9f, 0.3f) } };
        alarm = new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.3f, 0.3f) } };
        flag = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        status = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        wpt = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.6f, 0.95f) } };
        chkTitle = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
        chkItem = new GUIStyle { fontSize = 13, normal = { textColor = Color.white } };
        chkDone = new GUIStyle { fontSize = 12, normal = { textColor = new Color(0.5f, 0.75f, 0.55f) } };
        brief = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(0.85f, 0.9f, 1f) } };
        annun = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Flying || !gm.ScenarioActive || gm.ScenarioRunner == null) return;
        var e = gm.ScenarioRunner;
        if (!e.Active) return;
        Init();

        float cx = Screen.width / 2f;

        // status bar (just above the bottom Hud2D level bar)
        GUI.Label(new Rect(cx - 380f, Screen.height - 66f, 760f, 22f), e.StatusText(), status);

        // event banner
        if (e.Banner != "")
            GUI.Label(new Rect(cx - 260f, 64f, 520f, 30f), e.Banner, banner);

        // alarm prompt — must acknowledge with SPACE
        if (e.AlarmActive)
            GUI.Label(new Rect(cx - 240f, 100f, 480f, 36f), "⚠  " + e.AlarmLabel + "  —  press SPACE", alarm);

        // weather / fault flags top-right
        float fy = 92f;
        if (e.WeatherActive) { GUI.color = new Color(0.5f, 0.7f, 1f); GUI.Label(new Rect(Screen.width - 150f, fy, 140f, 20f), "WEATHER", flag); fy += 20f; }
        if (e.FaultActive) { GUI.color = new Color(1f, 0.7f, 0.2f); GUI.Label(new Rect(Screen.width - 150f, fy, 140f, 20f), "INSTRUMENT FAULT", flag); }
        GUI.color = Color.white;

        // ── during the in-task baseline, keep the objective on screen ──────────────
        // The baseline segment is the EEG reference, so it must not be a period where
        // the participant is confused about what they are supposed to be doing.
        if (e.IsExperiment && e.Segment == "BASELINE" && e.Mission != null)
            GUI.Label(new Rect(cx - 300f, Screen.height - 132f, 600f, 44f),
                      "OBJECTIVE: " + e.Mission.Objective, brief);

        // ── checklist / drill panel ────────────────────────────────────────────────
        var cl = e.Checklist;
        if (cl != null && !cl.Complete)
        {
            float px = 24f, py = 140f, pw = 420f;
            GUI.Box(new Rect(px - 8f, py - 8f, pw + 16f, 30f + 20f * cl.Def.Items.Count), "");
            GUI.Label(new Rect(px, py, pw, 20f), cl.Def.Title, chkTitle);
            for (int i = 0; i < cl.Def.Items.Count; i++)
            {
                var it = cl.Def.Items[i];
                bool done = i < cl.Index;
                bool cur = i == cl.Index;
                string mark = done ? "✓ " : cur ? "▶ " : "   ";
                string hint = cur && it.Kind == ChecklistItemKind.Check ? "   (SPACE)" : "";
                GUI.Label(new Rect(px, py + 24f + i * 20f, pw, 20f),
                          mark + it.Label + hint, done ? chkDone : chkItem);
            }
        }

        // ── systems annunciators ───────────────────────────────────────────────────
        // Only abnormal states are shown, and ONLY those a real panel would annunciate.
        // Gradual failures the mission wants the pilot to DIAGNOSE (carburettor ice)
        // are deliberately absent — an annunciator for those would delete the task.
        var sys = gm.Aircraft != null ? gm.Aircraft.GetComponent<AircraftSystems>() : null;
        if (sys != null)
        {
            float ay = 118f;
            void Ann(string text, Color col)
            {
                GUI.color = col;
                GUI.Label(new Rect(Screen.width - 230f, ay, 220f, 20f), text, annun);
                ay += 19f;
                GUI.color = Color.white;
            }
            if (!sys.AlternatorOn)   Ann("LOW VOLTS  " + sys.BusVolts.ToString("F1") + "V", new Color(1f, 0.75f, 0.2f));
            if (sys.LoadShed)        Ann("LOAD SHED", new Color(0.5f, 0.85f, 1f));
            if (sys.CarbHeatOn)      Ann("CARB HEAT ON", new Color(0.95f, 0.6f, 0.2f));
            if (sys.Selector != FuelSelector.Both) Ann("FUEL " + sys.Selector.ToString().ToUpper(), new Color(0.6f, 0.9f, 1f));
            if (sys.AlternateStaticOpen) Ann("ALT STATIC OPEN", new Color(0.6f, 0.9f, 1f));
            if (sys.DoorOpen)        Ann("DOOR OPEN", new Color(1f, 0.4f, 0.35f));
            if (sys.Engine == EngineState.Failed) Ann("ENGINE FAILURE", new Color(1f, 0.3f, 0.3f));
            if (!sys.FlapMotorOk)    Ann("FLAP INOP", new Color(1f, 0.75f, 0.2f));
        }

        // waypoint bearing + distance
        if (e.HasWaypoint && gm.Aircraft != null)
        {
            Vector3 p = gm.Aircraft.transform.position;
            Vector3 d = e.WaypointPos - p; d.y = 0f;
            float dist = d.magnitude;
            float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float rel = Mathf.DeltaAngle(gm.Aircraft.HeadingDeg, bearing);
            string arrow = rel > 8f ? "►" : rel < -8f ? "◄" : "▲";
            GUI.Label(new Rect(cx - 160f, 36f, 320f, 24f),
                $"{arrow}  {e.WaypointName}   {dist:F0} m   {(rel >= 0 ? "R" : "L")}{Mathf.Abs(rel):F0}°", wpt);
        }
    }
}
