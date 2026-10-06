using UnityEngine;

/// <summary>
/// The three STANDBY instruments every G1000 Cessna 172 carries on its panel — a mechanical
/// airspeed indicator, attitude indicator and altimeter — live, driven by the same aircraft
/// state as the PFD.
///
/// They are what makes the panel read as a real aeroplane's rather than two screens on a
/// board: round, black-faced, white-marked instruments with the standard airspeed arcs.
/// Units match the rest of this simulator (km/h, metres).
///
/// Placement is in RealCockpitModel (holder) space, on the only clear panel either side of
/// the displays, measured from the pilot's view:
///   * ASI         left of the PFD   (PFD's left edge x = -0.1134; panel edge ~ -0.157)
///   * attitude    right of the MFD. Since 6 Oct 2026 the MFD has slid 24 mm left
///                 (RealCockpit.CloseCentreStrip), so its surround now ends at x 0.0674;
///                 the dials moved ~11 mm left and keep a ~10 mm gap after the map.
///   * altimeter   outboard of it
/// all at y = 0.500 (the displays' centre line is 0.492), ABOVE the control placards and
/// clear of every control's capture volume, and standing proud of the panel face like the
/// display glass (z = 0.7535).
///
/// Purely visual: no colliders, nothing interactive, nothing logged.
/// </summary>
public class StandbyInstruments : MonoBehaviour
{
    CessnaPhysics phys;
    Transform asiNeedle, altLong, altShort;
    Mesh adiMesh; Vector2[] adiUv; Vector3[] adiPos; float adiR;

    const float Z = 0.7535f;
    static Shader textShader;

    public void Build(CessnaPhysics p, Transform holder, int layer)
    {
        phys = p;
        var root = new GameObject("StandbyInstruments").transform;
        root.SetParent(holder, false);

        var asi = Gauge(root, "StandbyASI", new Vector3(-0.1360f, 0.5000f, Z), 0.0145f, AsiFace());
        asiNeedle = Needle(asi, 0.0118f, 0.0011f, Color.white);
        AsiNumbers(asi, 0.0145f);

        var adi = Gauge(root, "StandbyADI", new Vector3(0.0936f, 0.5000f, Z), 0.0138f, null);
        BuildAdi(adi, 0.0138f);

        var alt = Gauge(root, "StandbyALT", new Vector3(0.1271f, 0.5000f, Z), 0.0138f, AltFace());
        altShort = Needle(alt, 0.0075f, 0.0016f, Color.white);
        altLong = Needle(alt, 0.0118f, 0.0010f, Color.white);
        AltNumbers(alt, 0.0138f);

        SetLayer(root, layer);
    }

    void LateUpdate()
    {
        if (phys == null) return;
        // ASI: 0..320 km/h over 330 degrees, clockwise from 12 o'clock
        float v = Mathf.Clamp(phys.AirspeedKmh, 0f, 320f);
        if (asiNeedle) asiNeedle.localRotation = Quaternion.Euler(0f, 0f, -v / 320f * 330f);
        // altimeter: long needle 1000 m per turn, short 10 000 m per turn
        float a = Mathf.Max(0f, phys.AltitudeM);
        if (altLong) altLong.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Repeat(a, 1000f) / 1000f * 360f);
        if (altShort) altShort.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Repeat(a, 10000f) / 10000f * 360f);
        // attitude: rotate + shift the ball's texture coordinates under the fixed face
        if (adiMesh != null)
        {
            float roll = phys.RollDeg * Mathf.Deg2Rad, pitch = phys.PitchDeg;
            float c = Mathf.Cos(roll), s = Mathf.Sin(roll);
            for (int i = 0; i < adiPos.Length; i++)
            {
                // disc point in units of the radius, rotated by bank, offset by pitch
                float x = adiPos[i].x / adiR, y = adiPos[i].y / adiR;
                float rx = c * x - s * y, ry = s * x + c * y;
                adiUv[i] = new Vector2(0.5f + rx * 0.3f, 0.5f + ry * 0.3f + Mathf.Clamp(pitch, -35f, 35f) * 0.015f);
            }
            adiMesh.uv = adiUv;
        }
    }

    // ── construction ────────────────────────────────────────────────────────
    Transform Gauge(Transform root, string name, Vector3 pos, float r, Texture2D face)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        g.localPosition = pos;
        // bezel: a black ring standing a little proud, with a faint metal lip
        Ring(g, r, r + 0.0022f, 0.0006f, new Color(0.05f, 0.05f, 0.055f), 0.35f);
        Ring(g, r + 0.0016f, r + 0.0024f, 0.0008f, new Color(0.35f, 0.36f, 0.38f), 0.6f);
        // a panel-coloured backing so the gauge seats on the panel even if the panel dips
        Disc(g, r + 0.0030f, 0.0012f, new Color(0.14f, 0.145f, 0.155f), null, 0.1f);
        if (face != null) Disc(g, r, 0f, Color.white, face, 0.25f);
        return g;
    }

    /// <summary>A flat disc facing the pilot (-Z), at depth dz behind the gauge plane.</summary>
    static MeshRenderer Disc(Transform parent, float r, float dz, Color col, Texture2D tex, float gloss)
    {
        const int N = 48;
        var m = new Mesh();
        var v = new Vector3[N + 1]; var uv = new Vector2[N + 1]; var n = new Vector3[N + 1];
        v[0] = new Vector3(0f, 0f, dz); uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < N; i++)
        {
            float a = i * Mathf.PI * 2f / N;
            v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, dz);
            uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
        }
        for (int i = 0; i <= N; i++) n[i] = Vector3.back;
        var t = new int[N * 3];
        // front face toward -Z: Cross(b-a, c-a) must point -Z -> go clockwise seen from -Z
        for (int i = 0; i < N; i++) { t[i * 3] = 0; t[i * 3 + 1] = 1 + (i + 1) % N; t[i * 3 + 2] = 1 + i; }
        m.vertices = v; m.uv = uv; m.normals = n; m.triangles = t;
        m.RecalculateBounds();
        var g = new GameObject("Disc");
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = g.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Standard")) { color = col };
        if (tex != null) mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", gloss);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return mr;
    }

    /// <summary>An annulus facing the pilot, standing `h` proud of the gauge plane,
    /// with its outer wall.</summary>
    static void Ring(Transform parent, float r0, float r1, float h, Color col, float gloss)
    {
        const int N = 48;
        var m = new Mesh();
        var v = new System.Collections.Generic.List<Vector3>();
        var n = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
            Vector3 i0 = new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0, -h), i1 = new Vector3(Mathf.Cos(a1) * r0, Mathf.Sin(a1) * r0, -h);
            Vector3 o0 = new Vector3(Mathf.Cos(a0) * r1, Mathf.Sin(a0) * r1, -h), o1 = new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1, -h);
            int b = v.Count;
            v.AddRange(new[] { i0, i1, o1, o0 }); n.AddRange(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            // outer wall down to the panel
            Vector3 w0 = new Vector3(o0.x, o0.y, 0.001f), w1 = new Vector3(o1.x, o1.y, 0.001f);
            Vector3 nn = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f), 0f);
            b = v.Count;
            v.AddRange(new[] { o0, o1, w1, w0 }); n.AddRange(new[] { nn, nn, nn, nn });
            t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            // inner wall down to the face
            Vector3 f0 = new Vector3(i0.x, i0.y, 0f), f1 = new Vector3(i1.x, i1.y, 0f);
            b = v.Count;
            v.AddRange(new[] { i0, i1, f1, f0 }); n.AddRange(new[] { -nn, -nn, -nn, -nn });
            t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
        // winding was chosen by hand above; make the material double-sided-safe by
        // appending the reversed set so a wrong guess can never leave a hole
        var tt = new System.Collections.Generic.List<int>(t);
        for (int i = 0; i < t.Count; i += 3) { tt.Add(t[i]); tt.Add(t[i + 2]); tt.Add(t[i + 1]); }
        m.SetTriangles(tt, 0);
        m.RecalculateBounds();
        var g = new GameObject("Ring");
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = g.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Standard")) { color = col };
        mat.SetFloat("_Glossiness", gloss);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Transform Needle(Transform gauge, float len, float w, Color col)
    {
        var pivot = new GameObject("Needle").transform;
        pivot.SetParent(gauge, false);
        pivot.localPosition = new Vector3(0f, 0f, -0.0003f);
        var m = new Mesh();
        // tapered needle pointing +Y (12 o'clock), with a short tail
        m.vertices = new[] { new Vector3(-w * 0.5f, -len * 0.18f, 0f), new Vector3(w * 0.5f, -len * 0.18f, 0f),
                             new Vector3(w * 0.2f, len, 0f), new Vector3(-w * 0.2f, len, 0f) };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.RecalculateBounds();
        var g = new GameObject("Blade");
        g.transform.SetParent(pivot, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = col };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // hub cap
        var cap = Disc(pivot, w * 1.6f, -0.0002f, new Color(0.1f, 0.1f, 0.1f), null, 0.4f);
        _ = cap;
        return pivot;
    }

    void BuildAdi(Transform gauge, float r)
    {
        // the moving ball: a disc whose UVs are rewritten every frame
        var mr = Disc(gauge, r, 0f, Color.white, AdiBall(), 0.2f);
        var mf = mr.GetComponent<MeshFilter>();
        adiMesh = Instantiate(mf.sharedMesh);
        mf.sharedMesh = adiMesh;
        adiPos = adiMesh.vertices;
        adiUv = adiMesh.uv;
        adiR = r;
        // fixed symbology in front: orange wings, centre dot, bank index at the top
        var o = new Color(1f, 0.55f, 0.08f);
        Bar(gauge, new Vector3(-0.0060f, 0f, -0.0004f), new Vector2(0.0060f, 0.0010f), o);
        Bar(gauge, new Vector3( 0.0060f, 0f, -0.0004f), new Vector2(0.0060f, 0.0010f), o);
        Bar(gauge, new Vector3(0f, 0f, -0.0004f), new Vector2(0.0014f, 0.0014f), o);
        for (int k = -3; k <= 3; k++)
        {
            float a = k * 30f * Mathf.Deg2Rad;
            var b = Bar(gauge, new Vector3(Mathf.Sin(a) * r * 0.9f, Mathf.Cos(a) * r * 0.9f, -0.0004f), new Vector2(0.0006f, k == 0 ? 0.0028f : 0.0018f), Color.white);
            b.localRotation = Quaternion.Euler(0f, 0f, -k * 30f);
        }
    }

    static Transform Bar(Transform parent, Vector3 pos, Vector2 size, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var c = g.GetComponent<Collider>(); if (c) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = new Vector3(size.x, size.y, 1f);
        var mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = col };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    // ── dial textures ───────────────────────────────────────────────────────
    const int T = 256;

    static Texture2D NewTex()
    {
        return new Texture2D(T, T, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
    }

    /// <summary>Polar coordinates of a texel: r in 0..1 of the face radius, a = degrees
    /// CLOCKWISE from 12 o'clock.</summary>
    static void Polar(int x, int y, out float r, out float a)
    {
        float dx = (x + 0.5f) / T * 2f - 1f, dy = (y + 0.5f) / T * 2f - 1f;
        r = Mathf.Sqrt(dx * dx + dy * dy);
        a = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);
    }

    static Texture2D AsiFace()
    {
        var t = NewTex(); var px = new Color[T * T];
        Color face = new Color(0.035f, 0.035f, 0.04f), wht = new Color(0.92f, 0.92f, 0.90f);
        Color green = new Color(0.10f, 0.70f, 0.20f), yellow = new Color(0.95f, 0.80f, 0.10f), red = new Color(0.85f, 0.08f, 0.06f);
        const float Vso = 61f, Vs1 = 89f, Vfe = 157f, Vno = 239f, Vne = 302f;
        for (int y = 0; y < T; y++)
            for (int x = 0; x < T; x++)
            {
                Polar(x, y, out float r, out float a);
                Color c = face;
                float v = a / 330f * 320f;
                if (a <= 330f)
                {
                    // arcs: white (flap range) inside, green/yellow outside
                    if (r > 0.80f && r < 0.86f && v >= Vso && v <= Vfe) c = wht;
                    if (r > 0.87f && r < 0.95f && v >= Vs1 && v <= Vno) c = green;
                    if (r > 0.87f && r < 0.95f && v > Vno && v <= Vne) c = yellow;
                    if (r > 0.80f && r < 0.97f && Mathf.Abs(v - Vne) < 1.6f) c = red;
                    // ticks: 10 km/h minor, 20 major
                    float mod10 = Mathf.Abs(Mathf.Repeat(v + 5f, 10f) - 5f);
                    float mod20 = Mathf.Abs(Mathf.Repeat(v + 10f, 20f) - 10f);
                    if (r > 0.72f && r < 0.80f && mod20 < 0.9f) c = wht;
                    else if (r > 0.75f && r < 0.80f && mod10 < 0.7f) c = wht;
                }
                if (r > 0.985f) c = face * 0.6f;
                c.a = 1f;
                px[y * T + x] = c;
            }
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    static Texture2D AltFace()
    {
        var t = NewTex(); var px = new Color[T * T];
        Color face = new Color(0.035f, 0.035f, 0.04f), wht = new Color(0.92f, 0.92f, 0.90f);
        for (int y = 0; y < T; y++)
            for (int x = 0; x < T; x++)
            {
                Polar(x, y, out float r, out float a);
                Color c = face;
                float d = Mathf.Abs(Mathf.Repeat(a + 18f, 36f) - 18f);     // 10 majors
                float m = Mathf.Abs(Mathf.Repeat(a + 3.6f, 7.2f) - 3.6f);  // 50 minors
                if (r > 0.74f && r < 0.94f && d < 1.4f) c = wht;
                else if (r > 0.84f && r < 0.94f && m < 0.9f) c = wht;
                // Kollsman-style window at 3 o'clock
                if (r > 0.30f && r < 0.55f && Mathf.Abs(a - 90f) < 14f) c = new Color(0.75f, 0.75f, 0.72f);
                if (r > 0.985f) c = face * 0.6f;
                c.a = 1f;
                px[y * T + x] = c;
            }
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    static Texture2D AdiBall()
    {
        var t = NewTex(); var px = new Color[T * T];
        Color sky = new Color(0.20f, 0.50f, 0.85f), gnd = new Color(0.45f, 0.30f, 0.14f), wht = Color.white;
        for (int y = 0; y < T; y++)
            for (int x = 0; x < T; x++)
            {
                float u = (x + 0.5f) / T, v = (y + 0.5f) / T;
                Color c = v > 0.5f ? sky : gnd;
                if (Mathf.Abs(v - 0.5f) < 0.004f) c = wht;
                // pitch marks every 10 degrees (0.15 uv), 5-degree half marks
                foreach (int k in new[] { -2, -1, 1, 2 })
                {
                    float vy = 0.5f + k * 0.15f;
                    if (Mathf.Abs(v - vy) < 0.003f && Mathf.Abs(u - 0.5f) < 0.07f) c = wht;
                    float vh = 0.5f + (k - Mathf.Sign(k) * 0.5f) * 0.15f;
                    if (Mathf.Abs(v - vh) < 0.0025f && Mathf.Abs(u - 0.5f) < 0.035f && Mathf.Abs(k) >= 1) c = wht;
                }
                c.a = 1f;
                px[y * T + x] = c;
            }
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    // ── numerals ────────────────────────────────────────────────────────────
    void AsiNumbers(Transform g, float r)
    {
        for (int v = 40; v <= 280; v += 40)
        {
            float a = v / 320f * 330f * Mathf.Deg2Rad;
            Label(g, new Vector3(Mathf.Sin(a) * r * 0.55f, Mathf.Cos(a) * r * 0.55f, -0.0002f), v.ToString(), 0.0021f);
        }
        Label(g, new Vector3(0f, -r * 0.30f, -0.0002f), "km/h", 0.0013f);
    }

    void AltNumbers(Transform g, float r)
    {
        for (int k = 0; k < 10; k++)
        {
            float a = k * 36f * Mathf.Deg2Rad;
            Label(g, new Vector3(Mathf.Sin(a) * r * 0.58f, Mathf.Cos(a) * r * 0.58f, -0.0002f), k.ToString(), 0.0024f);
        }
        Label(g, new Vector3(0f, r * 0.26f, -0.0002f), "ALT", 0.0012f);
        Label(g, new Vector3(0f, -r * 0.30f, -0.0002f), "x100 m", 0.0011f);
    }

    static void Label(Transform parent, Vector3 lp, string text, float capHeight)
    {
        const int Pt = 72;
        var tm = new GameObject("Num").AddComponent<TextMesh>();
        tm.transform.SetParent(parent, false);
        tm.transform.localPosition = lp;
        tm.text = text;
        tm.characterSize = capHeight * 10f / Pt;
        tm.fontSize = Pt;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.93f, 0.93f, 0.91f);
        var mr = tm.GetComponent<MeshRenderer>();
        if (textShader == null) textShader = Shader.Find("GUI/3D Text Shader");
        mr.sharedMaterial = textShader != null
            ? new Material(textShader) { mainTexture = tm.font.material.mainTexture }
            : tm.font.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var font = tm.font; var mat = mr.sharedMaterial;
        Font.textureRebuilt += f => { if (f == font && mat != null) mat.mainTexture = font.material.mainTexture; };
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }
}
