using UnityEngine;

/// <summary>
/// Left-screen Primary Flight Display.
///
/// 29 Sep 2026, second pass. The first overhaul copied a Garmin G1000 — speed and altitude
/// TAPES with scale numbers every 20 km/h / 100 m, a heading tape with letters, numbered
/// pitch ladder, colour bands — and on an 8.5 x 5.6 cm piece of cockpit glass that was
/// clutter: the tape and ladder numbers came out a few pixels tall and could not be read
/// from the seat. A real G1000 is 10.4 inches across; this glass is not.
///
/// So this layout keeps what reads at a glance and makes every NUMBER big:
///   * full-screen attitude (sky / ground) with plain pitch lines at ±10° and ±20°,
///     a bank scale and a sky pointer, and the yellow aircraft symbol — no small labels;
///   * six boxed readouts in large type, each with a short caption:
///       top centre  HDG
///       left        SPEED (km/h)   and below it   V/S (m/s, ^ climbing / v descending)
///       right       ALT (m)        and below it   FLAP (%, amber actual/selected on a
///                                                  disagreement — the flap-failure cue)
///       bottom      TRIM
///   * readouts are semi-opaque so the horizon still shows through at the edges.
///
/// Units stay km/h and metres, as everywhere else in this simulator. The symbology is
/// ordinary world geometry on a private layer, rendered by an orthographic camera into a
/// RenderTexture shown on the panel glass.
/// </summary>
public class LivePFD : MonoBehaviour
{
    public CessnaPhysics phys;
    public float pitchGain = 0.035f;   // attitude: PFD units per degree of pitch

    /// The quad this PFD is drawn onto, on the cockpit panel. ScreenTuner nudges it live.
    public Transform screenQuad;

    Transform horizon, rollPointer;
    TextMesh spdText, altText, hdgText, vsText, flapText, trimText;

    static readonly Color White  = new Color(0.97f, 0.97f, 0.97f);
    static readonly Color Cap    = new Color(0.45f, 0.88f, 1.00f);   // captions (cyan)
    static readonly Color Amber  = new Color(1.00f, 0.72f, 0.18f);
    static readonly Color Yellow = new Color(1.00f, 0.85f, 0.10f);
    static readonly Color Sky    = new Color(0.14f, 0.40f, 0.82f);
    static readonly Color Gnd    = new Color(0.47f, 0.31f, 0.13f);
    static readonly Color Bg     = new Color(0.02f, 0.02f, 0.03f);
    static readonly Color BoxCol = new Color(0.03f, 0.035f, 0.05f, 0.82f);
    static readonly Color Rim    = new Color(0.55f, 0.60f, 0.68f);

    // view: ortho 1.5 -> 3.0 tall, ~4.55 wide (x ±2.27)
    const float ValBig = 0.054f;   // SPEED / ALT / HDG
    const float ValMid = 0.044f;   // V/S / FLAP / TRIM
    const float CapSz  = 0.021f;   // captions

    Font font;
    static Material unlitTemplate, spriteTemplate;

    public void Build(CessnaPhysics p, Transform screenParent, Vector3 screenLocalPos,
                      Quaternion screenLocalRot, Vector2 size, int cockpitLayer)
    {
        phys = p;
        const int PFDLayer = 13;
        var scene = new GameObject("PFDScene").transform;
        scene.position = new Vector3(0f, -5000f, 0f);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── ATTITUDE ───────────────────────────────────────────────────────────
        horizon = new GameObject("Horizon").transform;
        horizon.SetParent(scene, false);
        Quad(horizon, new Vector3(0f,  9f, 0.62f), new Vector3(14f, 18f, 1f), Sky);
        Quad(horizon, new Vector3(0f, -9f, 0.62f), new Vector3(14f, 18f, 1f), Gnd);
        Quad(horizon, new Vector3(0f, 0f, 0.58f), new Vector3(14f, 0.03f, 1f), White);
        foreach (int d in new[] { -20, -10, 10, 20 })
            Quad(horizon, new Vector3(0f, d * pitchGain, 0.56f), new Vector3(Mathf.Abs(d) == 10 ? 0.42f : 0.62f, 0.022f, 1f), White);
        foreach (int d in new[] { -15, -5, 5, 15 })
            Quad(horizon, new Vector3(0f, d * pitchGain, 0.56f), new Vector3(0.18f, 0.016f, 1f), White);

        // bank scale (fixed) + sky pointer (rolls with the horizon)
        const float R = 0.74f;
        foreach (int a in new[] { -60, -45, -30, -20, -10, 10, 20, 30, 45, 60 })
        {
            float m = a * Mathf.Deg2Rad;
            float len = (a % 30 == 0) ? 0.13f : 0.08f;
            var q = Quad(scene, new Vector3(Mathf.Sin(m) * (R + len * 0.5f), Mathf.Cos(m) * (R + len * 0.5f), 0.5f), new Vector3(0.022f, len, 1f), White);
            q.localRotation = Quaternion.Euler(0f, 0f, -a);
        }
        Triangle(scene, new Vector3(0f, R + 0.02f, 0.5f), 0.10f, 0.09f, false, White);
        rollPointer = new GameObject("RollPointer").transform;
        rollPointer.SetParent(scene, false);
        rollPointer.localPosition = new Vector3(0f, 0f, 0.45f);
        Triangle(rollPointer, new Vector3(0f, R - 0.11f, 0f), 0.11f, 0.10f, true, Yellow);

        // aircraft symbol: yellow wings with a black outline, and a centre dot
        foreach (float s in new[] { -1f, 1f })
        {
            Quad(scene, new Vector3(s * 0.42f, 0f, 0.31f), new Vector3(0.40f, 0.075f, 1f), Color.black);
            Quad(scene, new Vector3(s * 0.42f, 0f, 0.30f), new Vector3(0.37f, 0.048f, 1f), Yellow);
            Quad(scene, new Vector3(s * 0.25f, -0.06f, 0.31f), new Vector3(0.075f, 0.17f, 1f), Color.black);
            Quad(scene, new Vector3(s * 0.25f, -0.06f, 0.30f), new Vector3(0.048f, 0.14f, 1f), Yellow);
        }
        Quad(scene, new Vector3(0f, 0f, 0.31f), new Vector3(0.09f, 0.09f, 1f), Color.black);
        Quad(scene, new Vector3(0f, 0f, 0.30f), new Vector3(0.062f, 0.062f, 1f), Yellow);

        // ── READOUTS ───────────────────────────────────────────────────────────
        const float CX = 1.56f, CW = 1.12f;           // side columns: centre |x|, width
        spdText  = Readout(scene, new Vector3(-CX, 0.24f), CW, 0.74f, "SPD  km/h", ValBig);
        vsText   = Readout(scene, new Vector3(-CX, -0.68f), CW, 0.64f, "V/S  m/s", ValMid);
        altText  = Readout(scene, new Vector3( CX, 0.24f), CW, 0.74f, "ALT  m", ValBig);
        flapText = Readout(scene, new Vector3( CX, -0.68f), CW, 0.64f, "FLAP  %", ValMid);
        hdgText  = Readout(scene, new Vector3(0f, 1.10f), 1.20f, 0.64f, "HDG", ValBig);
        trimText = Readout(scene, new Vector3(0f, -1.12f), 1.20f, 0.60f, "TRIM", ValMid);

        SetLayer(scene, PFDLayer);

        // ── PFD camera → RenderTexture (high-res, aspect = physical screen) ──
        var camGO = new GameObject("PFDCam");
        camGO.transform.SetParent(scene, false);
        camGO.transform.localPosition = new Vector3(0f, 0f, -3f);
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.5f;
        cam.cullingMask = 1 << PFDLayer;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Bg;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
        float aspect = size.x / Mathf.Max(0.0001f, size.y);
        int rtW = 1280, rtH = Mathf.Clamp(Mathf.RoundToInt(rtW / aspect), 128, 2048);
        var rt = new RenderTexture(rtW, rtH, 16) { antiAliasing = 4, filterMode = FilterMode.Bilinear };
        cam.targetTexture = rt;
        cam.aspect = aspect;

        // ── overlay quad over the panel's left screen ──
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = quad.GetComponent<Collider>(); if (col) Destroy(col);
        quad.name = "LivePFDQuad";
        quad.transform.SetParent(screenParent, false);
        quad.transform.localPosition = screenLocalPos;
        quad.transform.localRotation = screenLocalRot;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        quad.layer = cockpitLayer;
        var mat = new Material(Shader.Find("Unlit/Texture")); mat.mainTexture = rt;
        quad.GetComponent<Renderer>().material = mat;
        screenQuad = quad.transform;
    }

    /// <summary>A captioned box: semi-opaque dark fill, thin light rim, caption across the
    /// top, value large and centred below it. Returns the value text.</summary>
    TextMesh Readout(Transform scene, Vector2 c, float w, float h, string caption, float valSize)
    {
        Quad(scene, new Vector3(c.x, c.y, 0.21f), new Vector3(w + 0.03f, h + 0.03f, 1f), Rim);
        Quad(scene, new Vector3(c.x, c.y, 0.20f), new Vector3(w, h, 1f), BoxCol, true);
        Text(scene, new Vector3(c.x, c.y + h * 0.5f - 0.10f, 0.1f), CapSz, false, Cap).text = caption;
        var v = Text(scene, new Vector3(c.x, c.y - 0.09f, 0.1f), valSize, true, White);
        v.name = "PFDValue_" + caption.Split(' ')[0];
        return v;
    }

    void Update()
    {
        if (phys == null || horizon == null) return;
        float roll = phys.RollDeg, pitch = phys.PitchDeg;
        horizon.localRotation = Quaternion.Euler(0f, 0f, roll);
        horizon.localPosition = Quaternion.Euler(0f, 0f, roll) * new Vector3(0f, -pitch * pitchGain, 0f);
        if (rollPointer) rollPointer.localRotation = Quaternion.Euler(0f, 0f, roll);

        if (hdgText) Fit(hdgText, Mathf.RoundToInt(Mathf.Repeat(phys.HeadingDeg, 360f)).ToString("000") + "°", ValBig, 4);
        if (spdText) Fit(spdText, Mathf.RoundToInt(phys.AirspeedKmh).ToString(), ValBig, 3);
        if (altText) Fit(altText, Mathf.RoundToInt(phys.AltitudeM).ToString(), ValBig, 3);

        if (flapText)
        {
            int actual = Mathf.RoundToInt(phys.Flaps01 * 100f);
            var ctl = phys.GetComponent<AircraftController>();
            int selected = ctl != null ? Mathf.RoundToInt(ctl.FlapsSelected * 100f) : actual;
            // Amber when the flaps are not where they were asked to be — the flap-motor
            // failure's one honest visual cue.
            bool disagree = Mathf.Abs(selected - actual) > 4;
            Fit(flapText, disagree ? actual + "/" + selected : actual.ToString(), ValMid, 3);
            flapText.color = disagree ? Amber : White;
        }
        if (trimText)
        {
            float t = phys.trim;
            Fit(trimText, Mathf.Abs(t) < 0.02f ? "0" : (t > 0f ? "UP " : "DN ") + Mathf.Abs(t).ToString("F2"), ValMid, 7);
        }
        if (vsText)
        {
            float vs = phys.VerticalSpeedMs;
            Fit(vsText, vs > 0.05f  ? "^" + vs.ToString("F1")
                      : vs < -0.05f ? "v" + Mathf.Abs(vs).ToString("F1")
                      : "0.0", ValMid, 4);
        }
    }

    /// <summary>Set a readout's text, shrinking it only when it would overflow its box
    /// (e.g. a four-digit altitude), so short values always get the full size.</summary>
    static void Fit(TextMesh tm, string s, float size, int fullChars)
    {
        if (tm.text != s) tm.text = s;
        float k = s.Length > fullChars ? fullChars / (float)s.Length : 1f;
        tm.characterSize = size * Mathf.Max(0.6f, k);
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    TextMesh Text(Transform parent, Vector3 pos, float charSize, bool bold, Color color)
    {
        var g = new GameObject("PFDText");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var tm = g.AddComponent<TextMesh>();
        tm.font = font; tm.fontSize = 100; tm.characterSize = charSize;
        tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
        if (font != null) g.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return tm;
    }

    static Transform Quad(Transform parent, Vector3 pos, Vector3 scale, Color color, bool transparent = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var c = g.GetComponent<Collider>(); if (c) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().material = Mat(color, transparent);
        return g.transform;
    }

    static Material Mat(Color color, bool transparent)
    {
        if (transparent)
        {
            if (spriteTemplate == null) spriteTemplate = new Material(Shader.Find("Sprites/Default"));
            return new Material(spriteTemplate) { color = color };
        }
        if (unlitTemplate == null) unlitTemplate = new Material(Shader.Find("Unlit/Color"));
        return new Material(unlitTemplate) { color = color };
    }

    /// <summary>An isosceles triangle, apex up (up = true) or down.</summary>
    static Transform Triangle(Transform parent, Vector3 pos, float baseW, float height, bool up, Color color)
    {
        var g = new GameObject("Tri");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var m = new Mesh();
        float s = up ? 1f : -1f;
        m.vertices = new[] { new Vector3(-baseW * 0.5f, 0f, 0f), new Vector3(0f, s * height, 0f), new Vector3(baseW * 0.5f, 0f, 0f) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 1 };   // double-sided
        m.RecalculateBounds();
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().material = Mat(color, false);
        return g.transform;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform ch in t) SetLayer(ch, layer);
    }
}
