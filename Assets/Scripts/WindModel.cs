// WindModel — the moving air mass the aeroplane flies through.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHY THIS EXISTS
// ═══════════════════════════════════════════════════════════════════════════════
// Before this file the simulator had turbulence but no WIND. Those are different
// things and only one of them is a workload manipulation:
//
//   * Turbulence (already present, ScenarioEngine.FixedUpdate) is a zero-mean random
//     force. It makes the aeroplane wobble. The pilot's job does not change.
//   * Wind is a STEADY displacement of the whole air mass. It changes the task:
//     the aeroplane no longer goes where it points, so the pilot must hold a crab
//     angle, must anticipate drift, must decrab in the flare, and must keep straight
//     on the runway against a weathervaning tendency.
//
// Only the second is what the aviation human-factors literature means by
// "crosswind workload", and it was impossible to stage. `MissionDefinition` even
// declared a `CrosswindMs` field — which was written by nothing and read by nothing.
// Every landing mission was therefore flown in dead calm, which is why runway
// centreline deviation was a nearly free metric.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE CONTRACT — the same one AircraftSystems has
// ═══════════════════════════════════════════════════════════════════════════════
// With `Enabled == false` (or zero wind), `Sample()` returns Vector3.zero and the
// flight model is BIT-IDENTICAL to one with no wind layer at all. That is what lets
// the eleven inherited missions keep their verified behaviour while the new ones
// use wind as a controlled independent variable.
//
// ═══════════════════════════════════════════════════════════════════════════════
// REPRODUCIBILITY
// ═══════════════════════════════════════════════════════════════════════════════
// Gusts are Perlin noise sampled against the MISSION clock (physics-locked, 0.02 s)
// with an offset drawn from the trial seed. No Random.value, no Time.time, no frame
// dependence: the same participant + session + mission replays the same gust at the
// same instant on any machine. `ScenarioEngine` drives `Clock`; if no trial is
// running it falls back to Time.fixedTime so the free-flight/dev path still works.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE MODEL
// ═══════════════════════════════════════════════════════════════════════════════
//   1. STEADY WIND, specified meteorologically: direction the wind blows FROM, in
//      degrees true, and speed in m/s. "270/10" is a 10 m/s wind from the west.
//   2. SURFACE SHEAR. The wind is not the same at 3 m and at 600 m. A power-law
//      boundary layer, v(h) = v_ref * (h/h_ref)^alpha with alpha = 0.14 (the open-
//      country value used in wind-engineering practice) and h_ref = 300 m, is enough
//      to give the real effect: the reported surface wind is weaker than the wind the
//      aeroplane is in on final, so drift INCREASES as you climb and DECREASES as you
//      descend, and the pilot must keep re-trimming the crab all the way down.
//   3. GUST. A band-limited (Perlin) fluctuation about the steady vector, correlated
//      in time — not white noise, because real gusts have a timescale of seconds and
//      that timescale is what makes them trackable-but-effortful.
//   4. OPTIONAL DISCRETE SHEAR LAYER. A one-off change in the wind vector across a
//      narrow altitude band, for the windshear-on-final missions. This is the
//      microburst-lite case: modelled as a horizontal wind change only, deliberately
//      NOT as a downdraught, because a genuine microburst is unsurvivable in a C172
//      and the mission would then measure luck rather than workload.
//
// WHAT THIS IS NOT
//   Not a CFD wind field, no terrain-induced rotor, no thermals, no wake turbulence.
//   Those would add variance the experiment cannot control and would not change the
//   pilot's task in a way the design needs. Listed in every mission's Approximations.

using UnityEngine;

public static class WindModel
{
    // ── configuration (set per trial by ScenarioEngine) ─────────────────────────
    /// <summary>Master switch. False => Sample() is exactly Vector3.zero.</summary>
    public static bool Enabled { get; private set; }
    /// <summary>Direction the wind blows FROM, degrees true (meteorological).</summary>
    public static float FromDeg { get; private set; }
    /// <summary>Steady speed at the reference height, m/s.</summary>
    public static float SpeedMs { get; private set; }
    /// <summary>Peak gust excursion about the steady vector, m/s.</summary>
    public static float GustMs { get; private set; }
    /// <summary>Altitude (m AMSL) at the middle of the discrete shear layer. 0 = none.</summary>
    public static float ShearAltM { get; private set; }
    /// <summary>Change in wind speed across the shear layer, m/s (+ = stronger above).</summary>
    public static float ShearDeltaMs { get; private set; }
    /// <summary>Change in wind direction across the shear layer, degrees.</summary>
    public static float ShearDeltaDeg { get; private set; }

    /// <summary>Mission clock, seconds. Written by ScenarioEngine.FixedUpdate so the
    /// gust series is locked to the physics step, not the frame.</summary>
    public static float Clock;

    /// <summary>Reference height for the quoted steady wind. The surface wind an ATIS
    /// would report is measured at ~10 m; the value a mission specifies is the
    /// FREE-STREAM wind, quoted at 300 m, and the surface value follows from the
    /// profile. Missions are authored in free-stream terms because that is the wind
    /// the aeroplane spends most of the trial in.</summary>
    public const float RefHeightM = 300f;
    /// <summary>Power-law exponent for the surface layer. 0.14 = open country.</summary>
    public const float ShearExponent = 0.14f;
    /// <summary>Below this height the profile is frozen, so the model cannot divide
    /// by zero or hand a parked aeroplane a step change on the first frame.</summary>
    public const float MinHeightM = 3f;

    static float gustSeedA, gustSeedB;
    static float groundY;

    /// <summary>Turn the wind off entirely. Called at the head of every trial so no
    /// wind can leak from one mission into the next — the same reset discipline
    /// AircraftSystems.ResetAll() and AircraftController.ResetConfiguration() enforce.</summary>
    public static void Disable()
    {
        Enabled = false;
        FromDeg = SpeedMs = GustMs = 0f;
        ShearAltM = ShearDeltaMs = ShearDeltaDeg = 0f;
        Clock = 0f;
    }

    /// <summary>Configure for one trial. `seed` is the trial seed, so the gust series
    /// is reproducible from the value already written into metadata.json.</summary>
    public static void Configure(float fromDeg, float speedMs, float gustMs, int seed,
                                 float shearAltM = 0f, float shearDeltaMs = 0f, float shearDeltaDeg = 0f,
                                 float groundElevationM = 0f)
    {
        FromDeg = Mathf.Repeat(fromDeg, 360f);
        SpeedMs = Mathf.Max(0f, speedMs);
        GustMs = Mathf.Max(0f, gustMs);
        ShearAltM = shearAltM; ShearDeltaMs = shearDeltaMs; ShearDeltaDeg = shearDeltaDeg;
        groundY = groundElevationM;
        Clock = 0f;

        // Perlin offsets from the trial seed: same seed => same gust, any machine.
        var r = new System.Random(seed);
        gustSeedA = 3.7f + (float)r.NextDouble() * 40f;
        gustSeedB = 71.3f + (float)r.NextDouble() * 40f;

        Enabled = SpeedMs > 0.01f || GustMs > 0.01f;
    }

    /// <summary>Unit vector the wind blows TOWARDS, in world axes (x = east, z = north).
    /// Meteorological "from 090" means an easterly, i.e. air moving towards the west.</summary>
    static Vector3 Towards(float fromDeg)
    {
        float towards = fromDeg + 180f;
        float r = towards * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
    }

    /// <summary>The air-mass velocity at a world position, m/s, world axes.
    /// Returns exactly zero when the wind is off — see the contract at the top.</summary>
    public static Vector3 Sample(Vector3 worldPos)
    {
        if (!Enabled) return Vector3.zero;

        float h = Mathf.Max(MinHeightM, worldPos.y - groundY);

        // 1 + 2: steady wind scaled by the boundary-layer profile.
        float profile = Mathf.Pow(h / RefHeightM, ShearExponent);
        float spd = SpeedMs * profile;
        float dir = FromDeg;

        // 4: discrete shear layer, blended over a 60 m band so it is a shear and not
        // a discontinuity (a step change would be a physics impulse, not a wind).
        if (ShearAltM > 0.1f && (Mathf.Abs(ShearDeltaMs) > 0.01f || Mathf.Abs(ShearDeltaDeg) > 0.01f))
        {
            float k = Mathf.Clamp01((worldPos.y - (ShearAltM - 30f)) / 60f);   // 0 below, 1 above
            spd += ShearDeltaMs * k;
            dir += ShearDeltaDeg * k;
        }
        spd = Mathf.Max(0f, spd);

        Vector3 v = Towards(dir) * spd;

        // 3: gust — two decorrelated Perlin channels on the along/across axes, so the
        // gust is not merely a speed ripple but also swings the direction, which is
        // what makes a gusty crosswind harder than a steady one of the same mean.
        if (GustMs > 0.01f)
        {
            float t = Clock;
            float g1 = (Mathf.PerlinNoise(gustSeedA, t * 0.19f) - 0.5f) * 2f;
            float g2 = (Mathf.PerlinNoise(gustSeedB, t * 0.27f) - 0.5f) * 2f;
            Vector3 along = Towards(dir);
            Vector3 across = new Vector3(along.z, 0f, -along.x);
            // Gust is attenuated near the ground by the same profile: surface friction
            // damps the fluctuation as well as the mean.
            v += (along * g1 + across * g2 * 0.6f) * GustMs * profile;
        }

        return v;
    }

    // ── pure helpers ────────────────────────────────────────────────────────────
    // These take their arguments explicitly and read no static state, so a mission
    // definition, a brief, a doc generator or a test can ask "what wind is this?"
    // without a trial being configured. Everything above is the LIVE air mass; these
    // are the arithmetic.

    /// <summary>Surface (10 m) speed corresponding to a free-stream speed at RefHeightM.</summary>
    public static float SurfaceSpeed(float freeStreamMs) =>
        freeStreamMs * Mathf.Pow(10f / RefHeightM, ShearExponent);

    /// <summary>Crosswind component on a runway, m/s, positive = from the right.</summary>
    public static float Crosswind(float fromDeg, float freeStreamMs, float runwayHeadingDeg) =>
        SurfaceSpeed(freeStreamMs) * Mathf.Sin((fromDeg - runwayHeadingDeg) * Mathf.Deg2Rad);

    /// <summary>Headwind component on a runway, m/s. Negative = tailwind.</summary>
    public static float Headwind(float fromDeg, float freeStreamMs, float runwayHeadingDeg) =>
        SurfaceSpeed(freeStreamMs) * Mathf.Cos((fromDeg - runwayHeadingDeg) * Mathf.Deg2Rad);

    /// <summary>ATIS-style surface wind string for an arbitrary wind, e.g. "270/12G18 kt".</summary>
    public static string Report(float fromDeg, float freeStreamMs, float gustMs)
    {
        if (freeStreamMs <= 0.01f && gustMs <= 0.01f) return "CALM";
        float spd = SurfaceSpeed(freeStreamMs);
        string s = Mathf.RoundToInt(Mathf.Repeat(fromDeg, 360f)).ToString("000") + "/" +
                   Mathf.RoundToInt(spd * 1.94384f).ToString("00");
        if (gustMs > 1f) s += "G" + Mathf.RoundToInt((spd + SurfaceSpeed(gustMs)) * 1.94384f).ToString("00");
        return s + " kt";
    }

    /// <summary>Steady surface wind (at 10 m) as a pilot would be given it by ATIS:
    /// "270/08". Used in mission briefings and ATC text so the number the participant
    /// hears matches the wind they are actually in.</summary>
    public static string SurfaceReport()
    {
        if (!Enabled) return "CALM";
        float spd = SpeedMs * Mathf.Pow(10f / RefHeightM, ShearExponent);
        string s = Mathf.RoundToInt(FromDeg).ToString("000") + "/" + Mathf.RoundToInt(spd * 1.94384f).ToString("00");
        if (GustMs > 1f) s += "G" + Mathf.RoundToInt((spd + GustMs) * 1.94384f).ToString("00");
        return s + " kt";
    }

    /// <summary>Crosswind component (m/s) on a runway of the given heading, at the
    /// surface. Positive = from the right. This is the number the mission design
    /// reasons in, so it is computed here rather than by hand in the mission table.</summary>
    public static float CrosswindOn(float runwayHeadingDeg)
    {
        if (!Enabled) return 0f;
        float spd = SpeedMs * Mathf.Pow(10f / RefHeightM, ShearExponent);
        return spd * Mathf.Sin((FromDeg - runwayHeadingDeg) * Mathf.Deg2Rad);
    }

    /// <summary>Headwind component (m/s) on a runway of the given heading. Negative =
    /// a tailwind, which lengthens the take-off roll and flattens the approach.</summary>
    public static float HeadwindOn(float runwayHeadingDeg)
    {
        if (!Enabled) return 0f;
        float spd = SpeedMs * Mathf.Pow(10f / RefHeightM, ShearExponent);
        return spd * Mathf.Cos((FromDeg - runwayHeadingDeg) * Mathf.Deg2Rad);
    }

    /// <summary>Meteorological direction that puts `crossMs` of crosswind (positive =
    /// from the right) and `headMs` of headwind on a runway. Lets a mission be
    /// authored as "6 m/s crosswind from the right" — the quantity the workload
    /// argument is about — instead of as a direction the author has to solve for.
    /// Returns the FREE-STREAM speed to configure, via `speedOut`.</summary>
    public static float DirectionFor(float runwayHeadingDeg, float crossMs, float headMs, out float speedOut)
    {
        float surface = Mathf.Sqrt(crossMs * crossMs + headMs * headMs);
        float rel = Mathf.Atan2(crossMs, headMs) * Mathf.Rad2Deg;
        speedOut = surface / Mathf.Pow(10f / RefHeightM, ShearExponent);   // back out the free-stream value
        return Mathf.Repeat(runwayHeadingDeg + rel, 360f);
    }
}
