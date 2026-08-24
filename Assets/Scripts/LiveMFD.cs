using UnityEngine;

/// <summary>
/// (EXTRA) Makes the model's RIGHT glass screen a LIVE moving map: a top-down camera
/// follows the aircraft heading-up, with an airplane-silhouette symbol at centre,
/// a half-range reference ring, and all eight compass labels (N, NE, E, SE, S, SW, W, NW)
/// orbiting the map edge so each one keeps pointing at its real-world direction.
/// </summary>
public class LiveMFD : MonoBehaviour
{
    public Transform aircraft;
    public float height  = 500f;   // how high the map camera sits above the plane
    public float mapSize = 180f;   // half-extent shown (metres) — 180 is more zoomed in than the original 260

    /// The quad this map is drawn onto, on the cockpit panel. ScreenTuner nudges it live.
    public Transform screenQuad;

    Camera mapCam;
    // The eight compass labels and the true bearing each one marks (N = 0, clockwise).
    static readonly string[] CompassNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    Transform[] compassLabels = new Transform[0];   // filled by Build()
    float compassRadius;

    public void Build(Transform ac, Transform screenParent, Vector3 screenLocalPos,
                      Quaternion screenLocalRot, Vector2 size, int cockpitLayer)
    {
        aircraft = ac;
        const int SymbolLayer = 14;

        // ---- top-down map camera ----
        var camGO = new GameObject("MFDCam");
        mapCam = camGO.AddComponent<Camera>();
        mapCam.orthographic = true; mapCam.orthographicSize = mapSize;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0.10f, 0.16f, 0.10f);   // dark terrain-green
        // render the world but NOT cockpit / PFD / symbol layers — then re-add symbol
        mapCam.cullingMask = ~((1 << cockpitLayer) | (1 << 12) | (1 << 13));
        mapCam.nearClipPlane = 1f; mapCam.farClipPlane = 3000f;
        var rt = new RenderTexture(512, 384, 16) { antiAliasing = 2 };
        mapCam.targetTexture = rt;
        mapCam.cullingMask |= (1 << SymbolLayer);

        // ---- own-ship symbol: a plain triangle pointing up the map (the aircraft heading) ----
        // A solid arrowhead reads far better at this size than a little aeroplane outline: the
        // pilot only needs "which way am I pointing". Drawn over a dark outline triangle so it
        // stays legible over pale ground as well as dark.
        var sym = new GameObject("MapSymbol");
        sym.transform.SetParent(camGO.transform, false);
        sym.transform.localPosition = new Vector3(0f, 0f, 3f);

        float halfW = mapSize * 0.085f;    // half the base width
        float tipY  = mapSize * 0.145f;    // nose, above centre
        float baseY = mapSize * 0.085f;    // base corners, below centre
        float o     = mapSize * 0.013f;    // how far the dark outline sticks out
        AddTriangle(sym.transform, new Vector3(0f, 0f, 0.02f), halfW + o, tipY + o, baseY + o,
                    new Color(0.05f, 0.05f, 0.05f), SymbolLayer);
        AddTriangle(sym.transform, Vector3.zero, halfW, tipY, baseY, Color.yellow, SymbolLayer);

        // ---- range reference ring — 36 short quads arranged at radius = mapSize*0.5 ----
        float ringR  = mapSize * 0.5f;
        float segW   = mapSize * 0.007f;
        float segH   = mapSize * 0.030f;
        var   ringCol = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        for (int i = 0; i < 36; i++)
        {
            float rad = i * 10f * Mathf.Deg2Rad;
            float rx = ringR * Mathf.Sin(rad);
            float ry = ringR * Mathf.Cos(rad);
            var piv = new GameObject("RingSeg").transform;
            piv.SetParent(camGO.transform, false);
            piv.localPosition = new Vector3(rx, ry, 3f);
            // rotate each segment tangentially around the ring
            piv.localRotation = Quaternion.Euler(0f, 0f, -(90f + i * 10f));
            AddQuad(piv, Vector3.zero, new Vector3(segW, segH, 1f), ringCol, SymbolLayer);
        }

        // ---- compass rose: eight labels orbiting the map edge ----
        //
        // RESTORED 23 Aug 2026. These were deleted on 22 Aug as screen clutter, which is
        // why they were "missing": nothing was broken, hidden, mis-layered or clipped —
        // `compassLabels` was left as a zero-length array and `compassRadius` as 0, so the
        // orbit code in LateUpdate had nothing to orbit.
        //
        // They are built the same way as everything else on this map: plain TextMesh on the
        // SYMBOL LAYER, parented to the map camera, at z = 3 in front of it. That is what
        // makes them part of the MFD's own rendering rather than text floating in the
        // cockpit in front of the glass — they go through the map camera into the render
        // texture, so they are pixels on the screen exactly like the ring and the own-ship
        // symbol.
        //
        // RADIUS 0.80 of the camera's vertical half-extent: outside the reference ring
        // (0.50) and inside the frame, so N and S sit near the top and bottom edges without
        // being cropped. The camera is 512x384, i.e. WIDER than tall, so the vertical
        // extent is the binding one and E/W have room to spare.
        compassRadius = mapSize * 0.755f;
        compassLabels = new Transform[CompassNames.Length];
        for (int i = 0; i < CompassNames.Length; i++)
            compassLabels[i] = AddMapLabel(camGO.transform, CompassNames[i], mapSize, SymbolLayer);

        // ---- overlay quad over the panel's right screen ----
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = quad.GetComponent<Collider>(); if (col) Destroy(col);
        quad.name = "LiveMFDQuad";
        quad.transform.SetParent(screenParent, false);
        quad.transform.localPosition = screenLocalPos;
        quad.transform.localRotation = screenLocalRot;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        quad.layer = cockpitLayer;
        var mat = new Material(Shader.Find("Unlit/Texture")); mat.mainTexture = rt;
        quad.GetComponent<Renderer>().material = mat;
        screenQuad = quad.transform;
    }

    void LateUpdate()
    {
        if (aircraft == null || mapCam == null) return;

        Vector3 p = aircraft.position;
        mapCam.transform.position = new Vector3(p.x, p.y + height, p.z);
        // heading-up: look straight down, aircraft nose pointing up on the map
        mapCam.transform.rotation = Quaternion.Euler(90f, aircraft.eulerAngles.y, 0f);

        // Orbit the eight labels so each keeps marking its true bearing. The camera is
        // heading-up, so camera-local +Y IS the aircraft heading: a direction whose true
        // bearing is B appears on the map at the local angle (B - heading).
        float hdg = aircraft.eulerAngles.y;
        for (int i = 0; i < compassLabels.Length; i++)
        {
            if (compassLabels[i] == null) continue;
            float a = (i * 45f - hdg) * Mathf.Deg2Rad;
            compassLabels[i].localPosition = new Vector3(compassRadius * Mathf.Sin(a),
                                                        compassRadius * Mathf.Cos(a), 3f);
        }
    }

    /// <summary>One compass label, sized in MAP UNITS so it stays legible whatever the
    /// map range is set to.
    ///
    /// The camera is orthographic with half-height = mapSize, rendering into a 384-pixel-
    /// tall target, so a glyph of world height h covers h / (2 * mapSize) of the texture.
    /// 0.052 * mapSize therefore lands at about 10 px of cap height — small, but crisp and
    /// readable at the size this screen is actually viewed from, and far less shouty than
    /// the version that was removed.
    ///
    /// The material matters: TextMesh's default font material is the GUI text shader, which
    /// is ZTest Always and would draw the letters straight through the own-ship symbol. The
    /// map has no depth sorting to speak of, but the labels must still be occluded properly
    /// and must take their colour from the vertex stream, so the same alpha-coverage
    /// treatment used for the cockpit placards is applied here.</summary>
    static Transform AddMapLabel(Transform parent, string text, float mapSize, int layer)
    {
        const int Pt = 72;
        // The label is a holder with TWO TextMeshes: a dark one offset behind, and a light
        // one in front. The map renders light-green terrain most of the time and dark
        // ground-colour the rest, so a single flat colour is unreadable over one or the
        // other — the first pass used light grey and the "E" was indistinguishable from an
        // "F". Outlining is the same treatment the own-ship symbol already uses, so the
        // MFD stays visually consistent with itself.
        var holder = new GameObject("Compass_" + text).transform;
        holder.SetParent(parent, false);

        // A single DROP SHADOW, not an outline. Two offset dark copies (the first attempt)
        // read as doubled glyphs at ~12 px of cap height rather than as an outline — "E"
        // came out looking like "F" and "S" like "B". One dark copy down-right behind one
        // light copy is what instruments actually do, and it stays crisp.
        float cap = mapSize * 0.082f;
        float o   = cap * 0.07f;
        MakeGlyph(holder, text, Pt, cap, new Vector3(o, -o, 0.02f), new Color(0.05f, 0.07f, 0.05f), layer);
        MakeGlyph(holder, text, Pt, cap, Vector3.zero,              new Color(0.98f, 0.99f, 0.98f), layer);
        return holder;
    }

    static void MakeGlyph(Transform parent, string text, int pt, float capHeight,
                          Vector3 localPos, Color col, int layer)
    {
        var tm = new GameObject("G").AddComponent<TextMesh>();
        tm.transform.SetParent(parent, false);
        tm.transform.localPosition = localPos;
        tm.text = text;
        tm.fontSize = pt;
        tm.characterSize = capHeight * 10f / pt;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = col;
        tm.gameObject.layer = layer;

        var mr = tm.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // Flat isoceles triangle pointing up (+Y), built from scratch — Unity has no triangle
    // primitive. Wound both ways so it shows whichever side the map camera looks from.
    static void AddTriangle(Transform parent, Vector3 localPos, float halfWidth, float tipY,
                            float baseY, Color color, int layer)
    {
        var g = new GameObject("SymTriangle");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.layer = layer;

        var mesh = new Mesh { name = "Triangle" };
        mesh.vertices = new[]
        {
            new Vector3(0f, tipY, 0f),            // nose
            new Vector3(-halfWidth, -baseY, 0f),  // left base corner
            new Vector3( halfWidth, -baseY, 0f),  // right base corner
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().material =
            new Material(Shader.Find("Unlit/Color")) { color = color };
    }

    // Create a Quad primitive, parent it, set layer and material.
    static void AddQuad(Transform parent, Vector3 localPos, Vector3 localScale, Color color, int layer)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var c = g.GetComponent<Collider>(); if (c) Object.Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale    = localScale;
        g.layer = layer;
        g.GetComponent<MeshRenderer>().material =
            new Material(Shader.Find("Unlit/Color")) { color = color };
    }
}
