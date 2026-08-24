// ExperimentUI — the experimenter's and participant's screens for the session.
//
// Two jobs:
//   1. The RESTING BASELINE screen the participant sees during the rest blocks.
//      Deliberately plain: a black panel, a short instruction, and a countdown.
//      No decoration, no score, nothing to look at that might drive an evoked
//      response or invite eye movement — the point of a rest block is that
//      nothing happens.
//   2. A small EXPERIMENTER STATUS strip during the session (trial n of 12, the
//      realised order, an abort key) so the person running the study can see where
//      they are without alt-tabbing to a log.
//
// The status strip is drawn only in the menu/baseline states, never while flying,
// so it can never become a cue the participant reads mid-trial.

using UnityEngine;

public class ExperimentUI : MonoBehaviour
{
    GUIStyle big, mid, small, mono;
    Texture2D black;

    void Styles()
    {
        if (big != null) return;
        big   = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold,
                                               alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        mid   = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                                               normal = { textColor = new Color(0.88f, 0.9f, 0.95f) } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true,
                                               normal = { textColor = new Color(0.68f, 0.7f, 0.75f) } };
        mono  = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.55f, 0.85f, 0.6f) } };
        black = new Texture2D(1, 1);
        black.SetPixel(0, 0, new Color(0.03f, 0.03f, 0.05f, 1f));
        black.Apply();
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        Styles();

        if (gm.State == GameState.Baseline && gm.Baseline != null && gm.Baseline.Running)
        {
            DrawBaseline(gm);
            return;
        }
        if (gm.State == GameState.Menu && gm.ExperimentRunning) DrawStatusStrip(gm);
    }

    void DrawBaseline(GameManager gm)
    {
        var b = gm.Baseline;
        // Full-screen flat field. For the eyes-closed block the screen is left dark
        // on purpose — no bright panel behind closed lids.
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);

        float remain = Mathf.Max(0f, b.Duration - b.Elapsed);
        float w = 640f, h = 300f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUILayout.BeginArea(r);
        GUILayout.Space(10);
        GUILayout.Label(b.Kind == BaselineKind.RestEyesOpen ? "REST — EYES OPEN" : "REST — EYES CLOSED", big);
        GUILayout.Space(14);
        GUILayout.Label(b.Instruction, mid);
        GUILayout.Space(20);
        // A countdown is shown for the eyes-OPEN block only; during eyes-closed the
        // participant cannot see it, and a visible timer would only encourage peeking.
        if (b.Kind == BaselineKind.RestEyesOpen)
            GUILayout.Label(Mathf.CeilToInt(remain) + " s remaining", mid);
        else
            GUILayout.Label("The experimenter will tell you when to open your eyes.", small);
        GUILayout.Space(16);
        GUILayout.Label("[experimenter]  Esc = end this block early", small);
        GUILayout.EndArea();

        // A fixation cross for the eyes-open block: gives the eyes somewhere to rest
        // so ocular artifact is at least consistent across participants.
        if (b.Kind == BaselineKind.RestEyesOpen)
        {
            var c = new GUIStyle(big) { fontSize = 40 };
            GUI.Label(new Rect(Screen.width / 2f - 30f, Screen.height * 0.78f, 60f, 60f), "+", c);
        }
    }

    void DrawStatusStrip(GameManager gm)
    {
        var r = new Rect(10f, Screen.height - 92f, Screen.width - 20f, 82f);
        GUI.Box(r, "");
        GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height - 12f));
        GUILayout.Label("EXPERIMENT IN PROGRESS   ·   " + gm.ExperimentStatus, mono);
        GUILayout.Label("Order: " + ExperimentSession.OrderSummary(), small);
        GUILayout.Label("Participant " + ParticipantManager.ID + "   session " + ParticipantManager.Session +
                        "   square row " + ExperimentSession.SquareIndex + "   seed " + ExperimentSession.Seed +
                        "   ·   data: " + ExperimentLogger.ExperimentRoot, small);
        GUILayout.EndArea();
    }
}
