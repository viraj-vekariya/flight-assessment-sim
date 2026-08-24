using UnityEngine;

public class Hud2D : MonoBehaviour
{
    bool show = true;

    Texture2D bg, accent, barBg, labelBg, labelBgDark;
    GUIStyle titleStyle, labelStyle, valueStyle, unitStyle, warnStyle,
             statusStyle, dialNameStyle, dialValueStyle;

    // Each dial's column X + name + unit. The dial CENTRE is reconstructed at draw
    // time as (localX, rowY, DialZ) — first 3 entries are the top row, last 3 the
    // bottom row (matches CockpitBuilder RowTop/RowBot/PanelZ/Col). The label is then
    // drawn INSIDE that dial's face at 6 o'clock, so it can never overlap a dial.
    const float DialZ = 1.30f, RowTopY = 0.30f, RowBotY = 0.06f;
    static readonly (float localX, string name, string unit)[] Dials =
    {
        (-0.28f, "AIRSPEED",   "km/h"),   // top row
        ( 0.00f, "ATTITUDE",   ""),
        ( 0.28f, "ALTIMETER",  "m"),
        (-0.28f, "TURN COORD", ""),       // bottom row
        ( 0.00f, "HEADING",    "°"),
        ( 0.28f, "VERT SPEED", "m/s"),
    };


    void Update() { if (Input.GetKeyDown(KeyCode.H)) show = !show; }

    void Init()
    {
        if (bg != null) return;
        bg          = Tex(new Color(0.05f, 0.06f, 0.09f, 0.82f));
        accent      = Tex(new Color(0.16f, 0.72f, 0.86f, 1f));
        barBg       = Tex(new Color(0.05f, 0.06f, 0.09f, 0.72f));
        labelBg     = Tex(new Color(0.06f, 0.06f, 0.08f, 0.88f));
        labelBgDark = Tex(new Color(0.02f, 0.02f, 0.03f, 0.95f));

        titleStyle     = S(12, FontStyle.Bold, TextAnchor.MiddleLeft,   new Color(0.03f,0.06f,0.08f));
        labelStyle     = S(13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.58f,0.64f,0.70f));
        valueStyle     = S(16, FontStyle.Bold,   TextAnchor.MiddleRight,new Color(0.55f,1.00f,0.65f));
        unitStyle      = S(10, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.50f,0.55f,0.60f));
        warnStyle      = S(24, FontStyle.Bold,   TextAnchor.MiddleCenter,new Color(1f,0.30f,0.30f));
        statusStyle    = S(14, FontStyle.Normal, TextAnchor.MiddleCenter,Color.white);
        // dial label: bright white name on dark background (name small — full names
        // are already in the left HUD panel; the value is the useful bit here)
        dialNameStyle  = S(9,  FontStyle.Bold,   TextAnchor.UpperCenter, new Color(0.85f,0.92f,1.00f));
        dialValueStyle = S(13, FontStyle.Bold,   TextAnchor.LowerCenter, new Color(0.40f,1.00f,0.55f));
    }

    static GUIStyle S(int size, FontStyle fs, TextAnchor anchor, Color col) =>
        new GUIStyle { fontSize = size, fontStyle = fs, alignment = anchor,
                       normal = { textColor = col } };

    /// <summary>Developer dials OFF by default. They repeat what the PFD already shows, so
    /// for a participant they are duplicated diagnostic clutter in the middle of the view.
    /// Turn them on with the -hud command-line flag, or from the control bench, when
    /// debugging. Nothing about the experiment reads them.</summary>
    public static bool ShowDeveloperDials;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ReadHudFlag()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-hud") { ShowDeveloperDials = true; return; }
    }

    void OnGUI()
    {
        if (!ShowDeveloperDials && !ControlCheckMode.Active) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Flying || gm.Aircraft == null) return;
        Init();
        var ac = gm.Aircraft;

        // ── Left-corner flight data panel ──────────────────────────────────
        if (show)
        {
            const float x = 14, y = 14, w = 214, rh = 26;

            // extra rows: AoA, Pitch, Roll, optional HDG-error
            var eng = gm.ScenarioRunner;
            int extraRows = 3 + (eng != null && eng.Active ? 1 : 0);
            float h = 30 + (7 + extraRows) * rh + 6;   // 7 base rows incl. SPOIL

            GUI.DrawTexture(new Rect(x, y, w, h), bg);
            GUI.DrawTexture(new Rect(x, y, 4, h), accent);
            GUI.DrawTexture(new Rect(x, y, w, 26), accent);
            GUI.Label(new Rect(x + 14, y, w - 16, 26), "FLIGHT DATA", titleStyle);

            float ry = y + 32;
            Row(x, ry, w, "SPD",   ac.AirspeedKmh.ToString("F0"),   "km/h"); ry += rh;
            Row(x, ry, w, "ALT",   ac.AltitudeM.ToString("F0"),     "m");    ry += rh;
            Row(x, ry, w, "HDG",   ac.HeadingDeg.ToString("F0"),    "°");    ry += rh;
            Row(x, ry, w, "V/S",   ac.VerticalSpeedMs.ToString("F1"),"m/s"); ry += rh;
            Row(x, ry, w, "THR",   (ac.Throttle01*100f).ToString("F0"),"%"); ry += rh;
            Row(x, ry, w, "FLAP",  (ac.Flaps01*100f).ToString("F0"),  "%"); ry += rh;
            Row(x, ry, w, "SPOIL", (ac.Spoiler01*100f).ToString("F0"),"%"); ry += rh;
            Row(x, ry, w, "AoA",   ac.AoADeg.ToString("F1"),         "°");  ry += rh;
            Row(x, ry, w, "PITCH", ac.PitchDeg.ToString("F1"),       "°");  ry += rh;
            Row(x, ry, w, "ROLL",  ac.RollDeg.ToString("F1"),        "°");
            if (eng != null && eng.Active)
            {
                ry += rh;
                float herr = Mathf.Abs(Mathf.DeltaAngle(ac.HeadingDeg, eng.CurTargetHdg));
                Row(x, ry, w, "ΔHDG", herr.ToString("F0"), "°");
            }
        }

        // ── Cockpit dial name overlays (only in cockpit view) ───────────────
        // Only show when the panel is visible and we are in the cockpit view.
        // CockpitCamera is the main camera while in cockpit view; it lives on
        // the aircraft. If the camera is looking at the panel (z>0 projected),
        // each dial label floats just below that dial — always crisp because
        // it is 2D GUI, not 3D TextMesh.
        if (show)
        {
            var cam = Camera.main;
            if (cam != null && gm.Aircraft != null)
            {
                Transform acT = gm.Aircraft.transform;
                string[] liveVals =
                {
                    ac.AirspeedKmh.ToString("F0") + " km/h",
                    ac.PitchDeg.ToString("F1") + "° / " + ac.RollDeg.ToString("F1") + "°",
                    ac.AltitudeM.ToString("F0") + " m",
                    ac.YawRateDps.ToString("F1") + " °/s",
                    ac.HeadingDeg.ToString("F0") + "°",
                    ac.VerticalSpeedMs.ToString("F1") + " m/s",
                };

                const float boxW = 84f, nameH = 13f, valH = 14f;
                for (int i = 0; i < Dials.Length; i++)
                {
                    // dial centre (top row for the first 3, bottom row for the rest)
                    Vector3 wp = acT.TransformPoint(new Vector3(Dials[i].localX, i < 3 ? RowTopY : RowBotY, DialZ));
                    Vector3 sp = cam.WorldToScreenPoint(wp);
                    if (sp.z < 0f) continue;   // behind camera — skip

                    // sit the label in the lower-centre of the face — a fixed 20 px
                    // below the projected dial centre (consistent across all 6 dials)
                    float gx = sp.x - boxW * 0.5f;
                    float gy = Screen.height - sp.y + 20f;

                    GUI.DrawTexture(new Rect(gx, gy, boxW, 2f), accent);                 // cyan top border
                    GUI.DrawTexture(new Rect(gx, gy + 2f, boxW, nameH), labelBgDark);
                    GUI.Label(new Rect(gx, gy + 2f, boxW, nameH), Dials[i].name, dialNameStyle);

                    GUI.DrawTexture(new Rect(gx, gy + 2f + nameH, boxW, valH), labelBg);
                    GUI.Label(new Rect(gx, gy + 2f + nameH, boxW, valH), liveVals[i], dialValueStyle);
                }
            }
        }

        // ── Stall warnings ──────────────────────────────────────────────────
        if (ac.Stalled)
            GUI.Label(new Rect(Screen.width/2f-120, 24, 240, 36), "STALL", warnStyle);
        else if (ac.StallWarning)
        {
            var prev = GUI.color; GUI.color = new Color(1f, 0.75f, 0.2f);
            GUI.Label(new Rect(Screen.width/2f-140, 24, 280, 36), "STALL WARNING", warnStyle);
            GUI.color = prev;
        }

        // ── Landing quality line ────────────────────────────────────────────
        if (ac.Grounded && !ac.Crashed && ac.Landing != LandingTier.None)
            GUI.Label(new Rect(Screen.width/2f-160, 60, 320, 24),
                $"LANDING: {ac.Landing}   sink {ac.TouchdownSink:F1} m/s  bank {ac.TouchdownBank:F0}°",
                statusStyle);

        // ── Scenario status bar (bottom centre) ────────────────────────────
        var eng2 = gm.ScenarioRunner;
        if (eng2 != null && eng2.Active)
        {
            const float bw = 720;
            float bx = Screen.width/2f - bw/2f, by = Screen.height - 40;
            GUI.DrawTexture(new Rect(bx, by, bw, 30), barBg);
            GUI.Label(new Rect(bx, by, bw, 30), eng2.StatusText(), statusStyle);
        }
        else if (gm.Level != null)
        {
            const float bw = 720;
            float bx = Screen.width/2f - bw/2f, by = Screen.height - 40;
            GUI.DrawTexture(new Rect(bx, by, bw, 30), barBg);
            GUI.Label(new Rect(bx, by, bw, 30), gm.Level.StatusText(), statusStyle);
        }
    }

    void Row(float x, float y, float w, string lbl, string val, string unit)
    {
        GUI.Label(new Rect(x+14, y, 60,    24), lbl,  labelStyle);
        GUI.Label(new Rect(x+14, y, w-58,  24), val,  valueStyle);
        GUI.Label(new Rect(x+w-40, y, 38,  24), unit, unitStyle);
    }

    static Texture2D Tex(Color c)
    { var t = new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }
}
