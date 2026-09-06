// LevelDesignShots — renders the WORLD from fixed exterior viewpoints, under a chosen
// lighting/sky variant, so the look of the level can be judged from pictures.
//
// Dev/documentation tool. Runs ONLY with -levelshots; never ships and never touches
// experiment data, terrain, the aerodrome, the cockpit, physics, missions or telemetry.
//
// Why it exists: the mission battery proves the twelve scenarios RUN. It says nothing
// about whether the world looks like a place, whether the sun angle flatters or ruins
// the terrain, or whether the approach reads at 1.5 km. That needs eyes on a picture
// taken from somewhere a pilot actually is.
//
//   Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunLevelShots \
//         -levelshots -lighting=N -terrain=M -logFile level.log   (N, M = 0..3)
//
// NOTE: no -nographics (the PFD/MFD render to off-screen cameras and the null graphics
// device crashes on them) and no -quit (the editor must survive long enough to play).
//
// LIGHTING VARIANTS ARE FLAG-ONLY AND VARIANT 0 IS A NO-OP.
//   Variant 0 is the project exactly as it ships: ApplyLighting() returns before it
//   touches a single RenderSettings field or the sun. Variants 1..3 are reachable only
//   through -lighting=N on this dev tool, and WorldBuilder.cs is not modified at all —
//   the sun is taken from RenderSettings.sun and every sky material used here is a
//   fresh instance, so WorldBuilder's own shared clearSky/overcastSky are never mutated.
//   Anyone running the sim normally gets byte-identical lighting to before this file.
//
// TERRAIN VARIANTS ARE FLAG-ONLY AND VARIANT 0 IS A NO-OP, ON THE SAME TERMS.
//   -terrain=M calls WorldBuilder.ReskinTerrainForDesign, which returns immediately for
//   M = 0 and otherwise replaces ONLY the already-built terrain's layers and alphamap.
//   No height is read or written by that path, so terrain SHAPE — and therefore the
//   runway pad, take-off and every mission's ground clearance — is identical in all
//   four. The terrain look is a property of the world, not of a mission, so it is the
//   same in every scenario and cannot become a per-mission confound.

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public class LevelDesignShots : MonoBehaviour
{
    public static bool Finished { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-levelshots") { new GameObject("LevelDesignShots").AddComponent<LevelDesignShots>(); return; }
    }

    const int W = 1600, H = 1000;

    // ── the three fixed viewpoints ────────────────────────────────────────────────
    // The runway is a 600 m strip along +Z on the centreline x = 0 (WorldBuilder
    // .BuildRunway): surface at y = 0.4, thresholds at z = -290 and z = +290, and the
    // aeroplane spawns at (0, 1.2, -280) facing +Z. So the landing direction is +Z and
    // the approach comes in from -Z. Every number below is derived from that geometry,
    // not eyeballed.
    struct View { public string Name; public Vector3 Pos; public Vector3 LookAt; public float Fov;
                  public View(string n, Vector3 p, Vector3 l, float f) { Name = n; Pos = p; LookAt = l; Fov = f; } }

    static readonly View[] Views =
    {
        // Short final: 1.5 km out from the threshold, 150 m above the runway. (That is a
        // 5.2 deg path, steeper than a real 3 deg ILS — it is the framing that was asked
        // for, not a glideslope.)
        //
        // Aimed at the far END of the strip, not the touchdown zone, and on a 30 deg lens
        // rather than 42. At 42 deg aimed short, the runway was ~70 px in a frame that
        // was otherwise 1600x600 of undifferentiated grass — a picture in which no
        // lighting variant could be told from another. Aiming long lifts the horizon into
        // frame and the tighter lens makes the strip and the apron legible.
        new View("short_final", new Vector3(0f, 150f, -1790f), new Vector3(0f, 0f, 290f), 30f),

        // On the runway at the threshold, eye height, looking straight down the centreline.
        new View("threshold",   new Vector3(0f, 3f, -288f),    new Vector3(0f, 3f, 400f),  42f),

        // Cruise: 600 m up over the field, turned 22 deg right of the runway heading so
        // the city at (250, 1950) and the coastal range that grows toward +X are both in
        // frame, and pitched 7 deg down so ground and horizon share the picture.
        new View("cruise",      new Vector3(0f, 600f, 0f),     CruiseAim(),                42f),

        // Flare: 15 m over the threshold on the centreline, looking down the strip.
        //
        // This is the shot the terrain round exists for. The other three are at 150 m,
        // eye height on the ground, and 600 m — none of them is at the altitude where
        // ground TEXTURE decides whether the picture reads as real. At 15 m the grass
        // tile is a few metres across in frame, so a tile that is too coarse stops
        // being detail and becomes flat paint, which is the fault being hunted.
        //
        // Aimed at y = 6 down the far end rather than level, so ground fills the lower
        // two-thirds of the frame instead of the horizon splitting it.
        new View("flare",       new Vector3(0f, 15.4f, -290f), new Vector3(0f, 6f, 290f),  42f),
    };

    static Vector3 CruiseAim()
    {
        const float yawDeg = 22f, pitchDeg = 7f, reach = 4000f;
        var dir = Quaternion.Euler(pitchDeg, yawDeg, 0f) * Vector3.forward;
        return new Vector3(0f, 600f, 0f) + dir * reach;
    }

    static readonly string[] VariantNames = { "current", "clear_morning", "midday_haze", "overcast_soft" };
    static readonly string[] TerrainNames = { "current", "dry_season", "green_temperate", "worn_airfield" };

    int variant;
    int terrain;
    string dir;
    Camera cam;

    /// <summary>The stem both axes are readable in: L&lt;lighting&gt;_&lt;name&gt;_T&lt;terrain&gt;_&lt;name&gt;.</summary>
    string Stem => string.Format("L{0}_{1}_T{2}_{3}", variant, VariantNames[variant],
                                                      terrain, TerrainNames[terrain]);

    IEnumerator Start()
    {
        variant = ReadVariant();
        terrain = ReadTerrain();

        // Nothing here flies the aeroplane, but FlightTest does, and it would be moving
        // the machine (and therefore the terrain LOD and the aircraft's own shadow)
        // underneath a set of shots that are supposed to be identical between variants.
        SimDriver.Claim("LevelDesignShots");

        dir = Path.Combine(Application.persistentDataPath, "LevelDesign");
        Directory.CreateDirectory(dir);

        // WIPE FIRST — but only THIS variant's files.
        //
        // CockpitDesignShots wipes its whole folder, because a stale PNG from an older
        // build once got read as evidence that a fix had not worked. That rule is right
        // and it is kept: after this loop it is impossible for a lighting<N>_*.png to be
        // left over from an earlier run of variant N.
        //
        // It is scoped to the variant because this tool is run FOUR times, once per
        // -lighting value, to produce one set of twelve images. A whole-folder wipe would
        // leave only the last variant's three files and quietly destroy the comparison
        // the tool exists to make.
        //
        // Scoped to the LIGHTING x TERRAIN pair for the same reason: the tool is now run
        // sixteen times, and a wipe keyed on lighting alone would delete the other three
        // terrain variants shot under the same light.
        int wiped = 0;
        foreach (var f in Directory.GetFiles(dir, Stem + "_*"))
        { try { File.Delete(f); wiped++; } catch { } }
        Debug.Log("[LEVELSHOTS] lighting " + variant + " (" + VariantNames[variant] + ")"
                  + ", terrain " + terrain + " (" + TerrainNames[terrain] + "), wiped "
                  + wiped + " stale file(s) in " + dir);

        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Debug.LogError("[LEVELSHOTS] no GameManager"); Done(); yield break; }
            yield return null;
        }
        if (!ParticipantManager.IsSet) ParticipantManager.SetID("LEVEL");
        GameManager.Instance.SetParticipantReady();

        // TERRAIN VARIANT, before the settle. This replaces the already-built terrain's
        // layers and alphamap and nothing else — no height is read or written, so the
        // shape of the ground, the runway pad and every mission's clearance are the same
        // in all four. Variant 0 returns without touching anything.
        WorldBuilder.ReskinTerrainForDesign(terrain);

        // Let the terrain finish generating its splatmap/detail and the water animator
        // settle, so all four variants photograph an identically-settled world.
        yield return new WaitForSecondsRealtime(6f);

        ApplyLighting(variant);
        // A directional-light change needs a frame plus a GI bounce before it is on
        // screen; rendering in the same frame photographs the OLD lighting.
        yield return new WaitForSecondsRealtime(1.5f);

        BuildCam();
        foreach (var v in Views)
            yield return Shot(v, Stem + "_" + v.Name + ".png");

        DumpLightingState();
        Debug.Log("[LEVELSHOTS] wrote shots to " + dir);
        Done();
    }

    static int ReadVariant()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            if (!a.StartsWith("-lighting=")) continue;
            int n;
            if (int.TryParse(a.Substring("-lighting=".Length), out n) && n >= 0 && n <= 3) return n;
            Debug.LogError("[LEVELSHOTS] bad " + a + " — expected -lighting=0..3; using 0");
            return 0;
        }
        return 0;   // no flag = the project as it is
    }

    static int ReadTerrain()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
        {
            if (!a.StartsWith("-terrain=")) continue;
            int n;
            if (int.TryParse(a.Substring("-terrain=".Length), out n) && n >= 0 && n <= 3) return n;
            Debug.LogError("[LEVELSHOTS] bad " + a + " — expected -terrain=0..3; using 0");
            return 0;
        }
        return 0;   // no flag = the ground as it is
    }

    // ── the four looks ────────────────────────────────────────────────────────────
    //
    // A NOTE ON Skybox/Procedural, learned the expensive way. Its model is atmospheric
    // scattering around the directional light, so _AtmosphereThickness does NOT mean
    // "haziness" — raising it makes the sky MORE ORANGE, not more grey. The first pass
    // used 1.25 for "clear morning" and got a flat yellow-green wash, and 1.80 for
    // "overcast" and got a sunset. Warmth here comes from the SUN, the ambient and the
    // fog colour; thickness stays near 1.0. And a flat grey overcast is simply not
    // reachable with that shader at all — variant 3 uses a generated grey gradient on
    // Skybox/Panoramic instead (the same shader WorldBuilder already uses for its HDRI,
    // so it is guaranteed to be in the build).
    void ApplyLighting(int v)
    {
        // VARIANT 0 IS THE BASELINE AND MUST STAY A NO-OP. Do not "helpfully" re-apply
        // the current values here: re-deriving them is how a baseline stops being one.
        if (v == 0) return;

        var sun = RenderSettings.sun;
        if (sun == null) { Debug.LogError("[LEVELSHOTS] no RenderSettings.sun — lighting variant not applied"); return; }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;

        // A fresh material every time, so WorldBuilder's shared clearSky/overcastSky
        // instances are never written to by this tool.
        Material sky;

        switch (v)
        {
            case 1:   // Clear morning — sun low, warm, long shadows, a little haze
                sun.transform.rotation = Quaternion.Euler(25f, -55f, 0f);
                sun.color = new Color(1.00f, 0.90f, 0.76f);   // the warmth lives here
                sun.intensity = 1.20f;
                sun.shadows = LightShadows.Soft;
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_AtmosphereThickness", 0.90f);  // >1.05 goes yellow; see the note above
                sky.SetFloat("_Exposure", 1.30f);
                // NEUTRAL tint, not the project's blue-pushed (0.55, 0.68, 0.92). _SkyTint
                // scales the scattering coefficients, so a blue push turns a LOW sun's
                // horizon glow green rather than warm — the exact fault in the second pass.
                // Grey is Unity's own default and gives a blue zenith with a warm band.
                sky.SetColor("_SkyTint", new Color(0.50f, 0.50f, 0.50f));
                sky.SetColor("_GroundColor", new Color(0.38f, 0.36f, 0.32f));
                RenderSettings.ambientLight = new Color(0.54f, 0.52f, 0.50f);   // warm-neutral
                RenderSettings.fogColor = new Color(0.80f, 0.78f, 0.74f);
                RenderSettings.fogDensity = 0.00015f;         // ~1.6x the current 0.00009
                break;

            case 2:   // Midday haze — sun high and neutral, strong distance haze
                sun.transform.rotation = Quaternion.Euler(65f, -20f, 0f);
                sun.color = new Color(1.00f, 0.99f, 0.97f);
                sun.intensity = 1.30f;
                sun.shadows = LightShadows.Soft;
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetFloat("_Exposure", 1.35f);
                sky.SetColor("_SkyTint", new Color(0.60f, 0.70f, 0.90f));
                sky.SetColor("_GroundColor", new Color(0.48f, 0.49f, 0.48f));
                // Pulled back from the first pass: ambient 0.66 with a near-white fog
                // colour blew the coast and the terrain out to paper, so "haze" read as
                // "overexposed". Haze should veil the distance, not erase it.
                RenderSettings.ambientLight = new Color(0.60f, 0.62f, 0.66f);
                RenderSettings.fogColor = new Color(0.72f, 0.77f, 0.84f);
                RenderSettings.fogDensity = 0.00028f;         // ~3x
                break;

            default:  // 3 — Overcast soft: no hard sun, flat grey sky, low contrast
                // Shadows OFF is the whole point: under cloud there is no shadow-casting
                // disc, and merely dimming the directional light leaves crisp shadows
                // that give the trick away.
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                sun.color = new Color(0.90f, 0.91f, 0.93f);
                sun.intensity = 0.35f;
                sun.shadows = LightShadows.None;
                sky = GreySky();
                RenderSettings.ambientLight = new Color(0.62f, 0.63f, 0.66f);   // ambient replaces the sun
                RenderSettings.fogColor = new Color(0.66f, 0.68f, 0.70f);
                RenderSettings.fogDensity = 0.00032f;         // ~3.5x
                break;
        }

        RenderSettings.skybox = sky;
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>A flat grey overcast dome: a generated latitude-longitude gradient,
    /// slightly brighter at the horizon than overhead, which is how a real cloud deck
    /// reads. One pixel wide — the sky has no azimuthal variation, which is exactly the
    /// point of overcast.</summary>
    static Material GreySky()
    {
        const int N = 128;
        var tex = new Texture2D(1, N, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        var zenith  = new Color(0.60f, 0.61f, 0.64f);
        var horizon = new Color(0.80f, 0.81f, 0.83f);
        var nadir   = new Color(0.52f, 0.53f, 0.55f);
        for (int y = 0; y < N; y++)
        {
            float v = y / (N - 1f);                    // 0 = nadir, 0.5 = horizon, 1 = zenith
            Color c = v >= 0.5f ? Color.Lerp(horizon, zenith, (v - 0.5f) * 2f)
                                : Color.Lerp(nadir, horizon, v * 2f);
            tex.SetPixel(0, y, c);
        }
        tex.Apply();

        var m = new Material(Shader.Find("Skybox/Panoramic"));
        m.SetTexture("_MainTex", tex);
        m.SetFloat("_Mapping", 1f);     // latitude-longitude
        m.SetFloat("_ImageType", 0f);   // full 360
        m.SetFloat("_Exposure", 1f);
        return m;
    }

    void BuildCam()
    {
        var g = new GameObject("LevelShotCam");
        cam = g.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 20000f;   // the terrain is 12 km square; a short far plane clips the range off
        cam.clearFlags = CameraClearFlags.Skybox;
        // Exclude:
        //   DisplayOnlyMask (12/13/14) — required. The displays' symbology is built as
        //     world objects parented to their own off-screen cameras, and the MFD's sits
        //     500 m ABOVE the aeroplane; any world camera that renders those layers shows
        //     the moving map's compass letters hanging in the sky.
        //   CockpitLayer (11) — the panel would fill an exterior frame.
        //   ExteriorLayer (10) — the aeroplane parks at (0, 1.2, -280), which is 8 m in
        //     front of the threshold camera and would fill that shot. These are pictures
        //     of the WORLD's lighting.
        cam.cullingMask = ~((1 << CockpitBuilder.CockpitLayer)
                          | (1 << AircraftBuilder.ExteriorLayer)
                          | CockpitBuilder.DisplayOnlyMask);
    }

    IEnumerator Shot(View v, string file)
    {
        cam.transform.position = v.Pos;
        cam.transform.rotation = Quaternion.LookRotation((v.LookAt - v.Pos).normalized, Vector3.up);
        cam.fieldOfView = v.Fov;

        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Destroy(tex); Destroy(rt);
        Debug.Log("[LEVELSHOTS]   wrote " + file);
        yield return null;
    }

    /// <summary>Writes the lighting state the pictures were actually taken under, so a
    /// variant can be read as numbers rather than inferred from a JPEG. For variant 0
    /// these values must match the constants in WorldBuilder.SetupDayLighting/SetSkyClear
    /// exactly — that is the check that the baseline is still the baseline.</summary>
    void DumpLightingState()
    {
        var sb = new System.Text.StringBuilder();
        var sun = RenderSettings.sun;
        sb.AppendLine("LEVEL LIGHTING + TERRAIN STATE");
        sb.AppendLine("variant       : " + variant + "  (" + VariantNames[variant] + ")");
        if (variant == 0)
            sb.AppendLine("                variant 0 applies NOTHING; these are WorldBuilder's own values.");
        sb.AppendLine("terrain       : " + terrain + "  (" + TerrainNames[terrain] + ")");
        sb.AppendLine("  tiles/blend : " + WorldBuilder.DescribeLook(terrain));
        if (terrain == 0)
            sb.AppendLine("                terrain 0 applies NOTHING; the ground is the project's own.");
        if (terrain == 2)
        {
            sb.AppendLine("                NOTE: in this variant ONLY, layer 1 is tinted DARK GRASS");
            sb.AppendLine("                rather than dirt, so grass<->dirt reads as light<->dark green.");
        }
        sb.AppendLine("  heights     : UNTOUCHED — the re-skin replaces layers + alphamap only.");
        sb.AppendLine("sun rotation  : " + (sun != null ? sun.transform.rotation.eulerAngles.ToString("0.00") : "(none)"));
        sb.AppendLine("sun intensity : " + (sun != null ? sun.intensity.ToString("0.0000") : "-"));
        sb.AppendLine("sun color     : " + (sun != null ? sun.color.ToString("0.0000") : "-"));
        sb.AppendLine("sun shadows   : " + (sun != null ? sun.shadows.ToString() : "-"));
        sb.AppendLine("ambient mode  : " + RenderSettings.ambientMode);
        sb.AppendLine("ambient light : " + RenderSettings.ambientLight.ToString("0.0000"));
        sb.AppendLine("fog           : " + RenderSettings.fog + "  mode=" + RenderSettings.fogMode);
        sb.AppendLine("fog color     : " + RenderSettings.fogColor.ToString("0.0000"));
        sb.AppendLine("fog density   : " + RenderSettings.fogDensity.ToString("0.000000"));
        var sky = RenderSettings.skybox;
        sb.AppendLine("skybox        : " + (sky != null ? sky.shader.name : "(none)"));
        if (sky != null && sky.HasProperty("_AtmosphereThickness"))
        {
            sb.AppendLine("  atmosphere  : " + sky.GetFloat("_AtmosphereThickness").ToString("0.000"));
            sb.AppendLine("  exposure    : " + sky.GetFloat("_Exposure").ToString("0.000"));
            sb.AppendLine("  sky tint    : " + sky.GetColor("_SkyTint").ToString("0.0000"));
            sb.AppendLine("  ground col  : " + sky.GetColor("_GroundColor").ToString("0.0000"));
        }
        sb.AppendLine();
        sb.AppendLine("VIEWPOINTS (world space; runway is z -300..+300 on x=0, landing direction +Z)");
        foreach (var v in Views)
            sb.AppendLine(string.Format("  {0,-12} pos={1} lookAt={2} fov={3}",
                                        v.Name, v.Pos.ToString("0.0"), v.LookAt.ToString("0.0"), v.Fov));

        File.WriteAllText(Path.Combine(dir, "state_" + Stem + ".txt"), sb.ToString());
        Debug.Log("[LEVELSHOTS]   wrote state_" + Stem + ".txt");
    }

    void Done() { Finished = true; }
}
