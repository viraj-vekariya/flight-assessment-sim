using UnityEngine;

/// <summary>
/// Full-screen participant ID entry screen shown before the main menu.
/// Disappears the moment the ID is confirmed; never shown again during the session.
/// </summary>
public class ParticipantUI : MonoBehaviour
{
    string inputText = "";
    string errorMsg  = "";
    bool   focused   = false;

    GUIStyle bigTitle, subtitle, body, field, btn, err;

    void Styles()
    {
        if (bigTitle != null) return;
        bigTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        subtitle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.75f, 0.85f, 1f) }
        };
        body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
        field = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 38
        };
        btn = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17, fontStyle = FontStyle.Bold, fixedHeight = 40
        };
        err = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, normal = { textColor = new Color(1f, 0.4f, 0.3f) }
        };
    }

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.ParticipantReady) return;
        Styles();

        // dim overlay
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.80f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;

        float pw = 520f, ph = 330f;
        var pr = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
        GUI.Box(pr, "");

        GUILayout.BeginArea(new Rect(pr.x + 40, pr.y + 28, pw - 80, ph - 56));

        GUILayout.Label("CESSNA FLIGHT SIMULATOR", bigTitle);
        GUILayout.Space(4);
        GUILayout.Label("Research Data Collection — Participant Entry", subtitle);
        GUILayout.Space(24);

        GUILayout.Label("Enter your Participant ID  (e.g. P001, P002, CTRL03)", body);
        GUILayout.Space(6);

        GUI.SetNextControlName("pid_field");
        inputText = GUILayout.TextField(inputText, 12, field);
        inputText = inputText.ToUpper();

        if (!string.IsNullOrEmpty(errorMsg))
        {
            GUILayout.Space(4);
            GUILayout.Label(errorMsg, err);
        }
        else
        {
            GUILayout.Space(4 + err.fixedHeight > 0 ? 20 : 4);
        }

        GUILayout.Space(12);

        if (GUILayout.Button("CONFIRM  →", btn))
            TryConfirm(gm);

        // Enter key to confirm
        if (Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return ||
             Event.current.keyCode == KeyCode.KeypadEnter))
        {
            TryConfirm(gm);
            Event.current.Use();
        }

        GUILayout.EndArea();

        // auto-focus the text field on the first frame
        if (!focused)
        {
            GUI.FocusControl("pid_field");
            focused = true;
        }
    }

    void TryConfirm(GameManager gm)
    {
        string id = inputText.Trim();
        if (id.Length < 1)       { errorMsg = "ID cannot be empty."; return; }
        if (id.Length > 12)      { errorMsg = "ID too long (max 12 characters)."; return; }

        errorMsg = "";
        ParticipantManager.SetID(id);
        gm.SetParticipantReady();
    }
}
