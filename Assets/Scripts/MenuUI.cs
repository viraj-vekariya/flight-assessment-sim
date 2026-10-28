using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// THE HUB — everything between the participant code and the aeroplane.
///
/// Left: what to do next. One big NEXT MISSION card (the first unflown mission in
/// this participant's order), then free flight, the lab-session protocol, the
/// control bench and help. Right: the whole 42-mission bank, four ways — the 4 x 3
/// design grid with all three variants of every cell, the six crosswind missions
/// on their own tab, the participant's full order, and every attempt on disk.
///
/// Every mission button opens a BRIEFING first: objective, what will happen, what
/// counts as success, and only then START. Nothing starts from a single click.
///
/// Status marks come from ParticipantProgress, i.e. from trial.json on disk — the
/// same rule the dataset exporter uses — so a mission shows DONE only when a usable
/// trial of it exists, and the hub can never disagree with the analysis.
/// </summary>
public class MenuUI : MonoBehaviour
{
    enum Tab { Grid, Crosswind, Order, Attempts }
    enum Overlay { None, Briefing, LabConfirm, Help }

    Tab tab = Tab.Grid;
    Overlay overlay = Overlay.None;
    string briefId = "";
    bool briefNotes;
    Vector2 orderScroll, attemptScroll, briefScroll;
    float briefContentH;   // measured on the previous frame; 0 = fit the area
    static readonly WorkloadClass[] Classes = { WorkloadClass.Low, WorkloadClass.Medium, WorkloadClass.High };

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.ParticipantReady || gm.State != GameState.Menu) return;
        if (gm.ExperimentRunning) return;   // ExperimentUI owns the screen between session steps

        UiTheme.DimScreen(0.78f);
        UiTheme.Begin();

        DrawHeader(gm);
        DrawLeft(gm);
        DrawRight(gm);
        UiTheme.Label(new Rect(24f, 738f, 1232f, 18f),
            "Enter = brief the next mission     F = free flight     Esc = back     Click any mission to read its briefing",
            UiTheme.Small, UiTheme.Faint);

        switch (overlay)
        {
            case Overlay.Briefing:   DrawBriefing(gm); break;
            case Overlay.LabConfirm: DrawLabConfirm(gm); break;
            case Overlay.Help:       DrawHelp(); break;
            default:
                if (UiTheme.Key(KeyCode.Return) || UiTheme.Key(KeyCode.KeypadEnter))
                { var n = ParticipantProgress.NextMissionId(); if (n != null) OpenBriefing(n); }
                else if (UiTheme.Key(KeyCode.F)) gm.StartFlight(FlightMode.FreeFlight);
                break;
        }
        UiTheme.End();
    }

    void OpenBriefing(string id) { briefId = id; briefNotes = false; briefScroll = Vector2.zero; overlay = Overlay.Briefing; }

    // ── header ──────────────────────────────────────────────────────────────────
    void DrawHeader(GameManager gm)
    {
        UiTheme.Label(new Rect(24f, 14f, 600f, 36f), "FLIGHT ASSESSMENT SIM", UiTheme.H1);
        UiTheme.Label(new Rect(24f, 50f, 700f, 20f),
            "Cognitive-workload study  ·  Cessna 172  ·  36 cognitive-axis missions + 6 crosswind", UiTheme.Small);

        var box = new Rect(760f, 12f, 496f, 64f);
        UiTheme.Box(box);
        int done = ParticipantProgress.DoneCount, total = ParticipantProgress.Total, partial = ParticipantProgress.PartialCount;
        UiTheme.Label(new Rect(box.x + 14f, box.y + 6f, 300f, 24f), ParticipantManager.ID, UiTheme.H2);
        UiTheme.Label(new Rect(box.x + 14f, box.y + 32f, 300f, 20f),
            "visit " + ParticipantManager.Session + "   ·   " + done + " of " + total + " missions done" +
            (partial > 0 ? "   ·   " + partial + " started" : ""), UiTheme.Small);
        UiTheme.Bar(new Rect(box.x + 14f, box.y + 52f, 300f, 6f), (float)done / total, UiTheme.Good);
        if (UiTheme.Button(new Rect(box.x + 330f, box.y + 10f, 110f, 44f), "CHANGE\nPARTICIPANT", UiTheme.Line, true, false, 11))
            gm.ChangeParticipant();
        if (UiTheme.Button(new Rect(box.x + 446f, box.y + 10f, 40f, 44f), "?", UiTheme.Accent, true, false, 18))
            overlay = Overlay.Help;
    }

    // ── left column ─────────────────────────────────────────────────────────────
    void DrawLeft(GameManager gm)
    {
        float x = 24f, w = 420f, y = 92f;

        // NEXT MISSION
        string next = ParticipantProgress.NextMissionId();
        var card = new Rect(x, y, w, 176f);
        UiTheme.Box(card, true);
        if (next != null)
        {
            var m = MissionLibrary.Get(next);
            var col = UiTheme.ClassColor(m.Class);
            UiTheme.Fill(new Rect(card.x, card.y, 5f, card.height), col);
            UiTheme.Label(new Rect(x + 18f, y + 10f, 260f, 20f),
                "NEXT MISSION  ·  " + ParticipantProgress.Position(next) + " of " + ParticipantProgress.Total, UiTheme.Small, UiTheme.Faint);
            float cx = x + 18f;
            cx += UiTheme.Chip(cx, y + 36f, m.ClassTag, col) + 6f;
            cx += UiTheme.Chip(cx, y + 36f, m.Phase.ToString().ToUpper(), UiTheme.Line) + 6f;
            UiTheme.Chip(cx, y + 36f, "V" + m.Variant, UiTheme.Line, 20f, true);
            UiTheme.Label(new Rect(x + 18f, y + 62f, w - 36f, 26f), m.Id + "   " + UiTheme.Short(m.Name, 34), UiTheme.H2);
            UiTheme.Label(new Rect(x + 18f, y + 90f, w - 36f, 20f),
                UiTheme.PhaseBlurb(m.Phase) + "  ·  " + m.DurationS.ToString("F0") + " s", UiTheme.Small);
            if (ParticipantProgress.Of(next) == ParticipantProgress.Status.Partial)
                UiTheme.Label(new Rect(x + 18f, y + 108f, w - 36f, 20f),
                    "Started before but not finished — flying it again replaces nothing, the new trial is added.", UiTheme.Small, UiTheme.Warn);
            if (UiTheme.Button(new Rect(x + 18f, y + 130f, w - 36f, 36f), "READ BRIEFING  →", col, true, true, 15, TextAnchor.MiddleCenter, "Enter"))
                OpenBriefing(next);
        }
        else
        {
            UiTheme.Fill(new Rect(card.x, card.y, 5f, card.height), UiTheme.Good);
            UiTheme.Label(new Rect(x + 18f, y + 14f, w - 36f, 28f), "ALL " + ParticipantProgress.Total + " MISSIONS DONE", UiTheme.H2, UiTheme.Good);
            UiTheme.Label(new Rect(x + 18f, y + 48f, w - 36f, 60f),
                "Every mission has a usable trial on disk for this participant. Any mission can still be flown again from the grid.", UiTheme.Small);
        }
        y += 190f;

        // FREE FLIGHT
        if (UiTheme.Button(new Rect(x, y, w, 50f), "FREE FLIGHT", UiTheme.Accent, true, false, 16, TextAnchor.MiddleLeft, "F"))
            gm.StartFlight(FlightMode.FreeFlight);
        UiTheme.Label(new Rect(x + 16f, y + 52f, w - 16f, 18f), "Learn the aeroplane. Nothing is recorded. Esc returns here.", UiTheme.Small);
        y += 80f;

        // LAB SESSION
        if (UiTheme.Button(new Rect(x, y, w, 50f), "LAB SESSION  ·  12 trials + rest blocks", UiTheme.Violet, true, false, 15, TextAnchor.MiddleLeft))
            overlay = Overlay.LabConfirm;
        UiTheme.Label(new Rect(x + 16f, y + 52f, w - 16f, 18f), "EEG protocol: rest, this visit's 12 in order, rest. About 75 min.", UiTheme.Small);
        y += 80f;

        if (UiTheme.Button(new Rect(x, y, 206f, 40f), "CONTROL CHECK", UiTheme.Line, true, false, 13))
            ControlCheckMode.Enter();
        if (UiTheme.Button(new Rect(x + 214f, y, 206f, 40f), "HELP & CONTROLS", UiTheme.Line, true, false, 13))
            overlay = Overlay.Help;
        y += 56f;

        // STATUS
        var sb = new Rect(x, y, w, 724f - y);
        UiTheme.Box(sb);
        float sy = sb.y + 10f;
        UiTheme.Label(new Rect(x + 14f, sy, 200f, 20f), "RECORDING STATUS", UiTheme.H3); sy += 26f;
        StatusLine(x + 14f, ref sy, "Audio", AudioPolicy.ValidForExperiment ? "experiment profile" : "profile " + AudioPolicy.Profile + " — not valid for the study", AudioPolicy.ValidForExperiment);
        StatusLine(x + 14f, ref sy, "LSL", LSLSync.Available ? "stream open — EEG clock shared" : "not available — align by t_host", LSLSync.Available, true);
        StatusLine(x + 14f, ref sy, "Input", UiTheme.Short(HardwareInput.StatusLine(), 44), true);
        StatusLine(x + 14f, ref sy, "VR", UiTheme.Short(VRRuntime.StatusLine, 44), true);
        UiTheme.Label(new Rect(x + 14f, sy + 4f, w - 28f, 18f), "Data: " + UiTheme.Short(ExperimentLogger.ExperimentRoot, 58), UiTheme.Small, UiTheme.Faint);
    }

    void StatusLine(float x, ref float y, string k, string v, bool ok, bool soft = false)
    {
        var dot = new Rect(x, y + 6f, 8f, 8f);
        UiTheme.Fill(dot, ok ? UiTheme.Good : soft ? UiTheme.Warn : UiTheme.Bad);
        UiTheme.Label(new Rect(x + 16f, y, 50f, 20f), k, UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 66f, y, 340f, 20f), v, UiTheme.Small, ok ? UiTheme.Dim : soft ? UiTheme.Warn : UiTheme.Bad);
        y += 22f;
    }

    // ── right column ────────────────────────────────────────────────────────────
    void DrawRight(GameManager gm)
    {
        float x = 468f, w = 788f, y = 92f;
        string[] names = { "MISSION GRID", "CROSSWIND", "MY ORDER", "ATTEMPTS" };
        float tw = w / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(x + i * tw, y, tw - 4f, 34f);
            bool sel = (int)tab == i;
            UiTheme.Fill(r, sel ? UiTheme.PanelHi : UiTheme.Panel);
            UiTheme.Frame(r, sel ? UiTheme.Accent : UiTheme.Line);
            if (sel) UiTheme.Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), UiTheme.Accent);
            var st = UiTheme.H3; var pa = st.alignment; st.alignment = TextAnchor.MiddleCenter;
            UiTheme.Label(r, names[i], st, sel ? UiTheme.Text : UiTheme.Dim); st.alignment = pa;
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) tab = (Tab)i;
        }
        var panel = new Rect(x, y + 38f, w, 594f);
        UiTheme.Box(panel);
        switch (tab)
        {
            case Tab.Grid:      DrawGrid(panel); break;
            case Tab.Crosswind: DrawCrosswind(panel); break;
            case Tab.Order:     DrawOrder(panel); break;
            case Tab.Attempts:  DrawAttempts(panel); break;
        }
    }

    void DrawGrid(Rect p)
    {
        float x = p.x + 14f, y = p.y + 12f, phaseW = 150f, colW = 200f, gap = 6f;
        UiTheme.Label(new Rect(x, y, phaseW, 22f), "PHASE", UiTheme.Small, UiTheme.Faint);
        for (int c = 0; c < 3; c++)
        {
            var hr = new Rect(x + phaseW + c * (colW + gap), y, colW, 22f);
            UiTheme.Fill(hr, UiTheme.ClassColor(Classes[c]) * new Color(1, 1, 1, 0.18f));
            var st = UiTheme.H3; var pa = st.alignment; st.alignment = TextAnchor.MiddleCenter;
            UiTheme.Label(hr, Classes[c].ToString().ToUpper() + " WORKLOAD", st, UiTheme.ClassColor(Classes[c])); st.alignment = pa;
        }
        y += 30f;
        float rowH = 126f;
        foreach (var ph in MissionLibrary.Rows)
        {
            UiTheme.Rule(x, y - 4f, p.width - 28f);
            UiTheme.Label(new Rect(x, y + 4f, phaseW - 10f, 22f), ph.ToString().ToUpper(), UiTheme.H3);
            UiTheme.Paragraph(x, y + 28f, phaseW - 14f, UiTheme.PhaseBlurb(ph), 12, UiTheme.Dim);
            for (int c = 0; c < 3; c++)
            {
                float bx = x + phaseW + c * (colW + gap);
                for (int v = 1; v <= MissionLibrary.VariantCount; v++)
                {
                    var m = MissionLibrary.Cell(ph, Classes[c], v);
                    if (m == null) continue;
                    MissionButton(new Rect(bx, y + 2f + (v - 1) * 40f, colW, 36f), m, 12);
                }
            }
            y += rowH;
        }
        UiTheme.Rule(x, y - 4f, p.width - 28f);
        Legend(x, p.yMax - 26f);
    }

    void Legend(float x, float y)
    {
        float cx = x;
        cx += UiTheme.Chip(cx, y, "DONE", UiTheme.Good, 18f) + 6f;
        UiTheme.Label(new Rect(cx, y, 120f, 18f), "usable trial on disk", UiTheme.Small); cx += 128f;
        cx += UiTheme.Chip(cx, y, "STARTED", UiTheme.Warn, 18f) + 6f;
        UiTheme.Label(new Rect(cx, y, 150f, 18f), "flown, not usable yet", UiTheme.Small); cx += 150f;
        cx += UiTheme.Chip(cx, y, "NEXT", UiTheme.Accent, 18f, true) + 6f;
        UiTheme.Label(new Rect(cx, y, 200f, 18f), "first unflown in your order", UiTheme.Small);
    }

    /// <summary>One mission as a button with its status on the right. Opens the briefing.</summary>
    void MissionButton(Rect r, MissionDefinition m, int size)
    {
        var st = ParticipantProgress.Of(m.Id);
        bool isNext = ParticipantProgress.NextMissionId() == m.Id;
        string hint = st == ParticipantProgress.Status.Done ? "DONE" : st == ParticipantProgress.Status.Partial ? "STARTED" : isNext ? "NEXT" : "";
        var col = UiTheme.ClassColor(m.Class);
        int chars = size <= 12 ? 18 : 40;
        if (hint != "") chars -= size <= 12 ? 7 : 10;   // leave room for the status word on the right
        if (UiTheme.Button(r, m.Id + "   " + UiTheme.Short(m.Name, chars), col, true, false, size, TextAnchor.MiddleLeft, hint))
            OpenBriefing(m.Id);
        if (isNext) UiTheme.Frame(r, UiTheme.Accent);
        if (st == ParticipantProgress.Status.Done) UiTheme.Fill(new Rect(r.xMax - 6f, r.y, 6f, r.height), UiTheme.Good);
        else if (st == ParticipantProgress.Status.Partial) UiTheme.Fill(new Rect(r.xMax - 6f, r.y, 6f, r.height), UiTheme.Warn);
    }

    void DrawCrosswind(Rect p)
    {
        float x = p.x + 14f, y = p.y + 12f, w = p.width - 28f;
        UiTheme.Label(new Rect(x, y, w, 22f), "CROSSWIND  ·  the second axis", UiTheme.H3); y += 24f;
        y += UiTheme.Paragraph(x, y, w,
            "Six missions with a crosswind on the runway. They raise the demand on the hands rather than on " +
            "attention, so they are analysed separately from the Low / Medium / High scale and are never pooled " +
            "with it. They come last in your order, after the 36 cognitive-axis missions.", 13, UiTheme.Dim) + 14f;
        foreach (var m in MissionLibrary.PsychomotorAxis())
        {
            MissionButton(new Rect(x, y, w - 170f, 44f), m, 14);
            UiTheme.Label(new Rect(x + w - 160f, y, 160f, 44f),
                m.WindSpeedMs.ToString("F1") + " m/s wind  ·  " + m.Phase.ToString().ToLower(), UiTheme.Small);
            y += 50f;
        }
        Legend(x, p.yMax - 26f);
    }

    void DrawOrder(Rect p)
    {
        float x = p.x + 14f, y = p.y + 12f, w = p.width - 28f;
        UiTheme.Label(new Rect(x, y, w, 22f), "YOUR ORDER  ·  all " + ParticipantProgress.Total + " missions", UiTheme.H3); y += 24f;
        y += UiTheme.Paragraph(x, y, w,
            "Fixed for this participant code, so it is the same on any computer and on any day. The first twelve are " +
            "this visit's lab-session set; every later block uses a different variant of each cell. The crosswind six " +
            "come last. You can fly out of order from the grid; NEXT is simply the first one not yet done.", 12, UiTheme.Dim) + 10f;
        var order = ParticipantProgress.FullOrder();
        float rowH = 30f;
        var view = new Rect(0, 0, w - 18f, order.Count * rowH + 4f);
        var area = new Rect(x, y, w, p.yMax - y - 12f);
        orderScroll = GUI.BeginScrollView(area, orderScroll, view);
        string next = ParticipantProgress.NextMissionId();
        for (int i = 0; i < order.Count; i++)
        {
            var m = MissionLibrary.Get(order[i]);
            if (m == null) continue;
            float ry = i * rowH;
            if (i == 12 || i == 24 || i == 36) UiTheme.Rule(0, ry - 1f, view.width);
            UiTheme.Label(new Rect(0, ry, 34f, rowH), (i + 1).ToString(), UiTheme.Small, UiTheme.Faint);
            MissionButton(new Rect(36f, ry + 2f, 420f, rowH - 4f), m, 13);
            UiTheme.Chip(466f, ry + 6f, m.ClassTag, UiTheme.ClassColor(m.Class), 18f);
            UiTheme.Label(new Rect(546f, ry, 120f, rowH), m.Phase.ToString(), UiTheme.Small);
            UiTheme.Label(new Rect(640f, ry, 100f, rowH), m.Axis == LoadAxis.Cognitive ? "v" + m.Variant : "crosswind", UiTheme.Small, UiTheme.Faint);
        }
        GUI.EndScrollView();
    }

    void DrawAttempts(Rect p)
    {
        float x = p.x + 14f, y = p.y + 12f, w = p.width - 28f;
        var trials = ParticipantProgress.Trials;
        UiTheme.Label(new Rect(x, y, w, 22f), "ATTEMPTS ON DISK  ·  " + trials.Count + " trial folder" + (trials.Count == 1 ? "" : "s"), UiTheme.H3); y += 24f;
        y += UiTheme.Paragraph(x, y, w,
            "Every trial folder this participant has, newest visit first. A trial is USABLE when it ran to the end " +
            "(rows ≥ 99 % of expected) with no recording gap over 2 s. Only usable trials mark a mission DONE.", 12, UiTheme.Dim) + 10f;
        if (trials.Count == 0)
        {
            UiTheme.Label(new Rect(x, y + 10f, w, 22f), "Nothing recorded yet. The first mission flown appears here.", UiTheme.Body, UiTheme.Dim);
            return;
        }
        UiTheme.Label(new Rect(x, y, 70f, 20f), "VISIT", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 70f, y, 100f, 20f), "TRIAL", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 170f, y, 90f, 20f), "MISSION", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 260f, y, 150f, 20f), "ROWS", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 420f, y, 120f, 20f), "VERDICT", UiTheme.Small, UiTheme.Faint);
        UiTheme.Label(new Rect(x + 560f, y, 180f, 20f), "WHEN", UiTheme.Small, UiTheme.Faint);
        y += 22f;
        float rowH = 26f;
        var view = new Rect(0, 0, w - 18f, trials.Count * rowH + 4f);
        attemptScroll = GUI.BeginScrollView(new Rect(x, y, w, p.yMax - y - 12f), attemptScroll, view);
        for (int i = 0; i < trials.Count; i++)
        {
            var t = trials[i];
            float ry = i * rowH;
            if (i % 2 == 1) UiTheme.Fill(new Rect(0, ry, view.width, rowH), new Color(1, 1, 1, 0.03f));
            UiTheme.Label(new Rect(0, ry, 70f, rowH), t.Visit.Length > 3 ? t.Visit.Substring(0, 3) : t.Visit, UiTheme.Small);
            UiTheme.Label(new Rect(70f, ry, 100f, rowH), t.Tag.Length > 3 ? t.Tag.Substring(0, 3) : t.Tag, UiTheme.Small);
            var m = MissionLibrary.Get(t.MissionId);
            UiTheme.Label(new Rect(170f, ry, 90f, rowH), t.MissionId, UiTheme.Body, m != null ? UiTheme.ClassColor(m.Class) : UiTheme.Text);
            UiTheme.Label(new Rect(260f, ry, 150f, rowH), t.HasJson ? t.Rows + " / " + t.Expected : "no trial.json", UiTheme.Small);
            string verdict = !t.HasJson ? "not closed" : !t.Complete ? "incomplete" : !t.Continuous ? "gap > 2 s" : "USABLE";
            UiTheme.Label(new Rect(420f, ry, 120f, rowH), verdict, UiTheme.Small, t.Usable ? UiTheme.Good : UiTheme.Warn);
            UiTheme.Label(new Rect(560f, ry, 180f, rowH), t.When.ToString("yyyy-MM-dd HH:mm"), UiTheme.Small, UiTheme.Faint);
        }
        GUI.EndScrollView();
    }

    // ── briefing ────────────────────────────────────────────────────────────────
    void DrawBriefing(GameManager gm)
    {
        var m = MissionLibrary.Get(briefId);
        if (m == null) { overlay = Overlay.None; return; }
        var col = UiTheme.ClassColor(m.Class);
        UiTheme.Fill(new Rect(0, 0, UiTheme.W, UiTheme.H), new Color(0, 0, 0, 0.72f));
        float pw = 1040f, ph = 640f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 5f), col);

        float x = p.x + 30f, y = p.y + 22f;
        UiTheme.Label(new Rect(x, y, 300f, 20f), "MISSION BRIEFING  ·  " + ParticipantProgress.Position(m.Id) + " of " + ParticipantProgress.Total, UiTheme.Small, UiTheme.Faint);
        y += 24f;
        UiTheme.Label(new Rect(x, y, pw - 60f, 36f), m.Id + "   " + m.Name, UiTheme.H1);
        y += 42f;
        float cx = x;
        cx += UiTheme.Chip(cx, y, m.ClassTag + " WORKLOAD", col) + 8f;
        cx += UiTheme.Chip(cx, y, m.Phase.ToString().ToUpper(), UiTheme.Line) + 8f;
        if (m.Axis == LoadAxis.Cognitive) cx += UiTheme.Chip(cx, y, "VARIANT " + m.Variant, UiTheme.Line, 20f, true) + 8f;
        else cx += UiTheme.Chip(cx, y, "CROSSWIND AXIS", UiTheme.Violet) + 8f;
        var st = ParticipantProgress.Of(m.Id);
        if (st == ParticipantProgress.Status.Done) cx += UiTheme.Chip(cx, y, "DONE — flying it again adds a second trial", UiTheme.Good) + 8f;
        else if (st == ParticipantProgress.Status.Partial) cx += UiTheme.Chip(cx, y, "STARTED " + ParticipantProgress.Attempts(m.Id) + "x, not usable yet", UiTheme.Warn) + 8f;
        y += 34f;

        // left: the brief
        float lw = 620f;
        var area = new Rect(x, y, lw, p.yMax - y - 76f);
        briefScroll = GUI.BeginScrollView(area, briefScroll, new Rect(0, 0, lw - 18f, Mathf.Max(briefContentH, area.height - 2f)));
        float ly = 0f, cw = lw - 24f;
        ly = Section(ly, cw, "OBJECTIVE", m.Objective, 15);
        ly = Section(ly, cw, "WHAT WILL HAPPEN", m.Brief, 13);
        ly = Section(ly, cw, "COUNTS AS SUCCESS", m.SuccessCriteria, 13);
        ly = Section(ly, cw, "ENDS THE MISSION", m.FailureConditions, 13);
        if (briefNotes)
        {
            ly += 6f; UiTheme.Rule(0, ly, cw); ly += 10f;
            UiTheme.Label(new Rect(0, ly, cw, 20f), "RESEARCHER NOTES  (never shown to a participant during a trial)", UiTheme.Small, UiTheme.Violet); ly += 24f;
            ly = Section(ly, cw, "WHY THIS CLASS", m.LoadRationale, 12);
            ly = Section(ly, cw, "EEG RELEVANCE", m.EegRelevance, 12);
            ly = Section(ly, cw, "EXPECTED ERRORS", m.ExpectedErrors, 12);
            ly = Section(ly, cw, "AVIATION BASIS", m.AviationBasis, 12);
            ly = Section(ly, cw, "NOT MODELLED", m.Approximations, 12);
            ly = Section(ly, cw, "PREDICTED LOAD INDEX", m.Profile.PLI.ToString("F0") + " / 100  (a prediction from the task analysis, never a measurement)", 12);
        }
        if (Event.current.type == EventType.Repaint) briefContentH = ly;
        GUI.EndScrollView();

        // right: facts
        float rx = x + lw + 30f, ry = y, rw = pw - 60f - lw - 30f;
        UiTheme.Box(new Rect(rx, ry, rw, 236f));
        float fy = ry + 12f;
        UiTheme.Label(new Rect(rx + 14f, fy, rw, 20f), "AT A GLANCE", UiTheme.H3); fy += 28f;
        UiTheme.KV(rx + 14f, fy, 92f, "Duration", m.DurationS.ToString("F0") + " s"); fy += 24f;
        UiTheme.KV(rx + 14f, fy, 92f, "Baseline", "first " + m.InTaskBaselineS.ToString("F0") + " s, just fly"); fy += 24f;
        UiTheme.KV(rx + 14f, fy, 92f, "Starts", m.Start == ScenarioStart.Airborne ? "airborne, " + m.StartAltitudeM.ToString("F0") + " m" : "on the ground"); fy += 24f;
        UiTheme.KV(rx + 14f, fy, 92f, "Phase", UiTheme.PhaseBlurb(m.Phase)); fy += 24f;
        UiTheme.KV(rx + 14f, fy, 92f, "Wind", m.WindSpeedMs > 0.1f ? m.WindSpeedMs.ToString("F1") + " m/s from " + m.WindFromDeg.ToString("F0") + "°" : "calm"); fy += 24f;
        UiTheme.KV(rx + 14f, fy, 92f, "Attempts", ParticipantProgress.Attempts(m.Id).ToString()); fy += 24f;
        UiTheme.KV(rx + 14f, fy, 92f, "Recorded", "telemetry 50 Hz + events", UiTheme.Dim);

        ry += 250f;
        UiTheme.Box(new Rect(rx, ry, rw, 150f));
        fy = ry + 12f;
        UiTheme.Label(new Rect(rx + 14f, fy, rw, 20f), "DURING THE MISSION", UiTheme.H3); fy += 26f;
        fy += UiTheme.Paragraph(rx + 14f, fy, rw - 28f,
            "SPACE acknowledges an alarm or ticks a checklist item.\n" +
            "R restarts the mission from the top.\n" +
            "Esc abandons it and returns to the hub (the trial is kept, marked incomplete).\n" +
            "C changes the camera view.", 12, UiTheme.Dim);

        if (UiTheme.Button(new Rect(rx, ry + 164f, rw, 30f), briefNotes ? "HIDE RESEARCHER NOTES" : "SHOW RESEARCHER NOTES", UiTheme.Violet, true, false, 11))
            briefNotes = !briefNotes;

        // footer
        float by = p.yMax - 60f;
        if (UiTheme.Button(new Rect(x, by, 180f, 42f), "BACK", UiTheme.Line, true, false, 14, TextAnchor.MiddleCenter, "Esc"))
            overlay = Overlay.None;
        if (UiTheme.Button(new Rect(p.xMax - 30f - 360f, by, 360f, 42f), "START MISSION  " + m.Id, col, true, true, 16, TextAnchor.MiddleCenter, "Enter"))
            StartMission(gm, m.Id);
        if (UiTheme.Key(KeyCode.Return) || UiTheme.Key(KeyCode.KeypadEnter)) StartMission(gm, m.Id);
        else if (UiTheme.Key(KeyCode.Escape)) overlay = Overlay.None;
    }

    float Section(float y, float w, string head, string text, int size)
    {
        if (string.IsNullOrEmpty(text)) return y;
        UiTheme.Label(new Rect(0, y, w, 18f), head, UiTheme.Small, UiTheme.Faint);
        y += 20f;
        y += UiTheme.Paragraph(0, y, w, text, size);
        return y + 14f;
    }

    void StartMission(GameManager gm, string id)
    {
        overlay = Overlay.None;
        gm.StartSingleMission(id);
    }

    // ── lab session confirmation ───────────────────────────────────────────────
    void DrawLabConfirm(GameManager gm)
    {
        UiTheme.Fill(new Rect(0, 0, UiTheme.W, UiTheme.H), new Color(0, 0, 0, 0.72f));
        float pw = 820f, ph = 520f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 5f), UiTheme.Violet);
        float x = p.x + 30f, y = p.y + 24f, w = pw - 60f;
        UiTheme.Label(new Rect(x, y, w, 32f), "START THE LAB SESSION?", UiTheme.H1); y += 40f;
        UiTheme.Label(new Rect(x, y, w, 20f), "Participant " + ParticipantManager.ID + "  ·  visit " + ParticipantManager.Session, UiTheme.Body, UiTheme.Dim); y += 30f;

        y += UiTheme.Paragraph(x, y, w,
            "This is the EEG protocol, run with the amplifier connected. The sequence is fixed:", 14) + 10f;
        string[] steps =
        {
            "1   Rest, eyes open — 120 s, fixation cross",
            "2   Rest, eyes closed — 120 s",
            "3   Twelve missions in the order below, each 300 s, results page between them (Enter continues)",
            "4   Rest, eyes open — 120 s",
        };
        foreach (var s in steps) { UiTheme.Label(new Rect(x + 10f, y, w, 22f), s, UiTheme.Body); y += 24f; }
        y += 8f;

        int pnum = ExperimentSession.StableNumber(ParticipantManager.ID) + (ParticipantManager.Session - 1);
        var ids = new List<string>();
        foreach (var m in MissionLibrary.SessionMissions(pnum)) ids.Add(m.Id);
        ids = ExperimentSession.DeClumpClasses(ExperimentSession.BalancedLatinSquareRow(ids, pnum % 12));
        UiTheme.Label(new Rect(x, y, w, 20f), "THIS VISIT'S TWELVE, IN ORDER", UiTheme.Small, UiTheme.Faint); y += 24f;
        float cx = x;
        foreach (var id in ids)
        {
            var m = MissionLibrary.Get(id);
            if (m == null) continue;
            float cwid = UiTheme.Chip(cx, y, id, UiTheme.ClassColor(m.Class), 22f);
            cx += cwid + 6f;
        }
        y += 34f;
        y += UiTheme.Paragraph(x, y, w,
            "About 75 minutes. Esc during a rest block ends that block early; Esc during a mission abandons the whole " +
            "session (everything recorded so far is kept). Any mission flown earlier today from the grid is not re-used.",
            12, UiTheme.Dim);

        float by = p.yMax - 64f;
        if (UiTheme.Button(new Rect(x, by, 180f, 44f), "CANCEL", UiTheme.Line, true, false, 14, TextAnchor.MiddleCenter, "Esc"))
            overlay = Overlay.None;
        if (UiTheme.Button(new Rect(p.xMax - 30f - 320f, by, 320f, 44f), "START SESSION", UiTheme.Violet, true, true, 16, TextAnchor.MiddleCenter, "Enter"))
        { overlay = Overlay.None; gm.StartExperimentSession(); }
        if (UiTheme.Key(KeyCode.Return) || UiTheme.Key(KeyCode.KeypadEnter)) { overlay = Overlay.None; gm.StartExperimentSession(); }
        else if (UiTheme.Key(KeyCode.Escape)) overlay = Overlay.None;
    }

    // ── help ────────────────────────────────────────────────────────────────────
    void DrawHelp()
    {
        UiTheme.Fill(new Rect(0, 0, UiTheme.W, UiTheme.H), new Color(0, 0, 0, 0.72f));
        float pw = 960f, ph = 560f;
        var p = new Rect((UiTheme.W - pw) / 2f, (UiTheme.H - ph) / 2f, pw, ph);
        UiTheme.Box(p);
        UiTheme.Fill(new Rect(p.x, p.y, pw, 5f), UiTheme.Accent);
        float x = p.x + 30f, y = p.y + 24f, cw = (pw - 90f) / 2f;
        UiTheme.Label(new Rect(x, y, pw, 32f), "HELP & CONTROLS", UiTheme.H1); y += 44f;

        float ly = y;
        ly = HelpBlock(x, ly, cw, "HOW A VISIT GOES",
            "Fly FREE FLIGHT until the aeroplane feels familiar. Then take missions one at a time from NEXT MISSION; " +
            "each one is briefed before it starts and recorded automatically. Stop whenever you like — the next visit " +
            "carries on from the first mission not yet done. The LAB SESSION button is the EEG protocol for the lab only.");
        ly = HelpBlock(x, ly, cw, "FLYING",
            "W / S  pitch        A / D  roll        Q / E  rudder\n" +
            "Shift / Ctrl  throttle        F  flaps        B  brakes        [  ]  trim\n" +
            "C  camera view        H  developer dials        R  restart        Esc  back");
        ly = HelpBlock(x, ly, cw, "COCKPIT (MOUSE)",
            "Left-drag the yoke, throttle, trim wheel, flap and brake levers. Click switches. Right-drag to look around.");

        float rx = x + cw + 30f, ry = y;
        ry = HelpBlock(rx, ry, cw, "SYSTEMS",
            "H  carburettor heat        J  fuel selector\n" +
            "K  shed electrical load        L  alternate static");
        ry = HelpBlock(rx, ry, cw, "DURING A MISSION",
            "SPACE acknowledges an alarm or ticks the current checklist item. Follow the instruction banner; the status " +
            "bar at the bottom shows the current target. The first 60 s are the in-task baseline — just fly.");
        ry = HelpBlock(rx, ry, cw, "WHAT COUNTS",
            "A mission is DONE when a trial ran to the end with no recording gap over 2 s. A crash or an early Esc " +
            "keeps the data but marks it STARTED; fly it again. All 42 are flown eventually; the order is yours.");
        ry = HelpBlock(rx, ry, cw, "VR",
            VRRuntime.StatusLine + "\nIn VR: grip grabs a control, trigger presses a switch, A / X acknowledges, both grips + triggers recentre.");

        if (UiTheme.Button(new Rect(p.xMax - 30f - 200f, p.yMax - 60f, 200f, 42f), "CLOSE", UiTheme.Accent, true, true, 14, TextAnchor.MiddleCenter, "Esc"))
            overlay = Overlay.None;
        if (UiTheme.Key(KeyCode.Escape) || UiTheme.Key(KeyCode.Return)) overlay = Overlay.None;
    }

    float HelpBlock(float x, float y, float w, string head, string text)
    {
        UiTheme.Label(new Rect(x, y, w, 18f), head, UiTheme.Small, UiTheme.Accent);
        y += 20f;
        y += UiTheme.Paragraph(x, y, w, text, 13);
        return y + 16f;
    }
}
