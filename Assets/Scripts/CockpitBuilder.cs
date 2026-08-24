using UnityEngine;

/// <summary>
/// Builds the first-person cockpit: an instrument panel with the standard
/// six-pack (3D dials with live needles), an artificial horizon, a control yoke,
/// a throttle + flaps quadrant, a glareshield, and the pilot-eye camera.
/// The panel sits in the normal forward view (camera tilts slightly down) so the
/// instruments and levers are visible without looking around.
/// </summary>
public static class CockpitBuilder
{
    static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.12f);
    static readonly Color FaceColor = new Color(0.92f, 0.92f, 0.92f);
    static readonly Color NeedleColor = new Color(0.85f, 0.1f, 0.1f);
    static readonly Color HubColor = new Color(0.2f, 0.2f, 0.2f);
    static readonly Color SkyColor = new Color(0.35f, 0.6f, 0.9f);
    static readonly Color GroundColor = new Color(0.45f, 0.30f, 0.15f);
    static readonly Color FrameColor = new Color(0.83f, 0.83f, 0.87f);   // light windscreen frame

    // The cockpit interior lives on its own layer: the cockpit camera renders it,
    // the external chase/orbit camera culls it (so the panel never floats in view).
    public const int CockpitLayer = 11;

    const float PanelZ = 1.3f;
    const float RowTop = 0.30f;   // lowered so the panel top clears the forward view
    const float RowBot = 0.06f;
    const float Col = 0.28f;

    public static void Build(Transform root, CessnaPhysics phys)
    {
        var cockpit = new GameObject("Cockpit");
        cockpit.transform.SetParent(root);
        cockpit.transform.localPosition = Vector3.zero;
        cockpit.transform.localRotation = Quaternion.identity;
        Transform c = cockpit.transform;

        // Instrument panel — widened to a full-width Cessna dash so it can carry the
        // six-pack PLUS an avionics stack, engine cluster and a lower switch panel.
        Metal(Box(c, new Vector3(0f, 0.16f, PanelZ + 0.02f), new Vector3(1.20f, 0.54f, 0.03f), PanelColor), 0.35f, 0.35f);
        // Thin anti-glare coaming just above the panel top — shallow so it doesn't
        // overhang and block the downward view on approach.
        Box(c, new Vector3(0f, 0.435f, PanelZ - 0.03f), new Vector3(1.24f, 0.03f, 0.10f), PanelColor);

        Gauge(c, new Vector3(-Col, RowTop, PanelZ), GaugeType.Airspeed, phys);
        Attitude(c, new Vector3(0f, RowTop, PanelZ), phys);
        Gauge(c, new Vector3(Col, RowTop, PanelZ), GaugeType.Altimeter, phys);
        Gauge(c, new Vector3(-Col, RowBot, PanelZ), GaugeType.TurnRate, phys);
        Gauge(c, new Vector3(0f, RowBot, PanelZ), GaugeType.Heading, phys);
        Gauge(c, new Vector3(Col, RowBot, PanelZ), GaugeType.VerticalSpeed, phys);

        BuildGlareshield(c);
        BuildFloor(c);
        BuildYoke(c, phys);
        BuildLevers(c, phys);
        BuildWindscreen(c);
        BuildSeat(c);
        BuildDoors(c);
        BuildCamera(c);

        // (EXTRA) real-cockpit detailing — the things a Cessna 172 panel actually has,
        // around the working six-pack: avionics/radio stack, engine gauge cluster, a
        // lower switch panel with toggles + breakers, pedestal knobs, rudder pedals and
        // a brake lever. The yoke, levers, pedals, brake and switches are INTERACTIVE
        // (mouse in the cockpit view — see CockpitInteraction); the rest is detailing.
        BuildAvionicsStack(c);
        BuildEngineCluster(c);
        BuildLowerPanel(c);
        BuildPedestalControls(c);
        BuildRudderPedals(c);
        BuildBrakeLever(c);

        // Put the whole cockpit interior on its own layer so the external camera
        // can cull it (the cockpit camera renders this layer). Grab colliders live on
        // this layer too, so the interaction raycast (mask 1<<CockpitLayer) finds them.
        SetLayerRecursive(cockpit.transform, CockpitLayer);

        // Mouse interaction: raycasts from the cockpit camera against the grab colliders
        // and drives the flight inputs. Added on the aircraft root (alongside the
        // controller/physics it talks to). Keyboard still works; mouse wins while held.
        var interaction = root.gameObject.AddComponent<CockpitInteraction>();
        interaction.controller = root.GetComponent<AircraftController>();
        interaction.phys = phys;
        interaction.cam = c.GetComponentInChildren<Camera>();

        // (EXTRA) Load the photoreal Cessna glass cockpit and RIG its yoke to the
        // controls. On success it hides the code cockpit above; on failure the code
        // cockpit stays. This is what makes it a realistic, dynamic cockpit.
        var real = cockpit.AddComponent<RealCockpit>();
        real.phys = phys;
        real.cockpitRoot = c;
        real.cockpitCam = c.GetComponentInChildren<Camera>();
    }

    /// <summary>
    /// A thin, light, OPEN windscreen frame so the cockpit reads like a real
    /// cabin instead of a bare panel — one-piece windscreen (no center post),
    /// no roof and no side walls, to keep the bright open forward view that the
    /// accepted v5/v9 cockpit has. Just framing at the periphery.
    /// </summary>
    static void BuildWindscreen(Transform c)
    {
        // top windscreen bow
        Box(c, new Vector3(0f, 0.64f, 1.5f), new Vector3(1.12f, 0.05f, 0.06f), FrameColor);

        // A-pillars — restored to the old design (leaning forward up to the bow, splayed).
        BoxR(c, new Vector3(-0.56f, 0.52f, 1.45f), Quaternion.Euler(-28f, 0f, 9f),
             new Vector3(0.05f, 0.30f, 0.05f), FrameColor);
        BoxR(c, new Vector3(0.56f, 0.52f, 1.45f), Quaternion.Euler(-28f, 0f, -9f),
             new Vector3(0.05f, 0.30f, 0.05f), FrameColor);

        // low side door rails (peripheral, below the side windows) — restored
        Box(c, new Vector3(-0.62f, 0.14f, 0.95f), new Vector3(0.05f, 0.46f, 0.05f), FrameColor);
        Box(c, new Vector3(0.62f, 0.14f, 0.95f), new Vector3(0.05f, 0.46f, 0.05f), FrameColor);

        // thin header strip connecting the A-pillar tops (barely-there overhead)
        Box(c, new Vector3(0f, 0.70f, 1.34f), new Vector3(1.10f, 0.03f, 0.08f),
            new Color(0.82f, 0.82f, 0.85f));

        // rear quarter windows (small, behind the pilot — visible looking aft)
        BuildGlass(c, new Vector3(-0.60f, 0.38f, 0.25f),
                   Quaternion.Euler(0f, 90f, 0f), new Vector3(0.22f, 0.22f, 0.01f),
                   new Color(0.45f, 0.60f, 0.75f, 0.30f));
        BuildGlass(c, new Vector3(0.60f, 0.38f, 0.25f),
                   Quaternion.Euler(0f, 90f, 0f), new Vector3(0.22f, 0.22f, 0.01f),
                   new Color(0.45f, 0.60f, 0.75f, 0.30f));

        // low rear shelf behind the seat (no vertical wall — looking back stays open)
        Box(c, new Vector3(0f, -0.06f, -0.05f), new Vector3(1.00f, 0.06f, 0.20f),
            new Color(0.18f, 0.18f, 0.20f));
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform ch in t) SetLayerRecursive(ch, layer);
    }

    static void Gauge(Transform parent, Vector3 pos, GaugeType type, CessnaPhysics phys)
    {
        var g = new GameObject("Gauge_" + type);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.identity;

        // Layers separated cleanly in Z (thin discs) so they never z-fight. Camera
        // looks +Z, so smaller local-z = closer to the pilot / on top.
        Disc(g.transform, new Vector3(0f, 0f, 0.020f), 0.24f, 0.006f, HubColor);   // clean dark bezel (back)
        Disc(g.transform, new Vector3(0f, 0f, 0.008f), 0.23f, 0.006f, FaceColor);  // BIG white dial face (thin bezel rim)

        var pivot = new GameObject("NeedlePivot");
        pivot.transform.SetParent(g.transform);
        pivot.transform.localPosition = new Vector3(0f, 0f, -0.03f);
        pivot.transform.localRotation = Quaternion.identity;
        Box(pivot.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.010f, 0.10f, 0.008f), NeedleColor);  // shorter needle (stays on the face)
        Disc(g.transform, new Vector3(0f, 0f, -0.045f), 0.04f, 0.006f, HubColor);  // centre hub (front)

        var ig = g.AddComponent<InstrumentGauge>();
        ig.phys = phys; ig.type = type; ig.needle = pivot.transform;

        // tick marks ON the white face: 9 major (longer) + 8 shorter intermediate
        // ticks BETWEEN them, all inside the dial with a white margin to the bezel.
        AddTicks(g.transform, 9, 0.088f, 0.108f, -135f, 135f);
        AddTicks(g.transform, 8, 0.096f, 0.108f, -118.125f, 118.125f);
    }

    static void Attitude(Transform parent, Vector3 pos, CessnaPhysics phys)
    {
        var g = new GameObject("Gauge_Attitude");
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.identity;

        // Clean, unobstructed artificial horizon. Every layer is separated in Z so
        // nothing z-fights (that flicker read as a "brown block covering" the dial).
        Disc(g.transform, new Vector3(0f, 0f, 0.026f), 0.24f, 0.006f, HubColor);    // clean dark bezel (back)
        Disc(g.transform, new Vector3(0f, 0f, 0.020f), 0.23f, 0.006f, FaceColor);   // white dial face (ring for the ticks)
        Disc(g.transform, new Vector3(0f, 0f, 0.016f), 0.16f, 0.006f, SkyColor);    // CENTRAL sky area (leaves a white ring)

        var pivot = new GameObject("HorizonBar");
        pivot.transform.SetParent(g.transform);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;
        // moving card: brown GROUND fills the lower half of the central area + horizon line
        Box(pivot.transform, new Vector3(0f, -0.0425f, 0.006f), new Vector3(0.13f, 0.085f, 0.004f), GroundColor);
        Box(pivot.transform, new Vector3(0f, 0f, -0.004f), new Vector3(0.13f, 0.006f, 0.004f), Color.white);

        // fixed miniature-aircraft symbol — bright yellow, FRONTMOST, always clear
        Box(g.transform, new Vector3(0f, 0f, -0.014f), new Vector3(0.09f, 0.010f, 0.004f), new Color(1f, 0.85f, 0f)); // wings
        Box(g.transform, new Vector3(0f, 0f, -0.014f), new Vector3(0.014f, 0.014f, 0.004f), new Color(1f, 0.85f, 0f)); // hub dot

        var ai = g.AddComponent<AttitudeIndicator>();
        ai.phys = phys; ai.horizonBar = pivot.transform;

        // same tick language as the scale gauges — on the white ring, colour clear of them
        AddTicks(g.transform, 9, 0.088f, 0.108f, -135f, 135f);
        AddTicks(g.transform, 8, 0.096f, 0.108f, -118.125f, 118.125f);
    }

    static void BuildYoke(Transform parent, CessnaPhysics phys)
    {
        // Two nested pivots (fixes the old single-transform "fake" motion):
        //   YokePitch — a hinge low on the column; the whole column tilts about X.
        //   YokeWheel — the ram's-horn wheel; ONLY it turns about the column axis (roll).
        var pitchPivot = new GameObject("YokePitch");
        pitchPivot.transform.SetParent(parent);
        pitchPivot.transform.localPosition = new Vector3(0f, 0.00f, 1.14f);   // hinge at the panel base
        pitchPivot.transform.localRotation = Quaternion.identity;

        // Column/shaft: from the hinge toward the pilot (along -Z), tube look (metallic).
        Metal(Cyl(pitchPivot.transform, new Vector3(0f, 0.02f, -0.16f), Quaternion.Euler(90f, 0f, 0f),
                  new Vector3(0.045f, 0.16f, 0.045f), new Color(0.14f, 0.14f, 0.16f)), 0.7f, 0.5f);

        var wheel = new GameObject("YokeWheel");
        wheel.transform.SetParent(pitchPivot.transform);
        wheel.transform.localPosition = new Vector3(0f, 0.04f, -0.32f);       // near end, at the pilot
        wheel.transform.localRotation = Quaternion.identity;

        Color grip = new Color(0.06f, 0.06f, 0.07f);
        Box(wheel.transform, new Vector3(0f, 0.00f, 0f), new Vector3(0.34f, 0.038f, 0.045f), grip);   // cross bar
        Box(wheel.transform, new Vector3(-0.17f, -0.055f, 0f), new Vector3(0.04f, 0.13f, 0.045f), grip); // L horn
        Box(wheel.transform, new Vector3(0.17f, -0.055f, 0f),  new Vector3(0.04f, 0.13f, 0.045f), grip); // R horn
        Box(wheel.transform, new Vector3(-0.17f, -0.12f, 0f), new Vector3(0.05f, 0.05f, 0.05f), grip);   // L grip end
        Box(wheel.transform, new Vector3(0.17f, -0.12f, 0f),  new Vector3(0.05f, 0.05f, 0.05f), grip);   // R grip end
        Metal(Box(wheel.transform, new Vector3(0f, 0.02f, -0.015f), new Vector3(0.11f, 0.07f, 0.02f),
                  new Color(0.10f, 0.10f, 0.12f)), 0.4f, 0.6f);                                          // centre hub emblem

        var anim = pitchPivot.AddComponent<YokeAnimator>();
        anim.phys = phys; anim.pitchPivot = pitchPivot.transform; anim.wheel = wheel.transform;

        // Grab target — a trigger box around the wheel (child of the pitch pivot so it
        // tracks the pitch tilt). Left-drag here flies the plane (see CockpitInteraction).
        AddGrab(pitchPivot.transform, new Vector3(0f, -0.02f, -0.32f),
                new Vector3(0.46f, 0.30f, 0.16f), CockpitControlKind.Yoke);
    }

    // Throttle quadrant — the old rounded-knob lever design (restored), now with 3
    // levers in the standard left→right order SPOILERS · THROTTLE · FLAPS, colour
    // coded (blue / black / white). Spoiler + flaps are both stepped-level controls.
    static void BuildLevers(Transform parent, CessnaPhysics phys)
    {
        Metal(Box(parent, new Vector3(0.40f, 0.12f, 1.0f), new Vector3(0.24f, 0.14f, 0.40f), PanelColor), 0.5f, 0.4f);   // console

        Transform spo = Lever(parent, new Vector3(0.32f, 0.22f, 0.90f), new Color(0.30f, 0.45f, 0.85f)); // spoilers (blue)
        Transform thr = Lever(parent, new Vector3(0.40f, 0.22f, 0.90f), new Color(0.05f, 0.05f, 0.05f)); // throttle (black)
        Transform flp = Lever(parent, new Vector3(0.48f, 0.22f, 0.90f), new Color(0.90f, 0.90f, 0.90f)); // flaps (white)

        // Each knob is grabbable: throttle drags (continuous), flaps/spoiler click to step.
        AddGrab(spo, new Vector3(0f, 0.16f, 0f), new Vector3(0.11f, 0.13f, 0.11f), CockpitControlKind.Spoiler);
        AddGrab(thr, new Vector3(0f, 0.16f, 0f), new Vector3(0.11f, 0.13f, 0.11f), CockpitControlKind.Throttle);
        AddGrab(flp, new Vector3(0f, 0.16f, 0f), new Vector3(0.11f, 0.13f, 0.11f), CockpitControlKind.Flaps);

        var lev = parent.gameObject.AddComponent<CockpitLevers>();
        lev.phys = phys; lev.spoilerLever = spo; lev.throttleLever = thr; lev.flapLever = flp;
    }

    static Transform Lever(Transform parent, Vector3 pivotPos, Color knob)
    {
        var pivot = new GameObject("Lever");
        pivot.transform.SetParent(parent);
        pivot.transform.localPosition = pivotPos;
        pivot.transform.localRotation = Quaternion.identity;
        Box(pivot.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.025f, 0.20f, 0.025f), HubColor);
        var k = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        k.transform.SetParent(pivot.transform);
        k.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        k.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
        SimUtil.Destroy(k.GetComponent<Collider>());
        Paint(k, knob);
        return pivot.transform;
    }

    // Pilot seat behind + slightly left of the eye point (eye is at local
    // (0, 0.64, 0.42)). Below/behind the camera so it never blocks the forward view.
    static void BuildSeat(Transform c)
    {
        Color seatColor = new Color(0.22f, 0.20f, 0.22f);  // dark grey/charcoal
        Color trimColor = new Color(0.15f, 0.14f, 0.15f);

        // Seat pan (horizontal cushion)
        Box(c, new Vector3(-0.05f, 0.02f, 0.22f), new Vector3(0.48f, 0.08f, 0.46f), seatColor);
        // Backrest (near-vertical, angled back 8°)
        BoxR(c, new Vector3(-0.05f, 0.28f, 0.10f), Quaternion.Euler(8f, 0f, 0f),
             new Vector3(0.48f, 0.52f, 0.07f), seatColor);
        // Headrest (top of backrest)
        BoxR(c, new Vector3(-0.05f, 0.56f, 0.07f), Quaternion.Euler(8f, 0f, 0f),
             new Vector3(0.26f, 0.16f, 0.10f), trimColor);
        // Seat frame / legs (thin vertical supports)
        Box(c, new Vector3(-0.22f, -0.10f, 0.22f), new Vector3(0.04f, 0.20f, 0.04f), trimColor);
        Box(c, new Vector3(0.12f, -0.10f, 0.22f),  new Vector3(0.04f, 0.20f, 0.04f), trimColor);
    }

    // Doors are almost entirely GLASS (like a real Cessna 172): only a thin sill,
    // edge frames and a top rail are solid; you see sky/world through the big pane.
    static void BuildDoors(Transform c)
    {
        Color sillColor  = new Color(0.15f, 0.15f, 0.17f);   // dark grey sill/trim
        Color frameColor = new Color(0.20f, 0.20f, 0.22f);   // thin window frame
        Color glassColor = new Color(0.55f, 0.72f, 0.90f, 0.18f);

        BuildOneDoor(c, -0.625f, sillColor, frameColor, glassColor);  // LEFT
        BuildOneDoor(c,  0.625f, sillColor, frameColor, glassColor);  // RIGHT
    }

    static void BuildOneDoor(Transform c, float xPos, Color sill, Color frame, Color glass)
    {
        // Bottom door sill — the only solid (structural) part of the door
        Box(c, new Vector3(xPos, 0.10f, 0.72f), new Vector3(0.04f, 0.08f, 0.70f), sill);

        // Door frame: 4 thin strips around the window opening (5 cm)
        Box(c, new Vector3(xPos, 0.36f, 0.39f), new Vector3(0.04f, 0.56f, 0.05f), frame); // front pillar
        Box(c, new Vector3(xPos, 0.36f, 1.08f), new Vector3(0.04f, 0.56f, 0.05f), frame); // rear pillar
        Box(c, new Vector3(xPos, 0.64f, 0.72f), new Vector3(0.04f, 0.04f, 0.72f), frame); // top rail
        Box(c, new Vector3(xPos, 0.14f, 0.72f), new Vector3(0.04f, 0.03f, 0.72f), frame); // bottom rail

        // LARGE glass panel filling the window opening (~0.65 wide × 0.48 tall)
        float gx = xPos + (xPos < 0 ? 0.015f : -0.015f);   // slightly inset from frame
        BuildGlass(c, new Vector3(gx, 0.40f, 0.72f),
                   Quaternion.Euler(0f, 90f, 0f),
                   new Vector3(0.65f, 0.48f, 0.01f), glass);

        // Small door-latch handle on the inside frame
        Box(c, new Vector3(xPos + (xPos < 0 ? 0.03f : -0.03f), 0.35f, 1.02f),
            new Vector3(0.04f, 0.08f, 0.06f), new Color(0.55f, 0.55f, 0.58f));
    }

    // Translucent glass quad (transparent Standard material).
    static void BuildGlass(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        // A Quad ships with a flat (concave) MeshCollider; parented under the
        // Cessna's dynamic Rigidbody, Unity logs "Concave Mesh Colliders are not
        // supported" until it's gone. Deferred Destroy() leaves it alive for the
        // first physics step, so remove it IMMEDIATELY (safe: this runs in Awake,
        // before any FixedUpdate). DestroyImmediate is also the edit-mode path.
        var qc = g.GetComponent<Collider>();
        if (qc != null) Object.DestroyImmediate(qc);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        var mat = new Material(Shader.Find("Standard"));
        mat.color = col;
        mat.SetFloat("_Mode", 3f);   // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        g.GetComponent<Renderer>().material = mat;
    }

    // Scale tick marks drawn INSIDE the white dial face (radius ~0.088..0.108, well
    // within the 0.115 face and clear of the 0.12 bezel) — thin flat boxes swept
    // across a dial arc. Near-black for high contrast on white. `count` ticks are
    // spaced evenly from startDeg to endDeg; identical width on every gauge. Majors
    // and intermediates share the outer radius; intermediates use a larger innerR
    // (shorter) and a start/end offset so they fall BETWEEN the majors.
    static readonly Color TickColor = new Color(0.06f, 0.06f, 0.09f);
    static void AddTicks(Transform gauge, int count, float innerR, float outerR, float startDeg, float endDeg)
    {
        float mx = (innerR + outerR) * 0.5f;
        float len = outerR - innerR;
        for (int i = 0; i < count; i++)
        {
            float a = count <= 1 ? (startDeg + endDeg) * 0.5f : Mathf.Lerp(startDeg, endDeg, i / (float)(count - 1));
            float rad = a * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Sin(rad) * mx, Mathf.Cos(rad) * mx, 0.002f);   // on the face, behind the needle
            var tick = Box(gauge, pos, new Vector3(0.006f, len, 0.004f), TickColor);
            tick.transform.localRotation = Quaternion.Euler(0f, 0f, -a);
        }
    }

    static void BuildCamera(Transform parent)
    {
        var camGo = new GameObject("CockpitCamera");
        camGo.transform.SetParent(parent);
        // Pilot eye point: raised + a touch back so you look mostly OUT the
        // windscreen (horizon fills the view) with the panel in the lower third —
        // a realistic seated-pilot angle. Nothing is removed; only the angle/height.
        camGo.transform.localPosition = new Vector3(0f, 0.64f, 0.42f);
        camGo.transform.localRotation = Quaternion.identity;
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.fieldOfView = 78f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 15000f;   // big world: see the distant mountain ring
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = ~(1 << AircraftBuilder.ExteriorLayer); // don't draw the plane's exterior
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CockpitCamera>();
        BuildCabinFill(camGo.transform);
    }

    /// <summary>A soft fill light inside the cabin, rendering ONLY the cockpit layer.
    ///
    /// WHY A LIGHT THAT IS NOT REALLY THERE
    ///   The scene is lit by one directional sun and a flat ambient term. A flat ambient
    ///   gives every surface the same contribution regardless of what is around it, so it
    ///   cannot produce the light that in a real cabin bounces off the windscreen, the
    ///   glareshield and the pilot's own lap and fills the panel. The panel faces aft, away
    ///   from the sun, so with no bounce it renders at ambient only — which is why every
    ///   control below the glareshield came out as a dark grey silhouette that a participant
    ///   would have to guess at.
    ///
    ///   This stands in for that bounce. It is deliberately weak, warm, shadowless and
    ///   short-range, and its culling mask means it cannot touch the terrain, the aeroplane's
    ///   exterior or anything else in the world — so it changes how the cockpit READS
    ///   without changing the scene's lighting or the appearance of anything outside.
    ///
    ///   It is an approximation and is recorded as one. The alternative — leaving the panel
    ///   unreadable — would have been a much larger distortion of the task.</summary>
    static void BuildCabinFill(Transform eye)
    {
        var g = new GameObject("CabinFill");
        g.transform.SetParent(eye, false);
        // Slightly above and behind the eye, so the panel is lit from where the pilot's own
        // head is rather than from a point in front of them, which would flatten it.
        g.transform.localPosition = new Vector3(0f, 0.18f, -0.25f);
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.965f, 0.92f);
        l.intensity = 1.35f;
        l.range = 2.6f;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        l.cullingMask = 1 << CockpitLayer;
    }

    // ===== (EXTRA) real-cockpit detailing =====================================

    // Avionics / radio stack on the RIGHT of the six-pack: a column of black comm/nav
    // units, each with a lit display strip, two tuning knobs and a row of buttons.
    static void BuildAvionicsStack(Transform c)
    {
        Color housing = new Color(0.07f, 0.07f, 0.08f);
        Color unit    = new Color(0.11f, 0.11f, 0.12f);
        Color green   = new Color(0.15f, 0.85f, 0.45f);
        Color amber   = new Color(0.95f, 0.65f, 0.15f);
        Color knob    = new Color(0.18f, 0.18f, 0.20f);
        Color[] btns  = { new Color(0.85f,0.2f,0.2f), new Color(0.2f,0.6f,0.9f), new Color(0.9f,0.9f,0.9f), new Color(0.95f,0.75f,0.2f) };

        float x = 0.50f, zP = 1.29f, zProud = 1.255f;
        Box(c, new Vector3(x, 0.16f, 1.305f), new Vector3(0.20f, 0.44f, 0.02f), housing);   // recessed housing

        for (int i = 0; i < 5; i++)
        {
            float uy = 0.34f - i * 0.075f;
            Metal(Box(c, new Vector3(x, uy, zP), new Vector3(0.175f, 0.062f, 0.014f), unit), 0.5f, 0.55f);               // unit face
            Emissive(Box(c, new Vector3(x - 0.03f, uy + 0.010f, zP - 0.008f), new Vector3(0.075f, 0.020f, 0.006f), i % 2 == 0 ? green : amber), i % 2 == 0 ? green : amber, 1.8f); // lit display
            Cyl(c, new Vector3(x + 0.062f, uy - 0.012f, zProud), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.020f, 0.012f, 0.020f), knob); // big knob
            Cyl(c, new Vector3(x + 0.062f, uy + 0.012f, zProud), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.013f, 0.012f, 0.013f), knob); // small knob
            for (int b = 0; b < 4; b++)                                                                                  // button row
                Box(c, new Vector3(x - 0.06f + b * 0.022f, uy - 0.018f, zP - 0.008f), new Vector3(0.012f, 0.010f, 0.006f), btns[b % btns.Length]);
        }
    }

    // Engine gauge cluster on the far LEFT: a column of small static round gauges
    // (tach / fuel / oil / amps) — visual detail, needles fixed.
    static void BuildEngineCluster(Transform c)
    {
        Color bezel = new Color(0.12f, 0.12f, 0.13f);
        Color face  = new Color(0.90f, 0.90f, 0.90f);
        Color needle= new Color(0.10f, 0.10f, 0.10f);
        float[] ang = { -40f, 20f, -10f, 55f };
        float x = -0.52f, zP = 1.30f;
        for (int i = 0; i < 4; i++)
        {
            float uy = 0.33f - i * 0.085f;
            Disc(c, new Vector3(x, uy, zP + 0.005f), 0.085f, 0.006f, bezel);
            Disc(c, new Vector3(x, uy, zP - 0.004f), 0.070f, 0.006f, face);
            var n = Box(c, new Vector3(x, uy, zP - 0.012f), new Vector3(0.005f, 0.05f, 0.004f), needle);
            n.transform.localRotation = Quaternion.Euler(0f, 0f, ang[i]);
        }
    }

    // Lower sub-panel below the main dash (angled toward the pilot's lap, like a real
    // C172 lower panel): a row of toggle switches + a block of circuit breakers.
    static void BuildLowerPanel(Transform c)
    {
        var pivot = new GameObject("LowerPanel");
        pivot.transform.SetParent(c);
        pivot.transform.localPosition = new Vector3(0f, -0.16f, 1.20f);
        pivot.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);   // faces up toward the pilot
        Transform p = pivot.transform;

        Box(p, Vector3.zero, new Vector3(1.15f, 0.17f, 0.02f), new Color(0.13f, 0.13f, 0.14f));   // sub-panel plate

        Color baseC = new Color(0.20f, 0.20f, 0.22f);
        for (int i = 0; i < 12; i++)                                       // toggle switch row (first = red master)
        {
            float sx = -0.47f + i * 0.085f;
            Box(p, new Vector3(sx, 0.03f, -0.012f), new Vector3(0.018f, 0.022f, 0.012f), baseC);
            Color toggleC = i == 0 ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.65f, 0.65f, 0.68f);
            var t = Box(p, new Vector3(sx, 0.045f, -0.020f), new Vector3(0.008f, 0.026f, 0.008f), toggleC);
            t.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 22f : -22f, 0f, 0f);          // alternate up/down
            // clickable: flips the toggle up/down
            var sc = AddGrab(p, new Vector3(sx, 0.045f, -0.02f), new Vector3(0.03f, 0.055f, 0.05f), CockpitControlKind.Switch);
            sc.toggle = t.transform; sc.switchOffDeg = 22f; sc.switchOnDeg = -22f; sc.on = (i % 2 != 0);
        }

        Color brk = new Color(0.08f, 0.08f, 0.09f), brkTop = new Color(0.55f, 0.35f, 0.15f);
        for (int r = 0; r < 2; r++)                                        // circuit-breaker block
            for (int i = 0; i < 14; i++)
            {
                float bx = -0.46f + i * 0.071f, by = -0.03f - r * 0.035f;
                Cyl(p, new Vector3(bx, by, -0.012f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.008f, 0.012f), brk);
                Cyl(p, new Vector3(bx, by, -0.018f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.006f, 0.006f, 0.006f), brkTop);
            }
    }

    // Pedestal detail: a red mixture knob by the throttle quadrant + a trim wheel.
    static void BuildPedestalControls(Transform c)
    {
        Cyl(c, new Vector3(0.24f, 0.20f, 0.94f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.035f, 0.03f, 0.035f), new Color(0.75f, 0.12f, 0.12f)); // mixture knob
        Box(c, new Vector3(0.24f, 0.10f, 0.98f), new Vector3(0.02f, 0.14f, 0.02f), HubColor);                                                        // its shaft
        Metal(Cyl(c, new Vector3(0f, 0.02f, 0.98f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.10f, 0.02f, 0.10f), new Color(0.15f, 0.15f, 0.16f)), 0.6f, 0.5f); // trim wheel
        Box(c, new Vector3(0.09f, 0.02f, 0.98f), new Vector3(0.012f, 0.08f, 0.008f), new Color(0.9f, 0.9f, 0.9f));                                   // trim indicator
    }

    // Rudder pedals on the floor — press LEFT / RIGHT pedal to yaw (interactive rudder).
    static void BuildRudderPedals(Transform c)
    {
        Color pedal = new Color(0.09f, 0.09f, 0.10f);
        Color arm   = new Color(0.13f, 0.13f, 0.14f);
        foreach (float sx in new[] { -0.17f, 0.17f })
        {
            Metal(BoxR(c, new Vector3(sx, -0.30f, 1.06f), Quaternion.Euler(-52f, 0f, 0f),
                       new Vector3(0.17f, 0.15f, 0.03f), pedal), 0.3f, 0.4f);              // pedal face
            BoxR(c, new Vector3(sx, -0.37f, 0.98f), Quaternion.Euler(30f, 0f, 0f),
                 new Vector3(0.04f, 0.12f, 0.04f), arm);                                   // pedal arm
            AddGrab(c, new Vector3(sx, -0.30f, 1.04f), new Vector3(0.20f, 0.18f, 0.14f),
                    sx < 0f ? CockpitControlKind.YawLeft : CockpitControlKind.YawRight);
        }
    }

    // Wheel-brake lever under the left of the panel — hold to brake on the ground.
    static void BuildBrakeLever(Transform c)
    {
        BoxR(c, new Vector3(-0.34f, -0.06f, 1.02f), Quaternion.Euler(-18f, 0f, 0f),
             new Vector3(0.03f, 0.20f, 0.03f), new Color(0.12f, 0.12f, 0.13f));            // lever shaft
        Emissive(Cyl(c, new Vector3(-0.34f, 0.04f, 0.99f), Quaternion.Euler(90f, 0f, 0f),
                     new Vector3(0.05f, 0.02f, 0.05f), new Color(0.85f, 0.2f, 0.15f)), new Color(0.85f, 0.2f, 0.15f), 0.6f); // red knob
        AddGrab(c, new Vector3(-0.34f, 0.02f, 1.0f), new Vector3(0.10f, 0.16f, 0.10f), CockpitControlKind.Brake);
    }

    // Padded glareshield hood overhanging the instruments — depth + a real dashboard
    // look (shallow so it never blocks the downward view on approach).
    static void BuildGlareshield(Transform c)
    {
        Metal(BoxR(c, new Vector3(0f, 0.445f, 1.20f), Quaternion.Euler(20f, 0f, 0f),
                   new Vector3(1.22f, 0.05f, 0.17f), new Color(0.07f, 0.07f, 0.08f)), 0.25f, 0.2f);
    }

    // Cabin floor + a dark lower kick-panel so the cockpit reads as an enclosed cabin
    // (both below the sight line, so they don't intrude on the forward view).
    static void BuildFloor(Transform c)
    {
        Metal(Box(c, new Vector3(0f, -0.42f, 0.72f), new Vector3(1.16f, 0.04f, 1.5f), new Color(0.10f, 0.10f, 0.11f)), 0.2f, 0.25f);
        Box(c, new Vector3(0f, -0.22f, 1.36f), new Vector3(1.16f, 0.42f, 0.04f), new Color(0.09f, 0.09f, 0.10f));  // kick panel under the dash
    }

    // ---- primitive helpers ----

    static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g;
    }

    static GameObject BoxR(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g;
    }

    static GameObject Cyl(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g;
    }

    static GameObject Disc(Transform parent, Vector3 pos, float diameter, float thin, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        g.transform.localScale = new Vector3(diameter, thin, diameter);
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g;
    }

    static void Paint(GameObject g, Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        g.GetComponent<Renderer>().material = m;
    }

    // Give an already-painted primitive a metallic/glossy finish so it catches light
    // instead of reading as flat matte plastic.
    static GameObject Metal(GameObject g, float metallic = 0.65f, float smoothness = 0.5f)
    {
        var m = g.GetComponent<Renderer>().material;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        return g;
    }

    // Make a primitive self-lit (glowing display / lit marking) — reads as "powered on".
    static GameObject Emissive(GameObject g, Color c, float intensity = 1.6f)
    {
        var m = g.GetComponent<Renderer>().material;
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return g;
    }

    // Attach a trigger-collider grab/click target with a CockpitControl. Trigger so it
    // never disturbs the aircraft rigidbody; on the cockpit layer so the interaction
    // raycast (mask 1<<CockpitLayer) finds it. Returns the control for further config.
    static CockpitControl AddGrab(Transform parent, Vector3 localPos, Vector3 size, CockpitControlKind kind)
    {
        var go = new GameObject("Grab_" + kind);
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.isTrigger = true;
        var cc = go.AddComponent<CockpitControl>();
        cc.kind = kind;
        return cc;
    }
}
