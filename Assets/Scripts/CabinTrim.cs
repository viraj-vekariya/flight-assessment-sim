// CabinTrim — breaks up the cabin interior so it stops reading as one brown polygon.
//
// THE PROBLEM
//   The GLB's cabin is a handful of very large untextured surfaces. From the seat, looking
//   down, roughly half the field of view is a single unbroken flat brown shape with no
//   seams, no material change and no contact shadow anywhere in it. Every piece of cockpit
//   hardware could be perfect and the cockpit would still read as a shell around some
//   instruments rather than as the inside of an aeroplane.
//
// WHY IT MATTERS FOR THIS EXPERIMENT, RATHER THAN JUST LOOKING NICER
//   The participants are not pilots. They have no prior model of what a 172 feels like, so
//   everything they believe about the situation comes from what they are shown. A cockpit
//   that visibly is not a place damages the one thing the whole design rests on — that the
//   task is taken seriously as flying an aeroplane. That is not a cosmetic concern; it is
//   the ecological validity the workload manipulation is supposed to act through.
//
// WHAT THIS DOES NOT DO
//   It does not attempt photoreal upholstery, and deliberately so. The fix is structural:
//   put real edges, real material changes and real depth into the space, so the eye has
//   something to read. Four flat surfaces at different tones beat one flat surface, and
//   they cost nothing.
//
// It also tints the largest interior shells down and towards grey. The GLB's brown is both
// too light and too saturated for an interior in shadow, which is why it dominates.
// Materials are INSTANCED per renderer, so nothing outside the cockpit is affected.

using UnityEngine;

public static class CabinTrim
{
    static readonly Color Carpet   = new Color(0.108f, 0.106f, 0.100f);
    static readonly Color Kick     = new Color(0.170f, 0.163f, 0.150f);
    static readonly Color Trim     = new Color(0.238f, 0.228f, 0.208f);
    static readonly Color Sill     = new Color(0.300f, 0.290f, 0.268f);

    /// <summary>Interior shells to tone down, largest first. Measured from the design
    /// probe: these are the cabin walls and roof, and between them they are most of what a
    /// seated participant sees below the glareshield.</summary>
    static readonly string[] ShellNodes =
    {
        "Object_54", "Object_56", "Object_37", "Object_60",   // cabin shell and roof
        "Object_35", "Object_122", "Object_34", "Object_123", // door cards, left and right
        "Object_33", "Object_121", "Object_112",              // armrests and the aft sidewall
        "Object_96", "Object_98", "Object_100",               // seat
    };

    public static void Build(Transform model)
    {
        var root = new GameObject("CabinTrim").transform;
        root.SetParent(model, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        // ── FLOOR ────────────────────────────────────────────────────────────────
        // A dark carpet under the whole footwell. This is the single biggest change:
        // the floor was the largest continuous brown area in the down-view.
        CockpitHardware.Box(root, new Vector3(0f, 0.2640f, 0.560f),
                            new Vector3(0.322f, 0.004f, 0.470f), Carpet, 0.05f);
        // Seat rails, which give the floor a direction and a scale.
        foreach (float sx in new[] { -0.062f, 0.062f })
            CockpitHardware.Box(root, new Vector3(sx, 0.2670f, 0.470f),
                                new Vector3(0.020f, 0.005f, 0.240f), Sill, 0.45f, 0.5f);
        // Lateral seams across the carpet. Without them the new floor is simply a second
        // large flat area replacing the first, and the eye still has nothing to measure
        // depth against.
        for (int i = 0; i < 4; i++)
            CockpitHardware.Box(root, new Vector3(0f, 0.2655f, 0.400f + i * 0.090f),
                                new Vector3(0.318f, 0.003f, 0.004f), Kick, 0.10f);

        // ── KICK PANELS AND SIDEWALL TRIM ────────────────────────────────────────
        // A lower band down each sidewall, a lighter rail above it, and a sill at the
        // door line. Three tones instead of one, and two horizontal edges to read depth
        // against.
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx * 0.1665f;
            CockpitHardware.Box(root, new Vector3(x, 0.300f, 0.545f),
                                new Vector3(0.006f, 0.076f, 0.430f), Kick, 0.10f);
            CockpitHardware.Box(root, new Vector3(x, 0.3405f, 0.545f),
                                new Vector3(0.008f, 0.009f, 0.430f), Sill, 0.40f);
            CockpitHardware.Box(root, new Vector3(x, 0.395f, 0.545f),
                                new Vector3(0.005f, 0.100f, 0.430f), Trim, 0.14f);
        }

        // ── PANEL UNDERSIDE ──────────────────────────────────────────────────────
        // The lower edge of the instrument panel, so the panel ends somewhere instead of
        // dissolving into the wall.
        CockpitHardware.Box(root, new Vector3(0f, 0.2695f, 0.752f),
                            new Vector3(0.352f, 0.008f, 0.016f), Sill, 0.40f);

        SetLayer(root, CockpitBuilder.CockpitLayer);
        TintShells(model);
    }

    /// <summary>Tone the big interior shells down and towards grey.
    ///
    /// Instanced per renderer via `.material`, never `.sharedMaterial`, so this cannot
    /// reach any other object that happens to use the same source material — including
    /// the aeroplane's exterior, which is a different model on a different layer and must
    /// keep its own paint.</summary>
    static void TintShells(Transform model)
    {
        // THE COLOUR PROPERTY NAME IS THE SHADER'S, NOT UNITY'S.
        //
        // The GLB is imported by gltfast, whose materials use the glTF/PbrMetallicRoughness
        // shader. That shader has neither `_Color` (Standard) nor `_BaseColor` (URP) — its
        // albedo is `baseColorFactor`. Two earlier versions of this tint checked for the
        // first two names, found neither, and did nothing at all while reporting success.
        // The list below is tried in order and the outcome is logged either way.
        string[] candidates = { "baseColorFactor", "_BaseColorFactor", "_BaseColor", "_Color" };

        int done = 0, missed = 0;
        foreach (string n in ShellNodes)
        {
            var t = FindDeep(model, n);
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.material;                      // instance, not shared
                string prop = null;
                foreach (string cand in candidates)
                    if (m.HasProperty(cand)) { prop = cand; break; }
                if (prop == null)
                {
                    missed++;
                    Debug.LogWarning("[CabinTrim] " + n + ": shader '" + m.shader.name
                                   + "' exposes no known colour property — not tinted.");
                    continue;
                }
                Color c = m.GetColor(prop);
                float grey = c.r * 0.30f + c.g * 0.59f + c.b * 0.11f;
                // 45% toward its own luminance kills the saturation without turning it
                // grey; 0.62 darkens it to something in shadow rather than in daylight.
                Color tinted = Color.Lerp(c, new Color(grey, grey, grey), 0.45f) * 0.62f;
                tinted.a = c.a;
                m.SetColor(prop, tinted);
                done++;
            }
        }

        if (done == 0)
            Debug.LogWarning("[CabinTrim] toned down NOTHING (" + missed + " surfaces had no "
                           + "usable colour property). The cabin will still be flat brown.");
        else
            Debug.Log("[CabinTrim] toned down " + done + " interior surfaces"
                    + (missed > 0 ? " (" + missed + " skipped)" : "") + ".");
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
        return null;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
    }
}
