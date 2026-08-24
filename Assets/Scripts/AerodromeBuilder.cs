// AerodromeBuilder — the ground half of the airfield: an apron with a parking stand,
// a parallel taxiway, a link to the runway threshold, hold-short markings, and the
// signage and checkpoints a pilot taxis by.
//
// WHY THIS EXISTS
//   The experiment's requirements specify a take-off task that begins at an aircraft
//   parking stand and taxis to the assigned runway, with taxiways, direction
//   indicators and checkpoints. Before this, the simulator had a runway and nothing
//   else — the aeroplane simply materialised on the centreline — so a take-off task
//   could only ever have been "apply power", which carries almost no cognitive load
//   and cannot be graded into three levels.
//
//   Taxiing is genuinely load-bearing for this study. It is a NAVIGATION task on a
//   surface, under ATC instruction, with a hard stop (the hold-short line) that must
//   be respected, and it is the phase where runway incursions actually happen. It
//   loads working memory (hold the route), attention switching (outside picture vs
//   signs vs radio) and procedural compliance, while costing almost nothing in
//   psychomotor terms — which is exactly the kind of demand this experiment wants to
//   separate from stick-and-rudder effort.
//
// LAYOUT  (runway is 30 x 600 m, centred on the origin, take-off toward +Z)
//
//                            +Z  take-off direction
//                             ^
//          ┌──────────────────┴──────────────────┐
//          │             RUNWAY 01               │   x -15..+15, z -300..+300
//          └──────────────────┬──────────────────┘
//                    z=-300   │ threshold
//        LINK B  z=-320  ─────┤  x -80..0
//                             │
//    HOLD SHORT  z=-345  ═════╡
//                             │
//        TAXIWAY A   x=-80    │  z -590..-320
//                             │
//          ┌──────────────────┴──┐
//          │  APRON / STAND 1    │  centre (-80, -560)
//          └─────────────────────┘
//
//   Taxi route: STAND -> A (north) -> HOLD SHORT -> right onto LINK B (east)
//               -> left onto the runway -> LINE UP -> take off.
//   Two 90-degree turns and one mandatory stop, about 330 m in total.
//
// SCALE NOTE. A real airline gate-to-runway taxi is 10-15 minutes; this one is about
// a minute. That is deliberate and is argued in FINAL_EXPERIMENT_PROTOCOL.md: trial
// duration has to be constant across all twelve missions, and twelve 15-minute trials
// is three hours of flying inside one EEG session, which fatigue would dominate. The
// taxi is shortened by putting the stand close to the runway — a small-field layout —
// NOT by speeding the aeroplane up, so taxi speed, steering and braking stay real.

using UnityEngine;

/// <summary>Geometry of the taxi route, published so missions and the scenario
/// engine can reference named points instead of magic numbers.</summary>
public static class Aerodrome
{
    // --- runway ---
    public const float RunwayHalfWidth = 15f;
    public const float RunwayHalfLength = 300f;
    public const float ThresholdZ = -300f;      // take-off threshold (roll toward +Z)

    // --- ground layout ---
    public const float TaxiwayX = -80f;         // parallel taxiway centreline
    public const float LinkZ = -320f;           // link taxiway to the threshold
    public const float HoldShortZ = -345f;      // mandatory stop before the link
    public const float ApronZ = -560f;

    /// <summary>Where the aircraft is parked at the start of a take-off mission,
    /// nose north, on the stand.</summary>
    public static readonly Vector3 StandPos = new Vector3(TaxiwayX, 1.2f, -585f);
    public static readonly Quaternion StandRot = Quaternion.identity;   // facing +Z

    /// <summary>The line-up point on the runway centreline.</summary>
    public static readonly Vector3 LineUpPos = new Vector3(0f, 1.2f, -290f);

    /// <summary>The taxi route, in order. Radii are generous because a taxiing
    /// aeroplane is not expected to hit a point, only to pass through the area.</summary>
    public static Waypoint[] TaxiRoute() => new[]
    {
        new Waypoint(new Vector3(TaxiwayX, 0f, -460f), "ALPHA", 22f),
        new Waypoint(new Vector3(TaxiwayX, 0f, HoldShortZ), "HOLD SHORT 01", 18f),
        new Waypoint(new Vector3(-40f, 0f, LinkZ), "BRAVO", 20f),
        new Waypoint(new Vector3(0f, 0f, ThresholdZ + 12f), "LINE UP 01", 20f),
    };

    /// <summary>Index in TaxiRoute() of the hold-short point — the engine stops the
    /// aircraft here until ATC clears it to line up.</summary>
    public const int HoldShortIndex = 1;

    /// <summary>True when the aeroplane is on the runway surface.</summary>
    public static bool OnRunway(Vector3 p) =>
        Mathf.Abs(p.x) < RunwayHalfWidth + 1f && Mathf.Abs(p.z) < RunwayHalfLength + 5f;
}

public static class AerodromeBuilder
{
    static readonly Color Asphalt = new Color(0.19f, 0.19f, 0.21f);
    static readonly Color Concrete = new Color(0.36f, 0.36f, 0.38f);
    static readonly Color Yellow = new Color(0.95f, 0.80f, 0.10f);
    static readonly Color SignYellow = new Color(0.90f, 0.72f, 0.05f);
    static readonly Color SignRed = new Color(0.62f, 0.08f, 0.08f);
    static readonly Color White = Color.white;

    public static void Build(Transform parent)
    {
        var root = new GameObject("Aerodrome").transform;
        root.SetParent(parent);

        BuildApron(root);
        BuildTaxiways(root);
        BuildHoldShort(root);
        BuildSigns(root);
    }

    // ── apron + parking stand ───────────────────────────────────────────────────
    static void BuildApron(Transform root)
    {
        Slab(root, "Apron", new Vector3(Aerodrome.TaxiwayX, 0.18f, Aerodrome.ApronZ - 20f),
             new Vector3(70f, 0.36f, 70f), Concrete, SurfaceKind.Runway);

        // Stand box + a lead-in line the pilot taxis out along.
        Marking(root, new Vector3(Aerodrome.TaxiwayX, 0.38f, -585f), new Vector3(16f, 0.02f, 0.5f), Yellow);
        Marking(root, new Vector3(Aerodrome.TaxiwayX - 8f, 0.38f, -592f), new Vector3(0.5f, 0.02f, 16f), Yellow);
        Marking(root, new Vector3(Aerodrome.TaxiwayX + 8f, 0.38f, -592f), new Vector3(0.5f, 0.02f, 16f), Yellow);
        // Lead-out centreline from the stand to the taxiway.
        Marking(root, new Vector3(Aerodrome.TaxiwayX, 0.38f, -560f), new Vector3(0.4f, 0.02f, 56f), Yellow);
        Label(root, new Vector3(Aerodrome.TaxiwayX, 0.6f, -597f), "STAND 1", 3.0f, White);
    }

    // ── taxiway A (parallel) + link B (to the threshold) ────────────────────────
    static void BuildTaxiways(Transform root)
    {
        // A: from the apron north to the link.
        float aLen = Aerodrome.LinkZ - (Aerodrome.ApronZ - 40f);
        float aMid = (Aerodrome.LinkZ + (Aerodrome.ApronZ - 40f)) * 0.5f;
        Slab(root, "TaxiwayA", new Vector3(Aerodrome.TaxiwayX, 0.18f, aMid),
             new Vector3(23f, 0.36f, aLen), Asphalt, SurfaceKind.Runway);
        Marking(root, new Vector3(Aerodrome.TaxiwayX, 0.38f, aMid), new Vector3(0.4f, 0.02f, aLen - 4f), Yellow);

        // B: from A east to the runway threshold area.
        float bLen = 0f - Aerodrome.TaxiwayX;
        Slab(root, "TaxiwayB", new Vector3(Aerodrome.TaxiwayX + bLen * 0.5f, 0.18f, Aerodrome.LinkZ),
             new Vector3(bLen + 20f, 0.36f, 23f), Asphalt, SurfaceKind.Runway);
        Marking(root, new Vector3(Aerodrome.TaxiwayX + bLen * 0.5f, 0.38f, Aerodrome.LinkZ),
                new Vector3(bLen, 0.02f, 0.4f), Yellow);

        // Curved-ish corner fillets, faked with two short diagonal markings so the
        // turn reads as a taxiway turn rather than a right angle.
        MarkingR(root, new Vector3(Aerodrome.TaxiwayX + 7f, 0.38f, Aerodrome.LinkZ - 7f),
                 Quaternion.Euler(0f, 45f, 0f), new Vector3(0.4f, 0.02f, 18f), Yellow);
        MarkingR(root, new Vector3(-8f, 0.38f, Aerodrome.LinkZ + 8f),
                 Quaternion.Euler(0f, -45f, 0f), new Vector3(0.4f, 0.02f, 20f), Yellow);
    }

    // ── hold-short markings ─────────────────────────────────────────────────────
    static void BuildHoldShort(Transform root)
    {
        // Two solid + two dashed bars across taxiway A, the standard pattern.
        for (int i = 0; i < 4; i++)
        {
            float z = Aerodrome.HoldShortZ + i * 1.2f;
            bool solid = i < 2;
            if (solid)
                Marking(root, new Vector3(Aerodrome.TaxiwayX, 0.39f, z), new Vector3(22f, 0.02f, 0.5f), Yellow);
            else
                for (int k = -4; k <= 4; k++)
                    Marking(root, new Vector3(Aerodrome.TaxiwayX + k * 2.4f, 0.39f, z),
                            new Vector3(1.4f, 0.02f, 0.5f), Yellow);
        }
    }

    // ── signage: the "direction indicators" the requirements ask for ────────────
    static void BuildSigns(Transform root)
    {
        // Mandatory (red) runway holding-position sign at the hold-short line.
        Sign(root, new Vector3(Aerodrome.TaxiwayX - 14f, 0f, Aerodrome.HoldShortZ), "01", SignRed, White);
        Sign(root, new Vector3(Aerodrome.TaxiwayX + 14f, 0f, Aerodrome.HoldShortZ), "01", SignRed, White);

        // Location / direction (yellow) signs along the route.
        Sign(root, new Vector3(Aerodrome.TaxiwayX - 14f, 0f, -520f), "A", SignYellow, Color.black);
        Sign(root, new Vector3(Aerodrome.TaxiwayX - 14f, 0f, -460f), "A  ->  01", SignYellow, Color.black);
        Sign(root, new Vector3(-30f, 0f, Aerodrome.LinkZ - 14f), "B  ->  01", SignYellow, Color.black);

        // The painted runway designator was removed at the user's request (22 Aug 2026).
        // It is what real runways carry (01 = magnetic heading 010 deg) but it is purely
        // cosmetic here — nothing reads it. The RED HOLD-SHORT SIGNS above are kept: they
        // are the visual cue H1's blocked-runway hold depends on.
    }

    // ── primitives ──────────────────────────────────────────────────────────────
    static void Slab(Transform root, string name, Vector3 pos, Vector3 scale, Color col, SurfaceKind kind)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(root);
        g.transform.position = pos;
        g.transform.localScale = scale;
        Paint(g, col);
        SurfaceTag.Add(g, kind);
    }

    static void Marking(Transform root, Vector3 pos, Vector3 scale, Color col)
        => MarkingR(root, pos, Quaternion.identity, scale, col);

    static void MarkingR(Transform root, Vector3 pos, Quaternion rot, Vector3 scale, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(root);
        g.transform.position = pos;
        g.transform.rotation = rot;
        g.transform.localScale = scale;
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, col);
    }

    /// <summary>An airfield sign: a small coloured board on two legs, with text.</summary>
    static void Sign(Transform root, Vector3 basePos, string text, Color board, Color ink)
    {
        var holder = new GameObject("Sign_" + text).transform;
        holder.SetParent(root);
        holder.position = basePos + new Vector3(0f, 0.9f, 0f);

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.transform.SetParent(holder);
        panel.transform.localPosition = Vector3.zero;
        panel.transform.localScale = new Vector3(3.4f, 1.1f, 0.12f);
        SimUtil.Destroy(panel.GetComponent<Collider>());
        Paint(panel, board);

        for (int s = -1; s <= 1; s += 2)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.transform.SetParent(holder);
            leg.transform.localPosition = new Vector3(s * 1.3f, -0.75f, 0f);
            leg.transform.localScale = new Vector3(0.12f, 1.5f, 0.12f);
            SimUtil.Destroy(leg.GetComponent<Collider>());
            Paint(leg, new Color(0.75f, 0.75f, 0.78f));
        }

        // Text on both faces so the sign is readable taxiing either way.
        for (int f = -1; f <= 1; f += 2)
        {
            var tm = new GameObject("Text").AddComponent<TextMesh>();
            tm.transform.SetParent(holder);
            tm.transform.localPosition = new Vector3(0f, 0f, f * 0.09f);
            tm.transform.localRotation = Quaternion.Euler(0f, f > 0 ? 0f : 180f, 0f);
            tm.text = text;
            tm.characterSize = 0.12f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = ink;
        }
    }

    /// <summary>Flat text painted on the surface (stand name, runway designator).</summary>
    static void Label(Transform root, Vector3 pos, string text, float size, Color col)
    {
        var tm = new GameObject("Label_" + text).AddComponent<TextMesh>();
        tm.transform.SetParent(root);
        tm.transform.position = pos;
        tm.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lying on the ground
        tm.text = text;
        tm.characterSize = size * 0.1f;
        tm.fontSize = 64;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = col;
    }

    static void Paint(GameObject g, Color c)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", 0.15f);
        g.GetComponent<Renderer>().material = m;
    }
}
