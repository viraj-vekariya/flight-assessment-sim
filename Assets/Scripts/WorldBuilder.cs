using UnityEngine;
using UnityEngine.Rendering;

public struct RunwayInfo
{
    public Vector3 Start;       // where the aircraft spawns
    public Quaternion Rot;      // facing down the runway
}

/// <summary>
/// Builds a real, elevated, code-generated world: a Unity Terrain (heightmap from
/// layered noise — rolling hills, a coastal mountain range + distant peaks, an
/// irregular coastline where the land dips under the sea, and a flat runway pad),
/// blended grass/dirt/rock/sand/snow texturing by slope + altitude, a large
/// animated water surface, distance haze, and the city / suburbs / farm / forests
/// re-placed ON the terrain. SurfaceTag is preserved (terrain = Ground, water =
/// Water, buildings = Obstacle) so the crash/landing model is unchanged.
/// </summary>
public static class WorldBuilder
{
    // ---- terrain frame ----
    const float TSize = 12000f;     // 12 km square
    const float THeight = 1700f;    // vertical span
    const float TBaseY = -200f;     // terrain origin Y (heightmap 0 -> world -200)
    const int HRES = 513;           // heightmap resolution
    const int ARES = 512;           // alphamap (splat) resolution
    const float SeaLevel = -2f;     // water plane sits just below the flat pad (no z-fight)

    // settlement footprints: flattened so buildings sit level (centre xz + radius)
    struct Zone { public float X, Z, R; public Zone(float x, float z, float r) { X = x; Z = z; R = r; } }
    static readonly Zone[] Zones =
    {
        new Zone(250f, 1950f, 340f),   // downtown
        new Zone(-950f, 1500f, 250f),  // suburb A
        new Zone(1250f, 1050f, 250f),  // suburb B
        new Zone(-1250f, -300f, 220f), // farm
    };
    static float[] zoneY;

    static Terrain terrain;
    static Mesh roof4, treeCone;

    // ---- palettes (buildings/trees) ----
    static readonly Color[] CityWalls =
    {
        new Color(0.62f, 0.62f, 0.64f), new Color(0.55f, 0.66f, 0.72f),
        new Color(0.70f, 0.66f, 0.58f), new Color(0.48f, 0.50f, 0.55f),
        new Color(0.66f, 0.60f, 0.66f),
    };
    static readonly Color RoofGrey = new Color(0.30f, 0.30f, 0.33f);
    static readonly Color CityGround = new Color(0.24f, 0.24f, 0.26f);
    static readonly Color RoadGrey = new Color(0.34f, 0.34f, 0.37f);
    static readonly Color[] HouseWalls =
    {
        new Color(0.88f, 0.84f, 0.74f), new Color(0.74f, 0.78f, 0.70f),
        new Color(0.80f, 0.72f, 0.66f), new Color(0.72f, 0.76f, 0.82f),
    };
    static readonly Color[] RoofColors =
    {
        new Color(0.55f, 0.22f, 0.18f), new Color(0.35f, 0.26f, 0.20f), new Color(0.30f, 0.32f, 0.36f),
    };
    static readonly Color BarnRed = new Color(0.55f, 0.16f, 0.14f);
    static readonly Color SiloColor = new Color(0.80f, 0.80f, 0.82f);
    static readonly Color Trunk = new Color(0.34f, 0.24f, 0.12f);
    static readonly Color Leaf = new Color(0.17f, 0.40f, 0.18f);

    public static RunwayInfo BuildEnvironment(Transform parent)
    {
        roof4 = MeshUtil.Cone(4, 1f, 1f);
        treeCone = MeshUtil.Cone(8, 1f, 1f);

        SetupDayLighting(parent);
        BuildTerrain(parent);
        BuildWater(parent);
        BuildClouds(parent);
        BuildRunway(parent);
        AerodromeBuilder.Build(parent);   // apron, taxiways, hold-short, signage
        BuildSettlements(parent);
        BuildForests(parent);

        return new RunwayInfo
        {
            Start = new Vector3(0f, 1.2f, -280f),   // pad is flat at y=0; rests on the gear
            Rot = Quaternion.identity                // facing +Z down the runway
        };
    }

    // ── SKY / WEATHER STATE ────────────────────────────────────────────────────
    // Two skies, switchable at runtime, because weather has to be a VISIBLE VARIABLE.
    //
    // The bundled CC0 HDRI ("mud_road_puresky") is a heavy overcast. Using it for
    // every mission meant the LOW missions — which are specified as "clear day,
    // unlimited visibility" — looked exactly as gloomy as the HIGH weather missions.
    // That is not a cosmetic complaint: weather is one of the manipulated factors, and
    // if the clear condition and the bad-weather condition look the same, the
    // manipulation does not exist for the participant no matter what the fog density
    // says. `ScenarioEngine` now selects the sky per mission.
    static Material clearSky, overcastSky;
    static Light sunLight;

    /// <summary>Clear blue sky, bright sun, long visibility.</summary>
    public static void SetSkyClear()
    {
        if (clearSky == null)
        {
            clearSky = new Material(Shader.Find("Skybox/Procedural"));
            clearSky.SetFloat("_AtmosphereThickness", 0.85f);
            clearSky.SetFloat("_Exposure", 1.35f);
            clearSky.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.92f));
            clearSky.SetColor("_GroundColor", new Color(0.42f, 0.44f, 0.42f));
        }
        RenderSettings.skybox = clearSky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.63f, 0.70f);
        RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.92f);
        RenderSettings.fogDensity = 0.00009f;
        if (sunLight != null) { sunLight.intensity = 1.25f; sunLight.color = new Color(1f, 0.97f, 0.9f); }
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>Overcast HDRI, dimmer sun, shorter visibility. `severity` 0..1 also
    /// pulls the ambient down and the fog up, so heavy weather reads as heavier.</summary>
    public static void SetSkyOvercast(float severity)
    {
        severity = Mathf.Clamp01(severity);
        if (overcastSky == null)
        {
            var tex = TerrainTextures.StreamingImage("Sky/sky.jpg");
            if (tex != null)
            {
                overcastSky = new Material(Shader.Find("Skybox/Panoramic"));
                overcastSky.SetTexture("_MainTex", tex);
                overcastSky.SetFloat("_Mapping", 1f);     // latitude-longitude
                overcastSky.SetFloat("_ImageType", 0f);   // full 360
            }
        }
        if (overcastSky == null) { SetSkyClear(); return; }   // asset missing: stay flyable
        overcastSky.SetFloat("_Exposure", Mathf.Lerp(1.5f, 0.85f, severity));
        RenderSettings.skybox = overcastSky;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(new Color(0.50f, 0.53f, 0.58f),
                                                 new Color(0.30f, 0.32f, 0.36f), severity);
        RenderSettings.fogColor = Color.Lerp(new Color(0.68f, 0.71f, 0.76f),
                                             new Color(0.46f, 0.48f, 0.52f), severity);
        RenderSettings.fogDensity = Mathf.Lerp(0.00012f, 0.00030f, severity);
        if (sunLight != null)
        {
            sunLight.intensity = Mathf.Lerp(0.85f, 0.45f, severity);
            sunLight.color = Color.Lerp(new Color(0.95f, 0.95f, 0.95f), new Color(0.80f, 0.83f, 0.88f), severity);
        }
        DynamicGI.UpdateEnvironment();
    }

    static void SetupDayLighting(Transform parent)
    {

        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(parent);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = sun;
        sunLight = sun;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        SetSkyClear();     // default: the clear day the LOW missions are specified as
    }

    // ================= TERRAIN =================
    static void BuildTerrain(Transform parent)
    {
        var td = new TerrainData { heightmapResolution = HRES };
        td.size = new Vector3(TSize, THeight, TSize);

        int res = td.heightmapResolution;
        zoneY = new float[Zones.Length];
        for (int i = 0; i < Zones.Length; i++) zoneY[i] = BaseHeight(Zones[i].X, Zones[i].Z);

        var h = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
            {
                float wx = -TSize * 0.5f + (xi / (res - 1f)) * TSize;
                float wz = -TSize * 0.5f + (zi / (res - 1f)) * TSize;
                float y = Height(wx, wz);
                h[zi, xi] = Mathf.Clamp01((y - TBaseY) / THeight);
            }
        td.SetHeights(0, 0, h);

        td.terrainLayers = BuildLayers();
        td.alphamapResolution = ARES;
        td.SetAlphamaps(0, 0, BuildAlphamap(td));

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(-TSize * 0.5f, TBaseY, -TSize * 0.5f);
        terrain = go.GetComponent<Terrain>();
        terrain.heightmapPixelError = 6f;   // built-in terrain LOD
        terrain.basemapDistance = 4000f;
        SurfaceTag.Add(go, SurfaceKind.Ground);   // landable terrain (off-runway = crash, per rules)
    }

    // world-Y height of the terrain surface (without the settlement flatten)
    static float BaseHeight(float x, float z)
    {
        float dist = Mathf.Sqrt(x * x + z * z);

        float hills = Octave(x * 0.00018f + 11f, z * 0.00018f + 11f, 6);   // 0..1
        float baseLand = 12f + hills * 340f;

        // coastal mountain range growing toward +X, plus two distant framing peaks
        float rangeMask = Smooth(3200f, 4800f, x);
        float ridge = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * 0.0006f + 3f, z * 0.0006f + 3f) - 1f);
        float mtn = rangeMask * ridge * ridge * 1250f;
        mtn += Peak(x, z, 3900f, 2600f, 750f, 1050f);
        mtn += Peak(x, z, -3700f, -1300f, 680f, 920f);

        float landY = baseLand + mtn;

        // irregular coastline: land dips under the sea beyond a noisy radius
        float coastNoise = Mathf.PerlinNoise(x * 0.00035f + 50f, z * 0.00035f + 50f) - 0.5f;
        float coastR = 4300f + coastNoise * 1700f;
        float land = 1f - Smooth(coastR - 500f, coastR + 500f, dist);
        // Floor the coastline lerp at 0 (not -75): land never dips below the runway
        // datum while still rendering as terrain. The shore bottoms out flat at y=0
        // and meets the ocean plane (y=-2) at the terrain edge — no visible band of
        // "sunken but still green" land below the runway.
        float y = Mathf.Lerp(0f, landY, land);

        return PadFlatten(x, z, y);
    }

    static float Height(float x, float z)
    {
        float y = BaseHeight(x, z);
        for (int i = 0; i < Zones.Length; i++)
        {
            float d = Mathf.Sqrt((x - Zones[i].X) * (x - Zones[i].X) + (z - Zones[i].Z) * (z - Zones[i].Z));
            float f = 1f - Smooth(Zones[i].R * 0.6f, Zones[i].R, d);
            y = Mathf.Lerp(y, zoneY[i], f);
        }
        return y;
    }

    // Flat runway corridor at y = 0, blended into the hills. The corridor runs LONG in the
    // climb-out direction (+Z, the way the runway faces) so an aircraft can take off and climb
    // out over clear ground before the terrain rises — the hills/mountains remain off to the
    // sides and in the distance for scenery.
    static float PadFlatten(float x, float z, float y)
    {
        // MERGED aerodrome plain — the widest of the two source projects on every axis,
        // because each had fixed a different failure and both fixes are needed.
        //
        //   LATERAL (±260 m + 700 m blend). Wide enough to carry a parallel taxiway and
        //   an apron/parking stand beside the runway, which the take-off missions need.
        //
        //   DEPARTURE (+Z, flat to 1.6 km). Terrain here reaches ~130 m within a few
        //   hundred metres of the runway end and this aeroplane climbs at 3-4 m/s, so a
        //   straight-out departure used to fly into rising ground about 25 s after brake
        //   release ("Destroyed (terrain impact)" at ~110 m).
        //
        //   APPROACH (-Z, flat to 10 km). The landing missions fly a 12 km straight-in
        //   from 500 m on a ~3.3 degree path, which puts the aeroplane at roughly 180 m
        //   over z = -3000. Rolling terrain there reaches ~350 m, so the approach used to
        //   fly INTO A HILL — the automated battery caught it as a "touchdown" at 128 m
        //   altitude, 3 km short of the runway, on a hilltop.
        //
        // Beyond ~4.3 km the terrain is already sea, so the visible change is a normal
        // flat aerodrome and approach plain rather than a scoured landscape.
        //   *** WIDENED FROM A CORRIDOR TO A PLAIN ***
        //   The shape above was a narrow corridor: flat 260 m either side and 1.6 km
        //   straight out. That is exactly enough for a departure that goes STRAIGHT
        //   ahead, and the mission set at the time only had those. Once missions began
        //   assigning departure TURNS, the aeroplane left the corridor laterally while
        //   still climbing through 150 m and flew into ground that reaches ~190 m by
        //   2.2 km out. The automated terrain-clearance check found it on M1V2
        //   ("Destroyed (terrain impact)", 131 m, x = -677) and then showed that even
        //   the straight-out departures were passing within about 40 m of the rising
        //   ground — tight enough that ordinary tracking error would have hit it.
        //
        //   So the aerodrome now sits on a CIRCULAR PLAIN of radius 3.2 km, blended out
        //   over 900 m, which is both what a real aerodrome sits on and what makes a
        //   departure turn safe: at 3.2 km a climbing C172 is near 350 m, well above the
        //   terrain as it starts to rise. The long approach corridor on -Z is kept
        //   unchanged, because the 12 km straight-in still needs it.
        //
        //   Flattening only ever LOWERS terrain, so this cannot make any previously
        //   verified mission less safe.
        float rad = Mathf.Sqrt(x * x + z * z);
        float dPlain = Mathf.Max(0f, rad - 3200f);

        float ax = Mathf.Max(0f, Mathf.Abs(x) - 260f);
        float az = z < 0f ? Mathf.Max(0f, -z - 10000f) : 0f;
        float dCorridor = Mathf.Sqrt(ax * ax + az * az);

        float d = Mathf.Min(dPlain, dCorridor);
        float f = 1f - Smooth(0f, 900f, d);
        return Mathf.Lerp(y, 0f, f);
    }

    static float Octave(float x, float z, int oct)
    {
        float sum = 0f, amp = 1f, tot = 0f, fx = x, fz = z;
        for (int i = 0; i < oct; i++)
        {
            sum += amp * Mathf.PerlinNoise(fx, fz);
            tot += amp; amp *= 0.5f; fx *= 2f; fz *= 2f;
        }
        return sum / tot;
    }

    static float Peak(float x, float z, float px, float pz, float radius, float height)
    {
        float dx = x - px, dz = z - pz;
        return height * Mathf.Exp(-(dx * dx + dz * dz) / (radius * radius));
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static TerrainLayer[] BuildLayers()
    {
        return new[]
        {
            Layer(TerrainTextures.Grass(), 6f),
            Layer(TerrainTextures.Dirt(), 5f),
            Layer(TerrainTextures.Rock(), 9f),
            Layer(TerrainTextures.Sand(), 5f),
            Layer(TerrainTextures.Snow(), 10f),
        };
    }

    static TerrainLayer Layer(Texture2D t, float tile)
    {
        return new TerrainLayer { diffuseTexture = t, tileSize = new Vector2(tile, tile) };
    }

    // blend the 5 layers by altitude + slope: grass(0) dirt(1) rock(2) sand(3) snow(4)
    static float[,,] BuildAlphamap(TerrainData td)
    {
        int aw = td.alphamapWidth, ah = td.alphamapHeight;
        var map = new float[ah, aw, 5];
        for (int zi = 0; zi < ah; zi++)
            for (int xi = 0; xi < aw; xi++)
            {
                float u = xi / (aw - 1f), v = zi / (ah - 1f);
                float worldY = td.GetInterpolatedHeight(u, v) + TBaseY;
                float slope = td.GetSteepness(u, v);

                // World position, needed to tell "beach" from "flat ground".
                float wx = -TSize * 0.5f + u * TSize;
                float wz = -TSize * 0.5f + v * TSize;
                float distFromCentre = Mathf.Sqrt(wx * wx + wz * wz);

                float snow = Smooth(880f, 1150f, worldY);
                float steep = Smooth(28f, 48f, slope);

                // SAND means BEACH, not merely "low". The old rule was purely
                // altitude-based (anything under 14 m), which was fine when the only
                // flat low ground was the shoreline — but the aerodrome plain and the
                // 10 km approach corridor are deliberately flattened to y = 0, so the
                // whole airfield and every approach rendered as desert. Sand is now
                // gated on being near the actual coastline as well as low, so the
                // aerodrome and its approach are grass, as a real airfield would be.
                float nearCoast = Smooth(3000f, 4200f, distFromCentre);
                float sand = Mathf.Clamp01(1f - Smooth(5f, 14f, worldY))
                             * nearCoast * (1f - steep) * (1f - snow);
                float rock = Mathf.Max(steep, Smooth(620f, 920f, worldY)) * (1f - snow) * (1f - sand);
                float dirt = steep * 0.4f * (1f - snow) * (1f - sand);
                float grass = Mathf.Max(0.02f, (1f - snow) * (1f - sand) * (1f - steep));

                float sum = grass + dirt + rock + sand + snow + 1e-4f;
                map[zi, xi, 0] = grass / sum;
                map[zi, xi, 1] = dirt / sum;
                map[zi, xi, 2] = rock / sum;
                map[zi, xi, 3] = sand / sum;
                map[zi, xi, 4] = snow / sum;
            }
        return map;
    }

    // Ground height = the SAME function that built the heightmap, so settlements
    // sit exactly on the terrain surface (no SampleHeight offset ambiguity).
    static float GroundY(float x, float z) => Height(x, z);

    /// <summary>Terrain height at a world position, exposed so the mission battery can
    /// check that a mission's nominal track actually clears the ground. It is the SAME
    /// function that built the heightmap, so the check tests the terrain that exists
    /// rather than an approximation of it.</summary>
    public static float SampleGroundY(float x, float z) => Height(x, z);

    // ================= WATER =================
    static void BuildWater(Transform parent)
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Ocean";
        water.transform.SetParent(parent);
        water.transform.position = new Vector3(0f, SeaLevel, 0f);
        water.transform.localScale = new Vector3(6000f, 1f, 6000f);   // ~60 km

        var m = new Material(Shader.Find("Standard"));
        m.mainTexture = WaterTex();
        m.mainTextureScale = new Vector2(400f, 400f);
        m.color = new Color(0.20f, 0.42f, 0.60f);
        m.SetFloat("_Glossiness", 0.88f);
        m.SetFloat("_Metallic", 0.12f);
        water.GetComponent<Renderer>().sharedMaterial = m;

        SurfaceTag.Add(water, SurfaceKind.Water);
        water.AddComponent<WaterAnimator>();
    }

    static Texture2D WaterTex()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color[N * N];
        Color deep = new Color(0.16f, 0.36f, 0.55f), crest = new Color(0.30f, 0.52f, 0.68f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float w = Mathf.PerlinNoise(Mathf.Cos(u * 6.2831853f) * 2f + 3f, Mathf.Sin(v * 6.2831853f) * 2f + 3f);
                px[y * N + x] = Color.Lerp(deep, crest, w * 0.6f);
            }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // ================= CLOUDS =================
    static void BuildClouds(Transform parent)
    {
        var tex = CloudTex();
        var mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;
        mat.color = new Color(1f, 1f, 1f, 0.85f);
        // transparent (fade) mode on the Standard shader
        mat.SetFloat("_Mode", 2f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;

        var root = new GameObject("Clouds");
        root.transform.SetParent(parent);
        Random.InitState(404);
        for (int i = 0; i < 9; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Cloud";
            q.transform.SetParent(root.transform);
            q.transform.position = new Vector3(Random.Range(-3500f, 3500f), Random.Range(1700f, 2600f), Random.Range(-3500f, 3500f));
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, facing down
            float s = Random.Range(500f, 1100f);
            q.transform.localScale = new Vector3(s, s, s);
            SimUtil.Destroy(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    static Texture2D CloudTex()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        Vector2 c = new Vector2(N * 0.5f, N * 0.5f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (N * 0.5f);
                float soft = Mathf.Clamp01(1f - d);
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                float a = Mathf.Clamp01(soft * soft * (0.6f + 0.4f * n));
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply(true);
        return tex;
    }

    // ================= RUNWAY =================
    static void BuildRunway(Transform parent)
    {
        var rw = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rw.name = "Runway";
        rw.transform.SetParent(parent);
        rw.transform.localScale = new Vector3(30f, 0.4f, 600f);
        rw.transform.position = new Vector3(0f, 0.2f, 0f);
        Paint(rw, new Color(0.16f, 0.16f, 0.18f));
        SurfaceTag.Add(rw, SurfaceKind.Runway);

        for (int i = -9; i <= 9; i++)
        {
            var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.transform.SetParent(parent);
            dash.transform.localScale = new Vector3(1f, 0.02f, 12f);
            dash.transform.position = new Vector3(0f, 0.41f, i * 30f);
            SimUtil.Destroy(dash.GetComponent<Collider>());
            Paint(dash, Color.white);
        }
        foreach (int end in new[] { -290, 290 })
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.SetParent(parent);
            bar.transform.localScale = new Vector3(26f, 0.02f, 3f);
            bar.transform.position = new Vector3(0f, 0.41f, end);
            SimUtil.Destroy(bar.GetComponent<Collider>());
            Paint(bar, Color.white);
        }
    }


    // ================= SETTLEMENTS (on terrain) =================
    static void BuildSettlements(Transform parent)
    {
        BuildCity(parent, new Vector3(250f, 0f, 1950f));
        BuildSuburb(parent, new Vector3(-950f, 0f, 1500f));
        BuildSuburb(parent, new Vector3(1250f, 0f, 1050f));
        BuildFarm(parent, new Vector3(-1250f, 0f, -300f));
        // (v26: removed the 3 cosmetic "road" strips — they were thin collider-less
        // slabs that floated on the terrain near the runway and looked like stray
        // scripts. The city already has its own internal road grid.)
    }

    static void BuildCity(Transform parent, Vector3 center)
    {
        Random.InitState(9001);
        const int nx = 10, nz = 10;
        const float block = 64f, road = 16f;
        float foot = block - road;
        Vector3 origin = center - new Vector3((nx - 1) * block * 0.5f, 0f, (nz - 1) * block * 0.5f);
        float gy = GroundY(center.x, center.z);

        DecoAt("CityGround", parent, center.x, center.z, gy + 0.04f, new Vector3(nx * block, 0.06f, nz * block), CityGround);
        for (int i = 0; i <= nx; i++)
            DecoAt("Road", parent, origin.x + i * block - block * 0.5f, center.z, gy + 0.08f, new Vector3(road * 0.55f, 0.04f, nz * block), RoadGrey);
        for (int j = 0; j <= nz; j++)
            DecoAt("Road", parent, center.x, origin.z + j * block - block * 0.5f, gy + 0.08f, new Vector3(nx * block, 0.04f, road * 0.55f), RoadGrey);

        Vector2 mid = new Vector2((nx - 1) * 0.5f, (nz - 1) * 0.5f);
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vector3 b = origin + new Vector3(i * block, 0f, j * block);
                if (Random.value < 0.10f) { ParkBlock(parent, b.x, b.z, foot); continue; }

                float distN = Vector2.Distance(new Vector2(i, j), mid) / (nx * 0.5f);
                float tall = Mathf.Lerp(130f, 14f, Mathf.Clamp01(distN));
                int per = Random.Range(1, 5);
                for (int k = 0; k < per; k++)
                {
                    float w = Random.Range(11f, foot * 0.42f), d = Random.Range(11f, foot * 0.42f);
                    float ht = Mathf.Max(9f, Random.Range(tall * 0.55f, tall) * Random.Range(0.75f, 1.1f));
                    float ox = Random.Range(-foot * 0.22f, foot * 0.22f), oz = Random.Range(-foot * 0.22f, foot * 0.22f);
                    float bx = b.x + ox, bz = b.z + oz;
                    if (ModelLibrary.TryBuilding(parent, bx, bz, GroundY(bx, bz), ht, false)) continue;   // CC0 model
                    Solid("Tower", parent, bx, bz, new Vector3(w, ht, d), CityWalls[Random.Range(0, CityWalls.Length)]);  // fallback
                    if (ht > 38f) DecoAt("RoofCap", parent, bx, bz, GroundY(bx, bz) + ht + 0.9f, new Vector3(w * 0.5f, 1.8f, d * 0.5f), RoofGrey);
                }
            }
    }

    static void ParkBlock(Transform parent, float x, float z, float foot)
    {
        DecoAt("Park", parent, x, z, GroundY(x, z) + 0.06f, new Vector3(foot, 0.05f, foot), new Color(0.28f, 0.48f, 0.24f));
        for (int t = 0; t < 4; t++)
            Tree(parent, x + Random.Range(-foot * 0.3f, foot * 0.3f), z + Random.Range(-foot * 0.3f, foot * 0.3f));
    }

    static void BuildSuburb(Transform parent, Vector3 center)
    {
        Random.InitState((int)(center.x * 7f) ^ 991);
        const int cols = 7, rows = 5; const float gap = 26f;
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                float x = center.x + (i - (cols - 1) * 0.5f) * gap + Random.Range(-3f, 3f);
                float z = center.z + (j - (rows - 1) * 0.5f) * gap + Random.Range(-3f, 3f);
                House(parent, x, z, HouseWalls[Random.Range(0, HouseWalls.Length)], RoofColors[Random.Range(0, RoofColors.Length)]);
            }
    }

    static void House(Transform parent, float x, float z, Color wall, Color roof)
    {
        float gy = GroundY(x, z);
        if (ModelLibrary.TryBuilding(parent, x, z, gy, Random.Range(7f, 10f), true)) return;   // CC0 house model
        float w = Random.Range(8f, 13f), d = Random.Range(8f, 13f), ht = Random.Range(4f, 6.5f);
        Solid("House", parent, x, z, new Vector3(w, ht, d), wall);
        MeshUtil.MeshObject("Roof", roof4, parent, new Vector3(x, gy + ht, z), new Vector3(w * 0.82f, ht * 0.6f, d * 0.82f), roof);
    }

    static void BuildFarm(Transform parent, Vector3 center)
    {
        Random.InitState(2027);
        float bw = 26f, bd = 16f, bh = 9f;
        float gy = GroundY(center.x, center.z);
        Solid("Barn", parent, center.x, center.z, new Vector3(bw, bh, bd), BarnRed);
        MeshUtil.MeshObject("BarnRoof", roof4, parent, new Vector3(center.x, gy + bh, center.z), new Vector3(bw * 0.8f, bh * 0.7f, bd * 0.86f), RoofColors[1]);
        Silo(parent, center.x + bw * 0.6f, center.z, 3.5f, 12f);
        Silo(parent, center.x + bw * 0.6f + 9f, center.z, 3.5f, 12f);
        House(parent, center.x - bw, center.z + 14f, HouseWalls[0], RoofColors[0]);
    }

    static void Silo(Transform parent, float x, float z, float r, float ht)
    {
        float gy = GroundY(x, z);
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = "Silo";
        g.transform.SetParent(parent);
        g.transform.position = new Vector3(x, gy + ht * 0.5f, z);
        g.transform.localScale = new Vector3(r * 2f, ht * 0.5f, r * 2f);
        Paint(g, SiloColor);
        SurfaceTag.Add(g, SurfaceKind.Obstacle);

        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.transform.SetParent(parent);
        dome.transform.position = new Vector3(x, gy + ht, z);
        dome.transform.localScale = new Vector3(r * 2f, r * 1.1f, r * 2f);
        SimUtil.Destroy(dome.GetComponent<Collider>());
        Paint(dome, SiloColor);
    }

    static void BuildForests(Transform parent)
    {
        Random.InitState(555);
        for (int clump = 0; clump < 6; clump++)
        {
            float cx = Random.Range(-3800f, 3800f), cz = Random.Range(-3800f, 3800f);
            if (Mathf.Abs(cx) < 220f && Mathf.Abs(cz) < 820f) continue;   // off the runway corridor
            if (GroundY(cx, cz) < 4f) continue;                          // not on beach/water
            int n = Random.Range(12, 20);
            for (int i = 0; i < n; i++)
                Tree(parent, cx + Random.Range(-170f, 170f), cz + Random.Range(-170f, 170f));
            // a few CC0 rock models for close-up detail (no-op if models absent)
            for (int r = 0; r < 3; r++)
            {
                float rx = cx + Random.Range(-150f, 150f), rz = cz + Random.Range(-150f, 150f);
                float rgy = GroundY(rx, rz);
                if (rgy > 3f) ModelLibrary.TryRock(parent, rx, rz, rgy, Random.Range(4f, 9f));
            }
        }
    }

    static void Tree(Transform parent, float x, float z)
    {
        float gy = GroundY(x, z);
        if (gy < 2f) return;   // skip shoreline/water
        if (ModelLibrary.TryTree(parent, x, z, gy, Random.Range(8f, 15f))) return;   // CC0 tree model
        float s = Random.Range(3f, 7f);
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(parent);
        trunk.transform.localScale = new Vector3(0.4f, s * 0.4f, 0.4f);
        trunk.transform.position = new Vector3(x, gy + s * 0.4f, z);
        SimUtil.Destroy(trunk.GetComponent<Collider>());
        Paint(trunk, Trunk);
        MeshUtil.MeshObject("Canopy", treeCone, parent, new Vector3(x, gy + s * 0.6f, z), new Vector3(s * 0.7f, s, s * 0.7f), Leaf);
    }

    // ---- building helpers: position by terrain height ----
    static GameObject Solid(string name, Transform parent, float x, float z, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.position = new Vector3(x, GroundY(x, z) + scale.y * 0.5f, z);
        g.transform.localScale = scale;
        Paint(g, c);
        SurfaceTag.Add(g, SurfaceKind.Obstacle);
        return g;
    }

    static GameObject DecoAt(string name, Transform parent, float x, float z, float y, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.position = new Vector3(x, y, z);
        g.transform.localScale = scale;
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, c);
        return g;
    }

    // shared-material cache (buildings/trees by colour)
    static Shader _std;
    static readonly System.Collections.Generic.Dictionary<Color, Material> _matCache
        = new System.Collections.Generic.Dictionary<Color, Material>();

    static void Paint(GameObject g, Color c)
    {
        if (_std == null) _std = Shader.Find("Standard");
        if (!_matCache.TryGetValue(c, out Material m)) { m = new Material(_std) { color = c }; _matCache[c] = m; }
        g.GetComponent<Renderer>().sharedMaterial = m;
    }
}
