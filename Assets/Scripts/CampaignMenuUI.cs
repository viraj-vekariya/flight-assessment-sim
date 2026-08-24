using UnityEngine;

/// <summary>
/// The campaign ladder menu: the levels in order, each showing the skill it
/// trains, difficulty, and your best score. Continue jumps to the next unbeaten
/// level. Levels unlock as you pass the previous one.
/// </summary>
public class CampaignMenuUI : MonoBehaviour
{
    GUIStyle title, body, btn, done, small, locked;
    Vector2 scroll;

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };
        body = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.82f, 0.82f, 0.82f) } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.70f, 0.70f, 0.72f) } };
        btn = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
        done = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.45f, 1f, 0.6f) } };
        locked = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.5f, 0.5f, 0.52f) } };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Menu || !gm.InCampaignMenu) return;
        Styles();

        float w = 660f, h = 600f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 26, r.y + 20, w - 52, h - 40));

        GUILayout.Label("CAMPAIGN — fly the levels in order", title);
        GUILayout.Space(6);
        int next = Campaign.NextStage();
        if (GUILayout.Button($"▶  CONTINUE   (Level {next}: {Campaign.Get(next).Name})", done, GUILayout.Height(34)))
            gm.StartCampaignStage(next);
        GUILayout.Space(6);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(h - 150f));
        foreach (var s in Campaign.Stages)
        {
            bool unlocked = Campaign.Unlocked(s.Num);
            bool passed = Campaign.Passed(s.Num);
            float best = Campaign.Best(s.Num);

            GUILayout.BeginHorizontal();
            string diff = s.Diff < 0.34f ? "EASY" : s.Diff < 0.67f ? "MEDIUM" : "HARD";
            string status = passed ? $"✓ best {best:F0}" : best >= 0f ? $"best {best:F0}" : "";
            string label = $"Lv {s.Num,2} · {s.Name}  —  {s.Skill}  [{diff}]   {status}";

            if (unlocked)
            {
                if (GUILayout.Button(label, passed ? done : btn, GUILayout.Height(30)))
                    gm.StartCampaignStage(s.Num);
            }
            else
            {
                GUILayout.Label("🔒  " + label, locked, GUILayout.Height(30));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("◀  Back to main menu", btn, GUILayout.Height(30)))
            gm.CloseCampaignMenu();
        GUILayout.EndArea();
    }
}
