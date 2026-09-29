using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Left-screen Primary Flight Display, laid out like a Garmin G1000 PFD so the panel reads
/// as a real glass cockpit:
///
///   * full-screen attitude (blue sky / brown ground) with a pitch ladder every 5°,
///   * a fixed bank scale (0/10/20/30/45/60°) with a sky-pointer that rolls with the horizon,
///   * an airspeed TAPE on the left with the C172 colour bands (white flap range, green
///     normal range, yellow caution, red line) and a boxed current-speed pointer,
///   * an altitude TAPE on the right with a boxed current-altitude pointer,
///   * a vertical-speed scale beside the altitude tape,
///   * a heading tape across the top with a boxed current heading,
///   * a soft-key strip along the bottom carrying FLAP, TRIM and VS.
///
/// Units stay km/h and metres, as everywhere else in this simulator. The CURRENT values
/// are drawn as large as the old minimal display drew them, inside the pointer boxes, so
/// the display is no harder to read on the 8.5 x 5.6 cm glass; the tapes are context.
///
/// The symbology is ordinary world geometry on a private layer, rendered by an
/// orthographic camera into a RenderTexture shown on the panel glass.
/// </summary>
public class LivePFD : MonoBehaviour
{
    public CessnaPhysics phys;
    public float pitchGain = 0.035f;   // attitude: PFD units per degree of pitch

    /// The quad this PFD is drawn onto, on the cockpit panel. ScreenTuner nudges it live.
    public Transform screenQuad;

    Transform horizon, rollPointer;
    TextMesh spdText, altText, hdgText, vsText, flapText, trimText;
    Transform vsiNeedle;

    static readonly Color White  = new Color(0.96f, 0.96f, 0.96f);
    static readonly Color Cyan   = new Color(0.30f, 0.90f, 1.00f);
    static readonly Color Dim    = new Color(0.70f, 0.74f, 0.80f);
    static readonly Color Yellow = new Color(1.00f, 0.85f, 0.10f);
    static readonly Color Sky    = new Color(0.12f, 0.36f, 0.78f);
    static readonly Color SkyTop = new Color(0.05f, 0.22f, 0.60f);
    static readonly Color Gnd    = new Color(0.46f, 0.30f, 0.12f);
    static readonly Color GndLow = new Color(0.33f, 0.21f, 0.08f);
    static readonly Color Bg     = new Color(0.02f, 0.02f, 0.03f);
    static readonly Color Tape   = new Color(0.10f, 0.11f, 0.14f, 0.62f);
    static readonly Color Black  = new Color(0f, 0f, 0f);

    // world sizes (ortho 1.5 → view 3.0 tall, ~4.55 wide)
    const float ValSize = 0.046f;
    const float LabSize = 0.019f;
    const float TickLab = 0.024f;

    // tapes
    const float SpdX0 = -2.24f, SpdX1 = -1.56f, TapeY = 1.02f;
    const float SpdGain = 0.011f;            // units per km/h
    const float AltX0 = 1.44f, AltX1 = 2.06f;
    const float AltGain = 0.0045f;           // units per metre
    const float VsiX0 = 2.08f, VsiX1 = 2.26f, VsiGain = 0.085f, VsiMax = 10f;
    const float HdgY0 = 1.22f, HdgHalfW = 1.34f, HdgGain = 0.017f;

    // C172 airspeed bands (POH values converted to km/h)
    const float Vso = 61f, Vs1 = 89f, Vfe = 157f, Vno = 239f, Vne = 302f;

    readonly List<Transform> spdTicks = new List<Transform>();
    readonly List<TextMesh> spdLabs = new List<TextMesh>();
    readonly List<Transform> altTicks = new List<Transform>();
    readonly List<TextMesh> altLabs = new List<TextMesh>();
    readonly List<Transform> hdgTicks = new List<Transform>();
    readonly List<TextMesh> hdgLabs = new List<TextMesh>();
    Transform bandWhite, bandGreen, bandYellow, bandRed;

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

        // ── ATTITUDE (z 0.6 .. 0.5) ────────────────────────────────────────────
        horizon = new GameObject("Horizon").transform;
        horizon.SetParent(scene, false);
        // two-tone sky and ground: a deeper colour far from the horizon, like the G1000
        Quad(horizon, new Vector3(0f,  2.2f, 0.62f), new Vector3(12f, 4.4f, 1f), Sky);
        Quad(horizon, new Vector3(0f,  9.4f, 0.62f), new Vector3(12f, 10f, 1f), SkyTop);
        Quad(horizon, new Vector3(0f, -2.2f, 0.62f), new Vector3(12f, 4.4f, 1f), Gnd);
        Quad(horizon, new Vector3(0f, -9.4f, 0.62f), new Vector3(12f, 10f, 1f), GndLow);
        Quad(horizon, new Vector3(0f, 0f, 0.58f), new Vector3(12f, 0.022f, 1f), White);
        // pitch ladder
        for (int d = -25; d <= 25; d += 5)
        {
            if (d == 0) continue;
            bool ten = d % 10 == 0;
            float y = d * pitchGain, w = ten ? 0.34f : 0.16f;
            Quad(horizon, new Vector3(0f, y, 0.56f), new Vector3(w, 0.013f, 1f), White);
            if (ten)
            {
                string s = Mathf.Abs(d).ToString();
                Text(horizon, new Vector3(-w * 0.5f - 0.09f, y, 0.55f), TickLab * 0.72f, false, White).text = s;
                Text(horizon, new Vector3( w * 0.5f + 0.09f, y, 0.55f), TickLab * 0.72f, false, White).text = s;
            }
        }

        // bank scale (fixed) + sky pointer (rolls with the horizon)
        const float R = 0.93f;
        for (int a = -60; a < 60; a += 3)
        {
            float m = (a + 1.5f) * Mathf.Deg2Rad;
            var q = Quad(scene, new Vector3(Mathf.Sin(m) * R, Mathf.Cos(m) * R, 0.5f), new Vector3(0.012f, 0.052f, 1f), White);
            q.localRotation = Quaternion.Euler(0f, 0f, 90f - (a + 1.5f));
        }
        foreach (int a in new[] { -60, -45, -30, -20, -10, 10, 20, 30, 45, 60 })
        {
            float m = a * Mathf.Deg2Rad;
            float len = (a % 30 == 0) ? 0.11f : 0.06f;
            var q = Quad(scene, new Vector3(Mathf.Sin(m) * (R + len * 0.5f), Mathf.Cos(m) * (R + len * 0.5f), 0.5f), new Vector3(0.016f, len, 1f), White);
            q.localRotation = Quaternion.Euler(0f, 0f, -a);
        }
        Triangle(scene, new Vector3(0f, R + 0.02f, 0.5f), 0.075f, 0.07f, false, White);        // zero index (points down)
        rollPointer = new GameObject("RollPointer").transform;
        rollPointer.SetParent(scene, false);
        rollPointer.localPosition = new Vector3(0f, 0f, 0.45f);
        Triangle(rollPointer, new Vector3(0f, R - 0.085f, 0f), 0.075f, 0.07f, true, White);   // sky pointer (points up)
        Quad(rollPointer, new Vector3(0f, R - 0.14f, 0f), new Vector3(0.11f, 0.022f, 1f), White);   // slip bar

        // aircraft symbol: yellow wings with a black outline, and a centre dot
        foreach (float s in new[] { -1f, 1f })
        {
            Quad(scene, new Vector3(s * 0.40f, 0f, 0.31f), new Vector3(0.34f, 0.058f, 1f), Black);
            Quad(scene, new Vector3(s * 0.40f, 0f, 0.30f), new Vector3(0.32f, 0.036f, 1f), Yellow);
            Quad(scene, new Vector3(s * 0.25f, -0.045f, 0.31f), new Vector3(0.058f, 0.13f, 1f), Black);
            Quad(scene, new Vector3(s * 0.25f, -0.045f, 0.30f), new Vector3(0.036f, 0.11f, 1f), Yellow);
        }
        Quad(scene, new Vector3(0f, 0f, 0.31f), new Vector3(0.07f, 0.07f, 1f), Black);
        Quad(scene, new Vector3(0f, 0f, 0.30f), new Vector3(0.048f, 0.048f, 1f), Yellow);

        // ── AIRSPEED TAPE (left) ───────────────────────────────────────────────
        TapeBg(scene, SpdX0, SpdX1, -TapeY, TapeY);
        for (int i = 0; i < 22; i++)
            spdTicks.Add(Quad(scene, Vector3.zero, new Vector3(0.07f, 0.012f, 1f), White));
        for (int i = 0; i < 12; i++)
        {
            var t = Text(scene, Vector3.zero, TickLab, false, White);
            t.anchor = TextAnchor.MiddleRight;
            spdLabs.Add(t);
        }
        bandWhite = Quad(scene, Vector3.zero, Vector3.one, White);
        bandGreen = Quad(scene, Vector3.zero, Vector3.one, new Color(0.10f, 0.80f, 0.20f));
        bandYellow = Quad(scene, Vector3.zero, Vector3.one, Yellow);
        bandRed = Quad(scene, Vector3.zero, Vector3.one, new Color(0.90f, 0.10f, 0.10f));
        PointerBox(scene, SpdX0 + 0.02f, SpdX1 - 0.02f, true);
        spdText = Text(scene, new Vector3((SpdX0 + SpdX1) * 0.5f - 0.02f, 0f, 0.05f), ValSize * 0.95f, true, White);
        Text(scene, new Vector3((SpdX0 + SpdX1) * 0.5f + 0.06f, TapeY + 0.08f, 0.1f), LabSize, false, Cyan).text = "km/h";

        // ── ALTITUDE TAPE + VSI (right) ────────────────────────────────────────
        TapeBg(scene, AltX0, AltX1, -TapeY, TapeY);
        for (int i = 0; i < 26; i++)
            altTicks.Add(Quad(scene, Vector3.zero, new Vector3(0.07f, 0.012f, 1f), White));
        for (int i = 0; i < 6; i++)
        {
            var t = Text(scene, Vector3.zero, TickLab, false, White);
            t.anchor = TextAnchor.MiddleLeft;
            altLabs.Add(t);
        }
        PointerBox(scene, AltX0 + 0.02f, AltX1 + 0.02f, false);
        altText = Text(scene, new Vector3((AltX0 + AltX1) * 0.5f + 0.05f, 0f, 0.05f), ValSize * 0.95f, true, White);
        Text(scene, new Vector3((AltX0 + AltX1) * 0.5f, TapeY + 0.08f, 0.1f), LabSize, false, Cyan).text = "m";

        TapeBg(scene, VsiX0, VsiX1, -VsiMax * VsiGain - 0.05f, VsiMax * VsiGain + 0.05f);
        foreach (float v in new[] { -10f, -5f, 0f, 5f, 10f })
            Quad(scene, new Vector3(VsiX0 + 0.04f, v * VsiGain, 0.15f), new Vector3(v == 0f ? 0.08f : 0.05f, 0.012f, 1f), White);
        foreach (float v in new[] { -7.5f, -2.5f, 2.5f, 7.5f })
            Quad(scene, new Vector3(VsiX0 + 0.03f, v * VsiGain, 0.15f), new Vector3(0.03f, 0.010f, 1f), Dim);
        vsiNeedle = new GameObject("VsiNeedle").transform;
        vsiNeedle.SetParent(scene, false);
        Quad(vsiNeedle, new Vector3(VsiX0 + 0.11f, 0f, 0.12f), new Vector3(0.12f, 0.05f, 1f), Black);
        Quad(vsiNeedle, new Vector3(VsiX0 + 0.11f, 0f, 0.11f), new Vector3(0.10f, 0.032f, 1f), White);

        // ── HEADING TAPE (top) ─────────────────────────────────────────────────
        TapeBg(scene, -HdgHalfW, HdgHalfW, HdgY0, 1.52f);
        for (int i = 0; i < 34; i++)
            hdgTicks.Add(Quad(scene, Vector3.zero, new Vector3(0.012f, 0.06f, 1f), White));
        for (int i = 0; i < 7; i++) hdgLabs.Add(Text(scene, Vector3.zero, TickLab, false, White));
        // boxed heading: black box, white rim, dropping below the tape like a lubber window
        Quad(scene, new Vector3(0f, 1.30f, 0.09f), new Vector3(0.50f, 0.30f, 1f), White);
        Quad(scene, new Vector3(0f, 1.30f, 0.08f), new Vector3(0.47f, 0.27f, 1f), Black);
        Triangle(scene, new Vector3(0f, 1.125f, 0.08f), 0.07f, 0.05f, false, White);
        hdgText = Text(scene, new Vector3(0f, 1.30f, 0.05f), ValSize * 0.85f, true, White);

        // ── SOFT-KEY STRIP (bottom): FLAP, TRIM, VS ────────────────────────────
        Quad(scene, new Vector3(0f, -1.33f, 0.2f), new Vector3(5f, 0.36f, 1f), new Color(0.05f, 0.05f, 0.07f));
        Quad(scene, new Vector3(0f, -1.15f, 0.19f), new Vector3(5f, 0.012f, 1f), new Color(0.35f, 0.38f, 0.42f));
        Text(scene, new Vector3(-1.72f, -1.23f, 0.1f), LabSize, false, Cyan).text = "FLAP %";
        flapText = Text(scene, new Vector3(-1.72f, -1.39f, 0.1f), ValSize * 0.62f, true, White);
        Text(scene, new Vector3(-0.95f, -1.23f, 0.1f), LabSize, false, Cyan).text = "TRIM";
        trimText = Text(scene, new Vector3(-0.95f, -1.39f, 0.1f), ValSize * 0.62f, true, White);
        Text(scene, new Vector3(1.66f, -1.23f, 0.1f), LabSize, false, Cyan).text = "VS m/s";
        vsText = Text(scene, new Vector3(1.66f, -1.39f, 0.1f), ValSize * 0.62f, true, White);

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

    void TapeBg(Transform scene, float x0, float x1, float y0, float y1)
    {
        Quad(scene, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0.2f), new Vector3(x1 - x0, y1 - y0, 1f), Tape, true);
    }

    /// <summary>The boxed readout that pokes out of a tape toward the attitude (pointsRight
    /// for the airspeed box, left for the altitude box).</summary>
    void PointerBox(Transform scene, float x0, float x1, bool pointsRight)
    {
        float cx = (x0 + x1) * 0.5f, w = x1 - x0;
        Quad(scene, new Vector3(cx, 0f, 0.09f), new Vector3(w + 0.025f, 0.30f, 1f), White);
        Quad(scene, new Vector3(cx, 0f, 0.08f), new Vector3(w, 0.275f, 1f), Black);
        float tipX = pointsRight ? x1 + 0.0125f : x0 - 0.0125f;
        var t = Triangle(scene, new Vector3(tipX, 0f, 0.085f), 0.30f, 0.09f, pointsRight, White, true);
        var tb = Triangle(scene, new Vector3(tipX - (pointsRight ? 0.0125f : -0.0125f), 0f, 0.075f), 0.275f, 0.08f, pointsRight, Black, true);
        _ = t; _ = tb;
    }

    void Update()
    {
        if (phys == null || horizon == null) return;
        float roll = phys.RollDeg, pitch = phys.PitchDeg;
        horizon.localRotation = Quaternion.Euler(0f, 0f, roll);
        horizon.localPosition = Quaternion.Euler(0f, 0f, roll) * new Vector3(0f, -pitch * pitchGain, 0f);
        if (rollPointer) rollPointer.localRotation = Quaternion.Euler(0f, 0f, roll);

        float spd = phys.AirspeedKmh, alt = phys.AltitudeM, hdg = phys.HeadingDeg;
        if (hdgText) hdgText.text = Mathf.RoundToInt(Mathf.Repeat(hdg, 360f)).ToString("000") + "°";
        if (spdText) spdText.text = Mathf.RoundToInt(spd).ToString();
        if (altText) altText.text = Mathf.RoundToInt(alt).ToString();

        UpdateSpeedTape(spd);
        UpdateAltTape(alt);
        UpdateHeadingTape(hdg);

        if (flapText)
        {
            int actual = Mathf.RoundToInt(phys.Flaps01 * 100f);
            var ctl = phys.GetComponent<AircraftController>();
            int selected = ctl != null ? Mathf.RoundToInt(ctl.FlapsSelected * 100f) : actual;
            // Amber when the flaps are not where they were asked to be — the flap-motor
            // failure's one honest visual cue.
            bool disagree = Mathf.Abs(selected - actual) > 4;
            flapText.text = disagree ? actual + "/" + selected : actual.ToString();
            flapText.color = disagree ? new Color(1f, 0.75f, 0.2f) : White;
        }
        if (trimText)
        {
            float t = phys.trim;
            trimText.text = Mathf.Abs(t) < 0.02f ? "0" : (t > 0f ? "U" : "D") + Mathf.Abs(t).ToString("F2");
        }
        float vs = phys.VerticalSpeedMs;
        if (vsText)
        {
            vsText.text = vs > 0.05f  ? "^" + vs.ToString("F1")
                        : vs < -0.05f ? "v" + Mathf.Abs(vs).ToString("F1")
                        : "+0.0";
        }
        if (vsiNeedle) vsiNeedle.localPosition = new Vector3(0f, Mathf.Clamp(vs, -VsiMax, VsiMax) * VsiGain, 0f);
    }

    void UpdateSpeedTape(float v)
    {
        float baseV = Mathf.Floor(v / 10f) * 10f;
        int n = spdTicks.Count, li = 0;
        for (int k = 0; k < n; k++)
        {
            float val = baseV + (k - n / 2) * 10f;
            float y = (val - v) * SpdGain;
            bool on = val >= 0f && Mathf.Abs(y) < TapeY - 0.01f;
            var t = spdTicks[k];
            t.gameObject.SetActive(on);
            if (!on) continue;
            bool major = Mathf.RoundToInt(val) % 20 == 0;
            t.localScale = new Vector3(major ? 0.11f : 0.065f, 0.012f, 1f);
            t.localPosition = new Vector3(SpdX1 - t.localScale.x * 0.5f, y, 0.15f);
            if (major && Mathf.Abs(y) > 0.17f && Mathf.Abs(y) < TapeY - 0.05f && li < spdLabs.Count)
            {
                var l = spdLabs[li++];
                l.gameObject.SetActive(true);
                l.text = Mathf.RoundToInt(val).ToString();
                l.transform.localPosition = new Vector3(SpdX1 - 0.15f, y, 0.12f);
            }
        }
        for (; li < spdLabs.Count; li++) spdLabs[li].gameObject.SetActive(false);

        Band(bandWhite, v, Vso, Vfe, SpdX1 - 0.012f, 0.024f);
        Band(bandGreen, v, Vs1, Vno, SpdX1 + 0.012f, 0.024f);
        Band(bandYellow, v, Vno, Vne, SpdX1 + 0.012f, 0.024f);
        Band(bandRed, v, Vne, Vne + 400f, SpdX1 + 0.012f, 0.024f);
    }

    void Band(Transform b, float v, float lo, float hi, float x, float w)
    {
        float y0 = Mathf.Clamp((lo - v) * SpdGain, -TapeY, TapeY), y1 = Mathf.Clamp((hi - v) * SpdGain, -TapeY, TapeY);
        bool on = y1 - y0 > 0.002f;
        b.gameObject.SetActive(on);
        if (!on) return;
        b.localPosition = new Vector3(x, (y0 + y1) * 0.5f, 0.14f);
        b.localScale = new Vector3(w, y1 - y0, 1f);
    }

    void UpdateAltTape(float a)
    {
        float baseA = Mathf.Floor(a / 20f) * 20f;
        int n = altTicks.Count, li = 0;
        for (int k = 0; k < n; k++)
        {
            float val = baseA + (k - n / 2) * 20f;
            float y = (val - a) * AltGain;
            bool on = Mathf.Abs(y) < TapeY - 0.01f;
            var t = altTicks[k];
            t.gameObject.SetActive(on);
            if (!on) continue;
            bool major = Mathf.RoundToInt(val) % 100 == 0;
            t.localScale = new Vector3(major ? 0.11f : 0.06f, 0.012f, 1f);
            t.localPosition = new Vector3(AltX0 + t.localScale.x * 0.5f, y, 0.15f);
            if (major && Mathf.Abs(y) > 0.17f && Mathf.Abs(y) < TapeY - 0.05f && li < altLabs.Count)
            {
                var l = altLabs[li++];
                l.gameObject.SetActive(true);
                l.text = Mathf.RoundToInt(val).ToString();
                l.transform.localPosition = new Vector3(AltX0 + 0.14f, y, 0.12f);
            }
        }
        for (; li < altLabs.Count; li++) altLabs[li].gameObject.SetActive(false);
    }

    static readonly string[] Cardinal = { "N", "3", "6", "E", "12", "15", "S", "21", "24", "W", "30", "33" };

    void UpdateHeadingTape(float h)
    {
        float baseH = Mathf.Floor(h / 5f) * 5f;
        int n = hdgTicks.Count, li = 0;
        float yMid = (HdgY0 + 1.5f) * 0.5f;
        for (int k = 0; k < n; k++)
        {
            float val = baseH + (k - n / 2) * 5f;
            float x = (val - h) * HdgGain;
            bool on = Mathf.Abs(x) < HdgHalfW - 0.02f;
            var t = hdgTicks[k];
            t.gameObject.SetActive(on);
            if (!on) continue;
            int deg = Mathf.RoundToInt(Mathf.Repeat(val, 360f));
            bool ten = deg % 10 == 0;
            t.localScale = new Vector3(0.012f, ten ? 0.075f : 0.045f, 1f);
            t.localPosition = new Vector3(x, HdgY0 + t.localScale.y * 0.5f, 0.15f);
            if (deg % 30 == 0 && Mathf.Abs(x) > 0.3f && Mathf.Abs(x) < HdgHalfW - 0.08f && li < hdgLabs.Count)
            {
                var l = hdgLabs[li++];
                l.gameObject.SetActive(true);
                l.text = Cardinal[(deg / 30) % 12];
                l.color = deg % 90 == 0 ? Cyan : White;
                l.transform.localPosition = new Vector3(x, yMid + 0.05f, 0.12f);
            }
        }
        for (; li < hdgLabs.Count; li++) hdgLabs[li].gameObject.SetActive(false);
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

    /// <summary>An isosceles triangle with its apex pointing up (or down / sideways).</summary>
    static Transform Triangle(Transform parent, Vector3 pos, float baseW, float height, bool up, Color color, bool sideways = false)
    {
        var g = new GameObject("Tri");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var m = new Mesh();
        Vector3 a, b, c;
        if (!sideways)
        {
            float s = up ? 1f : -1f;
            a = new Vector3(-baseW * 0.5f, 0f, 0f); b = new Vector3(baseW * 0.5f, 0f, 0f); c = new Vector3(0f, s * height, 0f);
            if (!up) { a = new Vector3(-baseW * 0.5f, 0f, 0f); b = new Vector3(0f, -height, 0f); c = new Vector3(baseW * 0.5f, 0f, 0f); }
            else { a = new Vector3(-baseW * 0.5f, 0f, 0f); b = new Vector3(0f, height, 0f); c = new Vector3(baseW * 0.5f, 0f, 0f); }
        }
        else
        {
            // `up` = points right (+x)
            float s = up ? 1f : -1f;
            a = new Vector3(0f, -baseW * 0.5f, 0f); b = new Vector3(0f, baseW * 0.5f, 0f); c = new Vector3(s * height, 0f, 0f);
        }
        m.vertices = new[] { a, b, c };
        // double-sided so the winding can never hide it
        m.triangles = new[] { 0, 1, 2, 0, 2, 1 };
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
