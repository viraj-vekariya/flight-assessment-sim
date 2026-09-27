using System.IO;
using UnityEngine;

/// <summary>
/// Supplies the terrain ground textures. Prefers real CC0 image files dropped in
/// StreamingAssets/Terrain/ (grass/dirt/rock/sand/snow .jpg); if a file is absent
/// it falls back to a procedurally-generated noise texture, so the world always
/// builds with or without the texture files present.
/// </summary>
public static class TerrainTextures
{
    public static Texture2D Grass() => Load("grass", new Color(0.30f, 0.46f, 0.22f), new Color(0.40f, 0.55f, 0.28f), 0.10f);
    public static Texture2D Dirt() => Load("dirt", new Color(0.40f, 0.31f, 0.20f), new Color(0.50f, 0.40f, 0.27f), 0.12f);
    public static Texture2D Rock() => Load("rock", new Color(0.40f, 0.39f, 0.37f), new Color(0.55f, 0.53f, 0.50f), 0.16f);
    public static Texture2D Sand() => Load("sand", new Color(0.76f, 0.69f, 0.50f), new Color(0.86f, 0.79f, 0.60f), 0.07f);
    public static Texture2D Snow() => Load("snow", new Color(0.90f, 0.93f, 0.97f), new Color(1.00f, 1.00f, 1.00f), 0.05f);

    static Texture2D Load(string name, Color a, Color b, float grain)
    {
        var real = FromStreamingAssets(name);
        if (real != null) return real;
        if (name == "grass") return ProceduralGrass();   // richer 3-shade fallback
        return Procedural(name, a, b, grain);
    }

    // Richer grass fallback: 3 blended shades of green driven by two octaves of
    // Perlin (macro patches + fine blade-scale variation), so the ground reads as
    // grass rather than a flat green wash when no CC0 file is present.
    static Texture2D ProceduralGrass()
    {
        const int N = 256;
        Color darkGrass  = new Color(0.18f, 0.42f, 0.16f);
        Color midGrass   = new Color(0.28f, 0.54f, 0.22f);
        Color lightGrass = new Color(0.38f, 0.62f, 0.30f);
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float macro = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);   // broad patches
                float fine  = Mathf.PerlinNoise(x * 0.8f, y * 0.8f);     // fine blade detail
                float n = Mathf.Clamp01(macro * 0.65f + fine * 0.35f);
                Color c = n < 0.5f
                    ? Color.Lerp(darkGrass, midGrass, n * 2f)
                    : Color.Lerp(midGrass, lightGrass, (n - 0.5f) * 2f);
                px[y * N + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    /// <summary>A tinted COPY of a ground texture: saturation scaled about its own
    /// luminance, then multiplied by <paramref name="mul"/>. The source is untouched.
    ///
    /// WHY A TINT AND NOT A RECOLOURED ProceduralGrass(). The ground textures are REAL
    /// CC0 photographs in StreamingAssets/Terrain/*.jpg; Load() returns those and only
    /// falls back to the procedural generator when a file is absent, so while the .jpg
    /// files exist the procedural constants change nothing on screen. Colour has to be
    /// applied to the LOADED image.
    ///
    /// EVERY TINT MULTIPLIER PASSED IN IS BELOW 1.0, AND THAT IS A RULE. The photographs
    /// are already bright; a multiplier above 1 clips against 1.0 under a lit scene and
    /// the ground goes to flat paper — clipping destroys exactly the tonal range that makes
    /// a tile READ as texture. A tint darkens; the light supplies the brightness.
    /// Measured on the level-design renders, where two of four variants lost their
    /// texture entirely to this before the rule was adopted.</summary>
    public static Texture2D Tint(Texture2D src, Color mul, float sat)
    {
        if (src == null) return null;

        var px = src.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            // Rec.601 luma — desaturating toward luma keeps the texture's tonal detail,
            // which is the whole reason a photo reads as ground and a flat colour does not.
            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            c.r = Mathf.Clamp01(Mathf.Lerp(lum, c.r, sat) * mul.r);
            c.g = Mathf.Clamp01(Mathf.Lerp(lum, c.g, sat) * mul.g);
            c.b = Mathf.Clamp01(Mathf.Lerp(lum, c.b, sat) * mul.b);
            px[i] = c;
        }

        var dst = new Texture2D(src.width, src.height, TextureFormat.RGB24, true)
        { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        dst.SetPixels(px);
        dst.Apply(true);
        return dst;
    }

    static Texture2D FromStreamingAssets(string name) => StreamingImage("Terrain/" + name + ".jpg");

    /// <summary>Load a JPG/PNG from StreamingAssets at runtime, or null if absent/unreadable.</summary>
    public static Texture2D StreamingImage(string relPath)
    {
        try
        {
            string p = Path.Combine(Application.streamingAssetsPath, relPath);
            if (!File.Exists(p)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, true);
            if (tex.LoadImage(File.ReadAllBytes(p)))
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.anisoLevel = 4;
                return tex;
            }
        }
        catch { /* fall through */ }
        return null;
    }

    // noise-based fallback: two-colour blend + fine grain, tileable
    static Texture2D Procedural(string name, Color a, Color b, float grain)
    {
        const int N = 256;
        int seed = name.GetHashCode();
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var px = new Color[N * N];
        float fo = (seed & 1023);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                // tileable low-freq blend
                float blob = Mathf.PerlinNoise(fo + Mathf.Cos(u * 6.2831853f) * 1.3f + 4f,
                                               fo + Mathf.Sin(v * 6.2831853f) * 1.3f + 4f);
                float fine = Mathf.PerlinNoise(u * N * 0.25f + fo, v * N * 0.25f + fo) - 0.5f;
                Color c = Color.Lerp(a, b, Mathf.Clamp01(blob));
                c += new Color(fine, fine, fine) * grain;
                px[y * N + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}
