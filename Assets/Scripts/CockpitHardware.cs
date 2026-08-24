// CockpitHardware — the shapes a Cessna 172's controls actually have.
//
// WHY THIS EXISTS
//   The rig used to build every control out of the same vocabulary: a plate, a slot, a
//   carriage and a coloured block. That is a set of sliders, and from the seat it read as
//   a set of sliders. A 172 has almost none of that. It has PUSH-PULL PLUNGERS for the
//   engine controls, a small LEVER IN A GATED SLOT for the flaps, a large TRIM WHEEL edge-on
//   in the pedestal, a floor-mounted ROTARY VALVE for fuel, TOE PADS on the rudder pedals
//   for the brakes, and a CONTROL WHEEL on a shaft. The shape is what tells a pilot which
//   control is which without looking, so building them all the same shape destroys the
//   exact thing the cockpit is for.
//
// THE SCALE PROBLEM, AND HOW IT IS SOLVED
//   The GLB is NOT modelled in metres. Its cabin is 352 mm across and its wing spans
//   3.7 m — about a third of a real 172 — and the cockpit camera is placed inside that
//   same small model, so it looks correct from the seat while every dimension is roughly
//   0.35x life size. Building to real millimetres in model units would therefore produce
//   controls about three times too large, which is precisely why the old throttle ball and
//   flap paddle looked like toys sitting on the panel.
//
//   ModelScale below is measured, not assumed, and four independent features agree:
//       panel width      0.352 model  /  1.00 m real   = 0.352
//       eye to panel     0.235 model  /  0.67 m real   = 0.351
//       eye above floor  0.320 model  /  0.91 m real   = 0.352
//       rudder pedal span 0.254 model /  0.72 m real   = 0.353
//   So every dimension here is written in REAL MILLIMETRES and multiplied by MM. A number
//   in this file can be checked directly against a 172 drawing.

using UnityEngine;

public static class CockpitHardware
{
    /// <summary>Model units per real metre, measured from four independent features of
    /// the GLB (see the header). Every dimension in the cockpit is authored in real
    /// millimetres and scaled by this, so the numbers stay checkable against the aeroplane.</summary>
    public const float ModelScale = 0.352f;

    /// <summary>One real millimetre, in model units.</summary>
    public const float MM = ModelScale * 0.001f;

    // ── palette ────────────────────────────────────────────────────────────────────
    // A 172's cockpit is almost entirely dark grey plastic and black knobs. The only
    // saturated colours in it are the red mixture knob, the red fuel valve and a few
    // placards — which is exactly why those read instantly. Colour is used here as
    // information, not decoration.
    public static readonly Color PanelGrey  = new Color(0.208f, 0.212f, 0.222f);
    public static readonly Color PanelLight = new Color(0.290f, 0.296f, 0.308f);
    /// <summary>Sub-panel plates. Deliberately DARKER than the hardware on them and close
    /// to the GLB's own panel, because a plate lighter than its surroundings stops being a
    /// mounting surface and becomes a bright rectangle bolted to the aeroplane — which is
    /// how the engine bay read at first: a white slab with knobs on it.</summary>
    public static readonly Color PanelSub   = new Color(0.150f, 0.153f, 0.161f);
    public static readonly Color Bezel      = new Color(0.140f, 0.143f, 0.150f);
    public static readonly Color KnobBlack  = new Color(0.045f, 0.045f, 0.050f);
    public static readonly Color KnobRed    = new Color(0.560f, 0.055f, 0.045f);
    public static readonly Color KnobWhite  = new Color(0.780f, 0.782f, 0.790f);
    public static readonly Color Steel      = new Color(0.430f, 0.440f, 0.460f);
    public static readonly Color Placard    = new Color(0.800f, 0.806f, 0.818f);
    public static readonly Color Rubber     = new Color(0.085f, 0.087f, 0.092f);

    // ── primitives ─────────────────────────────────────────────────────────────────

    public static Transform Box(Transform parent, Vector3 lp, Vector3 size, Color col,
                                float gloss = 0.25f, float metal = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Strip(g);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = size;
        Paint(g, col, gloss, metal);
        return g.transform;
    }

    /// <summary>A cylinder whose AXIS is +Z (toward the panel), which is the axis almost
    /// every piece of cockpit hardware is turned about. Unity's cylinder primitive is
    /// Y-axis, so this rotates it once here rather than at forty call sites.</summary>
    public static Transform Barrel(Transform parent, Vector3 lp, float radius, float length,
                                   Color col, float gloss = 0.30f, float metal = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Strip(g);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        g.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        Paint(g, col, gloss, metal);
        return g.transform;
    }

    /// <summary>A cylinder whose axis is +X (across the cockpit) — a trim wheel, a pulley,
    /// a hinge pin.</summary>
    public static Transform Disc(Transform parent, Vector3 lp, float radius, float thickness,
                                 Color col, float gloss = 0.30f, float metal = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Strip(g);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        g.transform.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
        Paint(g, col, gloss, metal);
        return g.transform;
    }

    public static Transform Ball(Transform parent, Vector3 lp, float diameter, Color col,
                                 float gloss = 0.35f, float metal = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Strip(g);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = Vector3.one * diameter;
        Paint(g, col, gloss, metal);
        return g.transform;
    }

    // ── assemblies ─────────────────────────────────────────────────────────────────

    /// <summary>A PUSH-PULL PLUNGER: the 172's throttle, mixture and carburettor-heat
    /// control. A knurled knob on a shaft through a mounting boss in the panel; pulling it
    /// OUT toward the pilot brings the shaft with it.
    ///
    /// Returns the PLUNGER (knob + shaft), which the caller animates along local Z. The
    /// boss and escutcheon stay behind on the panel, which is what makes the movement read
    /// as a shaft sliding through a bearing rather than an object drifting in space.
    ///
    /// FULLY IN is the "more" end for throttle (full power) and the cold/normal end for
    /// carburettor heat, which is how a 172 is placarded and flown.</summary>
    public static Transform Plunger(Transform parent, float knobDiaMm, float shaftLenMm,
                                    Color knobColour, bool knurled = true)
    {
        float kd = knobDiaMm * MM;
        // Mounting boss, sunk into the panel and standing slightly proud of it.
        Barrel(parent, new Vector3(0f, 0f, -2f * MM), kd * 0.42f, 6f * MM, PanelLight, 0.35f, 0.4f);   // bezel ring, stays light so the knob reads against it
        Barrel(parent, new Vector3(0f, 0f, -4.5f * MM), kd * 0.30f, 3f * MM, Bezel, 0.20f);

        var plunger = new GameObject("Plunger").transform;
        plunger.SetParent(parent, false);

        // Shaft: long enough that the knob is never seen floating free of the panel at any
        // point in its travel. Steel, because on the aeroplane it is.
        // 5 mm radius, not 2.4. A 172's engine-control shaft is a 10 mm rod; at half that
        // it rendered as a pin and the knob looked like it was floating on a wire.
        Barrel(plunger, new Vector3(0f, 0f, -(shaftLenMm * 0.5f + 4f) * MM),
               5.0f * MM, (shaftLenMm + 10f) * MM, Steel, 0.65f, 0.85f);

        // The knob itself: a shallow cylinder with a domed face, not a sphere. A 172's
        // throttle knob is a flat-faced disc you push with the heel of the hand.
        float zKnob = -(shaftLenMm + 6f) * MM;
        Barrel(plunger, new Vector3(0f, 0f, zKnob), kd * 0.5f, 11f * MM, knobColour, 0.28f);
        Barrel(plunger, new Vector3(0f, 0f, zKnob - 6.5f * MM), kd * 0.44f, 3f * MM, knobColour, 0.40f);

        if (knurled)
        {
            // Knurling, as eight shallow ribs round the rim. It is what a hand grips, and
            // at this size it is the difference between "a knob" and "a grey cylinder".
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Box(plunger, new Vector3(Mathf.Cos(a) * kd * 0.48f, Mathf.Sin(a) * kd * 0.48f, zKnob),
                    new Vector3(kd * 0.13f, kd * 0.13f, 10f * MM), knobColour, 0.18f);
            }
        }
        return plunger;
    }

    /// <summary>The 172 FLAP SELECTOR: a small lever on the lower right panel that runs in
    /// a vertical slot with a physical gate at each setting. Returns the LEVER, which the
    /// caller slides along local Y.
    ///
    /// The gate teeth are cut into the slot's right-hand edge and the lever's pointer
    /// aligns with one of them, so the selected setting is readable from the seat before
    /// the lever is touched — which is the entire purpose of a gated selector and
    /// something a plain slider cannot do.</summary>
    public static Transform FlapSelector(Transform parent, float travelMm, int gates,
                                         out Transform[] gateMarks)
    {
        float travel = travelMm * MM;
        float half = travel * 0.5f;

        // Escutcheon, slot and its lip.
        Box(parent, new Vector3(0f, 0f, -1.0f * MM),
            new Vector3(34f * MM, (travelMm + 26f) * MM, 2.5f * MM), PanelLight, 0.30f);
        Box(parent, new Vector3(0f, 0f, -2.4f * MM),
            new Vector3(9f * MM, (travelMm + 14f) * MM, 2.0f * MM), Bezel, 0.05f);

        // Gate teeth down the right edge, one per flap setting.
        gateMarks = new Transform[gates];
        for (int i = 0; i < gates; i++)
        {
            float y = half - travel * i / Mathf.Max(1, gates - 1);
            gateMarks[i] = Box(parent, new Vector3(8.5f * MM, y, -3.0f * MM),
                               new Vector3(7f * MM, 1.6f * MM, 2.4f * MM), Placard, 0.35f);
        }

        var lever = new GameObject("Lever").transform;
        lever.SetParent(parent, false);
        // Arm through the slot, then a small flat handle that hangs to the LEFT of the
        // slot so it never covers the gate it is pointing at.
        Box(lever, new Vector3(0f, 0f, -4.0f * MM), new Vector3(6f * MM, 7f * MM, 6f * MM), Steel, 0.55f, 0.7f);
        Box(lever, new Vector3(0f, 0f, -8.0f * MM), new Vector3(4.5f * MM, 4.5f * MM, 12f * MM), Steel, 0.55f, 0.7f);
        // Handle: a flat white blade, the 14 CFR 23.781 flap shape, small enough to be a
        // 172 flap handle rather than a paddle.
        Box(lever, new Vector3(0f, 0f, -15f * MM), new Vector3(20f * MM, 9f * MM, 5f * MM), KnobWhite, 0.20f);
        Box(lever, new Vector3(0f, 0f, -19f * MM), new Vector3(20f * MM, 5.5f * MM, 4f * MM), KnobWhite, 0.20f);
        // Pointer toward the gates.
        Box(lever, new Vector3(7f * MM, 0f, -4.5f * MM), new Vector3(9f * MM, 2.0f * MM, 3f * MM), Placard, 0.4f);
        return lever;
    }

    /// <summary>The ELEVATOR TRIM WHEEL: a large wheel mounted edge-on in the pedestal, so
    /// only its forward rim shows through a slot. Returns the WHEEL, which the caller
    /// rotates about local X.
    ///
    /// Edge-on is not a detail — it is what the control IS. A trim wheel presented as a
    /// knob on the panel face would be a different control with different affordances, and
    /// no participant would roll it with a thumb the way this shape invites.</summary>
    public static Transform TrimWheel(Transform parent, float wheelDiaMm, out Transform indicator)
    {
        float r = wheelDiaMm * 0.5f * MM;

        // DEPTH ORDER MATTERS HERE AND WAS WRONG.
        //
        // The pilot is at NEGATIVE z from the pedestal face, so anything that should stand
        // proud of it must be at negative local z. The first version put the escutcheon at
        // -1 mm and the wheel at +3 mm — the wheel four millimetres BEHIND its own cover
        // plate, completely hidden. From the seat the trim wheel was a dark vertical slot
        // with nothing in it.
        //
        // The plate and its slot now sit BEHIND (positive z), and the wheel protrudes
        // FORWARD through them, which is how a trim wheel presents in the aeroplane: a rim
        // standing out of the pedestal with your fingers on it.
        Box(parent, new Vector3(0f, 0f, 2.0f * MM),
            new Vector3(34f * MM, (wheelDiaMm + 18f) * MM, 3.0f * MM), PanelLight, 0.28f);
        Box(parent, new Vector3(0f, 0f, 0.4f * MM),
            new Vector3(16f * MM, (wheelDiaMm + 4f) * MM, 2.2f * MM), Bezel, 0.05f);

        var wheel = new GameObject("Wheel").transform;
        wheel.SetParent(parent, false);
        // Rim standing 7 mm proud of the pedestal face.
        wheel.localPosition = new Vector3(0f, 0f, -7f * MM);
        Disc(wheel, Vector3.zero, r, 11f * MM, new Color(0.115f, 0.117f, 0.124f), 0.22f);
        Disc(wheel, Vector3.zero, r * 0.55f, 12f * MM, PanelLight, 0.30f, 0.3f);
        // Finger ribs round the rim: the reason a thumb can roll it.
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Box(wheel, new Vector3(0f, Mathf.Sin(a) * r * 0.88f, Mathf.Cos(a) * r * 0.88f),
                new Vector3(13f * MM, 3.0f * MM, 3.0f * MM), new Color(0.055f, 0.056f, 0.060f), 0.15f);
        }

        // Trim POSITION indicator: a separate pointer beside the wheel, because the wheel
        // itself spins many turns and cannot show absolute position. This is the "TAKEOFF"
        // band a 172 pilot actually sets against.
        Box(parent, new Vector3(-15f * MM, 0f, -1.0f * MM),
            new Vector3(6f * MM, (wheelDiaMm - 6f) * MM, 2.0f * MM), Bezel, 0.05f);
        indicator = Box(parent, new Vector3(-15f * MM, 0f, -2.6f * MM),
                        new Vector3(10f * MM, 3.0f * MM, 2.0f * MM), KnobWhite, 0.30f);
        return wheel;
    }

    /// <summary>The FUEL SELECTOR VALVE: a substantial rotary handle on a placarded plate,
    /// mounted low on the pedestal where a 172's is. Returns the HANDLE, rotated about
    /// local Z.</summary>
    public static Transform FuelValve(Transform parent, float plateWMm, float plateHMm)
    {
        Box(parent, new Vector3(0f, 0f, -1f * MM),
            new Vector3(plateWMm * MM, plateHMm * MM, 2.5f * MM), PanelLight, 0.26f);
        // Detent ring the handle turns against.
        Barrel(parent, new Vector3(0f, 0f, -2.6f * MM), 17f * MM, 3.0f * MM, Bezel, 0.10f);

        var handle = new GameObject("Handle").transform;
        handle.SetParent(parent, false);
        Barrel(handle, new Vector3(0f, 0f, -4.5f * MM), 8.5f * MM, 7f * MM, KnobRed, 0.30f);
        // The pointer arm — a fuel valve handle is a bar, and its direction IS the reading.
        Box(handle, new Vector3(0f, 11f * MM, -6.5f * MM),
            new Vector3(7.5f * MM, 24f * MM, 5.5f * MM), KnobRed, 0.30f);
        Box(handle, new Vector3(0f, 21f * MM, -6.5f * MM),
            new Vector3(12f * MM, 7f * MM, 5.5f * MM), KnobRed, 0.30f);
        return handle;
    }

    /// <summary>The PARKING BRAKE handle: the small black T-pull under the left panel edge.
    /// Returns the PULL, animated along local Z.</summary>
    public static Transform BrakePull(Transform parent)
    {
        // Sized so it can be FOUND. The first version was a 30 x 22 mm plate with a 26 mm
        // T-bar, which is about right for the real handle but rendered as a smudge from the
        // seat and would have been unfindable in a headset. A 172's park brake is a small
        // control on a big panel; this one has to be a small control a non-pilot can still
        // pick out at a glance, so it is built at the top of the plausible size range.
        Box(parent, new Vector3(0f, 0f, -1f * MM), new Vector3(44f * MM, 32f * MM, 2.5f * MM), PanelLight, 0.28f);
        Barrel(parent, new Vector3(0f, 0f, -3.5f * MM), 8f * MM, 6f * MM, Bezel, 0.15f);

        var pull = new GameObject("Pull").transform;
        pull.SetParent(parent, false);
        Barrel(pull, new Vector3(0f, 0f, -16f * MM), 4f * MM, 30f * MM, Steel, 0.6f, 0.85f);
        // T-bar grip, across the cockpit, sized for two fingers.
        Box(pull, new Vector3(0f, 0f, -32f * MM), new Vector3(38f * MM, 11f * MM, 11f * MM), KnobBlack, 0.25f);
        return pull;
    }

    /// <summary>A TOE-BRAKE PAD on the upper face of a rudder pedal. Purely visual: it
    /// belongs to the pedal, moves with it, and carries no collider of its own.</summary>
    public static void ToeBrakePad(Transform pedal, Vector3 lp, float widthMm, float heightMm)
    {
        var pad = Box(pedal, lp, new Vector3(widthMm * MM, heightMm * MM, 5f * MM), Rubber, 0.10f);
        // Tread ribs, so the pad is legible as the thing a foot presses.
        for (int i = 0; i < 3; i++)
            Box(pad, new Vector3(0f, -0.28f + i * 0.28f, -0.7f), new Vector3(0.86f, 0.14f, 0.5f),
                new Color(0.145f, 0.147f, 0.152f), 0.08f);
    }

    /// <summary>A round instrument: bezel, glass, dark face. Returns the FACE, which
    /// markings and a needle pivot are parented to.</summary>
    public static Transform GaugeCan(Transform parent, Vector3 lp, float diaMm)
    {
        float r = diaMm * 0.5f * MM;
        Barrel(parent, lp + new Vector3(0f, 0f, -1.5f * MM), r, 6f * MM, Bezel, 0.30f, 0.3f);
        Barrel(parent, lp + new Vector3(0f, 0f, -3.0f * MM), r * 0.90f, 2.0f * MM,
               new Color(0.030f, 0.031f, 0.034f), 0.12f);
        var face = new GameObject("Face").transform;
        face.SetParent(parent, false);
        face.localPosition = lp + new Vector3(0f, 0f, -4.1f * MM);
        return face;
    }

    /// <summary>A panel TOGGLE SWITCH. Returns the lever, so a switch that means something
    /// can be animated; decorative ones simply keep theirs at rest.</summary>
    public static Transform Toggle(Transform parent, Vector3 lp, Color col)
    {
        Box(parent, lp + new Vector3(0f, 0f, -1f * MM), new Vector3(11f * MM, 15f * MM, 2f * MM), PanelLight, 0.30f);
        Barrel(parent, lp + new Vector3(0f, 0f, -2.4f * MM), 3.2f * MM, 2.5f * MM, Bezel, 0.20f);
        var lever = new GameObject("Sw").transform;
        lever.SetParent(parent, false);
        lever.localPosition = lp + new Vector3(0f, 0f, -3f * MM);
        Barrel(lever, new Vector3(0f, 2.2f * MM, -3.5f * MM), 1.5f * MM, 8f * MM, col, 0.5f, 0.6f);
        Ball(lever, new Vector3(0f, 3.6f * MM, -6.5f * MM), 4.2f * MM, col, 0.4f);
        lever.localRotation = Quaternion.Euler(-22f, 0f, 0f);
        return lever;
    }

    /// <summary>A CIRCUIT BREAKER: the little white-collared buttons that fill the right
    /// side of a 172 panel. Non-functional, and honestly so — but their absence is a large
    /// part of why the panel reads as empty.</summary>
    public static void Breaker(Transform parent, Vector3 lp)
    {
        Barrel(parent, lp + new Vector3(0f, 0f, -1.2f * MM), 3.4f * MM, 2f * MM, Bezel, 0.25f);
        Barrel(parent, lp + new Vector3(0f, 0f, -2.6f * MM), 2.2f * MM, 2.5f * MM, KnobBlack, 0.30f);
    }


    /// <summary>A Cessna CONTROL WHEEL — the "ram's horn": a chunky central hub on the
    /// column, two arms sweeping outboard, and two grips angled back toward the pilot.
    ///
    /// The GLB's own yoke is a flat U with two straight prongs. From the seat it is the
    /// largest object in the cockpit and it reads as a bracket, not as something to hold —
    /// which matters more here than in most sims, because the participants are not pilots
    /// and have no prior expectation to fall back on. Everything about the ANIMATION stays
    /// with RealCockpit's existing rig; only the shape changes.
    ///
    /// Built at the origin, +Z toward the panel, so the caller places it at the wheel's own
    /// measured centre.</summary>
    public static void ControlWheel(Transform parent, float widthMm)
    {
        float w = widthMm * MM;
        float half = w * 0.5f;

        // Hub: the boss the column ends in. Deep, because on the aeroplane it is.
        Barrel(parent, new Vector3(0f, 0f, 6f * MM), 26f * MM, 30f * MM, KnobBlack, 0.22f);
        Barrel(parent, new Vector3(0f, 0f, -6f * MM), 20f * MM, 14f * MM,
               new Color(0.115f, 0.117f, 0.124f), 0.30f, 0.2f);
        // Column stub disappearing into the panel, so the wheel is visibly ON something.
        Barrel(parent, new Vector3(0f, 0f, 34f * MM), 11f * MM, 58f * MM, Steel, 0.55f, 0.8f);

        // Cross-arms: from the hub outboard, tapering.
        foreach (float sx in new[] { -1f, 1f })
        {
            Box(parent, new Vector3(sx * half * 0.46f, -2f * MM, 0f),
                new Vector3(half * 0.92f, 26f * MM, 21f * MM), KnobBlack, 0.22f);
            // Elbow where the arm turns aft into the grip.
            Barrel(parent, new Vector3(sx * half * 0.90f, -2f * MM, 0f), 13f * MM, 26f * MM, KnobBlack, 0.22f);

            // GRIP: angled back toward the pilot and slightly up, which is the whole point
            // of a ram's horn — a straight vertical prong is a handle on a drawer.
            // GRIP AXIS POINTS AT THE PILOT. Barrel() builds along +Z, which is toward the
            // PANEL, so a grip left at zero rotation sticks forward into the instrument
            // panel and one merely pitched down becomes a peg hanging off the crossbar.
            // The barrel is therefore pushed to negative z — aft, into the pilot's hand —
            // and the whole grip is raked back and canted, which is the shape that makes a
            // ram's horn a ram's horn.
            var grip = new GameObject(sx < 0f ? "GripL" : "GripR").transform;
            grip.SetParent(parent, false);
            grip.localPosition = new Vector3(sx * half * 0.90f, 2f * MM, -8f * MM);
            grip.localRotation = Quaternion.Euler(-14f, 0f, sx * -10f);
            Barrel(grip, new Vector3(0f, 0f, -44f * MM), 14f * MM, 84f * MM, KnobBlack, 0.26f);
            // End cap, at the far (pilot) end.
            Barrel(grip, new Vector3(0f, 0f, -86f * MM), 16f * MM, 8f * MM,
                   new Color(0.115f, 0.117f, 0.124f), 0.30f);
            // Finger ridges, so the grip is legible as a grip at a distance.
            for (int i = 0; i < 3; i++)
                Barrel(grip, new Vector3(0f, 0f, -34f * MM - i * 17f * MM), 15.5f * MM, 4f * MM,
                       new Color(0.075f, 0.076f, 0.080f), 0.18f);
        }

        // Push-to-talk on the left grip: the one red thing on the yoke, and the detail that
        // makes a yoke unmistakably a yoke.
        Barrel(parent, new Vector3(-half * 0.90f + 13f * MM, 12f * MM, -30f * MM),
               4.5f * MM, 5f * MM, KnobRed, 0.35f);
    }

    // ── plumbing ───────────────────────────────────────────────────────────────────

    static void Strip(GameObject g)
    {
        var c = g.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        var r = g.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = true;
        }
    }

    static void Paint(GameObject g, Color c, float gloss, float metal)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", gloss);
        m.SetFloat("_Metallic", metal);
        g.GetComponent<Renderer>().material = m;
    }
}
