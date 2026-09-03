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

    // ── NAVIGATION SYMBOLOGY ────────────────────────────────────────────────
    // Before this, the map was a top-down camera on the terrain with a triangle in the
    // middle and eight compass letters. That is a MINIMAP: it tells the pilot where they
    // are on a picture, and nothing else. An aviation navigation display has to answer
    // "where am I GOING, how far, and how fast" — the route, the active leg, the range
    // the picture is drawn at, and the numbers. Those are what is added here.
    const int MaxLegs = 16;
    Transform routeRoot;
    Transform[] wpMarks = new Transform[0];
    Transform[] wpLabels = new Transform[0];
    Transform[] legs = new Transform[0];
    TextMesh gsText, trkText, altText, wptText, rngText;
    float halfWidth;                 // ortho half-width in map units (aspect * mapSize)
    /// <summary>Everything drawn ON the map hangs off this. Scaling it by
    /// (currentRange / mapSize) keeps every symbol the same size ON SCREEN while the
    /// camera's range changes, which is what a real range-selectable display does.</summary>
    Transform symRoot;
    float rangeM;                    // current map range (ortho half-height), metres
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

        // One container for all symbology, so the whole overlay can be scaled with the
        // selected range in a single place.
        symRoot = new GameObject("Symbology").transform;
        symRoot.SetParent(camGO.transform, false);
        rangeM = mapSize;

        // ---- own-ship symbol: a plain triangle pointing up the map (the aircraft heading) ----
        // A solid arrowhead reads far better at this size than a little aeroplane outline: the
        // pilot only needs "which way am I pointing". Drawn over a dark outline triangle so it
        // stays legible over pale ground as well as dark.
        var sym = new GameObject("MapSymbol");
        sym.transform.SetParent(symRoot, false);
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
            piv.SetParent(symRoot, false);
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
            compassLabels[i] = AddMapLabel(symRoot, CompassNames[i], mapSize, SymbolLayer);

        // ---- DIM OVERLAY ----
        // The map camera renders the real world, and from directly above at midday the
        // terrain is a flat sheet of bright green. That is why the display read as a
        // game minimap: an aviation screen is DARK, and its symbology is the brightest
        // thing on it. A translucent dark sheet between the terrain and the symbology
        // gives the instrument its own value range back — the ground is still there,
        // still moving, still readable as terrain, but it is now a background instead of
        // the subject.
        float aspect = 512f / 384f;
        halfWidth = mapSize * aspect;
        // Z ORDER MATTERS AND IS COUNTER-INTUITIVE HERE. The camera looks along its own
        // local +Z, so a LARGER local z is FURTHER AWAY. The symbology sits at z = 3, so
        // an overlay at z = 2 would be in front of it and would dim the very numbers it
        // exists to make readable — which is exactly what the first version did: the
        // GS/TRK/ALT block came out washed out while the near-white compass letters
        // survived, so the fault looked like a font-colour problem rather than a
        // depth-ordering one. The overlay belongs BEHIND the symbols and in front of the
        // world: z = 4.
        // LIGHTENED 3 Sep 2026, on request. The overlay was 78% opaque over a very dark
        // navy, which at dusk — when the terrain it sits on is already dark — left the map
        // almost black and the ground on it unreadable. It is now 48% over a slightly
        // lighter, less saturated tone, which brightens the terrain while keeping the
        // display darker than the symbology drawn in front of it. That ordering is the
        // point of the overlay and is preserved: the numbers and the compass are still the
        // brightest things on the screen, which is what makes it read as an instrument
        // rather than as a game minimap.
        AddTransparentQuad(symRoot, new Vector3(0f, 0f, 4.0f),
                           new Vector3(halfWidth * 2.4f, mapSize * 2.4f, 1f),
                           new Color(0.06f, 0.10f, 0.13f, 0.48f), SymbolLayer);

        // ---- ROUTE LAYER ----
        // Legs and waypoint marks live in MAP-CAMERA-LOCAL space and are repositioned
        // every LateUpdate from the aircraft's own position and heading. They are NOT
        // placed in the world: a world-space symbol on this layer would be rendered by
        // any other camera that happened to include the layer, and the participant would
        // see magenta diamonds hanging over the countryside.
        routeRoot = new GameObject("Route").transform;
        routeRoot.SetParent(symRoot, false);
        wpMarks = new Transform[MaxLegs];
        wpLabels = new Transform[MaxLegs];
        legs = new Transform[MaxLegs];
        var magenta = new Color(0.95f, 0.35f, 0.95f, 1f);
        for (int i = 0; i < MaxLegs; i++)
        {
            var leg = new GameObject("Leg" + i).transform;
            leg.SetParent(routeRoot, false);
            AddQuad(leg, Vector3.zero, Vector3.one, magenta, SymbolLayer);
            leg.gameObject.SetActive(false);
            legs[i] = leg;

            var mk = new GameObject("Wpt" + i).transform;
            mk.SetParent(routeRoot, false);
            // A diamond — the standard en-route waypoint symbol — as a square turned 45°.
            var d = new GameObject("Dia").transform;
            d.SetParent(mk, false);
            d.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddQuad(d, Vector3.zero, new Vector3(mapSize * 0.045f, mapSize * 0.045f, 1f), magenta, SymbolLayer);
            mk.gameObject.SetActive(false);
            wpMarks[i] = mk;

            wpLabels[i] = MakeDataLabel(routeRoot, "", mapSize * 0.045f, magenta, SymbolLayer).transform;
            wpLabels[i].gameObject.SetActive(false);
        }

        // ---- DATA BLOCK ----
        // The four numbers a pilot actually reads off a moving map, in the corners where
        // a G1000 puts them, plus the RANGE — because a map without a stated range is a
        // picture, not an instrument, and the pilot cannot judge any distance on it.
        var cyan = new Color(0.45f, 1f, 1f);
        float cap = mapSize * 0.068f;
        float mx = halfWidth * 0.97f, my = mapSize * 0.94f;
        gsText  = MakeDataLabel(symRoot, "GS ---", cap, cyan, SymbolLayer);
        trkText = MakeDataLabel(symRoot, "TRK ---", cap, cyan, SymbolLayer);
        altText = MakeDataLabel(symRoot, "ALT ---", cap, cyan, SymbolLayer);
        wptText = MakeDataLabel(symRoot, "", cap, new Color(0.95f, 0.35f, 0.95f), SymbolLayer);
        rngText = MakeDataLabel(symRoot, "", mapSize * 0.055f, new Color(0.85f, 0.90f, 0.94f), SymbolLayer);
        Place(gsText,  new Vector3(-mx, my, 3f), TextAnchor.UpperLeft);
        Place(trkText, new Vector3( mx, my, 3f), TextAnchor.UpperRight);
        Place(altText, new Vector3( mx, -my, 3f), TextAnchor.LowerRight);
        Place(wptText, new Vector3(-mx, -my, 3f), TextAnchor.LowerLeft);
        // On the ring, down-right — straight up collides with the N compass label.
        Place(rngText, new Vector3(mapSize * 0.37f, -mapSize * 0.30f, 3f), TextAnchor.MiddleLeft);
        rngText.text = FormatDistance(mapSize * 0.5f);

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

        UpdateRange();
        UpdateSymbology(p, hdg);
    }

    /// <summary>Choose the map range.
    ///
    /// The range used to be FIXED at 180 m — about two runway widths. That is a
    /// reasonable taxi range and a useless en-route one: a waypoint 4 km ahead is
    /// twenty-two screens away, so the route this display exists to show could never
    /// appear on it. A fixed range is also why the range annotation did not earn its
    /// place; a number that never changes is decoration.
    ///
    /// So the range follows the task: close on the ground where the geometry is the
    /// aerodrome, wide in the air, and wide enough to hold the ACTIVE WAYPOINT with
    /// room around it when there is one. Real displays let the pilot select this; a
    /// participant flying a workload experiment should not be spending attention on a
    /// range knob, so it is automatic and always annotated.
    ///
    /// It steps rather than slides — a continuously-zooming map is unreadable, because
    /// nothing on it holds still.</summary>
    void UpdateRange()
    {
        var phys = aircraft != null ? aircraft.GetComponent<CessnaPhysics>() : null;
        var gm = GameManager.Instance;
        var eng = gm != null ? gm.ScenarioRunner : null;
        bool onGround = phys != null && phys.Grounded;

        float want = onGround ? 250f : 1500f;
        if (eng != null && eng.Active && eng.HasWaypoint)
        {
            Vector3 w = eng.WaypointPos, a = aircraft.position;
            float d = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(w.x, w.z));
            want = Mathf.Max(want, d * 1.25f);
        }
        // Standard-ish range steps, so the picture settles instead of breathing.
        float[] steps = { 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
        float chosen = steps[steps.Length - 1];
        foreach (float st in steps) if (want <= st) { chosen = st; break; }

        if (Mathf.Abs(chosen - rangeM) < 1f) return;
        rangeM = chosen;
        mapCam.orthographicSize = rangeM;
        // Keep every symbol the same size on screen as the range changes.
        if (symRoot != null) symRoot.localScale = Vector3.one * (rangeM / mapSize);
        if (rngText != null) rngText.text = FormatDistance(rangeM * 0.5f);
        // The far clip has to reach the ground from the camera's height whatever the range.
        mapCam.farClipPlane = Mathf.Max(3000f, height * 3f);
    }

    /// <summary>World offset -> map-camera-local position. The camera is heading-up, so
    /// map +Y is the aircraft's nose: a point due north appears straight up only when the
    /// aeroplane is heading north.</summary>
    Vector2 ToMap(Vector3 worldOffset, float hdgDeg)
    {
        float r = hdgDeg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(worldOffset.x * c - worldOffset.z * sn,
                           worldOffset.x * sn + worldOffset.z * c);
    }

    void UpdateSymbology(Vector3 pos, float hdgDeg)
    {
        var phys = aircraft != null ? aircraft.GetComponent<CessnaPhysics>() : null;
        var gm = GameManager.Instance;
        var eng = gm != null ? gm.ScenarioRunner : null;

        // ---- data block ----
        if (phys != null)
        {
            // GROUND speed, not airspeed: this is the display that answers "when do I get
            // there", and in a wind those are different numbers. The PFD has the airspeed.
            if (gsText != null) gsText.text = "GS " + Mathf.RoundToInt(phys.GroundSpeedMs * 3.6f) + " km/h";
            // TRACK, not heading: on a moving map the useful number is the direction the
            // aeroplane is actually going over the ground.
            float trk = Mathf.Repeat(hdgDeg + phys.DriftAngleDeg, 360f);
            if (trkText != null) trkText.text = "TRK " + Mathf.RoundToInt(trk).ToString("000") + "\u00B0";
            if (altText != null) altText.text = "ALT " + Mathf.RoundToInt(phys.AltitudeM) + " m";
        }

        // ---- route ----
        int shown = 0;
        var sc = eng != null ? eng.Current : null;
        if (sc != null && sc.Waypoints != null && eng.Active)
        {
            int n = Mathf.Min(sc.Waypoints.Count, MaxLegs);
            Vector2 prev = Vector2.zero;
            bool havePrev = false;
            for (int i = 0; i < n; i++)
            {
                // Divide by the symRoot scale so that AFTER scaling the marker lands at
                // its true offset in metres — the container scaling that keeps symbol
                // SIZES constant would otherwise also move these off their positions.
                float inv = mapSize / Mathf.Max(1f, rangeM);
                Vector2 m = ToMap(sc.Waypoints[i].Pos - pos, hdgDeg) * inv;
                // Skip anything far outside the picture — a symbol pinned to the edge of
                // the screen is worse than no symbol, because it implies a position.
                bool visible = Mathf.Abs(m.x) < halfWidth * 1.02f && Mathf.Abs(m.y) < mapSize * 1.02f;
                if (wpMarks[i] != null)
                {
                    wpMarks[i].gameObject.SetActive(visible);
                    if (visible) wpMarks[i].localPosition = new Vector3(m.x, m.y, 2.9f);
                }
                if (wpLabels[i] != null)
                {
                    var tm = wpLabels[i].GetComponent<TextMesh>();
                    if (tm != null) tm.text = sc.Waypoints[i].Name;
                    wpLabels[i].gameObject.SetActive(visible);
                    if (visible)
                        wpLabels[i].localPosition = new Vector3(m.x, m.y + mapSize * 0.062f, 2.9f);
                }
                if (havePrev && legs[i] != null)
                {
                    Vector2 a = prev, b = m;
                    Vector2 mid = (a + b) * 0.5f;
                    float len = Vector2.Distance(a, b);
                    float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    legs[i].gameObject.SetActive(len > 0.01f);
                    legs[i].localPosition = new Vector3(mid.x, mid.y, 2.8f);
                    legs[i].localRotation = Quaternion.Euler(0f, 0f, ang);
                    legs[i].localScale = new Vector3(len, mapSize * 0.009f, 1f);
                }
                else if (legs[i] != null) legs[i].gameObject.SetActive(false);
                prev = m; havePrev = true;
                shown++;
            }
            // ---- active leg readout ----
            if (wptText != null)
            {
                if (eng.HasWaypoint)
                {
                    Vector3 w = eng.WaypointPos;
                    float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(w.x, w.z));
                    float brg = Mathf.Repeat(Mathf.Atan2(w.x - pos.x, w.z - pos.z) * Mathf.Rad2Deg, 360f);
                    string nm = (eng.Current != null && eng.WaypointIndex >= 0 &&
                                 eng.WaypointIndex < eng.Current.Waypoints.Count)
                              ? eng.Current.Waypoints[eng.WaypointIndex].Name : "WPT";
                    wptText.text = nm + "  " + FormatDistance(d) + "  " +
                                   Mathf.RoundToInt(brg).ToString("000") + "\u00B0";
                }
                else wptText.text = "";
            }
        }
        for (int i = shown; i < MaxLegs; i++)
        {
            if (wpMarks[i] != null) wpMarks[i].gameObject.SetActive(false);
            if (wpLabels[i] != null) wpLabels[i].gameObject.SetActive(false);
            if (legs[i] != null) legs[i].gameObject.SetActive(false);
        }
        if (shown == 0 && wptText != null) wptText.text = "";
    }

    static string FormatDistance(float metres) =>
        metres >= 1000f ? (metres / 1000f).ToString("F1") + " km" : Mathf.RoundToInt(metres) + " m";

    static void Place(TextMesh tm, Vector3 lp, TextAnchor anchor)
    {
        if (tm == null) return;
        tm.transform.localPosition = lp;
        tm.anchor = anchor;
        tm.alignment = anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight
                     ? TextAlignment.Right : TextAlignment.Left;
    }

    /// <summary>A data-block glyph on the map: same rendering path as the compass labels
    /// (TextMesh on the symbol layer, parented to the map camera) so it becomes pixels in
    /// the render texture rather than text floating in the cockpit.</summary>
    static TextMesh MakeDataLabel(Transform parent, string text, float cap, Color col, int layer)
    {
        const int Pt = 72;
        var tm = new GameObject("Data").AddComponent<TextMesh>();
        tm.transform.SetParent(parent, false);
        tm.text = text;
        tm.characterSize = cap * 10f / Pt;
        tm.fontSize = Pt;
        tm.color = col;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.gameObject.layer = layer;
        var mr = tm.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return tm;
    }

    /// <summary>A translucent quad. AddQuad uses Unlit/Color, which is opaque — the alpha
    /// in a colour handed to it is silently ignored, which is why the reference ring's
    /// 0.6 alpha never did anything.</summary>
    static void AddTransparentQuad(Transform parent, Vector3 localPos, Vector3 localScale,
                                   Color color, int layer)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var c = g.GetComponent<Collider>(); if (c) Object.Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        g.layer = layer;
        var sh = Shader.Find("Unlit/Transparent Colored") ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh) { color = color };
        mat.renderQueue = 3000;
        g.GetComponent<MeshRenderer>().material = mat;
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
