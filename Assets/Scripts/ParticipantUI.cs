using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The first screen: who is flying. A code is typed (or picked from the people
/// already on this computer), the visit number is worked out from the data tree,
/// and the hub opens. Shown again only if "Change participant" is used in the hub.
/// </summary>
public class ParticipantUI : MonoBehaviour
{
    string input = "";
    string error = "";
    bool wantFocus = true;
    bool wasReady;
    List<ParticipantProgress.Person> people;
    float peopleAt = -1f;
    Vector2 scroll;

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.ParticipantReady) { wasReady = true; return; }
        if (wasReady) { wasReady = false; wantFocus = true; people = null; }   // came back via Change participant

        if (people == null || Time.unscaledTime - peopleAt > 5f) { people = ParticipantProgress.PeopleOnDisk(); peopleAt = Time.unscaledTime; }

        UiTheme.DimScreen(0.86f);
        UiTheme.Begin();

        float pw = 980f, ph = 520f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 4f), UiTheme.Accent);

        // ── left: the form ──────────────────────────────────────────────────────
        float lx = p.x + 40f, ly = p.y + 34f, lw = 520f;
        UiTheme.Label(new Rect(lx, ly, lw, 36f), "FLIGHT ASSESSMENT SIM", UiTheme.H1);
        UiTheme.Label(new Rect(lx, ly + 36f, lw, 22f), "Cognitive-workload study  ·  Cessna 172  ·  42 missions per participant", UiTheme.Small);

        ly += 92f;
        UiTheme.Label(new Rect(lx, ly, lw, 20f), "PARTICIPANT CODE", UiTheme.Small, UiTheme.Faint);
        ly += 24f;
        GUI.SetNextControlName("pid_field");
        var fr = new Rect(lx, ly, lw, 52f);
        UiTheme.Fill(fr, new Color(0.04f, 0.05f, 0.07f, 1f));
        UiTheme.Frame(fr, string.IsNullOrEmpty(error) ? UiTheme.Line : UiTheme.Bad);
        string typed = GUI.TextField(fr, input, 12, UiTheme.Field);
        if (typed != input) { input = typed.ToUpperInvariant(); error = ""; }
        ly += 60f;
        UiTheme.Label(new Rect(lx, ly, lw, 20f),
            "A study code such as P001 — never a name. A new code starts a new participant.", UiTheme.Small);
        ly += 20f;
        UiTheme.Label(new Rect(lx, ly, lw, 20f),
            "Visits are counted from the data folder, so stopping half-way and coming back later is fine.", UiTheme.Small);
        ly += 30f;
        if (!string.IsNullOrEmpty(error))
            UiTheme.Label(new Rect(lx, ly, lw, 22f), "⚠  " + error, UiTheme.Body, UiTheme.Bad);
        ly += 34f;

        string trimmed = input.Trim();
        bool canGo = trimmed.Length >= 1;
        string preview = "";
        if (canGo)
        {
            var known = people.Find(x => x.Id == trimmed);
            preview = known != null
                ? $"Returning participant — visit {known.Visits + 1}, {known.Done} of {ParticipantProgress.Total} missions done"
                : "New participant — visit 1, nothing flown yet";
        }
        UiTheme.Label(new Rect(lx, ly, lw, 20f), preview, UiTheme.Body, UiTheme.Accent);
        ly += 32f;
        if (UiTheme.Button(new Rect(lx, ly, lw, 54f), "CONTINUE", UiTheme.Accent, canGo, true, 18, TextAnchor.MiddleCenter, "Enter"))
            Confirm(gm);
        if (UiTheme.Key(KeyCode.Return) || UiTheme.Key(KeyCode.KeypadEnter)) Confirm(gm);

        // ── right: people already on this computer ──────────────────────────────
        float rx = p.x + 620f, ry = p.y + 34f, rw = 320f;
        UiTheme.Label(new Rect(rx, ry, rw, 24f), "ON THIS COMPUTER", UiTheme.H3);
        UiTheme.Label(new Rect(rx, ry + 24f, rw, 20f), "Click to pick a returning participant", UiTheme.Small);
        ry += 56f;
        var listRect = new Rect(rx, ry, rw, ph - 120f);
        UiTheme.Fill(listRect, new Color(0.08f, 0.09f, 0.12f, 1f));
        UiTheme.Frame(listRect, UiTheme.Line);
        if (people.Count == 0)
        {
            UiTheme.Label(new Rect(rx + 14f, ry + 14f, rw - 28f, 40f), "No participants yet.\nThe first code typed on the left starts the list.", UiTheme.Small);
        }
        else
        {
            float rowH = 54f;
            var view = new Rect(0, 0, rw - 18f, people.Count * rowH);
            scroll = GUI.BeginScrollView(new Rect(rx + 1f, ry + 1f, rw - 2f, listRect.height - 2f), scroll, view);
            for (int i = 0; i < people.Count; i++)
            {
                var pe = people[i];
                var r = new Rect(4f, i * rowH + 4f, view.width - 8f, rowH - 8f);
                bool sel = pe.Id == trimmed;
                if (UiTheme.Button(r, "", sel ? UiTheme.Accent : UiTheme.Line, true, false, 14, TextAnchor.MiddleLeft))
                { input = pe.Id; error = ""; GUI.FocusControl("pid_field"); }
                UiTheme.Label(new Rect(r.x + 14f, r.y + 4f, 120f, 22f), pe.Id, UiTheme.H3);
                string done = pe.Done + "/" + ParticipantProgress.Total + " done";
                UiTheme.Label(new Rect(r.x + 14f, r.y + 24f, r.width - 28f, 18f),
                    pe.Visits + (pe.Visits == 1 ? " visit  ·  " : " visits  ·  ") + done + (pe.Partial > 0 ? "  ·  " + pe.Partial + " partial" : ""),
                    UiTheme.Small);
                UiTheme.Bar(new Rect(r.xMax - 90f, r.y + 8f, 76f, 12f), (float)pe.Done / ParticipantProgress.Total, UiTheme.Good);
            }
            GUI.EndScrollView();
        }

        UiTheme.Label(new Rect(p.x + 40f, p.yMax - 30f, pw - 80f, 20f),
            "Data folder: " + ExperimentLogger.ExperimentRoot, UiTheme.Small, UiTheme.Faint);

        UiTheme.End();

        if (wantFocus && Event.current.type == EventType.Repaint) { GUI.FocusControl("pid_field"); wantFocus = false; }
    }

    void Confirm(GameManager gm)
    {
        string id = input.Trim().ToUpperInvariant();
        if (id.Length < 1) { error = "Type a participant code first."; return; }
        if (id.Length > 12) { error = "Codes are at most 12 characters."; return; }
        foreach (char c in id)
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') { error = "Letters, digits, - and _ only — it becomes a folder name."; return; }
        error = "";
        ParticipantManager.SetID(id);
        gm.SetParticipantReady();
        ParticipantProgress.Refresh();
    }
}
