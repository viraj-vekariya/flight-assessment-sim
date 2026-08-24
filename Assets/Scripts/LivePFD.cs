using UnityEngine;

/// <summary>
/// (EXTRA) Left-screen Primary Flight Display — deliberately MINIMAL, so it stays readable on a
/// 8.5 x 5.6 cm piece of cockpit glass: blue sky / brown ground that banks and pitches with the
/// aircraft, a white horizon line, two +-10 degree pitch marks, a simple aircraft cross-symbol,
/// and four readouts (HDG, SPD, ALT, VS) on the dark border around the attitude window.
/// VS uses "^" / "v" prefixes so climb or descent reads at a glance.
/// </summary>
public class LivePFD : MonoBehaviour
{
    public CessnaPhysics phys;
    public float pitchGain = 0.035f;   // attitude window: PFD units per degree

    /// The quad this PFD is drawn onto, on the cockpit panel. ScreenTuner nudges it live.
    public Transform screenQuad;

    Transform horizon;
    TextMesh spdText, altText, hdgText, vsText, flapText, trimText;

    static readonly Color Blue   = new Color(0.40f, 0.72f, 1.00f);   // readouts (readable blue)
    static readonly Color Dim    = new Color(0.58f, 0.68f, 0.82f);   // labels
    static readonly Color Yellow = new Color(1.00f, 0.85f, 0.15f);   // aircraft symbol
    static readonly Color Sky    = new Color(0.15f, 0.35f, 0.68f);   // BLUE sky
    static readonly Color Gnd    = new Color(0.42f, 0.28f, 0.13f);   // BROWN ground
    static readonly Color Bg     = new Color(0.03f, 0.04f, 0.06f);   // dark screen / border
    static readonly Color Frame  = new Color(0.28f, 0.40f, 0.55f);   // subtle window frame

    // world sizes (ortho 1.5 → view 3.0 tall)
    const float ValSize = 0.046f;   // numbers
    const float LabSize = 0.019f;   // labels / units
    // centre attitude window half-extents
    const float WinX = 0.90f, WinY = 0.72f;

    public void Build(CessnaPhysics p, Transform screenParent, Vector3 screenLocalPos,
                      Quaternion screenLocalRot, Vector2 size, int cockpitLayer)
    {
        phys = p;
        const int PFDLayer = 13;
        var scene = new GameObject("PFDScene").transform;
        scene.position = new Vector3(0f, -5000f, 0f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── filled attitude (blue sky / brown ground) — 18 units tall so pitch never clips ──
        horizon = new GameObject("Horizon").transform;
        horizon.SetParent(scene, false);
        Quad(horizon, new Vector3(0f,  9f, 0.6f), new Vector3(9f, 18f, 1f), Sky);
        Quad(horizon, new Vector3(0f, -9f, 0.6f), new Vector3(9f, 18f, 1f), Gnd);
        Quad(horizon, new Vector3(0f, 0f, 0.55f), new Vector3(1.7f, 0.03f, 1f), Color.white);   // horizon line

        // ── the only pitch reference: one short mark at +10° and one at -10° ──
        // Children of horizon, so they bank and pitch with it.
        Quad(horizon, new Vector3(0f,  10f * pitchGain, 0.55f), new Vector3(0.20f, 0.018f, 1f), Color.white);
        Quad(horizon, new Vector3(0f, -10f * pitchGain, 0.55f), new Vector3(0.20f, 0.018f, 1f), Color.white);

        // ── dark border frames the window so nothing bleeds outside it (z=0.2) ──
        // Each panel must reach EXACTLY the window edge: half-size 1.5 -> centre at 1.5 + Win*.
        // It sits closer to the camera than the sky/ground quads, so it masks them by depth.
        Quad(scene, new Vector3(0f,  1.5f + WinY, 0.2f), new Vector3(7f, 3f, 1f), Bg);   // top
        Quad(scene, new Vector3(0f, -1.5f - WinY, 0.2f), new Vector3(7f, 3f, 1f), Bg);   // bottom
        Quad(scene, new Vector3(-1.5f - WinX, 0f, 0.2f), new Vector3(3f, 6f, 1f), Bg);   // left
        Quad(scene, new Vector3( 1.5f + WinX, 0f, 0.2f), new Vector3(3f, 6f, 1f), Bg);   // right
        // subtle window frame lines (z=0.15)
        Quad(scene, new Vector3(0f,  WinY, 0.15f), new Vector3(2f * WinX, 0.02f, 1f), Frame);
        Quad(scene, new Vector3(0f, -WinY, 0.15f), new Vector3(2f * WinX, 0.02f, 1f), Frame);
        Quad(scene, new Vector3(-WinX, 0f, 0.15f), new Vector3(0.02f, 2f * WinY, 1f), Frame);
        Quad(scene, new Vector3( WinX, 0f, 0.15f), new Vector3(0.02f, 2f * WinY, 1f), Frame);

        // ── fixed aircraft symbol (yellow): two wings + a thin fuselage line ──
        Quad(scene, new Vector3(0f, 0f, 0.10f), new Vector3(0.022f, 0.18f, 1f), Yellow);        // fuselage
        Quad(scene, new Vector3(-0.185f, 0f, 0.10f), new Vector3(0.15f, 0.028f, 1f), Yellow);   // left wing
        Quad(scene, new Vector3( 0.185f, 0f, 0.10f), new Vector3(0.15f, 0.028f, 1f), Yellow);   // right wing

        // ── readouts on the dark border (z=0.1) ──
        hdgText = Text(scene, new Vector3(0f, 1.18f, 0.1f), ValSize, true, font, Blue);
        Text(scene, new Vector3(0f, 0.92f, 0.1f), LabSize, false, font, Dim).text = "HDG";
        spdText = Text(scene, new Vector3(-1.58f, 0.08f, 0.1f), ValSize, true, font, Blue);
        Text(scene, new Vector3(-1.58f, -0.19f, 0.1f), LabSize, false, font, Dim).text = "SPD  km/h";
        altText = Text(scene, new Vector3(1.58f, 0.08f, 0.1f), ValSize, true, font, Blue);
        Text(scene, new Vector3(1.58f, -0.19f, 0.1f), LabSize, false, font, Dim).text = "ALT  m";
        vsText = Text(scene, new Vector3(0f, -1.02f, 0.1f), ValSize * 0.8f, true, font, Blue);
        Text(scene, new Vector3(0f, -1.26f, 0.1f), LabSize, false, font, Dim).text = "VS  m/s";

        // FLAP POSITION AND TRIM.
        //
        // The PFD showed heading, speed, altitude and vertical speed — and nothing about
        // the aeroplane's CONFIGURATION. A real 172 has a flap position indicator, and
        // the difference between the flap SELECTOR and the flap POSITION is the whole
        // content of the flap-failure mission: the lever moves, the flaps do not, and
        // without an indicator the only cue is the aeroplane not slowing down, which is
        // a much harsher and much less realistic way to discover it.
        //
        // ACTUAL position is shown, as the real indicator does. When it disagrees with
        // what the pilot selected, the readout says so — that is the indication a real
        // cockpit gives, and noticing it is the pilot's job, not the display's.
        flapText = Text(scene, new Vector3(-1.58f, -1.02f, 0.1f), ValSize * 0.7f, true, font, Blue);
        Text(scene, new Vector3(-1.58f, -1.26f, 0.1f), LabSize, false, font, Dim).text = "FLAP";
        trimText = Text(scene, new Vector3(1.58f, -1.02f, 0.1f), ValSize * 0.7f, true, font, Blue);
        Text(scene, new Vector3(1.58f, -1.26f, 0.1f), LabSize, false, font, Dim).text = "TRIM";

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

    void Update()
    {
        if (phys == null || horizon == null) return;
        horizon.localRotation = Quaternion.Euler(0f, 0f, phys.RollDeg);
        horizon.localPosition = new Vector3(0f, -phys.PitchDeg * pitchGain, 0f);

        if (hdgText) hdgText.text = Mathf.RoundToInt(phys.HeadingDeg).ToString("000") + "°";
        if (spdText) spdText.text = Mathf.RoundToInt(phys.AirspeedKmh).ToString();
        if (altText) altText.text = Mathf.RoundToInt(phys.AltitudeM).ToString();
        if (flapText)
        {
            int actual = Mathf.RoundToInt(phys.Flaps01 * 100f);
            var ctl = phys.GetComponent<AircraftController>();
            int selected = ctl != null ? Mathf.RoundToInt(ctl.FlapsSelected * 100f) : actual;
            // Amber when the flaps are not where they were asked to be — the flap-motor
            // failure's one honest visual cue.
            bool disagree = Mathf.Abs(selected - actual) > 4;
            flapText.text = disagree ? actual + "/" + selected : actual.ToString();
            flapText.color = disagree ? new Color(1f, 0.75f, 0.2f) : new Color(0.40f, 0.72f, 1.00f);
        }
        if (trimText)
        {
            float t = phys.trim;
            trimText.text = Mathf.Abs(t) < 0.02f ? "0" : (t > 0f ? "U" : "D") + Mathf.Abs(t).ToString("F2");
        }
        if (vsText)
        {
            float vs = phys.VerticalSpeedMs;
            vsText.text = vs > 0.05f  ? "^" + vs.ToString("F1")
                        : vs < -0.05f ? "v" + Mathf.Abs(vs).ToString("F1")
                        : "+0.0";
        }
    }

    static TextMesh Text(Transform parent, Vector3 pos, float charSize, bool bold, Font font, Color color)
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

    static void Quad(Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var c = g.GetComponent<Collider>(); if (c) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Color")) { color = color };
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform ch in t) SetLayer(ch, layer);
    }
}
