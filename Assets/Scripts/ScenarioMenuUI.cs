using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scenario picker (shown from the main menu). Choose a difficulty, then a
/// scenario, or run the graded baseline->ramp session. Each "level" comes from
/// ScenarioLibrary as data.
/// </summary>
public class ScenarioMenuUI : MonoBehaviour
{
    GUIStyle title, body, btn, btnSel, small;
    int diffIdx = 1;                       // 0 easy, 1 medium, 2 hard
    static readonly float[] Diffs = { 0.25f, 0.55f, 0.85f };
    static readonly string[] DiffNames = { "EASY", "MEDIUM", "HARD" };

    List<Scenario> cached;
    int cachedDiff = -1;
    Vector2 scroll;

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };
        body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.82f, 0.82f, 0.82f) }, wordWrap = true };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        btn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        btnSel = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 1f, 0.6f) } };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Menu || !gm.InScenarioMenu) return;
        Styles();

        if (cachedDiff != diffIdx) { cached = ScenarioLibrary.All(Diffs[diffIdx]); cachedDiff = diffIdx; }

        float w = 620f, h = 580f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 26, r.y + 20, w - 52, h - 40));

        GUILayout.Label("SCENARIOS  —  Phase 2", title);
        GUILayout.Space(8);

        // difficulty selector
        GUILayout.BeginHorizontal();
        GUILayout.Label("Difficulty:", body, GUILayout.Width(70));
        for (int i = 0; i < 3; i++)
            if (GUILayout.Button(DiffNames[i], i == diffIdx ? btnSel : btn, GUILayout.Height(28)))
                diffIdx = i;
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        if (GUILayout.Button("▶  GRADED SESSION  (baseline → ramp)", btnSel, GUILayout.Height(34)))
            gm.StartGradedSession();
        GUILayout.Space(4);
        if (GUILayout.Button("✈  Weather Transfer (Full Mission)", btnSel, GUILayout.Height(30)))
            gm.StartSingleScenario(ScenarioLibrary.WeatherTransfer(0.5f));
        GUILayout.Space(6);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(h - 220f));
        for (int i = 0; i < cached.Count; i++)
        {
            var s = cached[i];
            if (GUILayout.Button((i + 1) + ".  " + s.Title, btn, GUILayout.Height(30)))
                gm.StartSingleScenario(s);
            GUILayout.Label("     " + s.Desc, small);
            GUILayout.Space(3);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("◀  Back to main menu", btn, GUILayout.Height(30)))
            gm.CloseScenarioMenu();

        GUILayout.EndArea();
    }
}
