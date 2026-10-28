using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One look for every menu screen: the participant entry, the mission hub, the
/// briefing, the results page and the session report.
///
/// Everything is laid out on a fixed 1280 x 760 design canvas and scaled to the
/// window with GUI.matrix, so the screens are the same shape on a laptop, a lab
/// monitor and the Windows build, and no rect in any screen has to care about the
/// resolution. Input is transformed by the same matrix, so hit-testing just works.
///
/// IMGUI (OnGUI) on purpose: the whole project is code-generated with no scene
/// assets, every existing screen is OnGUI, and the automated batteries drive the
/// state machine through GameManager's programmatic API rather than through clicks,
/// so the screens can change freely without touching a single test.
/// </summary>
public static class UiTheme
{
    public const float W = 1280f, H = 760f;

    // ── palette ─────────────────────────────────────────────────────────────────
    public static readonly Color Bg      = new Color(0.06f, 0.07f, 0.10f, 0.97f);
    public static readonly Color Panel   = new Color(0.11f, 0.12f, 0.16f, 1.00f);
    public static readonly Color PanelHi = new Color(0.17f, 0.19f, 0.25f, 1.00f);
    public static readonly Color Line    = new Color(0.27f, 0.30f, 0.38f, 1.00f);
    public static readonly Color Text    = new Color(0.94f, 0.95f, 0.97f, 1.00f);
    public static readonly Color Dim     = new Color(0.64f, 0.68f, 0.75f, 1.00f);
    public static readonly Color Faint   = new Color(0.44f, 0.47f, 0.54f, 1.00f);
    public static readonly Color Accent  = new Color(0.32f, 0.72f, 0.96f, 1.00f);
    public static readonly Color Good    = new Color(0.38f, 0.83f, 0.52f, 1.00f);
    public static readonly Color Warn    = new Color(0.98f, 0.77f, 0.32f, 1.00f);
    public static readonly Color Bad     = new Color(0.96f, 0.42f, 0.38f, 1.00f);
    public static readonly Color Violet  = new Color(0.72f, 0.58f, 0.98f, 1.00f);
    public static readonly Color Ink     = new Color(0.05f, 0.06f, 0.08f, 1.00f);

    public static Color ClassColor(WorkloadClass c) =>
        c == WorkloadClass.Low ? Good : c == WorkloadClass.Medium ? Warn : Bad;

    // ── canvas scaling ──────────────────────────────────────────────────────────
    public static float Scale { get; private set; } = 1f;

    /// <summary>Map the 1280x760 design canvas onto the window, letter-boxed and
    /// centred. Call at the top of OnGUI, End() at the bottom.</summary>
    public static void Begin()
    {
        float s = Mathf.Min(Screen.width / W, Screen.height / H);
        Scale = Mathf.Max(0.4f, s);
        float ox = (Screen.width - W * Scale) * 0.5f, oy = (Screen.height - H * Scale) * 0.5f;
        GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity, new Vector3(Scale, Scale, 1f));
    }

    public static void End() { GUI.matrix = Matrix4x4.identity; }

    /// <summary>HUD scaling: uniform by HEIGHT, no letter-box, so an overlay drawn at
    /// the canvas edge sits at the screen edge. The canvas is H tall and HudW wide.
    /// On a Retina Mac the window is ~3400 px wide, and an unscaled 14 px label is
    /// unreadable; this keeps every in-flight overlay the size it was designed at.</summary>
    public static float HudScale { get; private set; } = 1f;
    public static float HudW { get; private set; } = W;

    public static void BeginHud()
    {
        HudScale = Mathf.Max(0.4f, Screen.height / H);
        HudW = Screen.width / HudScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(HudScale, HudScale, 1f));
    }

    public static void EndHud() { GUI.matrix = Matrix4x4.identity; }

    /// <summary>Darken the whole window. Call BEFORE Begin() (screen space).</summary>
    public static void DimScreen(float alpha)
    {
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    // ── primitives ──────────────────────────────────────────────────────────────
    static readonly Dictionary<Color, Texture2D> texCache = new Dictionary<Color, Texture2D>();

    public static Texture2D Tex(Color c)
    {
        Texture2D t;
        if (texCache.TryGetValue(c, out t) && t != null) return t;
        t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c); t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        texCache[c] = t;
        return t;
    }

    public static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Tex(c));

    public static void Frame(Rect r, Color c, float t = 1f)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    public static void Box(Rect r, bool raised = false)
    {
        Fill(r, raised ? PanelHi : Panel);
        Frame(r, Line);
    }

    public static void Rule(float x, float y, float w) => Fill(new Rect(x, y, w, 1f), Line);

    // ── styles ──────────────────────────────────────────────────────────────────
    static GUIStyle h1, h2, h3, body, small, mono, chip, btnText, field, wrap;
    static bool built;

    static void Build()
    {
        if (built) return;
        built = true;
        h1    = Make(30, FontStyle.Bold,   TextAnchor.MiddleLeft, Text);
        h2    = Make(20, FontStyle.Bold,   TextAnchor.MiddleLeft, Text);
        h3    = Make(15, FontStyle.Bold,   TextAnchor.MiddleLeft, Text);
        body  = Make(14, FontStyle.Normal, TextAnchor.MiddleLeft, Text);
        small = Make(12, FontStyle.Normal, TextAnchor.MiddleLeft, Dim);
        mono  = Make(12, FontStyle.Normal, TextAnchor.MiddleLeft, Dim);
        chip  = Make(11, FontStyle.Bold,   TextAnchor.MiddleCenter, Ink);
        btnText = Make(14, FontStyle.Bold, TextAnchor.MiddleCenter, Text);
        wrap  = Make(14, FontStyle.Normal, TextAnchor.UpperLeft, Text); wrap.wordWrap = true;
        field = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        };
        field.normal.textColor = Text; field.focused.textColor = Text; field.hover.textColor = Text;
    }

    static GUIStyle Make(int size, FontStyle fs, TextAnchor anchor, Color col)
    {
        var s = new GUIStyle { fontSize = size, fontStyle = fs, alignment = anchor, clipping = TextClipping.Clip };
        s.normal.textColor = col;
        return s;
    }

    public static GUIStyle H1    { get { Build(); return h1; } }
    public static GUIStyle H2    { get { Build(); return h2; } }
    public static GUIStyle H3    { get { Build(); return h3; } }
    public static GUIStyle Body  { get { Build(); return body; } }
    public static GUIStyle Small { get { Build(); return small; } }
    public static GUIStyle Mono  { get { Build(); return mono; } }
    public static GUIStyle Wrap  { get { Build(); return wrap; } }
    public static GUIStyle Field { get { Build(); return field; } }

    // ── widgets ─────────────────────────────────────────────────────────────────
    /// <summary>Label in a given colour without allocating a style per call.</summary>
    public static void Label(Rect r, string text, GUIStyle st, Color col)
    {
        var prev = st.normal.textColor;
        st.normal.textColor = col;
        GUI.Label(r, text, st);
        st.normal.textColor = prev;
    }

    public static void Label(Rect r, string text, GUIStyle st) => GUI.Label(r, text, st);

    /// <summary>Word-wrapped paragraph; returns the height it used.</summary>
    public static float Paragraph(float x, float y, float w, string text, int size = 14, Color? col = null)
    {
        Build();
        var st = wrap; st.fontSize = size;
        var prev = st.normal.textColor; st.normal.textColor = col ?? Text;
        float h = st.CalcHeight(new GUIContent(text), w);
        GUI.Label(new Rect(x, y, w, h), text, st);
        st.normal.textColor = prev; st.fontSize = 14;
        return h;
    }

    /// <summary>A button. `primary` fills it with the colour (dark text); otherwise it
    /// is a panel with a coloured left bar and light text. Hover brightens it.
    /// Returns true on click. A disabled button draws flat and never fires.</summary>
    public static bool Button(Rect r, string label, Color col, bool enabled = true, bool primary = false,
                              int size = 14, TextAnchor anchor = TextAnchor.MiddleCenter, string hint = null)
    {
        Build();
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        if (!enabled)
        {
            Fill(r, new Color(0.10f, 0.11f, 0.14f, 1f)); Frame(r, new Color(0.2f, 0.22f, 0.27f, 1f));
        }
        else if (primary)
        {
            Fill(r, hover ? Color.Lerp(col, Color.white, 0.18f) : col);
        }
        else
        {
            Fill(r, hover ? PanelHi : Panel); Frame(r, hover ? col : Line);
            Fill(new Rect(r.x, r.y, 4f, r.height), col);
        }

        var st = btnText;
        st.fontSize = size; st.alignment = anchor;
        st.padding = new RectOffset(anchor == TextAnchor.MiddleLeft ? 16 : 6, 6, 0, 0);
        st.normal.textColor = !enabled ? Faint : primary ? Ink : Text;
        st.hover.textColor = st.normal.textColor; st.active.textColor = st.normal.textColor;

        if (!string.IsNullOrEmpty(hint))
        {
            var hs = small; var prev = hs.normal.textColor; var pa = hs.alignment;
            hs.alignment = TextAnchor.MiddleRight;
            hs.normal.textColor = !enabled ? Faint : primary ? new Color(0.1f, 0.1f, 0.12f, 0.8f) : Faint;
            GUI.Label(new Rect(r.x, r.y, r.width - 10f, r.height), hint, hs);
            hs.normal.textColor = prev; hs.alignment = pa;
        }
        if (!enabled) { GUI.Label(r, label, st); return false; }
        return GUI.Button(r, label, st);
    }

    /// <summary>Small filled tag, e.g. "HIGH", "DONE", "V2".</summary>
    public static float Chip(float x, float y, string text, Color col, float h = 20f, bool outline = false)
    {
        Build();
        float w = chip.CalcSize(new GUIContent(text)).x + 14f;
        var r = new Rect(x, y, w, h);
        if (outline) { Frame(r, col); chip.normal.textColor = col; }
        else { Fill(r, col); chip.normal.textColor = Ink; }
        GUI.Label(r, text, chip);
        chip.normal.textColor = Ink;
        return w;
    }

    /// <summary>Key/value line: "LABEL   value".</summary>
    public static void KV(float x, float y, float labelW, string k, string v, Color? vcol = null)
    {
        Label(new Rect(x, y, labelW, 20f), k, Small, Faint);
        Label(new Rect(x + labelW, y, 600f, 20f), v, Body, vcol ?? Text);
    }

    /// <summary>Progress bar with a caption.</summary>
    public static void Bar(Rect r, float frac, Color col, string caption = null)
    {
        Fill(r, new Color(0.14f, 0.15f, 0.2f, 1f));
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), col);
        Frame(r, Line);
        if (caption != null)
        {
            var st = chip; st.normal.textColor = Text; st.fontSize = 11;
            GUI.Label(r, caption, st); st.normal.textColor = Ink;
        }
    }

    public static string Short(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n - 1) + "…");

    /// <summary>True once per key press inside OnGUI, and eats the event so the
    /// text field or another screen does not also act on it.</summary>
    public static bool Key(KeyCode k)
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == k) { e.Use(); return true; }
        return false;
    }

    public static string PhaseBlurb(FlightPhase p)
    {
        switch (p)
        {
            case FlightPhase.Takeoff:  return "Taxi from the stand, line up and depart";
            case FlightPhase.Climb:    return "Climb from 400 m to the assigned altitude";
            case FlightPhase.Cruise:   return "Hold 700 m in level flight";
            case FlightPhase.Approach: return "Descend from 500 m and land";
            default: return p.ToString();
        }
    }
}
