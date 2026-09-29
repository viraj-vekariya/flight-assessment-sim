// CityTextures — procedural, tileable textures for the city, farmland and runway.
//
// Everything the city is dressed in is generated here at start-up, so the world has
// no texture-asset dependency to go missing. Each texture is authored to a KNOWN
// physical size (see the "represents" note on each), and CityBuilder maps UVs in
// metres against that size — so a storey is always 3.6 m tall and a lane is always
// 3.5 m wide, whatever the building or road dimensions are.
//
// All textures are mip-mapped, trilinear and anisotropic: the city is mostly seen at
// grazing angles from an aeroplane, which is exactly where an un-filtered texture
// shimmers.

using UnityEngine;

public static class CityTextures
{
    static System.Random rng = new System.Random(4242);
    static float R() => (float)rng.NextDouble();
    static float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

    static Texture2D New(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8,
        };
    }

    static Texture2D Done(Texture2D t, Color[] px)
    {
        t.SetPixels(px);
        t.Apply(true, true);   // mips, then drop the CPU copy
        return t;
    }

    static float Noise(float x, float y, float s) => Mathf.PerlinNoise(x * s + 17.3f, y * s + 91.7f);

    static Color Jit(Color c, float amt)
    {
        float k = 1f + (R() - 0.5f) * 2f * amt;
        return new Color(c.r * k, c.g * k, c.b * k, 1f);
    }

    public static void Seed(int s) { rng = new System.Random(s); }

    // ── FACADES ──────────────────────────────────────────────────────────────
    // 256 x 256 = 4 bays x 4 storeys. Represents 14.4 m x 14.4 m (3.6 m bay/storey).

    /// <summary>Curtain-wall glass tower: slim mullions, dark spandrel at each floor
    /// slab, panes with a sky-reflection gradient and pane-to-pane variation.</summary>
    public static Texture2D GlassCurtain(Color glass, Color frame, Color spandrel)
    {
        const int N = 256, C = 64;
        var t = New(N, N); var px = new Color[N * N];
        var pane = new float[16, 16];
        for (int i = 0; i < 16; i++) for (int j = 0; j < 16; j++) pane[i, j] = R(-0.06f, 0.06f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int cx = x % C, cy = y % C;
                int sub = cx / 32;                      // two panes per bay
                int scx = cx % 32;
                Color c;
                if (cy < 11) c = spandrel * (0.95f + 0.1f * Noise(x, y, 0.3f));
                else if (cy < 13 || scx < 2) c = frame;
                else
                {
                    float g = (cy - 13) / 51f;          // brighter toward the top of the pane (sky)
                    float v = pane[(x / 32) % 16, (y / C) % 16];
                    c = glass * (0.82f + 0.35f * g + v);
                }
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Office block: concrete spandrel band + continuous ribbon window with
    /// mullions, some panes with blinds drawn.</summary>
    public static Texture2D OfficeBands(Color concrete, Color glass)
    {
        const int N = 256, C = 64;
        var t = New(N, N); var px = new Color[N * N];
        var blind = new float[32, 4];
        for (int i = 0; i < 32; i++) for (int j = 0; j < 4; j++) blind[i, j] = R() < 0.25f ? R(0.2f, 0.7f) : 0f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int cy = y % C, fl = y / C;
                Color c;
                if (cy < 22) c = concrete * (0.93f + 0.12f * Noise(x, y, 0.15f));
                else if (cy < 24 || cy > 61) c = concrete * 0.7f;
                else if (x % 16 < 2) c = new Color(0.45f, 0.46f, 0.48f);
                else
                {
                    float g = (cy - 24) / 38f;
                    c = glass * (0.85f + 0.3f * g);
                    float b = blind[(x / 16) % 32, fl];
                    if (b > 0f && (cy - 24) / 38f > 1f - b) c = new Color(0.62f, 0.61f, 0.57f) * (cy % 3 == 0 ? 0.85f : 1f);
                }
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Punched-window masonry. brick=true lays courses with mortar; otherwise
    /// render/plaster. balconies adds a slab + railing under alternate windows.</summary>
    public static Texture2D Punched(Color wall, bool brick, bool balconies)
    {
        const int N = 256, C = 64;
        var t = New(N, N); var px = new Color[N * N];
        var brickShade = new float[64, 64];
        for (int i = 0; i < 64; i++) for (int j = 0; j < 64; j++) brickShade[i, j] = R(-0.10f, 0.10f);
        var lit = new float[4, 4];
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) lit[i, j] = R() < 0.3f ? 1f : 0f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int cx = x % C, cy = y % C, bay = x / C, fl = y / C;
                Color c;
                if (brick)
                {
                    int row = y / 4;
                    int off = (row % 2) * 5;
                    int col = (x + off) / 10;
                    bool mortar = (y % 4 == 0) || ((x + off) % 10 == 0);
                    c = mortar ? new Color(0.66f, 0.63f, 0.58f) : wall * (1f + brickShade[col % 64, row % 64]);
                }
                else c = wall * (0.94f + 0.10f * Noise(x, y, 0.08f) + 0.03f * Noise(x, y, 0.9f));

                bool win = cx >= 18 && cx < 46 && cy >= 18 && cy < 54;
                if (win)
                {
                    bool frameP = cx < 20 || cx >= 44 || cy < 20 || cy >= 52 || cx == 31 || cx == 32;
                    if (frameP) c = new Color(0.88f, 0.87f, 0.84f);
                    else
                    {
                        float g = (cy - 20) / 32f;
                        c = new Color(0.16f, 0.20f, 0.25f) * (0.8f + 0.5f * g);
                        if (lit[bay, fl] > 0f && cy > 40) c = new Color(0.72f, 0.68f, 0.60f);   // curtains
                    }
                }
                else if (cx >= 16 && cx < 48 && cy >= 15 && cy < 18) c = new Color(0.80f, 0.78f, 0.74f);   // sill
                if (balconies && (bay + fl) % 2 == 0)
                {
                    if (cx >= 12 && cx < 52 && cy >= 12 && cy < 15) c = new Color(0.55f, 0.54f, 0.52f);          // slab
                    else if (cx >= 12 && cx < 52 && cy >= 15 && cy < 27)
                        c = (cx % 4 == 0 || cy == 26) ? new Color(0.20f, 0.21f, 0.23f) : c * 0.8f;              // railing
                }
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Ground-floor shopfronts. 256 x 64 = 4 bays x 1 storey of 4.5 m
    /// (represents 14.4 m x 4.5 m): a fascia sign band, glazing, the odd door.</summary>
    public static Texture2D Shopfront()
    {
        const int W = 256, H = 64, C = 64;
        var t = New(W, H); var px = new Color[W * H];
        Color[] signs =
        {
            new Color(0.62f, 0.16f, 0.14f), new Color(0.14f, 0.30f, 0.52f), new Color(0.18f, 0.42f, 0.24f),
            new Color(0.80f, 0.62f, 0.18f), new Color(0.22f, 0.22f, 0.24f), new Color(0.85f, 0.85f, 0.82f),
        };
        var bayCol = new Color[4]; var door = new bool[4];
        for (int i = 0; i < 4; i++) { bayCol[i] = signs[rng.Next(signs.Length)]; door[i] = R() < 0.5f; }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int cx = x % C, bay = x / C;
                Color c;
                if (y < 3) c = new Color(0.30f, 0.30f, 0.31f);
                else if (y >= 50) c = (y >= 53 && y < 61 && cx > 6 && cx < 58) ? bayCol[bay] : new Color(0.34f, 0.34f, 0.36f);
                else if (cx < 3 || cx > 60 || y < 6 || y > 47) c = new Color(0.18f, 0.18f, 0.19f);
                else if (door[bay] && cx > 40 && cx < 55) c = new Color(0.10f, 0.11f, 0.12f);
                else { float g = (y - 6) / 42f; c = new Color(0.22f, 0.27f, 0.30f) * (0.85f + 0.5f * g); }
                c.a = 1f;
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Profiled-metal cladding. 256 x 128 represents 24 m x 12 m: vertical
    /// ribs, a dark eaves band, a high window strip and one roller door per 24 m.</summary>
    public static Texture2D Warehouse(Color clad)
    {
        const int W = 256, H = 128;
        var t = New(W, H); var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float rib = 0.9f + 0.1f * Mathf.Sin(x * 1.05f);
                Color c = clad * rib * (0.96f + 0.06f * Noise(x, y, 0.05f));
                if (y > 118) c = clad * 0.55f;
                else if (y > 98 && y < 106 && x % 32 > 3) c = new Color(0.55f, 0.62f, 0.66f);
                if (x >= 150 && x < 210 && y < 62)
                    c = (y % 5 == 0 || x < 152 || x > 207 || y > 59) ? new Color(0.36f, 0.37f, 0.38f) : new Color(0.52f, 0.53f, 0.54f);
                if (y < 3) c = new Color(0.32f, 0.32f, 0.33f);
                c.a = 1f;
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>House walls: horizontal siding with one window per bay.
    /// 128 x 64 represents 8 m x 3 m (two bays, one storey).</summary>
    public static Texture2D HouseSiding()
    {
        const int W = 128, H = 64;
        var t = New(W, H); var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float s = (y % 5 == 0) ? 0.82f : 1f;
                Color c = Color.white * s * (0.95f + 0.05f * Noise(x, y, 0.2f));
                int cx = x % 64;
                if (cx >= 20 && cx < 44 && y >= 18 && y < 46)
                {
                    bool fr = cx < 22 || cx > 41 || y < 20 || y > 43 || cx == 31 || cx == 32;
                    c = fr ? new Color(0.95f, 0.95f, 0.93f) * 1.0f : new Color(0.18f, 0.22f, 0.27f) * (0.8f + 0.4f * (y - 20) / 24f);
                    // dark trim reads against light siding (tint multiplies the wall only)
                    if (fr) c = new Color(0.40f, 0.40f, 0.42f);
                }
                if (y < 3) c = new Color(0.45f, 0.44f, 0.42f);
                c.a = 1f;
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    // ── ROOFS ────────────────────────────────────────────────────────────────

    /// <summary>Flat roof, gravel/membrane. 256² represents 16 m.</summary>
    public static Texture2D FlatRoof(Color baseCol, bool seams)
    {
        const int N = 256;
        var t = New(N, N); var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = 0.88f + 0.14f * Noise(x, y, 0.03f) + 0.08f * (R() - 0.5f);
                Color c = baseCol * n;
                if (seams && (x % 64 == 0)) c *= 0.85f;
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Pitched roof tiles/slates in courses. 128² represents 4 m.
    /// v runs UP the slope.</summary>
    public static Texture2D RoofTiles(Color baseCol)
    {
        const int N = 128;
        var t = New(N, N); var px = new Color[N * N];
        var shade = new float[64, 64];
        for (int i = 0; i < 64; i++) for (int j = 0; j < 64; j++) shade[i, j] = R(-0.08f, 0.08f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int row = y / 8, off = (row % 2) * 4;
                int col = (x + off) / 8;
                float inRow = (y % 8) / 8f;
                float k = 0.78f + 0.3f * inRow + shade[col % 64, row % 64];
                if ((x + off) % 8 == 0) k *= 0.8f;
                Color c = baseCol * k;
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    // ── GROUND ───────────────────────────────────────────────────────────────

    /// <summary>Plain asphalt. 256² represents 8 m.</summary>
    public static Texture2D Asphalt(float lum)
    {
        const int N = 256;
        var t = New(N, N); var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = lum * (0.90f + 0.14f * Noise(x, y, 0.02f) + 0.10f * (R() - 0.5f));
                px[y * N + x] = new Color(n, n, n * 1.03f, 1f);
            }
        return Done(t, px);
    }

    /// <summary>A road surface with markings. u runs ACROSS the full road width
    /// (0..1); v runs along it, one texture = 12 m. lanesPerSide 1 = local street
    /// (dashed centre line), 2 = avenue (double centre + lane dashes), 0 = one
    /// carriageway of a divided highway (edge lines + lane dash).</summary>
    public static Texture2D Road(int lanesPerSide)
    {
        const int W = 128, H = 256;
        var t = New(W, H); var px = new Color[W * H];
        Color line = new Color(0.86f, 0.86f, 0.84f), yellow = new Color(0.86f, 0.72f, 0.26f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                float n = 0.20f * (0.90f + 0.14f * Noise(x, y, 0.03f) + 0.10f * (R() - 0.5f));
                // wheel-path wear: slightly darker bands in each lane
                Color c = new Color(n, n, n * 1.03f, 1f);
                bool edge = (u > 0.035f && u < 0.055f) || (u > 0.945f && u < 0.965f);
                if (lanesPerSide == 1)
                {
                    if (edge) c = line;
                    if (u > 0.49f && u < 0.51f && v < 0.33f) c = line;
                }
                else if (lanesPerSide == 2)
                {
                    if (edge) c = line;
                    if ((u > 0.478f && u < 0.492f) || (u > 0.508f && u < 0.522f)) c = yellow;
                    if (((u > 0.245f && u < 0.26f) || (u > 0.74f && u < 0.755f)) && v < 0.25f) c = line;
                }
                else
                {
                    if (u > 0.04f && u < 0.06f) c = yellow;                     // inside (median) edge
                    if (u > 0.94f && u < 0.965f) c = line;
                    if (u > 0.49f && u < 0.51f && v < 0.25f) c = line;
                }
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Zebra crossing: u in metres/1.2 (one white + one dark bar per texture).</summary>
    public static Texture2D Zebra()
    {
        const int W = 64, H = 16;
        var t = New(W, H); var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = 0.20f * (0.92f + 0.12f * (R() - 0.5f));
                px[y * W + x] = x < 30 ? new Color(0.84f, 0.84f, 0.82f, 1f) : new Color(n, n, n, 1f);
            }
        return Done(t, px);
    }

    /// <summary>Concrete paving slabs. 128² represents 3 m (1.5 m flags).</summary>
    public static Texture2D Paving(Color baseCol, int flagPx)
    {
        const int N = 128;
        var t = New(N, N); var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = 0.93f + 0.10f * Noise(x, y, 0.05f) + 0.05f * (R() - 0.5f);
                Color c = baseCol * n;
                if (x % flagPx == 0 || y % flagPx == 0) c *= 0.80f;
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Car park: 128 x 256 represents 5 m x 16 m — two rows of bays (lines
    /// every 2.5 m, 5 m long) either side of a 6 m aisle.</summary>
    public static Texture2D Parking()
    {
        const int W = 128, H = 256;
        var t = New(W, H); var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = 0.23f * (0.90f + 0.14f * Noise(x, y, 0.03f) + 0.10f * (R() - 0.5f));
                Color c = new Color(n, n, n * 1.02f, 1f);
                float vm = y / (float)H * 16f;                   // metres along
                bool bayZone = vm < 5f || vm > 11f;
                if (bayZone && (x % 64 < 3)) c = new Color(0.82f, 0.82f, 0.80f, 1f);
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Rail corridor: 128 x 64, u across 10 m of ballast carrying two tracks,
    /// v along 5 m (sleepers every 0.6 m).</summary>
    public static Texture2D Rail()
    {
        const int W = 128, H = 64;
        var t = New(W, H); var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W * 10f, v = y / (float)H * 5f;
                float n = 0.9f + 0.2f * (R() - 0.5f);
                Color c = new Color(0.45f, 0.42f, 0.38f) * n;
                foreach (float tc in new[] { 3.0f, 7.0f })
                {
                    float d = Mathf.Abs(u - tc);
                    if (d < 1.3f && (v % 0.6f) < 0.25f) c = new Color(0.30f, 0.24f, 0.18f);   // sleeper
                    if (Mathf.Abs(d - 0.72f) < 0.06f) c = new Color(0.55f, 0.55f, 0.57f);    // rail head
                }
                c.a = 1f;
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Crop field in drill rows. 128² represents 12 m; rows run along v.</summary>
    public static Texture2D Field(Color a, Color b, int rowPx, float macro)
    {
        const int N = 128;
        var t = New(N, N); var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float row = 0.5f + 0.5f * Mathf.Sin(x * 6.2831853f / rowPx);
                float n = Noise(x, y, 0.06f) * macro + (R() - 0.5f) * 0.12f;
                Color c = Color.Lerp(a, b, Mathf.Clamp01(row * 0.7f + n));
                c.a = 1f;
                px[y * N + x] = c;
            }
        return Done(t, px);
    }

    /// <summary>Leafy canopy mottling, used on every tree.</summary>
    public static Texture2D Leaves()
    {
        const int N = 128;
        var t = New(N, N); var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float n = 0.70f + 0.45f * Noise(x, y, 0.18f) + 0.25f * (R() - 0.5f);
                px[y * N + x] = new Color(n, n, n, 1f);
            }
        return Done(t, px);
    }

    /// <summary>Football pitch with mown stripes and white lines. 256 x 160 is the
    /// whole pitch (105 m x 68 m) — mapped once, not tiled.</summary>
    public static Texture2D Pitch()
    {
        const int W = 256, H = 166;
        var t = New(W, H); t.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool stripe = (x / 16) % 2 == 0;
                Color c = stripe ? new Color(0.30f, 0.52f, 0.22f) : new Color(0.26f, 0.46f, 0.19f);
                float cx = x - W * 0.5f, cy = y - H * 0.5f;
                bool line = x == 6 || x == W - 7 || y == 6 || y == H - 7 || x == W / 2
                            || Mathf.Abs(Mathf.Sqrt(cx * cx + cy * cy) - 22f) < 0.8f
                            || ((x == 45 || x == W - 46) && Mathf.Abs(cy) < 50f)
                            || ((y == (int)(H * 0.5f - 50f) || y == (int)(H * 0.5f + 50f)) && (x < 45 || x > W - 46) && x > 6 && x < W - 7);
                if (line) c = new Color(0.9f, 0.9f, 0.88f);
                c.a = 1f;
                px[y * W + x] = c;
            }
        return Done(t, px);
    }

    // ── RUNWAY ───────────────────────────────────────────────────────────────

    // 5x7 bitmap digits (rows top->bottom)
    static readonly string[] Digits =
    {
        "01110,10001,10001,10001,10001,10001,01110",
        "00100,01100,00100,00100,00100,00100,01110",
        "01110,10001,00001,00010,00100,01000,11111",
        "11110,00001,00001,01110,00001,00001,11110",
        "00010,00110,01010,10010,11111,00010,00010",
        "11111,10000,11110,00001,00001,10001,01110",
        "00110,01000,10000,11110,10001,10001,01110",
        "11111,00001,00010,00100,01000,01000,01000",
        "01110,10001,10001,01110,10001,10001,01110",
        "01110,10001,10001,01111,00001,00010,01100",
    };

    /// <summary>The whole runway surface, painted to ICAO Annex 14 for a 30 m x 600 m
    /// runway: threshold "piano keys", designation numbers, centreline (30 m stripe /
    /// 20 m gap), aiming-point blocks, side stripes, and rubber deposits in the
    /// touchdown zones. u runs across (0 = west edge), v along (0 = the 01 threshold,
    /// the south end). 256 x 4096 texels ≈ 8.5 px/m across, 6.8 px/m along.</summary>
    public static Texture2D Runway(string southDesig, string northDesig)
    {
        const int W = 256, H = 4096;
        const float Wm = 30f, Lm = 600f;
        var t = New(W, H); t.wrapMode = TextureWrapMode.Clamp; t.anisoLevel = 16;
        var px = new Color[W * H];
        Color paint = new Color(0.90f, 0.90f, 0.88f, 1f);
        for (int y = 0; y < H; y++)
        {
            float vm = (y + 0.5f) / H * Lm;
            float fromEnd = Mathf.Min(vm, Lm - vm);
            for (int x = 0; x < W; x++)
            {
                float um = (x + 0.5f) / W * Wm;
                float n = 0.19f * (0.86f + 0.18f * Noise(x * 0.4f, y * 0.4f, 0.02f) + 0.10f * (R() - 0.5f));
                // rubber deposits in the touchdown zone, concentrated along the wheel tracks
                float tdz = Mathf.Clamp01(1f - Mathf.Abs(fromEnd - 120f) / 110f);
                float track = Mathf.Exp(-Mathf.Pow((Mathf.Abs(um - 15f) - 2.6f) / 2.4f, 2f));
                n *= 1f - 0.35f * tdz * track * (0.7f + 0.6f * Noise(x, y, 0.08f));
                // longitudinal texture of the lay (paver lanes)
                if (Mathf.Abs(um - 7.5f) < 0.05f || Mathf.Abs(um - 22.5f) < 0.05f) n *= 0.93f;
                Color c = new Color(n, n, n * 1.03f, 1f);

                bool p = false;
                // side stripes, 0.9 m
                if (um > 0.3f && um < 1.2f || um > Wm - 1.2f && um < Wm - 0.3f) p = true;
                // threshold bar-less piano keys: 8 stripes 1.8 m wide, 30 m long, from 6 m in
                if (fromEnd > 6f && fromEnd < 36f)
                {
                    float k = um - 1.95f;                  // first key starts 1.95 m from the edge
                    for (int s = 0; s < 8; s++)
                    {
                        float x0 = s < 4 ? k - s * 3.6f : um - (Wm - 1.95f - 1.8f) + (7 - s) * 3.6f;
                        if (x0 >= 0f && x0 < 1.8f) p = true;
                    }
                    if (um > 12.3f && um < 17.7f) p = false;   // wider centre gap
                }
                // centreline: 30 m stripe / 20 m gap, 0.9 m wide, between the numbers
                if (fromEnd > 66f && Mathf.Abs(um - 15f) < 0.45f && ((vm - 66f) % 50f) < 30f) p = true;
                // aiming point: two 45 m x 4 m blocks, 150 m in (short-runway rule)
                if (fromEnd > 150f && fromEnd < 195f && (Mathf.Abs(um - 15f) > 4.5f && Mathf.Abs(um - 15f) < 8.5f)) p = true;
                if (p) c = paint * (0.92f + 0.08f * R());
                px[y * W + x] = c;
            }
        }
        // designation numbers, 9 m tall, starting 12 m after the keys (48 m from the end)
        PaintNumber(px, W, H, Wm, Lm, southDesig, 48f, false);
        PaintNumber(px, W, H, Wm, Lm, northDesig, 48f, true);
        return Done(t, px);
    }

    static void PaintNumber(Color[] px, int W, int H, float Wm, float Lm, string s, float startM, bool fromNorth)
    {
        const float digitH = 9f, digitW = 3.0f, gap = 1.6f;
        float totalW = s.Length * digitW + (s.Length - 1) * gap;
        float u0 = (Wm - totalW) * 0.5f;
        for (int d = 0; d < s.Length; d++)
        {
            var rows = Digits[s[d] - '0'].Split(',');
            for (int r = 0; r < 7; r++)
                for (int col = 0; col < 5; col++)
                {
                    if (rows[r][col] != '1') continue;
                    // glyph space: col across, r down; the reader stands at the threshold
                    // looking along the runway, so the glyph's TOP is the far end.
                    float gu0 = u0 + d * (digitW + gap) + col / 5f * digitW;
                    float gu1 = gu0 + digitW / 5f;
                    float gv1 = startM + digitH - r / 7f * digitH;    // metres from this end
                    float gv0 = gv1 - digitH / 7f;
                    FillM(px, W, H, Wm, Lm, fromNorth, gu0, gu1, gv0, gv1);
                }
        }
    }

    static void FillM(Color[] px, int W, int H, float Wm, float Lm, bool fromNorth, float u0, float u1, float v0, float v1)
    {
        if (fromNorth)
        {   // rotate 180 degrees: seen by a pilot rolling south
            float a = Wm - u1, b = Wm - u0; u0 = a; u1 = b;
            float c = Lm - v1, d = Lm - v0; v0 = c; v1 = d;
        }
        int x0 = Mathf.Clamp(Mathf.RoundToInt(u0 / Wm * W), 0, W), x1 = Mathf.Clamp(Mathf.RoundToInt(u1 / Wm * W), 0, W);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(v0 / Lm * H), 0, H), y1 = Mathf.Clamp(Mathf.RoundToInt(v1 / Lm * H), 0, H);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                px[y * W + x] = new Color(0.90f, 0.90f, 0.88f, 1f);
    }
}
