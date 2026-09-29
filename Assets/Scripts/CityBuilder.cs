// CityBuilder — the settled landscape around the aerodrome.
//
// Replaces the old "toy mat" settlements (a dark square with Kenney models dropped on
// it, three 7x5 grids of identical houses, one barn) with a place that reads as a real
// town from the air:
//
//   * EAST CITY   — a street grid with a downtown core (podium + tower blocks), a ring of
//                   mid-rise perimeter blocks with courtyards, and houses at the fringe.
//   * WEST SUBURB — long residential blocks of detached houses with pitched roofs,
//                   driveways and gardens, a small town centre with a church.
//   * INDUSTRIAL  — sheds, truck yards, tank farms south-east of the runway.
//   * TRANSPORT   — an east-west dual carriageway and a railway north of the aerodrome,
//                   the airport access road, and connectors into each grid.
//   * AIRPORT     — hangars, terminal, control tower and car park west of the apron.
//   * FARMLAND    — a patchwork of crop fields, hedgerows, farmsteads and woodlots on the
//                   rest of the plain and under the long approach.
//   * FORESTS     — mixed woodland over the hills beyond the plain.
//
// ── Rules it keeps ──────────────────────────────────────────────────────────────
//   * Heights are capped by an obstacle-limitation envelope around the runway (the
//     ICAO Annex 14 approach / take-off / transitional surfaces, and a horizontal
//     surface that relaxes with distance) so no mission path gains a new obstacle.
//     Missions fly over the city at 250 m or more; the tallest tower is ~120 m.
//   * Everything stands on ground that is exactly flat (the aerodrome plain), checked
//     against WorldBuilder.SampleGroundY, except forest trees which follow the terrain.
//   * The aerodrome surfaces (runway, taxiways, apron) are untouched — the airport zone
//     is reserved before anything else is laid out.
//   * Uses System.Random only, never UnityEngine.Random: downstream code depends on the
//     UnityEngine.Random sequence that WorldBuilder.BuildForests seeds.
//   * Buildings and trees carry colliders tagged Obstacle (flying into them is a crash);
//     roads, fields and cars carry none.
//
// Everything is merged into a few hundred chunk meshes (500 m tiles x 4 layers), so a
// city of ~10,000 buildings and ~60,000 trees costs a few hundred draw calls.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class CityBuilder
{
    // ════════════════════════════════════════════════════════════════════════
    // MESH BUFFERS
    // ════════════════════════════════════════════════════════════════════════
    enum Lyr { Solid = 0, Ground = 1, Tree = 2, Prop = 3 }

    class Buf
    {
        public Lyr layer; public int cx, cz;
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> U = new List<Vector2>();
        public readonly Dictionary<int, List<int>> S = new Dictionary<int, List<int>>();
    }

    const float Chunk = 500f;
    static Dictionary<long, Buf> bufs;
    static Buf cur;
    static List<int> curIdx;
    static List<Material> mats;
    static System.Random rng;
    static Terrain terrain;
    static int nBuildings, nTrees, nCars, nFields;

    static float R() => (float)rng.NextDouble();
    static float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
    static int RI(int a, int bExcl) => rng.Next(a, bExcl);
    static T Pick<T>(T[] a) => a[rng.Next(a.Length)];

    static void Use(Lyr l, float x, float z, int mat)
    {
        int cx = Mathf.FloorToInt(x / Chunk), cz = Mathf.FloorToInt(z / Chunk);
        long key = ((long)l << 40) | ((long)(cx + 5000) << 20) | (long)(cz + 5000);
        if (!bufs.TryGetValue(key, out cur)) { cur = new Buf { layer = l, cx = cx, cz = cz }; bufs[key] = cur; }
        M(mat);
    }

    /// <summary>Switch material within the current chunk buffer.</summary>
    static void M(int mat)
    {
        if (!cur.S.TryGetValue(mat, out curIdx)) { curIdx = new List<int>(); cur.S[mat] = curIdx; }
    }

    static int Vtx(Vector3 p, Vector3 n, Vector2 uv)
    {
        cur.V.Add(p); cur.N.Add(n); cur.U.Add(uv);
        return cur.V.Count - 1;
    }

    /// <summary>Index a triangle so that its front face points along `hint`.
    /// (Unity's front face normal is Cross(b-a, c-a).)</summary>
    static void TriIdx(int a, int b, int c, Vector3 hint)
    {
        var V = cur.V;
        Vector3 n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        if (Vector3.Dot(n, hint) >= 0f) { curIdx.Add(a); curIdx.Add(b); curIdx.Add(c); }
        else { curIdx.Add(a); curIdx.Add(c); curIdx.Add(b); }
    }

    /// <summary>A flat-shaded planar quad a-b-c-d (in perimeter order), facing `hint`.</summary>
    static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 hint)
    {
        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
        if (Vector3.Dot(n, hint) < 0f) n = -n;
        int i0 = Vtx(a, n, ua), i1 = Vtx(b, n, ub), i2 = Vtx(c, n, uc), i3 = Vtx(d, n, ud);
        TriIdx(i0, i1, i2, n); TriIdx(i0, i2, i3, n);
    }

    static void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 hint)
    {
        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
        if (Vector3.Dot(n, hint) < 0f) n = -n;
        int i0 = Vtx(a, n, ua), i1 = Vtx(b, n, ub), i2 = Vtx(c, n, uc);
        TriIdx(i0, i1, i2, n);
    }

    /// <summary>Upward horizontal rectangle, UVs in world metres / tile.</summary>
    static void HQuad(float x0, float z0, float x1, float z1, float y, float tile)
    {
        Quad(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1),
             new Vector2(x0 / tile, z0 / tile), new Vector2(x1 / tile, z0 / tile),
             new Vector2(x1 / tile, z1 / tile), new Vector2(x0 / tile, z1 / tile), Vector3.up);
    }

    /// <summary>Horizontal rectangle with UVs rotated 90° (texture rows run along X).</summary>
    static void HQuadRot(float x0, float z0, float x1, float z1, float y, float tile)
    {
        Quad(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1),
             new Vector2(z0 / tile, x0 / tile), new Vector2(z0 / tile, x1 / tile),
             new Vector2(z1 / tile, x1 / tile), new Vector2(z1 / tile, x0 / tile), Vector3.up);
    }

    // ════════════════════════════════════════════════════════════════════════
    // MATERIALS
    // ════════════════════════════════════════════════════════════════════════
    /// <summary>How a facade texture maps onto a wall, in metres.</summary>
    struct Fac
    {
        public int mat; public float bay, baysPerTex, storey, storeysPerTex;
        public Fac(int m, float b, float bpt, float s, float spt) { mat = m; bay = b; baysPerTex = bpt; storey = s; storeysPerTex = spt; }
    }

    static Shader std;

    static int Mat(Texture2D tex, Color col, float gloss, float metal = 0f)
    {
        var m = new Material(std) { color = col, mainTexture = tex };
        m.SetFloat("_Glossiness", gloss);
        m.SetFloat("_Metallic", metal);
        m.enableInstancing = false;
        mats.Add(m);
        return mats.Count - 1;
    }

    // facades
    static Fac[] fGlass, fOffice, fBrick, fPlaster, fBalcony;
    static Fac fShop, fSiding;
    static Fac[] fShed;
    static Fac fShedRed, fStone;
    // roofs & surfaces
    static int mGravel, mMembrane, mMembraneLight, mCoping, mTileTerra, mTileSlate, mTileBrown, mHvac;
    static int mRoad1, mRoad2, mHwy, mAsph, mZebra, mWalk, mPlaza, mConcrete, mPark, mRail;
    static int mLawn, mWater, mPitch, mDirt, mYard, mSeats;
    static int[] mFields;
    static int[] mLeaf; static int mConifer, mTrunk;
    static int[] mCar; static int mCarGlass, mTruck, mTank, mTrain, mCabGlass;
    static int[] mSidingTint;

    static void BuildMaterials()
    {
        std = Shader.Find("Standard");
        mats = new List<Material>();
        CityTextures.Seed(4242);

        Color W = Color.white;
        var gTex = new[]
        {
            CityTextures.GlassCurtain(new Color(0.30f, 0.42f, 0.50f), new Color(0.55f, 0.58f, 0.60f), new Color(0.18f, 0.22f, 0.26f)),
            CityTextures.GlassCurtain(new Color(0.42f, 0.36f, 0.28f), new Color(0.30f, 0.26f, 0.22f), new Color(0.22f, 0.19f, 0.16f)),
            CityTextures.GlassCurtain(new Color(0.46f, 0.52f, 0.56f), new Color(0.78f, 0.79f, 0.80f), new Color(0.40f, 0.43f, 0.46f)),
            CityTextures.GlassCurtain(new Color(0.22f, 0.36f, 0.34f), new Color(0.20f, 0.22f, 0.22f), new Color(0.14f, 0.18f, 0.18f)),
        };
        fGlass = new Fac[gTex.Length];
        for (int i = 0; i < gTex.Length; i++) fGlass[i] = new Fac(Mat(gTex[i], W, 0.80f, 0.35f), 3.6f, 4, 3.6f, 4);

        fOffice = new[]
        {
            new Fac(Mat(CityTextures.OfficeBands(new Color(0.78f, 0.76f, 0.72f), new Color(0.26f, 0.33f, 0.38f)), W, 0.35f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.OfficeBands(new Color(0.62f, 0.63f, 0.64f), new Color(0.22f, 0.28f, 0.34f)), W, 0.35f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.OfficeBands(new Color(0.74f, 0.68f, 0.60f), new Color(0.30f, 0.30f, 0.30f)), W, 0.35f), 3.6f, 4, 3.6f, 4),
        };
        fBrick = new[]
        {
            new Fac(Mat(CityTextures.Punched(new Color(0.56f, 0.28f, 0.20f), true, false), W, 0.08f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.Punched(new Color(0.72f, 0.60f, 0.44f), true, false), W, 0.08f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.Punched(new Color(0.46f, 0.30f, 0.26f), true, false), W, 0.08f), 3.6f, 4, 3.6f, 4),
        };
        fPlaster = new[]
        {
            new Fac(Mat(CityTextures.Punched(new Color(0.88f, 0.84f, 0.74f), false, false), W, 0.10f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.Punched(new Color(0.90f, 0.90f, 0.88f), false, false), W, 0.10f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.Punched(new Color(0.84f, 0.72f, 0.60f), false, false), W, 0.10f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.Punched(new Color(0.74f, 0.78f, 0.72f), false, false), W, 0.10f), 3.6f, 4, 3.6f, 4),
        };
        fBalcony = new[]
        {
            new Fac(Mat(CityTextures.Punched(new Color(0.92f, 0.90f, 0.86f), false, true), W, 0.10f), 3.6f, 4, 3.6f, 4),
            new Fac(Mat(CityTextures.Punched(new Color(0.86f, 0.78f, 0.66f), false, true), W, 0.10f), 3.6f, 4, 3.6f, 4),
        };
        fShop = new Fac(Mat(CityTextures.Shopfront(), W, 0.45f), 3.6f, 4, 4.5f, 1);
        var sidingTex = CityTextures.HouseSiding();
        Color[] sidingCols =
        {
            new Color(0.95f, 0.93f, 0.86f), new Color(0.86f, 0.90f, 0.84f), new Color(0.92f, 0.84f, 0.74f),
            new Color(0.82f, 0.86f, 0.92f), new Color(0.96f, 0.96f, 0.95f), new Color(0.88f, 0.78f, 0.70f),
        };
        mSidingTint = new int[sidingCols.Length];
        for (int i = 0; i < sidingCols.Length; i++) mSidingTint[i] = Mat(sidingTex, sidingCols[i], 0.08f);
        fSiding = new Fac(mSidingTint[0], 4f, 2, 3f, 1);

        fShed = new[]
        {
            new Fac(Mat(CityTextures.Warehouse(new Color(0.74f, 0.75f, 0.76f)), W, 0.30f, 0.2f), 24f, 1, 12f, 1),
            new Fac(Mat(CityTextures.Warehouse(new Color(0.46f, 0.56f, 0.66f)), W, 0.30f, 0.2f), 24f, 1, 12f, 1),
            new Fac(Mat(CityTextures.Warehouse(new Color(0.80f, 0.76f, 0.66f)), W, 0.30f, 0.2f), 24f, 1, 12f, 1),
            new Fac(Mat(CityTextures.Warehouse(new Color(0.56f, 0.62f, 0.52f)), W, 0.30f, 0.2f), 24f, 1, 12f, 1),
        };
        fShedRed = new Fac(Mat(CityTextures.Warehouse(new Color(0.60f, 0.20f, 0.16f)), W, 0.15f), 24f, 1, 12f, 1);
        fStone = new Fac(Mat(CityTextures.Punched(new Color(0.78f, 0.74f, 0.66f), true, false), W, 0.05f), 3.6f, 4, 3.6f, 4);

        mGravel = Mat(CityTextures.FlatRoof(new Color(0.55f, 0.54f, 0.52f), false), W, 0.05f);
        mMembrane = Mat(CityTextures.FlatRoof(new Color(0.30f, 0.31f, 0.33f), true), W, 0.15f);
        mMembraneLight = Mat(CityTextures.FlatRoof(new Color(0.78f, 0.79f, 0.80f), true), W, 0.20f);
        mCoping = Mat(CityTextures.FlatRoof(new Color(0.62f, 0.62f, 0.61f), false), W, 0.10f);
        mTileTerra = Mat(CityTextures.RoofTiles(new Color(0.62f, 0.28f, 0.20f)), W, 0.12f);
        mTileSlate = Mat(CityTextures.RoofTiles(new Color(0.30f, 0.31f, 0.34f)), W, 0.18f);
        mTileBrown = Mat(CityTextures.RoofTiles(new Color(0.40f, 0.30f, 0.24f)), W, 0.12f);
        mHvac = Mat(CityTextures.FlatRoof(new Color(0.70f, 0.71f, 0.72f), true), W, 0.35f, 0.4f);

        mRoad1 = Mat(CityTextures.Road(1), W, 0.12f);
        mRoad2 = Mat(CityTextures.Road(2), W, 0.12f);
        mHwy = Mat(CityTextures.Road(0), W, 0.12f);
        mAsph = Mat(CityTextures.Asphalt(0.20f), W, 0.12f);
        mZebra = Mat(CityTextures.Zebra(), W, 0.12f);
        mWalk = Mat(CityTextures.Paving(new Color(0.66f, 0.65f, 0.62f), 32), W, 0.08f);
        mPlaza = Mat(CityTextures.Paving(new Color(0.72f, 0.66f, 0.58f), 21), W, 0.10f);
        mConcrete = Mat(CityTextures.Paving(new Color(0.60f, 0.60f, 0.58f), 64), W, 0.06f);
        mPark = Mat(CityTextures.Parking(), W, 0.12f);
        mRail = Mat(CityTextures.Rail(), W, 0.08f);

        mLawn = Mat(TerrainTextures.Tint(TerrainTextures.Grass(), new Color(0.62f, 0.90f, 0.46f), 1.25f), W, 0.05f);
        mWater = Mat(CityTextures.Asphalt(0.9f), new Color(0.16f, 0.30f, 0.36f), 0.92f, 0.1f);
        mPitch = Mat(CityTextures.Pitch(), W, 0.05f);
        mSeats = Mat(CityTextures.Field(new Color(0.16f, 0.30f, 0.62f), new Color(0.55f, 0.56f, 0.58f), 8, 0.2f), W, 0.25f);
        mDirt = Mat(CityTextures.Field(new Color(0.46f, 0.38f, 0.28f), new Color(0.54f, 0.46f, 0.34f), 128, 0.5f), W, 0.02f);
        mYard = Mat(CityTextures.Field(new Color(0.50f, 0.46f, 0.38f), new Color(0.56f, 0.52f, 0.44f), 128, 0.6f), W, 0.02f);

        mFields = new[]
        {
            Mat(CityTextures.Field(new Color(0.40f, 0.30f, 0.20f), new Color(0.52f, 0.41f, 0.28f), 8, 0.4f), W, 0.02f),   // ploughed
            Mat(CityTextures.Field(new Color(0.70f, 0.60f, 0.32f), new Color(0.80f, 0.70f, 0.40f), 6, 0.5f), W, 0.03f),   // wheat
            Mat(CityTextures.Field(new Color(0.28f, 0.44f, 0.16f), new Color(0.40f, 0.56f, 0.22f), 8, 0.5f), W, 0.03f),   // green crop
            Mat(CityTextures.Field(new Color(0.36f, 0.52f, 0.24f), new Color(0.44f, 0.60f, 0.28f), 128, 0.9f), W, 0.02f), // pasture
            Mat(CityTextures.Field(new Color(0.52f, 0.50f, 0.32f), new Color(0.60f, 0.58f, 0.38f), 128, 1.0f), W, 0.02f), // fallow
            Mat(CityTextures.Field(new Color(0.58f, 0.60f, 0.26f), new Color(0.66f, 0.68f, 0.32f), 5, 0.5f), W, 0.03f),   // rapeseed/maize
        };

        var leaves = CityTextures.Leaves();
        mLeaf = new[]
        {
            Mat(leaves, new Color(0.20f, 0.36f, 0.14f), 0.05f),
            Mat(leaves, new Color(0.26f, 0.42f, 0.16f), 0.05f),
            Mat(leaves, new Color(0.17f, 0.30f, 0.13f), 0.05f),
            Mat(leaves, new Color(0.34f, 0.44f, 0.18f), 0.05f),
        };
        mConifer = Mat(leaves, new Color(0.11f, 0.22f, 0.12f), 0.05f);
        mTrunk = Mat(null, new Color(0.30f, 0.22f, 0.15f), 0.05f);

        mCar = new[]
        {
            Mat(null, new Color(0.85f, 0.85f, 0.86f), 0.75f, 0.5f), Mat(null, new Color(0.10f, 0.10f, 0.11f), 0.8f, 0.5f),
            Mat(null, new Color(0.55f, 0.57f, 0.60f), 0.75f, 0.6f), Mat(null, new Color(0.55f, 0.10f, 0.10f), 0.75f, 0.4f),
            Mat(null, new Color(0.14f, 0.24f, 0.46f), 0.75f, 0.4f), Mat(null, new Color(0.90f, 0.90f, 0.88f), 0.7f, 0.3f),
        };
        mCarGlass = Mat(null, new Color(0.08f, 0.10f, 0.12f), 0.9f, 0.2f);
        mTruck = Mat(null, new Color(0.88f, 0.88f, 0.86f), 0.4f, 0.1f);
        mTank = Mat(CityTextures.FlatRoof(new Color(0.90f, 0.90f, 0.88f), false), W, 0.4f, 0.2f);
        mTrain = Mat(null, new Color(0.62f, 0.66f, 0.72f), 0.6f, 0.5f);
        mCabGlass = Mat(null, new Color(0.14f, 0.22f, 0.26f), 0.9f, 0.4f);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SITE RULES: flatness, occupancy, obstacle-limitation envelope
    // ════════════════════════════════════════════════════════════════════════
    const float OccMin = -2500f, OccCell = 10f; const int OccN = 500;
    static bool[] occ;

    // A cell belongs to a rectangle when the cell's CENTRE lies inside it, so two
    // rectangles that share an edge never claim the same cell.
    static void Cells(float x0, float z0, float x1, float z1, out int i0, out int i1, out int j0, out int j1)
    {
        i0 = Mathf.Max(0, Mathf.CeilToInt((x0 - OccMin) / OccCell - 0.5f)); i1 = Mathf.Min(OccN - 1, Mathf.FloorToInt((x1 - OccMin) / OccCell - 0.5f));
        j0 = Mathf.Max(0, Mathf.CeilToInt((z0 - OccMin) / OccCell - 0.5f)); j1 = Mathf.Min(OccN - 1, Mathf.FloorToInt((z1 - OccMin) / OccCell - 0.5f));
    }

    static void Mark(float x0, float z0, float x1, float z1)
    {
        Cells(x0, z0, x1, z1, out int i0, out int i1, out int j0, out int j1);
        for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) occ[j * OccN + i] = true;
    }

    static bool Free(float x0, float z0, float x1, float z1)
    {
        Cells(x0, z0, x1, z1, out int i0, out int i1, out int j0, out int j1);
        for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) if (occ[j * OccN + i]) return false;
        return true;
    }

    static bool FlatAt(float x, float z) => Mathf.Abs(WorldBuilder.SampleGroundY(x, z)) < 0.01f;

    /// <summary>True if the whole rectangle (plus `pad`) is on the exactly-flat plain.</summary>
    static bool Flat(float x0, float z0, float x1, float z1, float pad)
    {
        x0 -= pad; z0 -= pad; x1 += pad; z1 += pad;
        int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / 40f)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / 40f));
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
                if (!FlatAt(Mathf.Lerp(x0, x1, i / (float)nx), Mathf.Lerp(z0, z1, j / (float)nz))) return false;
        return true;
    }

    /// <summary>Maximum height (m above the runway) of anything built at (x,z).
    /// Runway strip 80 m x 720 m. Approach and take-off surfaces: 60 m inner edge,
    /// 10 % divergence, 5 % slope. Transitional: 20 %. Horizontal surface 45 m,
    /// rising 15 % beyond 1 km from the strip. 2 m margin under all of it.</summary>
    /// <summary>Obstacle-free ground around the aerodrome: open fields only, no trees
    /// or buildings (the airport's own buildings are placed separately). An aeroplane
    /// that runs off a taxiway or the runway in a crosswind rolls onto grass or crop,
    /// not into a hedgerow — the mission battery's crosswind take-off (XT3) found
    /// exactly that: a 1 km ground-roll excursion south of the apron ending in a tree.</summary>
    static bool AirfieldClear(float x, float z) => x > -480f && x < 400f && z > -1800f && z < 560f;

    public static float MaxHeight(float x, float z)
    {
        float ax = Mathf.Abs(x), az = Mathf.Abs(z);
        float dx = Mathf.Max(0f, ax - 40f), dz = Mathf.Max(0f, az - 360f);
        float dStrip = Mathf.Sqrt(dx * dx + dz * dz);
        float cap = 45f + Mathf.Max(0f, dStrip - 1000f) * 0.15f;
        if (az <= 360f) cap = Mathf.Min(cap, dx * 0.2f);
        else
        {
            float s = az - 360f;
            float edge = 30f + s * 0.10f;
            cap = Mathf.Min(cap, 0.05f * s + Mathf.Max(0f, ax - edge) * 0.2f);
        }
        return cap - 2f;
    }

    // ════════════════════════════════════════════════════════════════════════
    // BUILDING PRIMITIVES
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>One wall from p0 to p1 (horizontal, xz), y0..y1, facing away from `centre`.
    /// Bays are rounded to whole windows so no window is cut at a corner.</summary>
    static void Wall(Vector2 p0, Vector2 p1, float y0, float y1, Fac f, float vBase, Vector2 centre)
    {
        float len = (p1 - p0).magnitude;
        if (len < 0.01f || y1 - y0 < 0.01f) return;
        float bays = Mathf.Max(1f, Mathf.Round(len / f.bay));
        float u1 = bays / f.baysPerTex;
        float vh = f.storey * f.storeysPerTex;
        float v0 = (y0 - vBase) / vh, v1 = (y1 - vBase) / vh;
        Vector2 mid = (p0 + p1) * 0.5f;
        Vector3 hint = new Vector3(mid.x - centre.x, 0f, mid.y - centre.y);
        // u should run left-to-right seen from outside: swap ends if needed
        Vector3 dir = new Vector3(p1.x - p0.x, 0f, p1.y - p0.y);
        if (Vector3.Dot(Vector3.Cross(Vector3.up, dir), hint) < 0f)
        { var t = p0; p0 = p1; p1 = t; }
        M(f.mat);
        Quad(new Vector3(p0.x, y0, p0.y), new Vector3(p0.x, y1, p0.y), new Vector3(p1.x, y1, p1.y), new Vector3(p1.x, y0, p1.y),
             new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(u1, v1), new Vector2(u1, v0), hint);
    }

    static void Walls(float x0, float z0, float x1, float z1, float y0, float y1, Fac f, float vBase)
    {
        var c = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
        Wall(new Vector2(x0, z0), new Vector2(x1, z0), y0, y1, f, vBase, c);
        Wall(new Vector2(x1, z0), new Vector2(x1, z1), y0, y1, f, vBase, c);
        Wall(new Vector2(x1, z1), new Vector2(x0, z1), y0, y1, f, vBase, c);
        Wall(new Vector2(x0, z1), new Vector2(x0, z0), y0, y1, f, vBase, c);
    }

    /// <summary>Plain box with one material on the sides and one on top.</summary>
    static void SlabBox(float x0, float z0, float x1, float z1, float y0, float y1, int side, int top, float tile)
    {
        M(side);
        float h = y1 - y0;
        Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0),
             new Vector2(x0 / tile, 0), new Vector2(x0 / tile, h / tile), new Vector2(x1 / tile, h / tile), new Vector2(x1 / tile, 0), Vector3.back);
        Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1),
             new Vector2(x0 / tile, 0), new Vector2(x0 / tile, h / tile), new Vector2(x1 / tile, h / tile), new Vector2(x1 / tile, 0), Vector3.forward);
        Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1),
             new Vector2(z0 / tile, 0), new Vector2(z0 / tile, h / tile), new Vector2(z1 / tile, h / tile), new Vector2(z1 / tile, 0), Vector3.left);
        Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1),
             new Vector2(z0 / tile, 0), new Vector2(z0 / tile, h / tile), new Vector2(z1 / tile, h / tile), new Vector2(z1 / tile, 0), Vector3.right);
        M(top);
        HQuad(x0, z0, x1, z1, y1, tile);
    }

    /// <summary>A flat-roofed building volume: facade walls, roof, and a parapet.</summary>
    static void FlatBlock(float x0, float z0, float x1, float z1, float y0, float y1, Fac f, float vBase, int roof, float parapet)
    {
        Walls(x0, z0, x1, z1, y0, y1, f, vBase);
        M(roof);
        HQuad(x0, z0, x1, z1, y1, 16f);
        if (parapet > 0f)
        {
            float t = 0.35f, yp = y1 + parapet;
            // outer face in coping, inner face, top rim
            var c = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            M(mCoping);
            Vector3[] o = { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) };
            Vector3[] n = { new Vector3(x0 + t, 0, z0 + t), new Vector3(x1 - t, 0, z0 + t), new Vector3(x1 - t, 0, z1 - t), new Vector3(x0 + t, 0, z1 - t) };
            for (int k = 0; k < 4; k++)
            {
                int k1 = (k + 1) % 4;
                Vector3 mid = (o[k] + o[k1]) * 0.5f;
                Vector3 outw = mid - c; outw.y = 0f;
                Quad(o[k] + Vector3.up * y1, o[k] + Vector3.up * yp, o[k1] + Vector3.up * yp, o[k1] + Vector3.up * y1,
                     Vector2.zero, new Vector2(0, 0.1f), new Vector2(1, 0.1f), new Vector2(1, 0), outw);
                Quad(n[k] + Vector3.up * y1, n[k] + Vector3.up * yp, n[k1] + Vector3.up * yp, n[k1] + Vector3.up * y1,
                     Vector2.zero, new Vector2(0, 0.1f), new Vector2(1, 0.1f), new Vector2(1, 0), -outw);
                Quad(o[k] + Vector3.up * yp, o[k1] + Vector3.up * yp, n[k1] + Vector3.up * yp, n[k] + Vector3.up * yp,
                     Vector2.zero, new Vector2(1, 0), new Vector2(1, 0.05f), new Vector2(0, 0.05f), Vector3.up);
            }
        }
    }

    /// <summary>Gable roof over the rectangle; ridge along X if alongX. Gable ends in `gable` facade.</summary>
    static void GableRoof(float x0, float z0, float x1, float z1, float y, float pitchDeg, bool alongX, int roofMat, Fac gable, float overhang)
    {
        float half = alongX ? (z1 - z0) * 0.5f : (x1 - x0) * 0.5f;
        float rise = Mathf.Tan(pitchDeg * Mathf.Deg2Rad) * half;
        float slope = Mathf.Sqrt(half * half + rise * rise) + overhang;
        float o = overhang;
        float yo = y - o * Mathf.Tan(pitchDeg * Mathf.Deg2Rad);
        M(roofMat);
        const float T = 4f;
        if (alongX)
        {
            float zm = (z0 + z1) * 0.5f, len = x1 - x0 + 2f * o;
            Quad(new Vector3(x0 - o, yo, z0 - o), new Vector3(x1 + o, yo, z0 - o), new Vector3(x1 + o, y + rise, zm), new Vector3(x0 - o, y + rise, zm),
                 new Vector2(0, 0), new Vector2(len / T, 0), new Vector2(len / T, slope / T), new Vector2(0, slope / T), new Vector3(0, 1, -1));
            Quad(new Vector3(x0 - o, yo, z1 + o), new Vector3(x1 + o, yo, z1 + o), new Vector3(x1 + o, y + rise, zm), new Vector3(x0 - o, y + rise, zm),
                 new Vector2(0, 0), new Vector2(len / T, 0), new Vector2(len / T, slope / T), new Vector2(0, slope / T), new Vector3(0, 1, 1));
            M(gable.mat);
            float vh = gable.storey * gable.storeysPerTex;
            foreach (float gx in new[] { x0, x1 })
                Tri(new Vector3(gx, y, z0), new Vector3(gx, y, z1), new Vector3(gx, y + rise, zm),
                    new Vector2(0, 0.9f), new Vector2((z1 - z0) / (gable.bay * gable.baysPerTex), 0.9f), new Vector2((z1 - z0) * 0.5f / (gable.bay * gable.baysPerTex), 0.9f + rise / vh * 0.1f),
                    new Vector3(gx == x0 ? -1 : 1, 0, 0));
        }
        else
        {
            float xm = (x0 + x1) * 0.5f, len = z1 - z0 + 2f * o;
            Quad(new Vector3(x0 - o, yo, z0 - o), new Vector3(x0 - o, yo, z1 + o), new Vector3(xm, y + rise, z1 + o), new Vector3(xm, y + rise, z0 - o),
                 new Vector2(0, 0), new Vector2(len / T, 0), new Vector2(len / T, slope / T), new Vector2(0, slope / T), new Vector3(-1, 1, 0));
            Quad(new Vector3(x1 + o, yo, z0 - o), new Vector3(x1 + o, yo, z1 + o), new Vector3(xm, y + rise, z1 + o), new Vector3(xm, y + rise, z0 - o),
                 new Vector2(0, 0), new Vector2(len / T, 0), new Vector2(len / T, slope / T), new Vector2(0, slope / T), new Vector3(1, 1, 0));
            M(gable.mat);
            foreach (float gz in new[] { z0, z1 })
                Tri(new Vector3(x0, y, gz), new Vector3(x1, y, gz), new Vector3(xm, y + rise, gz),
                    new Vector2(0, 0.9f), new Vector2((x1 - x0) / (gable.bay * gable.baysPerTex), 0.9f), new Vector2((x1 - x0) * 0.5f / (gable.bay * gable.baysPerTex), 0.95f),
                    new Vector3(0, 0, gz == z0 ? -1 : 1));
        }
    }

    /// <summary>Hip roof: four sloping faces, ridge along the longer side.</summary>
    static void HipRoof(float x0, float z0, float x1, float z1, float y, float pitchDeg, int roofMat, float o)
    {
        x0 -= o; z0 -= o; x1 += o; z1 += o;
        float w = x1 - x0, d = z1 - z0;
        float half = Mathf.Min(w, d) * 0.5f;
        float rise = Mathf.Tan(pitchDeg * Mathf.Deg2Rad) * half;
        float yo = y - o * Mathf.Tan(pitchDeg * Mathf.Deg2Rad);
        float yr = yo + rise;
        M(roofMat);
        const float T = 4f;
        float sl = Mathf.Sqrt(half * half + rise * rise) / T;
        Vector3 a = new Vector3(x0, yo, z0), b = new Vector3(x1, yo, z0), c = new Vector3(x1, yo, z1), e = new Vector3(x0, yo, z1);
        Vector3 r0, r1;
        if (w >= d) { r0 = new Vector3(x0 + half, yr, (z0 + z1) * 0.5f); r1 = new Vector3(x1 - half, yr, (z0 + z1) * 0.5f); }
        else { r0 = new Vector3((x0 + x1) * 0.5f, yr, z0 + half); r1 = new Vector3((x0 + x1) * 0.5f, yr, z1 - half); }
        if (w >= d)
        {
            Quad(a, b, r1, r0, new Vector2(0, 0), new Vector2(w / T, 0), new Vector2((w - half) / T, sl), new Vector2(half / T, sl), new Vector3(0, 1, -1));
            Quad(e, c, r1, r0, new Vector2(0, 0), new Vector2(w / T, 0), new Vector2((w - half) / T, sl), new Vector2(half / T, sl), new Vector3(0, 1, 1));
            Tri(a, e, r0, new Vector2(0, 0), new Vector2(d / T, 0), new Vector2(d * 0.5f / T, sl), new Vector3(-1, 1, 0));
            Tri(b, c, r1, new Vector2(0, 0), new Vector2(d / T, 0), new Vector2(d * 0.5f / T, sl), new Vector3(1, 1, 0));
        }
        else
        {
            Quad(a, e, r1, r0, new Vector2(0, 0), new Vector2(d / T, 0), new Vector2((d - half) / T, sl), new Vector2(half / T, sl), new Vector3(-1, 1, 0));
            Quad(b, c, r1, r0, new Vector2(0, 0), new Vector2(d / T, 0), new Vector2((d - half) / T, sl), new Vector2(half / T, sl), new Vector3(1, 1, 0));
            Tri(a, b, r0, new Vector2(0, 0), new Vector2(w / T, 0), new Vector2(w * 0.5f / T, sl), new Vector3(0, 1, -1));
            Tri(e, c, r1, new Vector2(0, 0), new Vector2(w / T, 0), new Vector2(w * 0.5f / T, sl), new Vector3(0, 1, 1));
        }
    }

    static void Cylinder(float cx, float cz, float r, float y0, float y1, int sides, int mat, int capMat, float tile)
    {
        M(mat);
        var c = new Vector3(cx, 0f, cz);
        float circ = 2f * Mathf.PI * r;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = c + new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r), p1 = c + new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
            float u0 = circ * i / sides / tile, u1 = circ * (i + 1) / sides / tile;
            Vector3 hint = (p0 + p1) * 0.5f - c;
            // smooth radial normals for a round look
            int i0 = Vtx(new Vector3(p0.x, y0, p0.z), (p0 - c).normalized, new Vector2(u0, y0 / tile));
            int i1 = Vtx(new Vector3(p0.x, y1, p0.z), (p0 - c).normalized, new Vector2(u0, y1 / tile));
            int i2 = Vtx(new Vector3(p1.x, y1, p1.z), (p1 - c).normalized, new Vector2(u1, y1 / tile));
            int i3 = Vtx(new Vector3(p1.x, y0, p1.z), (p1 - c).normalized, new Vector2(u1, y0 / tile));
            TriIdx(i0, i1, i2, hint); TriIdx(i0, i2, i3, hint);
        }
        if (capMat >= 0)
        {
            M(capMat);
            int ic = Vtx(new Vector3(cx, y1, cz), Vector3.up, new Vector2(cx / tile, cz / tile));
            int first = cur.V.Count;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float px = cx + Mathf.Cos(a0) * r, pz = cz + Mathf.Sin(a0) * r;
                Vtx(new Vector3(px, y1, pz), Vector3.up, new Vector2(px / tile, pz / tile));
            }
            for (int i = 0; i < sides; i++) TriIdx(ic, first + i, first + (i + 1) % sides, Vector3.up);
        }
    }

    static void Cone(float cx, float cz, float r, float y0, float h, int sides, int mat)
    {
        M(mat);
        var apex = new Vector3(cx, y0 + h, cz);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = new Vector3(cx + Mathf.Cos(a0) * r, y0, cz + Mathf.Sin(a0) * r), p1 = new Vector3(cx + Mathf.Cos(a1) * r, y0, cz + Mathf.Sin(a1) * r);
            Vector3 mid = (p0 + p1) * 0.5f - new Vector3(cx, y0, cz);
            Tri(p0, p1, apex, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, h / 4f), mid + Vector3.up * (r / Mathf.Max(h, 0.1f)));
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // TREES, CARS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>A broadleaf tree: short trunk + a two-ring low-poly canopy with smooth
    /// radial normals (reads as a round crown under the sun).</summary>
    static void Broadleaf(float x, float z, float gy, float h)
    {
        if (h > MaxHeight(x, z) || AirfieldClear(x, z)) return;
        float cr = h * R(0.30f, 0.40f);
        float trunkH = h * 0.35f;
        Use(Lyr.Tree, x, z, mTrunk);
        float tr = Mathf.Max(0.18f, h * 0.025f);
        Quad(new Vector3(x - tr, gy - 0.5f, z), new Vector3(x - tr, gy + trunkH + 0.5f, z), new Vector3(x + tr, gy + trunkH + 0.5f, z), new Vector3(x + tr, gy - 0.5f, z),
             Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.back);
        Quad(new Vector3(x, gy - 0.5f, z - tr), new Vector3(x, gy + trunkH + 0.5f, z - tr), new Vector3(x, gy + trunkH + 0.5f, z + tr), new Vector3(x, gy - 0.5f, z + tr),
             Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.left);
        Quad(new Vector3(x - tr, gy - 0.5f, z), new Vector3(x - tr, gy + trunkH + 0.5f, z), new Vector3(x + tr, gy + trunkH + 0.5f, z), new Vector3(x + tr, gy - 0.5f, z),
             Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.forward);
        Quad(new Vector3(x, gy - 0.5f, z - tr), new Vector3(x, gy + trunkH + 0.5f, z - tr), new Vector3(x, gy + trunkH + 0.5f, z + tr), new Vector3(x, gy - 0.5f, z + tr),
             Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.right);

        M(mLeaf[RI(0, mLeaf.Length)]);
        float yc = gy + trunkH + (h - trunkH) * 0.5f;
        float halfH = (h - trunkH) * 0.5f;
        var ctr = new Vector3(x, yc, z);
        float rot = R(0f, 6.283f);
        int sides = 6;
        int bot = Vtx(new Vector3(x, gy + trunkH * 0.85f, z), Vector3.down, new Vector2(0.5f, 0f));
        int top = Vtx(new Vector3(x, gy + h, z), Vector3.up, new Vector2(0.5f, 1f));
        int r1 = cur.V.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = rot + i * 6.2832f / sides;
            float rr = cr * R(0.85f, 1.1f);
            var p = new Vector3(x + Mathf.Cos(a) * rr, yc - halfH * 0.35f, z + Mathf.Sin(a) * rr);
            Vtx(p, (p - ctr).normalized, new Vector2(i / (float)sides * 2f, 0.3f));
        }
        int r2 = cur.V.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = rot + (i + 0.5f) * 6.2832f / sides;
            float rr = cr * R(0.70f, 0.95f);
            var p = new Vector3(x + Mathf.Cos(a) * rr, yc + halfH * 0.45f, z + Mathf.Sin(a) * rr);
            Vtx(p, (p - ctr).normalized, new Vector2((i + 0.5f) / sides * 2f, 0.7f));
        }
        for (int i = 0; i < sides; i++)
        {
            int a0 = r1 + i, a1 = r1 + (i + 1) % sides, b0 = r2 + i, bm = r2 + (i + sides - 1) % sides;
            TriIdx(bot, a0, a1, (cur.V[a0] + cur.V[a1]) * 0.5f - ctr + Vector3.down * halfH);
            TriIdx(a0, a1, b0, (cur.V[a0] + cur.V[a1] + cur.V[b0]) / 3f - ctr);
            TriIdx(a0, b0, bm, (cur.V[a0] + cur.V[b0] + cur.V[bm]) / 3f - ctr);
            int b1 = r2 + (i + 1) % sides;
            TriIdx(b0, b1, top, (cur.V[b0] + cur.V[b1]) * 0.5f - ctr + Vector3.up * halfH);
        }
        nTrees++;
    }

    /// <summary>A conifer: thin trunk + two stacked 7-sided cones.</summary>
    static void Conifer(float x, float z, float gy, float h)
    {
        if (h > MaxHeight(x, z) || AirfieldClear(x, z)) return;
        Use(Lyr.Tree, x, z, mConifer);
        float r = h * R(0.20f, 0.26f);
        Cone(x, z, r, gy + h * 0.12f, h * 0.60f, 7, mConifer);
        Cone(x, z, r * 0.72f, gy + h * 0.45f, h * 0.55f, 7, mConifer);
        M(mTrunk);
        float tr = 0.25f;
        Quad(new Vector3(x - tr, gy - 0.5f, z), new Vector3(x - tr, gy + h * 0.2f, z), new Vector3(x + tr, gy + h * 0.2f, z), new Vector3(x + tr, gy - 0.5f, z),
             Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.back);
        Quad(new Vector3(x, gy - 0.5f, z - tr), new Vector3(x, gy + h * 0.2f, z - tr), new Vector3(x, gy + h * 0.2f, z + tr), new Vector3(x, gy - 0.5f, z + tr),
             Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.left);
        nTrees++;
    }

    static void TreeOnPlain(float x, float z, float gy, bool city)
    {
        float h = city ? R(7f, 12f) : R(8f, 16f);
        if (!city && R() < 0.18f) Conifer(x, z, gy, h * 1.2f);
        else Broadleaf(x, z, gy, h);
    }

    /// <summary>A car (or a truck) standing on y, axis-aligned: alongX = long axis on X.</summary>
    static void Car(float x, float z, float y, bool alongX)
    {
        Use(Lyr.Prop, x, z, mCar[RI(0, mCar.Length)]);
        float L = R(4.1f, 4.8f), W = 1.8f;
        float hx = alongX ? L * 0.5f : W * 0.5f, hz = alongX ? W * 0.5f : L * 0.5f;
        int body = mCar[RI(0, mCar.Length)];
        SlabBox(x - hx, z - hz, x + hx, z + hz, y + 0.25f, y + 0.95f, body, body, 4f);
        float cx = alongX ? L * 0.26f : W * 0.46f, cz = alongX ? W * 0.46f : L * 0.26f;
        float sh = (R() < 0.5f ? 0.3f : -0.2f);
        float ox = alongX ? sh : 0f, oz = alongX ? 0f : sh;
        SlabBox(x - cx + ox, z - cz + oz, x + cx + ox, z + cz + oz, y + 0.95f, y + 1.45f, mCarGlass, body, 4f);
        nCars++;
    }

    static void Truck(float x, float z, float y, bool alongX, float dirSign)
    {
        Use(Lyr.Prop, x, z, mTruck);
        float W = 2.5f;
        float tl = 13.6f, cl = 2.4f;
        // trailer
        float hx = alongX ? tl * 0.5f : W * 0.5f, hz = alongX ? W * 0.5f : tl * 0.5f;
        SlabBox(x - hx, z - hz, x + hx, z + hz, y + 1.1f, y + 4.0f, mTruck, mTruck, 4f);
        // cab ahead of the trailer
        float off = (tl * 0.5f + cl * 0.5f + 0.4f) * dirSign;
        float cx = x + (alongX ? off : 0f), cz = z + (alongX ? 0f : off);
        float chx = alongX ? cl * 0.5f : W * 0.5f, chz = alongX ? W * 0.5f : cl * 0.5f;
        int cab = mCar[RI(0, mCar.Length)];
        SlabBox(cx - chx, cz - chz, cx + chx, cz + chz, y + 0.5f, y + 3.4f, cab, cab, 4f);
        nCars++;
    }

    // ════════════════════════════════════════════════════════════════════════
    // BUILD
    // ════════════════════════════════════════════════════════════════════════
    const float YField = 0.03f, YDirt = 0.05f, YRoad = 0.06f, YHwy = 0.10f, YRail = 0.12f;
    const float YWalk = 0.22f, YLot = 0.30f;

    public static void Build(Transform parent)
    {
        var t0 = System.DateTime.Now;
        rng = new System.Random(20260929);
        bufs = new Dictionary<long, Buf>();
        occ = new bool[OccN * OccN];
        nBuildings = nTrees = nCars = nFields = 0;
        terrain = Terrain.activeTerrain;
        BuildMaterials();

        // aerodrome reserve: runway strip, taxiways, apron and the airport buildings
        Mark(-300f, -1000f, 150f, 430f);

        BuildTransport();
        BuildAirport();
        BuildEastCity();
        BuildWestSuburb();
        BuildIndustrial();
        BuildFarmland();
        BuildHillForests();

        int tris = Emit(parent);
        Debug.Log($"[CITY] built {nBuildings} buildings, {nTrees} trees, {nCars} vehicles, {nFields} fields; " +
                  $"{bufs.Count} chunk meshes, {tris / 1000}k triangles, {(System.DateTime.Now - t0).TotalSeconds:F1}s");
        bufs = null; occ = null; cur = null; curIdx = null;
    }

    static int Emit(Transform parent)
    {
        var root = new GameObject("City").transform;
        root.SetParent(parent);
        int tris = 0;
        foreach (var b in bufs.Values)
        {
            if (b.V.Count == 0) continue;
            var mesh = new Mesh { name = $"City_{b.layer}_{b.cx}_{b.cz}", indexFormat = b.V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(b.V); mesh.SetNormals(b.N); mesh.SetUVs(0, b.U);
            var keys = new List<int>(b.S.Keys);
            keys.RemoveAll(k => b.S[k].Count == 0);
            mesh.subMeshCount = keys.Count;
            var rmats = new Material[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                mesh.SetTriangles(b.S[keys[i]], i, false);
                rmats[i] = mats[keys[i]];
                tris += b.S[keys[i]].Count / 3;
            }
            mesh.RecalculateBounds();
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = rmats;
            if (b.layer == Lyr.Ground) { mr.shadowCastingMode = ShadowCastingMode.Off; }
            if (b.layer == Lyr.Solid || b.layer == Lyr.Tree)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                SurfaceTag.Add(go, SurfaceKind.Obstacle);
            }
        }
        return tris;
    }

    // ── ROADS ───────────────────────────────────────────────────────────────

    /// <summary>A road segment along Z (x centre, width w) from z0 to z1.</summary>
    static void RoadZ(float x, float z0, float z1, float w, int mat, float y)
    {
        if (z1 - z0 < 0.1f) return;
        for (float s = z0; s < z1 - 0.01f; s += 100f)
        {
            float e = Mathf.Min(z1, s + 100f);
            Use(Lyr.Ground, x, (s + e) * 0.5f, mat);
            Quad(new Vector3(x - w * 0.5f, y, s), new Vector3(x + w * 0.5f, y, s), new Vector3(x + w * 0.5f, y, e), new Vector3(x - w * 0.5f, y, e),
                 new Vector2(0, s / 12f), new Vector2(1, s / 12f), new Vector2(1, e / 12f), new Vector2(0, e / 12f), Vector3.up);
        }
        Mark(x - w * 0.5f, z0, x + w * 0.5f, z1);
    }

    static void RoadX(float z, float x0, float x1, float w, int mat, float y)
    {
        if (x1 - x0 < 0.1f) return;
        for (float s = x0; s < x1 - 0.01f; s += 100f)
        {
            float e = Mathf.Min(x1, s + 100f);
            Use(Lyr.Ground, (s + e) * 0.5f, z, mat);
            Quad(new Vector3(s, y, z + w * 0.5f), new Vector3(s, y, z - w * 0.5f), new Vector3(e, y, z - w * 0.5f), new Vector3(e, y, z + w * 0.5f),
                 new Vector2(0, s / 12f), new Vector2(1, s / 12f), new Vector2(1, e / 12f), new Vector2(0, e / 12f), Vector3.up);
        }
        Mark(x0, z - w * 0.5f, x1, z + w * 0.5f);
    }

    static void Zebra(float x0, float z0, float x1, float z1, bool acrossX)
    {
        Use(Lyr.Ground, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f, mZebra);
        const float T = 1.2f;
        if (acrossX)   // bars stacked across X (road runs along Z)
            Quad(new Vector3(x0, YRoad + 0.005f, z0), new Vector3(x1, YRoad + 0.005f, z0), new Vector3(x1, YRoad + 0.005f, z1), new Vector3(x0, YRoad + 0.005f, z1),
                 new Vector2(x0 / T, 0), new Vector2(x1 / T, 0), new Vector2(x1 / T, 1), new Vector2(x0 / T, 1), Vector3.up);
        else
            Quad(new Vector3(x0, YRoad + 0.005f, z0), new Vector3(x1, YRoad + 0.005f, z0), new Vector3(x1, YRoad + 0.005f, z1), new Vector3(x0, YRoad + 0.005f, z1),
                 new Vector2(z0 / T, 0), new Vector2(z0 / T, 1), new Vector2(z1 / T, 1), new Vector2(z1 / T, 0), Vector3.up);
    }

    /// <summary>Cars along a road segment. Left-hand traffic (India).</summary>
    static void TrafficZ(float x, float z0, float z1, float w, float density)
    {
        int n = Mathf.RoundToInt((z1 - z0) / 30f * density * R(0.5f, 1.5f));
        for (int i = 0; i < n; i++)
        {
            float side = R() < 0.5f ? -1f : 1f;
            float l = w >= 20f ? (R() < 0.5f ? 2.8f : 7.6f) : 2.9f;
            float z = R(z0 + 4f, z1 - 4f);
            if (R() < 0.08f) Truck(x + side * l, z, YRoad, false, -side);
            else Car(x + side * l, z, YRoad, false);
        }
    }

    static void TrafficX(float z, float x0, float x1, float w, float density)
    {
        int n = Mathf.RoundToInt((x1 - x0) / 30f * density * R(0.5f, 1.5f));
        for (int i = 0; i < n; i++)
        {
            float side = R() < 0.5f ? -1f : 1f;
            float l = w >= 20f ? (R() < 0.5f ? 2.8f : 7.6f) : 2.9f;
            float x = R(x0 + 4f, x1 - 4f);
            if (R() < 0.08f) Truck(x, z + side * l, YRoad, true, side);
            else Car(x, z + side * l, YRoad, true);
        }
    }

    const float HwyZ = 620f, RailZ = 672f;

    static void BuildTransport()
    {
        // dual carriageway: two 9 m carriageways around a 2 m median, run in 50 m pieces
        // wherever the ground is flat.
        for (float x = -2450f; x < 2450f; x += 50f)
        {
            if (!Flat(x, HwyZ - 12f, x + 50f, HwyZ + 12f, 0f)) continue;
            Use(Lyr.Ground, x + 25f, HwyZ, mHwy);
            float e = x + 50f;
            // south carriageway: inside (median) edge at z=619 -> u=0, outer at 610 -> u=1
            Quad(new Vector3(x, YHwy, HwyZ - 1f), new Vector3(e, YHwy, HwyZ - 1f), new Vector3(e, YHwy, HwyZ - 10f), new Vector3(x, YHwy, HwyZ - 10f),
                 new Vector2(0, x / 12f), new Vector2(0, e / 12f), new Vector2(1, e / 12f), new Vector2(1, x / 12f), Vector3.up);
            Quad(new Vector3(x, YHwy, HwyZ + 1f), new Vector3(e, YHwy, HwyZ + 1f), new Vector3(e, YHwy, HwyZ + 10f), new Vector3(x, YHwy, HwyZ + 10f),
                 new Vector2(0, x / 12f), new Vector2(0, e / 12f), new Vector2(1, e / 12f), new Vector2(1, x / 12f), Vector3.up);
            // concrete median barrier
            M(mConcrete);
            SlabBox(x, HwyZ - 0.3f, e, HwyZ + 0.3f, YHwy, YHwy + 0.8f, mConcrete, mConcrete, 3f);
            Mark(x, HwyZ - 12f, e, HwyZ + 12f);
            // traffic: eastbound on the north carriageway (keep left)
            int n = RI(0, 4);
            for (int i = 0; i < n; i++)
            {
                bool north = R() < 0.5f;
                float zc = north ? HwyZ + (R() < 0.5f ? 3.4f : 7.2f) : HwyZ - (R() < 0.5f ? 3.4f : 7.2f);
                float xc = R(x + 4f, e - 4f);
                if (R() < 0.2f) Truck(xc, zc, YHwy, true, north ? 1f : -1f);
                else Car(xc, zc, YHwy, true);
            }

            // railway alongside, with a gravel bed
            if (Flat(x, RailZ - 8f, e, RailZ + 8f, 0f))
            {
                Use(Lyr.Ground, x + 25f, RailZ, mRail);
                Quad(new Vector3(x, YRail, RailZ - 5f), new Vector3(e, YRail, RailZ - 5f), new Vector3(e, YRail, RailZ + 5f), new Vector3(x, YRail, RailZ + 5f),
                     new Vector2(0, x / 5f), new Vector2(0, e / 5f), new Vector2(1, e / 5f), new Vector2(1, x / 5f), Vector3.up);
                Mark(x, RailZ - 7f, e, RailZ + 7f);
            }
        }

        // a passenger train on the railway
        for (int c = 0; c < 7; c++)
        {
            float x0 = 520f + c * 21f;
            Use(Lyr.Prop, x0, RailZ, mTrain);
            SlabBox(x0, RailZ - 3f - 1.45f, x0 + 20f, RailZ - 3f + 1.45f, YRail + 0.9f, YRail + 4.2f, mTrain, mMembraneLight, 4f);
        }

        // airport access road: car park -> west -> north to the highway
        RoadX(-470f, -420f, -290f, 11f, mRoad1, YRoad);
        RoadZ(-420f, -475.5f, HwyZ - 10f, 11f, mRoad1, YRoad);
        Use(Lyr.Ground, -420f, -470f, mAsph);
        HQuad(-425.5f, -475.5f, -414.5f, -464.5f, YRoad + 0.002f, 8f);
        TrafficZ(-420f, -460f, 600f, 11f, 0.4f);
    }

    // ── AIRPORT BUILDINGS ───────────────────────────────────────────────────

    static void BuildAirport()
    {
        // two hangars west of the apron (apron x -115..-45, z -615..-545)
        foreach (float hz in new[] { -596f, -552f })
        {
            float x0 = -208f, x1 = -170f, z0 = hz - 18f, z1 = hz + 18f;
            Use(Lyr.Solid, (x0 + x1) * 0.5f, hz, fShed[0].mat);
            Walls(x0, z0, x1, z1, YLot, 11f, fShed[hz < -570f ? 1 : 0], YLot);
            // barrel-ish roof: shallow gable along x
            GableRoof(x0, z0, x1, z1, 11f, 12f, true, mMembraneLight, fShed[0], 0.3f);
            nBuildings++;
        }
        // hangar forecourt: concrete from the hangar doors to the apron edge
        Use(Lyr.Ground, -140f, -575f, mConcrete);
        HQuad(-170f, -614f, -116f, -534f, 0.34f, 3f);

        // terminal (two storeys, glazed, with a canopy)
        Use(Lyr.Solid, -190f, -480f, fGlass[2].mat);
        FlatBlock(-225f, -500f, -160f, -462f, 0.3f, 8.1f, fGlass[2], 0.3f, mMembrane, 0.9f);
        FlatBlock(-218f, -493f, -188f, -470f, 8.1f, 11.7f, fOffice[0], 8.1f, mMembrane, 0.6f);
        nBuildings++;

        // control tower: concrete shaft + glazed cab + roof
        Use(Lyr.Solid, -250f, -525f, mConcrete);
        Cylinder(-250f, -525f, 2.8f, 0.3f, 21f, 12, mConcrete, -1, 3f);
        Cylinder(-250f, -525f, 4.6f, 21f, 21.6f, 12, mConcrete, mConcrete, 3f);
        Cylinder(-250f, -525f, 4.4f, 21.6f, 24.6f, 12, mCabGlass, -1, 3f);
        Cylinder(-250f, -525f, 5.0f, 24.6f, 25.2f, 12, mCoping, mMembrane, 3f);
        nBuildings++;

        // car park in front of the terminal with parked cars
        Use(Lyr.Ground, -260f, -470f, mPark);
        ParkingLot(-290f, -460f, -228f, -410f);
        Use(Lyr.Ground, -250f, -470f, mWalk);
        HQuad(-290f, -464.5f, -160f, -460f, YWalk, 3f);
    }

    /// <summary>A parking lot rectangle: bay stripes run along Z, cars 55 % occupied.</summary>
    static void ParkingLot(float x0, float z0, float x1, float z1)
    {
        Use(Lyr.Ground, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f, mPark);
        Quad(new Vector3(x0, YLot - 0.04f, z0), new Vector3(x1, YLot - 0.04f, z0), new Vector3(x1, YLot - 0.04f, z1), new Vector3(x0, YLot - 0.04f, z1),
             new Vector2(x0 / 5f, (z0 - z0) / 16f), new Vector2(x1 / 5f, 0), new Vector2(x1 / 5f, (z1 - z0) / 16f), new Vector2(x0 / 5f, (z1 - z0) / 16f), Vector3.up);
        for (float zr = z0; zr + 16f <= z1 + 0.01f; zr += 16f)
            foreach (float zc in new[] { zr + 2.5f, zr + 13.5f })
                for (float xc = Mathf.Ceil(x0 / 2.5f) * 2.5f + 1.25f; xc < x1 - 1.3f; xc += 2.5f)
                    if (R() < 0.55f)
                    {
                        // a car is 1.8 m wide in a 2.5 m bay, parked nose-in along Z
                        Use(Lyr.Prop, xc, zc, mCar[0]);
                        float L = R(4.1f, 4.7f);
                        int body = mCar[RI(0, mCar.Length)];
                        SlabBox(xc - 0.9f, zc - L * 0.5f, xc + 0.9f, zc + L * 0.5f, YLot + 0.2f, YLot + 0.9f, body, body, 4f);
                        SlabBox(xc - 0.82f, zc - 1.1f, xc + 0.82f, zc + 1.0f, YLot + 0.9f, YLot + 1.4f, mCarGlass, body, 4f);
                        nCars++;
                    }
    }

    // ── GRID DISTRICTS ──────────────────────────────────────────────────────

    struct Line { public float c, w; public bool ave; }

    static List<Line> Lines(float from, float to, float pMin, float pMax, float street, float avenue, int aveEvery)
    {
        var l = new List<Line>();
        int k = 0;
        for (float c = from; c <= to; c += R(pMin, pMax), k++)
        {
            bool ave = aveEvery > 0 && k % aveEvery == 0;
            l.Add(new Line { c = c, w = ave ? avenue : street, ave = ave });
        }
        return l;
    }

    delegate void BlockFn(float x0, float z0, float x1, float z1, int i, int j);

    /// <summary>Lay out a grid: decide which blocks exist (flat + free), draw the roads
    /// that bound any existing block, then build every block with `fn`.</summary>
    static void Grid(List<Line> xs, List<Line> zs, BlockFn fn, float crossDensity, float traffic, bool connectSouth)
    {
        int nx = xs.Count - 1, nz = zs.Count - 1;
        var exists = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float x0 = xs[i].c + xs[i].w * 0.5f, x1 = xs[i + 1].c - xs[i + 1].w * 0.5f;
                float z0 = zs[j].c + zs[j].w * 0.5f, z1 = zs[j + 1].c - zs[j + 1].w * 0.5f;
                exists[i, j] = x1 - x0 > 20f && z1 - z0 > 20f && Flat(x0, z0, x1, z1, 30f) && Free(x0, z0, x1, z1);
            }
        bool E(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && exists[i, j];

        // roads along Z (x-lines), segment j between z-lines j and j+1
        var segZ = new bool[nx + 1, nz];
        var segX = new bool[nx, nz + 1];
        for (int i = 0; i <= nx; i++) for (int j = 0; j < nz; j++) segZ[i, j] = E(i - 1, j) || E(i, j);
        for (int i = 0; i < nx; i++) for (int j = 0; j <= nz; j++) segX[i, j] = E(i, j - 1) || E(i, j);

        for (int i = 0; i <= nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (!segZ[i, j]) continue;
                var L = xs[i];
                float z0 = zs[j].c + zs[j].w * 0.5f, z1 = zs[j + 1].c - zs[j + 1].w * 0.5f;
                int mat = L.ave ? mRoad2 : mRoad1;
                bool zeb = crossDensity > 0f && R() < crossDensity;
                float zc0 = zeb ? z0 + 4f : z0, zc1 = zeb ? z1 - 4f : z1;
                RoadZ(L.c, zc0, zc1, L.w, mat, YRoad);
                if (zeb)
                {
                    Zebra(L.c - L.w * 0.5f, z0, L.c + L.w * 0.5f, z0 + 4f, true);
                    Zebra(L.c - L.w * 0.5f, z1 - 4f, L.c + L.w * 0.5f, z1, true);
                    Mark(L.c - L.w * 0.5f, z0, L.c + L.w * 0.5f, z1);
                }
                TrafficZ(L.c, z0, z1, L.w, traffic * (L.ave ? 1.6f : 1f));
            }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                if (!segX[i, j]) continue;
                var L = zs[j];
                float x0 = xs[i].c + xs[i].w * 0.5f, x1 = xs[i + 1].c - xs[i + 1].w * 0.5f;
                int mat = L.ave ? mRoad2 : mRoad1;
                bool zeb = crossDensity > 0f && R() < crossDensity;
                float xc0 = zeb ? x0 + 4f : x0, xc1 = zeb ? x1 - 4f : x1;
                RoadX(L.c, xc0, xc1, L.w, mat, YRoad);
                if (zeb)
                {
                    Zebra(x0, L.c - L.w * 0.5f, x0 + 4f, L.c + L.w * 0.5f, false);
                    Zebra(x1 - 4f, L.c - L.w * 0.5f, x1, L.c + L.w * 0.5f, false);
                    Mark(x0, L.c - L.w * 0.5f, x1, L.c + L.w * 0.5f);
                }
                TrafficX(L.c, x0, x1, L.w, traffic * (L.ave ? 1.6f : 1f));
            }
        // intersections
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                bool any = (j < nz && segZ[i, j]) || (j > 0 && segZ[i, j - 1]) || (i < nx && segX[i, j]) || (i > 0 && segX[i - 1, j]);
                if (!any) continue;
                float hx = xs[i].w * 0.5f, hz = zs[j].w * 0.5f;
                Use(Lyr.Ground, xs[i].c, zs[j].c, mAsph);
                HQuad(xs[i].c - hx, zs[j].c - hz, xs[i].c + hx, zs[j].c + hz, YRoad, 8f);
                Mark(xs[i].c - hx, zs[j].c - hz, xs[i].c + hx, zs[j].c + hz);
            }
        // connectors from the avenues on the southern edge down to the highway
        if (connectSouth)
            for (int i = 0; i <= nx; i++)
            {
                if (!xs[i].ave) continue;
                bool touches = (i < nx && E(i, 0)) || (i > 0 && E(i - 1, 0));
                if (!touches) continue;
                float zTop = zs[0].c - zs[0].w * 0.5f;
                if (zTop > HwyZ + 10f && Flat(xs[i].c - 10f, HwyZ, xs[i].c + 10f, zTop, 0f))
                    RoadZ(xs[i].c, HwyZ + 10f, zTop, xs[i].w, mRoad2, YRoad);
            }

        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (!exists[i, j]) continue;
                float x0 = xs[i].c + xs[i].w * 0.5f, x1 = xs[i + 1].c - xs[i + 1].w * 0.5f;
                float z0 = zs[j].c + zs[j].w * 0.5f, z1 = zs[j + 1].c - zs[j + 1].w * 0.5f;
                Mark(x0, z0, x1, z1);
                fn(x0, z0, x1, z1, i, j);
            }
    }

    /// <summary>Block base: kerbed sidewalk slab, then the raised lot inside it.</summary>
    static void BlockBase(float x0, float z0, float x1, float z1, float walk, int lotMat, float lotTile)
    {
        Use(Lyr.Ground, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f, mWalk);
        SlabBox(x0, z0, x1, z1, YRoad - 0.02f, YWalk, mConcrete, mWalk, 3f);
        if (lotMat >= 0)
        {
            SlabBox(x0 + walk, z0 + walk, x1 - walk, z1 - walk, YWalk - 0.02f, YLot, mConcrete, lotMat, lotTile);
        }
    }

    static void StreetTrees(float x0, float z0, float x1, float z1, float inset, float spacing)
    {
        for (float x = x0 + 6f; x < x1 - 6f; x += spacing * R(0.85f, 1.15f))
        {
            Broadleaf(x, z0 + inset, YWalk, R(7f, 11f));
            Broadleaf(x, z1 - inset, YWalk, R(7f, 11f));
        }
        for (float z = z0 + 10f; z < z1 - 10f; z += spacing * R(0.85f, 1.15f))
        {
            Broadleaf(x0 + inset, z, YWalk, R(7f, 11f));
            Broadleaf(x1 - inset, z, YWalk, R(7f, 11f));
        }
    }

    // ── EAST CITY ───────────────────────────────────────────────────────────
    static readonly Vector2 CBD = new Vector2(820f, 1720f);

    static bool stadiumBuilt;

    static void BuildEastCity()
    {
        stadiumBuilt = false;
        var xs = Lines(320f, 2380f, 88f, 118f, 14f, 22f, 4);
        var zs = Lines(710f, 2400f, 78f, 104f, 14f, 22f, 4);
        Grid(xs, zs, EastBlock, 0.55f, 0.55f, true);
    }

    static void EastBlock(float x0, float z0, float x1, float z1, int i, int j)
    {
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float r = Vector2.Distance(new Vector2(cx, cz), CBD);
        float roll = R();
        if (r < 340f)
        {
            if (roll < 0.07f) { Park(x0, z0, x1, z1, true); return; }
            Downtown(x0, z0, x1, z1, r);
        }
        else if (r < 760f)
        {
            if (!stadiumBuilt && r > 420f && x1 - x0 > 80f && z1 - z0 > 70f) { Stadium(x0, z0, x1, z1); stadiumBuilt = true; return; }
            if (roll < 0.08f) { Park(x0, z0, x1, z1, false); return; }
            if (roll < 0.12f) { Civic(x0, z0, x1, z1); return; }
            Perimeter(x0, z0, x1, z1, r);
        }
        else
        {
            if (roll < 0.07f) { Park(x0, z0, x1, z1, false); return; }
            if (roll < 0.13f) { Retail(x0, z0, x1, z1); return; }
            if (roll < 0.16f) { School(x0, z0, x1, z1); return; }
            HouseBlock(x0, z0, x1, z1, 2f);
        }
    }

    static void Downtown(float x0, float z0, float x1, float z1, float r)
    {
        const float walk = 4.5f;
        BlockBase(x0, z0, x1, z1, walk, mPlaza, 3f);
        StreetTrees(x0, z0, x1, z1, 1.6f, 11f);
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        bool splitX = (lx1 - lx0) >= (lz1 - lz0);
        float span = splitX ? lx1 - lx0 : lz1 - lz0;
        int parcels = Mathf.Clamp(Mathf.FloorToInt(span / R(30f, 45f)), 1, 3);
        float tallBase = Mathf.Lerp(125f, 55f, Mathf.Clamp01(r / 340f));
        for (int p = 0; p < parcels; p++)
        {
            float a = p / (float)parcels, b = (p + 1) / (float)parcels;
            float px0 = splitX ? Mathf.Lerp(lx0, lx1, a) : lx0, px1 = splitX ? Mathf.Lerp(lx0, lx1, b) : lx1;
            float pz0 = splitX ? lz0 : Mathf.Lerp(lz0, lz1, a), pz1 = splitX ? lz1 : Mathf.Lerp(lz0, lz1, b);
            if (p > 0) { if (splitX) px0 += 1.5f; else pz0 += 1.5f; }
            if (p < parcels - 1) { if (splitX) px1 -= 1.5f; else pz1 -= 1.5f; }
            float pcx = (px0 + px1) * 0.5f, pcz = (pz0 + pz1) * 0.5f;
            float cap = MaxHeight(pcx, pcz);

            // podium: shopfront ground floor + 1-2 office floors
            int podFloors = RI(1, 3);
            float podTop = YLot + 4.5f + podFloors * 3.6f;
            Use(Lyr.Solid, pcx, pcz, fShop.mat);
            Walls(px0, pz0, px1, pz1, YLot, YLot + 4.5f, fShop, YLot);
            var podFac = R() < 0.5f ? Pick(fOffice) : Pick(fBrick);
            FlatBlock(px0, pz0, px1, pz1, YLot + 4.5f, podTop, podFac, YLot + 4.5f, mGravel, 0.8f);
            nBuildings++;

            // tower
            float inset = R(4f, 8f);
            float tx0 = px0 + inset, tx1 = px1 - inset, tz0 = pz0 + inset, tz1 = pz1 - inset;
            if (tx1 - tx0 < 14f || tz1 - tz0 < 14f) continue;
            float want = tallBase * R(0.55f, 1.1f);
            int floors = Mathf.FloorToInt((Mathf.Min(want, cap - 4f) - podTop) / 3.6f);
            if (floors < 3) continue;
            float top = podTop + floors * 3.6f;
            var tf = R() < 0.65f ? Pick(fGlass) : Pick(fOffice);
            if (floors > 10 && R() < 0.5f)
            {
                // setback: the upper third steps in
                float mid = podTop + Mathf.Round(floors * 0.65f) * 3.6f;
                FlatBlock(tx0, tz0, tx1, tz1, podTop, mid, tf, podTop, mGravel, 0.6f);
                float s = Mathf.Min(3.5f, (tx1 - tx0) * 0.15f);
                tx0 += s; tx1 -= s; tz0 += s; tz1 -= s;
                FlatBlock(tx0, tz0, tx1, tz1, mid, top, tf, podTop, mMembrane, 1.2f);
            }
            else FlatBlock(tx0, tz0, tx1, tz1, podTop, top, tf, podTop, mMembrane, 1.2f);
            // rooftop plant
            int hv = RI(1, 4);
            for (int k = 0; k < hv; k++)
            {
                float w = R(3f, 7f), d = R(3f, 6f);
                float hx = R(tx0 + 1.5f, Mathf.Max(tx0 + 1.6f, tx1 - w - 1.5f)), hz = R(tz0 + 1.5f, Mathf.Max(tz0 + 1.6f, tz1 - d - 1.5f));
                SlabBox(hx, hz, hx + w, hz + d, top, top + R(1.8f, 3.2f), mHvac, mHvac, 3f);
            }
            if (top + 8f < cap && R() < 0.3f)
                SlabBox(pcx - 0.3f, pcz - 0.3f, pcx + 0.3f, pcz + 0.3f, top, top + R(5f, 8f), mHvac, mHvac, 3f);   // mast
        }
    }

    /// <summary>A European-style perimeter block: buildings around the edge, split into
    /// individually-heighted segments, around a green courtyard.</summary>
    static void Perimeter(float x0, float z0, float x1, float z1, float r)
    {
        const float walk = 3f;
        BlockBase(x0, z0, x1, z1, walk, mLawn, 2.5f);
        if (R() < 0.6f) StreetTrees(x0, z0, x1, z1, 1.3f, 13f);
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        float D = R(11f, 14f);
        if (lx1 - lx0 < 2f * D + 10f || lz1 - lz0 < 2f * D + 10f) { Downtown(x0, z0, x1, z1, 340f); return; }
        int maxSt = r < 500f ? 8 : 6;
        bool shops = r < 560f;
        // wings: south & north full width, west & east between them
        Wing(lx0, lz0, lx1, lz0 + D, true, maxSt, shops, new Vector2(0, -1));
        Wing(lx0, lz1 - D, lx1, lz1, true, maxSt, shops, new Vector2(0, 1));
        Wing(lx0, lz0 + D, lx0 + D, lz1 - D, false, maxSt, shops, new Vector2(-1, 0));
        Wing(lx1 - D, lz0 + D, lx1, lz1 - D, false, maxSt, shops, new Vector2(1, 0));
        // courtyard
        float cx0 = lx0 + D + 4f, cx1 = lx1 - D - 4f, cz0 = lz0 + D + 4f, cz1 = lz1 - D - 4f;
        int n = RI(2, 7);
        for (int k = 0; k < n && cx1 > cx0 && cz1 > cz0; k++) Broadleaf(R(cx0, cx1), R(cz0, cz1), YLot, R(7f, 12f));
    }

    static void Wing(float x0, float z0, float x1, float z1, bool alongX, int maxSt, bool shops, Vector2 street)
    {
        float len = alongX ? x1 - x0 : z1 - z0;
        float s = 0f;
        while (s < len - 0.5f)
        {
            float segLen = Mathf.Min(len - s, R(14f, 30f));
            if (len - s - segLen < 10f) segLen = len - s;
            float a0 = (alongX ? x0 : z0) + s, a1 = a0 + segLen;
            float bx0 = alongX ? a0 : x0, bx1 = alongX ? a1 : x1, bz0 = alongX ? z0 : a0, bz1 = alongX ? z1 : a1;
            float cx = (bx0 + bx1) * 0.5f, cz = (bz0 + bz1) * 0.5f;
            int st = RI(3, maxSt + 1);
            float cap = MaxHeight(cx, cz);
            while (st > 1 && YLot + 4.5f + (st - 1) * 3.6f + 5f > cap) st--;
            Fac f;
            float pick = R();
            if (pick < 0.35f) f = Pick(fBrick);
            else if (pick < 0.65f) f = Pick(fPlaster);
            else if (pick < 0.85f) f = Pick(fBalcony);
            else f = Pick(fOffice);
            Use(Lyr.Solid, cx, cz, f.mat);
            float yb = YLot;
            if (shops && R() < 0.7f)
            {
                Walls(bx0, bz0, bx1, bz1, YLot, YLot + 4.5f, fShop, YLot);
                yb = YLot + 4.5f; st--;
            }
            float top = yb + st * 3.6f;
            if (R() < 0.35f)
            {
                Walls(bx0, bz0, bx1, bz1, yb, top, f, yb);
                HipRoof(bx0, bz0, bx1, bz1, top, R(25f, 35f), R() < 0.6f ? mTileTerra : mTileSlate, 0.4f);
            }
            else FlatBlock(bx0, bz0, bx1, bz1, yb, top, f, yb, mGravel, 0.8f);
            nBuildings++;
            s += segLen;
        }
    }

    static void Park(float x0, float z0, float x1, float z1, bool urban)
    {
        const float walk = 3f;
        BlockBase(x0, z0, x1, z1, walk, mLawn, 2.5f);
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        float cx = (lx0 + lx1) * 0.5f, cz = (lz0 + lz1) * 0.5f;
        // cross paths
        Use(Lyr.Ground, cx, cz, mPlaza);
        HQuad(cx - 2f, lz0, cx + 2f, lz1, YLot + 0.01f, 3f);
        HQuad(lx0, cz - 2f, lx1, cz + 2f, YLot + 0.012f, 3f);
        bool pond = R() < 0.4f;
        float pw = (lx1 - lx0) * 0.18f, pd = (lz1 - lz0) * 0.18f;
        float px = cx + (lx1 - lx0) * 0.24f, pz = cz + (lz1 - lz0) * 0.24f;
        if (pond)
        {
            Use(Lyr.Ground, px, pz, mWater);
            Octagon(px, pz, pw, pd, YLot + 0.02f);
        }
        else if (urban)
        {
            // fountain plaza in the middle
            Use(Lyr.Ground, cx, cz, mPlaza);
            HQuad(cx - 12f, cz - 12f, cx + 12f, cz + 12f, YLot + 0.015f, 3f);
            Use(Lyr.Ground, cx, cz, mWater);
            Octagon(cx, cz, 5f, 5f, YLot + 0.25f);
        }
        float area = (lx1 - lx0) * (lz1 - lz0);
        int n = Mathf.RoundToInt(area / (urban ? 260f : 200f));
        for (int k = 0; k < n; k++)
        {
            float x = R(lx0 + 3f, lx1 - 3f), z = R(lz0 + 3f, lz1 - 3f);
            if (Mathf.Abs(x - cx) < 4f || Mathf.Abs(z - cz) < 4f) continue;
            if (pond && Mathf.Abs(x - px) < pw + 3f && Mathf.Abs(z - pz) < pd + 3f) continue;
            if (urban && Mathf.Abs(x - cx) < 14f && Mathf.Abs(z - cz) < 14f) continue;
            TreeOnPlain(x, z, YLot, true);
        }
    }

    /// <summary>A football ground filling the block: raked elliptical stands around a
    /// pitch, a concrete outer wall and a cantilevered roof ring over the upper tiers.</summary>
    static void Stadium(float x0, float z0, float x1, float z1)
    {
        const float walk = 3f;
        BlockBase(x0, z0, x1, z1, walk, mPlaza, 3f);
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float oa = (x1 - x0) * 0.5f - walk - 2f, ob = (z1 - z0) * 0.5f - walk - 2f;   // outer semi-axes
        float ia = oa * 0.70f, ib = ob * 0.66f;                                          // inner (pitch edge)
        float h = Mathf.Min(16f, MaxHeight(cx, cz) - 6f);
        if (h < 8f) { Park(x0, z0, x1, z1, false); return; }
        const int N = 40;
        Use(Lyr.Solid, cx, cz, mSeats);
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
            Vector3 in0 = new Vector3(cx + Mathf.Cos(a0) * ia, YLot + 1.2f, cz + Mathf.Sin(a0) * ib);
            Vector3 in1 = new Vector3(cx + Mathf.Cos(a1) * ia, YLot + 1.2f, cz + Mathf.Sin(a1) * ib);
            Vector3 out0 = new Vector3(cx + Mathf.Cos(a0) * oa, YLot + h, cz + Mathf.Sin(a0) * ob);
            Vector3 out1 = new Vector3(cx + Mathf.Cos(a1) * oa, YLot + h, cz + Mathf.Sin(a1) * ob);
            Vector3 inward = new Vector3(cx, 0f, cz) - (in0 + in1) * 0.5f; inward.y = 0f;
            float arc = (in1 - in0).magnitude;
            // raked seating, facing up and in
            M(mSeats);
            Quad(in0, in1, out1, out0, new Vector2(0, 0), new Vector2(arc / 6f, 0), new Vector2(arc / 6f, 3f), new Vector2(0, 3f),
                 inward.normalized + Vector3.up);
            // pitch-side wall
            M(mConcrete);
            Quad(new Vector3(in0.x, YLot, in0.z), in0, in1, new Vector3(in1.x, YLot, in1.z),
                 Vector2.zero, new Vector2(0, 0.4f), new Vector2(arc / 3f, 0.4f), new Vector2(arc / 3f, 0), inward);
            // outer facade
            Quad(new Vector3(out0.x, YLot, out0.z), out0, out1, new Vector3(out1.x, YLot, out1.z),
                 Vector2.zero, new Vector2(0, h / 3f), new Vector2(arc / 3f, h / 3f), new Vector2(arc / 3f, 0), -inward);
            // roof ring over the upper 45 % of the stand
            Vector3 r0 = Vector3.Lerp(in0, out0, 0.55f), r1 = Vector3.Lerp(in1, out1, 0.55f);
            r0.y = r1.y = YLot + h + 3.5f;
            Vector3 o0 = out0, o1 = out1; o0.y = o1.y = YLot + h + 3.5f;
            M(mMembraneLight);
            Quad(r0, r1, o1, o0, Vector2.zero, new Vector2(1, 0), Vector2.one, new Vector2(0, 1), Vector3.up);
            Quad(r0, r1, o1, o0, Vector2.zero, new Vector2(1, 0), Vector2.one, new Vector2(0, 1), Vector3.down);
            M(mCoping);
            Quad(out0, o0, o1, out1, Vector2.zero, new Vector2(0, 0.2f), new Vector2(1, 0.2f), new Vector2(1, 0), -inward);
        }
        nBuildings++;
        // pitch inside the bowl
        float pw = ia * 1.55f, pd = Mathf.Min(ib * 1.5f, pw * 0.66f);
        Use(Lyr.Ground, cx, cz, mPitch);
        Quad(new Vector3(cx - pw * 0.5f, YLot + 0.01f, cz - pd * 0.5f), new Vector3(cx + pw * 0.5f, YLot + 0.01f, cz - pd * 0.5f),
             new Vector3(cx + pw * 0.5f, YLot + 0.01f, cz + pd * 0.5f), new Vector3(cx - pw * 0.5f, YLot + 0.01f, cz + pd * 0.5f),
             new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Vector3.up);
        Use(Lyr.Ground, cx, cz, mLawn);
        Octagon(cx, cz, ia, ib, YLot + 0.005f);
        // floodlight masts at the four corners of the block
        foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
        {
            float mx = cx + sx * (oa + 0.5f), mz = cz + sz * (ob + 0.5f);
            float mh = Mathf.Min(h + 16f, MaxHeight(mx, mz) - 2f);
            Use(Lyr.Solid, mx, mz, mHvac);
            SlabBox(mx - 0.5f, mz - 0.5f, mx + 0.5f, mz + 0.5f, YLot, YLot + mh, mHvac, mHvac, 3f);
            SlabBox(mx - 2.5f, mz - 0.6f, mx + 2.5f, mz + 0.6f, YLot + mh, YLot + mh + 2.2f, mHvac, mHvac, 3f);
        }
    }

    static void Octagon(float cx, float cz, float rx, float rz, float y)
    {
        int c = Vtx(new Vector3(cx, y, cz), Vector3.up, new Vector2(cx / 8f, cz / 8f));
        int first = cur.V.Count;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            float x = cx + Mathf.Cos(a) * rx, z = cz + Mathf.Sin(a) * rz;
            Vtx(new Vector3(x, y, z), Vector3.up, new Vector2(x / 8f, z / 8f));
        }
        for (int i = 0; i < 12; i++) TriIdx(c, first + i, first + (i + 1) % 12, Vector3.up);
    }

    static void Retail(float x0, float z0, float x1, float z1)
    {
        const float walk = 2.5f;
        BlockBase(x0, z0, x1, z1, walk, mConcrete, 3f);
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        // store at the back (north) 55 %, car park in front
        float split = Mathf.Lerp(lz0, lz1, 0.45f);
        float bx0 = lx0 + 4f, bx1 = lx1 - 4f, bz0 = split + 4f, bz1 = lz1 - 4f;
        if (bx1 - bx0 > 20f && bz1 - bz0 > 15f)
        {
            var f = Pick(fShed);
            Use(Lyr.Solid, (bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f, f.mat);
            Wall(new Vector2(bx0, bz0), new Vector2(bx1, bz0), YLot, YLot + 4.5f, fShop, YLot, new Vector2((bx0 + bx1) * 0.5f, bz1));
            Wall(new Vector2(bx0, bz0), new Vector2(bx1, bz0), YLot + 4.5f, YLot + 8f, f, YLot, new Vector2((bx0 + bx1) * 0.5f, bz1));
            var c = new Vector2((bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f);
            Wall(new Vector2(bx1, bz0), new Vector2(bx1, bz1), YLot, YLot + 8f, f, YLot, c);
            Wall(new Vector2(bx1, bz1), new Vector2(bx0, bz1), YLot, YLot + 8f, f, YLot, c);
            Wall(new Vector2(bx0, bz1), new Vector2(bx0, bz0), YLot, YLot + 8f, f, YLot, c);
            M(mMembraneLight);
            HQuad(bx0, bz0, bx1, bz1, YLot + 8f, 16f);
            for (int k = 0; k < 4; k++)
            {
                float hx = R(bx0 + 3f, bx1 - 8f), hz = R(bz0 + 3f, bz1 - 6f);
                SlabBox(hx, hz, hx + R(3f, 5f), hz + R(2f, 4f), YLot + 8f, YLot + 9.8f, mHvac, mHvac, 3f);
            }
            nBuildings++;
        }
        ParkingLot(lx0 + 3f, lz0 + 3f, lx1 - 3f, lz0 + 3f + Mathf.Floor((split - lz0 - 3f) / 16f) * 16f);
    }

    static void Civic(float x0, float z0, float x1, float z1)
    {
        const float walk = 4f;
        BlockBase(x0, z0, x1, z1, walk, mPlaza, 3f);
        float lx0 = x0 + walk + 6f, lz0 = z0 + walk + 6f, lx1 = x1 - walk - 6f, lz1 = z1 - walk - 6f;
        var f = R() < 0.5f ? fStone : fPlaster[1];
        float h = YLot + 3 * 3.6f;
        // U-shaped civic building around a forecourt facing south
        float d = Mathf.Min(16f, (lz1 - lz0) * 0.35f);
        Use(Lyr.Solid, (lx0 + lx1) * 0.5f, (lz0 + lz1) * 0.5f, f.mat);
        Walls(lx0, lz1 - d, lx1, lz1, YLot, h, f, YLot);
        HipRoof(lx0, lz1 - d, lx1, lz1, h, 28f, mTileSlate, 0.5f);
        Walls(lx0, lz0, lx0 + d, lz1 - d, YLot, h, f, YLot);
        HipRoof(lx0, lz0, lx0 + d, lz1 - d, h, 28f, mTileSlate, 0.5f);
        Walls(lx1 - d, lz0, lx1, lz1 - d, YLot, h, f, YLot);
        HipRoof(lx1 - d, lz0, lx1, lz1 - d, h, 28f, mTileSlate, 0.5f);
        nBuildings++;
        float cx = (lx0 + lx1) * 0.5f;
        for (float z = lz0 + 4f; z < lz1 - d - 4f; z += 9f)
        {
            Broadleaf(cx - 9f, z, YLot, R(6f, 9f));
            Broadleaf(cx + 9f, z, YLot, R(6f, 9f));
        }
    }

    static void School(float x0, float z0, float x1, float z1)
    {
        const float walk = 2.5f;
        BlockBase(x0, z0, x1, z1, walk, mLawn, 2.5f);
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        float w = lx1 - lx0, d = lz1 - lz0;
        // a pitch if it fits (scaled to the lot), school building on the remaining strip
        float pw = Mathf.Min(100f, w - 8f), pd = pw * 0.65f;
        if (pd > d * 0.62f) { pd = d * 0.62f; pw = pd / 0.65f; }
        float px0 = lx0 + (w - pw) * 0.5f, pz0 = lz0 + 4f;
        Use(Lyr.Ground, px0 + pw * 0.5f, pz0 + pd * 0.5f, mPitch);
        Quad(new Vector3(px0, YLot + 0.01f, pz0), new Vector3(px0 + pw, YLot + 0.01f, pz0), new Vector3(px0 + pw, YLot + 0.01f, pz0 + pd), new Vector3(px0, YLot + 0.01f, pz0 + pd),
             new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Vector3.up);
        float bz0 = pz0 + pd + 6f, bz1 = lz1 - 3f;
        if (bz1 - bz0 > 10f)
        {
            var f = Pick(fBrick);
            float bx1 = lx0 + w * 0.7f;
            Use(Lyr.Solid, (lx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f, f.mat);
            FlatBlock(lx0 + 4f, bz0, bx1, bz1, YLot, YLot + 2 * 3.6f, f, YLot, mGravel, 0.7f);
            nBuildings++;
        }
    }

    // ── HOUSES ──────────────────────────────────────────────────────────────

    /// <summary>Detached houses in two rows along the long axis of the block, each
    /// facing its street, with lawns, driveways, back-garden trees and the odd pool.</summary>
    static void HouseBlock(float x0, float z0, float x1, float z1, float walk)
    {
        BlockBase(x0, z0, x1, z1, walk, mLawn, 2.5f);
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        bool alongX = (lx1 - lx0) >= (lz1 - lz0);
        float len = alongX ? lx1 - lx0 : lz1 - lz0;
        float depth = alongX ? lz1 - lz0 : lx1 - lx0;
        bool twoRows = depth > 40f;
        for (int row = 0; row < (twoRows ? 2 : 1); row++)
        {
            float rowDepth = twoRows ? depth * 0.5f : depth;
            float s = 0f;
            while (s < len - 12f)
            {
                float lotW = Mathf.Min(len - s, R(14f, 19f));
                if (len - s - lotW < 12f) lotW = len - s;
                // lot-local frame: a along the street, b away from it
                float a0 = s, a1 = s + lotW;
                float hw = Mathf.Min(lotW - 3.5f, R(8.5f, 12.5f));
                float hd = Mathf.Min(rowDepth - 14f, R(8f, 11f));
                if (hd < 6f) { s += lotW; continue; }
                float setback = R(5f, 7.5f);
                bool driveLeft = R() < 0.5f;
                float ha0 = driveLeft ? a1 - 1.2f - hw : a0 + 1.2f, ha1 = ha0 + hw;
                float hb0 = setback, hb1 = setback + hd;
                // map local (a along, b depth-from-street) to world
                LotRect(alongX, row, lx0, lz0, lx1, lz1, ha0, hb0, ha1, hb1, out float wx0, out float wz0, out float wx1, out float wz1);
                House(wx0, wz0, wx1, wz1, alongX);
                // driveway
                float da0 = driveLeft ? a0 + 1f : a1 - 4.2f, da1 = da0 + 3.2f;
                LotRect(alongX, row, lx0, lz0, lx1, lz1, da0, 0f, da1, setback + hd * 0.6f, out float dx0, out float dz0, out float dx1, out float dz1);
                Use(Lyr.Ground, (dx0 + dx1) * 0.5f, (dz0 + dz1) * 0.5f, mConcrete);
                HQuad(dx0, dz0, dx1, dz1, YLot + 0.01f, 3f);
                if (R() < 0.45f) Car((dx0 + dx1) * 0.5f, (dz0 + dz1) * 0.5f + (alongX ? 0f : 0f), YLot, !alongX);
                // back garden
                float gb0 = hb1 + 2f, gb1 = rowDepth - 1.5f;
                if (gb1 - gb0 > 4f)
                {
                    if (R() < 0.10f && gb1 - gb0 > 7f && lotW > 12f)
                    {
                        LotRect(alongX, row, lx0, lz0, lx1, lz1, a0 + 3f, gb0 + 1f, a0 + 3f + R(7f, 9f), gb0 + 1f + 4f, out float px0, out float pz0, out float px1, out float pz1);
                        Use(Lyr.Ground, (px0 + px1) * 0.5f, (pz0 + pz1) * 0.5f, mPlaza);
                        HQuad(px0 - 1f, pz0 - 1f, px1 + 1f, pz1 + 1f, YLot + 0.01f, 3f);
                        M(mWater);
                        HQuad(px0, pz0, px1, pz1, YLot + 0.02f, 3f);
                    }
                    int nt = RI(0, 3);
                    for (int k = 0; k < nt; k++)
                    {
                        LotPt(alongX, row, lx0, lz0, lx1, lz1, R(a0 + 1.5f, a1 - 1.5f), R(gb0 + 1f, gb1 - 0.5f), out float tx, out float tz);
                        TreeOnPlain(tx, tz, YLot, true);
                    }
                }
                // front tree
                if (R() < 0.35f)
                {
                    LotPt(alongX, row, lx0, lz0, lx1, lz1, driveLeft ? a0 + 1.5f + lotW * 0.4f : a1 - 1.5f - lotW * 0.4f, 2.2f, out float tx, out float tz);
                    Broadleaf(tx, tz, YLot, R(5f, 8f));
                }
                s += lotW;
            }
        }
    }
    static void LotPt(bool alongX, int row, float lx0, float lz0, float lx1, float lz1, float a, float b, out float x, out float z)
    {
        if (alongX) { x = lx0 + a; z = row == 0 ? lz0 + b : lz1 - b; }
        else { z = lz0 + a; x = row == 0 ? lx0 + b : lx1 - b; }
    }

    /// <summary>Lot-local to world: `a` along the street from the block's min corner,
    /// `b` inward from the street for this row (row 0 faces the min-side street).</summary>
    static void LotRect(bool alongX, int row, float lx0, float lz0, float lx1, float lz1,
                        float a0, float b0, float a1, float b1, out float x0, out float z0, out float x1, out float z1)
    {
        if (alongX)
        {
            x0 = lx0 + a0; x1 = lx0 + a1;
            if (row == 0) { z0 = lz0 + b0; z1 = lz0 + b1; }
            else { z0 = lz1 - b1; z1 = lz1 - b0; }
        }
        else
        {
            z0 = lz0 + a0; z1 = lz0 + a1;
            if (row == 0) { x0 = lx0 + b0; x1 = lx0 + b1; }
            else { x0 = lx1 - b1; x1 = lx1 - b0; }
        }
        if (x1 < x0) { var t = x0; x0 = x1; x1 = t; }
        if (z1 < z0) { var t = z0; z0 = z1; z1 = t; }
    }

    static void House(float x0, float z0, float x1, float z1, bool streetAlongX)
    {
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        int st = R() < 0.55f ? 2 : 1;
        var f = fSiding; f.mat = mSidingTint[RI(0, mSidingTint.Length)];
        if (R() < 0.3f) f = Pick(fBrick);
        float top = YLot + 0.25f + st * 3f;
        if (top + 4f > MaxHeight(cx, cz)) return;
        Use(Lyr.Solid, cx, cz, f.mat);
        // a plinth, then the walls
        SlabBox(x0, z0, x1, z1, YLot, YLot + 0.25f, mConcrete, mConcrete, 3f);
        Walls(x0, z0, x1, z1, YLot + 0.25f, top, f, YLot + 0.25f);
        int roof = R() < 0.5f ? mTileTerra : R() < 0.5f ? mTileSlate : mTileBrown;
        float roll = R();
        if (roll < 0.55f) GableRoof(x0, z0, x1, z1, top, R(28f, 40f), streetAlongX, roof, f, 0.45f);
        else if (roll < 0.92f) HipRoof(x0, z0, x1, z1, top, R(24f, 32f), roof, 0.45f);
        else FlatBlock(x0, z0, x1, z1, top - 0.01f, top, f, YLot + 0.25f, mGravel, 0.5f);
        // an attached garage, one storey, on some houses
        nBuildings++;
    }

    // ── WEST SUBURB ─────────────────────────────────────────────────────────
    static readonly Vector2 TownCentre = new Vector2(-1100f, 1450f);
    static bool churchBuilt;

    static void BuildWestSuburb()
    {
        var xs = Lines(-2420f, -150f, 118f, 142f, 12f, 20f, 5);
        var zs = Lines(710f, 2400f, 56f, 64f, 12f, 20f, 6);
        // the last x-line must not reach into the departure green corridor
        if (xs[xs.Count - 1].c > -150f) xs.RemoveAt(xs.Count - 1);
        churchBuilt = false;
        Grid(xs, zs, WestBlock, 0.12f, 0.25f, true);
    }

    static void WestBlock(float x0, float z0, float x1, float z1, int i, int j)
    {
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float r = Vector2.Distance(new Vector2(cx, cz), TownCentre);
        float roll = R();
        if (r < 150f && !churchBuilt) { Church(x0, z0, x1, z1); churchBuilt = true; return; }
        if (r < 260f)
        {
            if (roll < 0.25f) { Retail(x0, z0, x1, z1); return; }
            if (roll < 0.35f) { Park(x0, z0, x1, z1, true); return; }
            Perimeter(x0, z0, x1, z1, 600f);
            return;
        }
        if (roll < 0.06f) { Park(x0, z0, x1, z1, false); return; }
        if (roll < 0.075f) { School(x0, z0, x1, z1); return; }
        HouseBlock(x0, z0, x1, z1, 2f);
    }

    static void Church(float x0, float z0, float x1, float z1)
    {
        const float walk = 3f;
        BlockBase(x0, z0, x1, z1, walk, mLawn, 2.5f);
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float nl = Mathf.Min(34f, x1 - x0 - 30f), nw = 13f;
        float nx0 = cx - nl * 0.5f, nx1 = cx + nl * 0.5f, nz0 = cz - nw * 0.5f, nz1 = cz + nw * 0.5f;
        Use(Lyr.Solid, cx, cz, fStone.mat);
        Walls(nx0, nz0, nx1, nz1, YLot, YLot + 11f, fStone, YLot);
        GableRoof(nx0, nz0, nx1, nz1, YLot + 11f, 50f, true, mTileSlate, fStone, 0.4f);
        // west tower + spire
        float t = 7f, tx1 = nx0, tx0 = nx0 - t;
        Walls(tx0, cz - t * 0.5f, tx1, cz + t * 0.5f, YLot, YLot + 24f, fStone, YLot);
        M(mCoping);
        HQuad(tx0, cz - t * 0.5f, tx1, cz + t * 0.5f, YLot + 24f, 4f);
        Cone((tx0 + tx1) * 0.5f, cz, t * 0.62f, YLot + 24f, 16f, 8, mTileSlate);
        nBuildings++;
        Use(Lyr.Ground, cx, cz, mPlaza);
        HQuad(x0 + walk, cz - 2f, tx0, cz + 2f, YLot + 0.01f, 3f);
        for (int k = 0; k < 10; k++)
        {
            float x = R(x0 + 6f, x1 - 6f), z = R(z0 + 6f, z1 - 6f);
            if (Mathf.Abs(z - cz) < nw * 0.5f + 4f && x > tx0 - 4f && x < nx1 + 4f) continue;
            Broadleaf(x, z, YLot, R(8f, 13f));
        }
    }

    // ── INDUSTRIAL ──────────────────────────────────────────────────────────
    static void BuildIndustrial()
    {
        var xs = Lines(420f, 2150f, 130f, 170f, 12f, 16f, 3);
        var zs = Lines(-1150f, 540f, 110f, 150f, 12f, 16f, 3);
        Grid(xs, zs, IndustrialBlock, 0f, 0.2f, false);
        // connect the industrial grid to the highway
        float zTop = zs[zs.Count - 1].c + zs[zs.Count - 1].w * 0.5f;
        foreach (var L in xs)
            if (L.ave && Flat(L.c - 10f, zTop, L.c + 10f, HwyZ - 10f, 0f))
                RoadZ(L.c, zTop, HwyZ - 10f, L.w, mRoad1, YRoad);
    }

    static void IndustrialBlock(float x0, float z0, float x1, float z1, int i, int j)
    {
        const float walk = 2f;
        float roll = R();
        float lx0 = x0 + walk, lz0 = z0 + walk, lx1 = x1 - walk, lz1 = z1 - walk;
        if (roll < 0.14f)
        {
            // tank farm on gravel
            BlockBase(x0, z0, x1, z1, walk, mYard, 12f);
            float r = R(7f, 11f);
            for (float x = lx0 + r + 6f; x < lx1 - r - 4f; x += r * 2f + 8f)
                for (float z = lz0 + r + 6f; z < lz1 - r - 4f; z += r * 2f + 8f)
                {
                    float h = R(9f, 15f);
                    if (h > MaxHeight(x, z)) continue;
                    Use(Lyr.Solid, x, z, mTank);
                    Cylinder(x, z, r, YLot, YLot + h, 16, mTank, mTank, 6f);
                    nBuildings++;
                }
            return;
        }
        if (roll < 0.28f)
        {
            // low office campus + parking
            BlockBase(x0, z0, x1, z1, walk, mLawn, 2.5f);
            var f = Pick(fOffice);
            float bx0 = lx0 + 8f, bx1 = Mathf.Lerp(lx0, lx1, 0.55f), bz0 = lz0 + 8f, bz1 = lz1 - 8f;
            Use(Lyr.Solid, (bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f, f.mat);
            FlatBlock(bx0, bz0, bx1, bz1, YLot, YLot + RI(2, 5) * 3.6f, f, YLot, mMembraneLight, 0.8f);
            nBuildings++;
            ParkingLot(bx1 + 8f, lz0 + 6f, lx1 - 6f, lz0 + 6f + Mathf.Floor((lz1 - lz0 - 12f) / 16f) * 16f);
            return;
        }
        if (roll < 0.35f)
        {
            BlockBase(x0, z0, x1, z1, walk, mDirt, 12f);   // cleared plot
            return;
        }
        // warehouses with a truck yard in front
        BlockBase(x0, z0, x1, z1, walk, mConcrete, 3f);
        int sheds = (lx1 - lx0) > 120f ? 2 : 1;
        float yardD = Mathf.Min(38f, (lz1 - lz0) * 0.35f);
        float sw = (lx1 - lx0 - 6f * (sheds + 1)) / sheds;
        for (int s = 0; s < sheds; s++)
        {
            float bx0 = lx0 + 6f + s * (sw + 6f), bx1 = bx0 + sw;
            float bz0 = lz0 + yardD, bz1 = lz1 - 6f;
            float h = R(8f, 13f);
            var f = Pick(fShed);
            Use(Lyr.Solid, (bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f, f.mat);
            Walls(bx0, bz0, bx1, bz1, YLot, YLot + h, f, YLot);
            if (R() < 0.5f) { M(mMembraneLight); HQuad(bx0, bz0, bx1, bz1, YLot + h, 16f); }
            else GableRoof(bx0, bz0, bx1, bz1, YLot + h, 6f, false, mMembraneLight, f, 0.3f);
            nBuildings++;
            // docked trucks, noses south
            int nt = RI(1, 5);
            for (int k = 0; k < nt; k++)
            {
                float tx = bx0 + 6f + k * 5f;
                if (tx > bx1 - 4f) break;
                Truck(tx, bz0 - 8.5f, YLot, false, -1f);
            }
        }
        Use(Lyr.Ground, (lx0 + lx1) * 0.5f, lz0 + yardD * 0.5f, mAsph);
        HQuad(lx0 + 3f, lz0 + 3f, lx1 - 3f, lz0 + yardD - 1f, YLot + 0.01f, 8f);
    }

    // ── FARMLAND ────────────────────────────────────────────────────────────
    static void BuildFarmland()
    {
        // the plain: rows of fields of varying depth, split into varying widths
        for (float z = -2400f; z < 2400f;)
        {
            float rowD = R(90f, 170f);
            for (float x = -2400f; x < 2400f;)
            {
                float w = R(110f, 240f);
                FieldCell(x, z, x + w, z + rowD, false);
                x += w;
            }
            z += rowD;
        }
        // under the long approach (the flat corridor |x|<260 out to 10 km)
        for (float z = -2400f; z > -9800f;)
        {
            float rowD = R(100f, 180f);
            float xs = -250f;
            while (xs < 250f)
            {
                float w = Mathf.Min(250f - xs, R(120f, 260f));
                if (250f - xs - w < 60f) w = 250f - xs;
                FieldCell(xs, z - rowD, xs + w, z, true);
                xs += w;
            }
            z -= rowD;
        }
    }

    static void FieldCell(float x0, float z0, float x1, float z1, bool corridor)
    {
        if (!corridor && !Flat(x0, z0, x1, z1, 0f)) return;
        if (!Free(x0, z0, x1, z1)) return;
        Mark(x0, z0, x1, z1);
        float roll = R();
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        bool bigEnough = x1 - x0 > 110f && z1 - z0 > 100f;
        if (roll < 0.06f && !corridor && !AirfieldClear(cx, cz))
        {
            // woodlot
            for (float x = x0 + 4f; x < x1 - 4f; x += R(7f, 11f))
                for (float z = z0 + 4f; z < z1 - 4f; z += R(7f, 11f))
                    if (R() < 0.8f) TreeOnPlain(x + R(-2f, 2f), z + R(-2f, 2f), 0f, false);
            return;
        }
        int mat = mFields[WeightedField()];
        Use(Lyr.Ground, cx, cz, mat);
        if (R() < 0.5f) HQuad(x0 + 1.5f, z0 + 1.5f, x1 - 1.5f, z1 - 1.5f, YField, 12f);
        else HQuadRot(x0 + 1.5f, z0 + 1.5f, x1 - 1.5f, z1 - 1.5f, YField, 12f);
        nFields++;
        // hedgerows along the south and west edges
        if (R() < 0.4f) Hedge(x0, z0, x1, z0, true);
        if (R() < 0.4f) Hedge(x0, z0, x0, z1, false);
        if (bigEnough && roll > 0.93f && !AirfieldClear(cx, cz) && !AirfieldClear(x0, z0)) Farmstead(x0 + R(8f, 20f), z0 + R(8f, 20f));
    }

    static int WeightedField()
    {
        float r = R();
        if (r < 0.14f) return 0;
        if (r < 0.40f) return 1;
        if (r < 0.62f) return 2;
        if (r < 0.80f) return 3;
        if (r < 0.90f) return 4;
        return 5;
    }

    static void Hedge(float x0, float z0, float x1, float z1, bool alongX)
    {
        float len = alongX ? x1 - x0 : z1 - z0;
        for (float s = 3f; s < len - 3f; s += R(6f, 11f))
        {
            if (R() < 0.15f) { s += R(10f, 30f); continue; }
            float x = alongX ? x0 + s : x0 + R(-0.8f, 0.8f), z = alongX ? z0 + R(-0.8f, 0.8f) : z0 + s;
            TreeOnPlain(x, z, 0f, false);
        }
    }

    static void Farmstead(float x0, float z0)
    {
        float yw = 48f, yd = 38f;
        Use(Lyr.Ground, x0 + yw * 0.5f, z0 + yd * 0.5f, mYard);
        HQuad(x0, z0, x0 + yw, z0 + yd, YDirt, 12f);
        // barn
        float bx0 = x0 + 4f, bz0 = z0 + 4f, bx1 = bx0 + 24f, bz1 = bz0 + 13f;
        Use(Lyr.Solid, (bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f, fShedRed.mat);
        Walls(bx0, bz0, bx1, bz1, YDirt, 6.5f, fShedRed, YDirt);
        GableRoof(bx0, bz0, bx1, bz1, 6.5f, 30f, true, R() < 0.5f ? mTileSlate : mMembraneLight, fShedRed, 0.4f);
        nBuildings++;
        // silos
        int ns = RI(1, 3);
        for (int k = 0; k < ns; k++)
        {
            float sx = bx1 + 5f + k * 8f, sz = bz0 + 3.5f;
            Use(Lyr.Solid, sx, sz, mTank);
            Cylinder(sx, sz, 3.2f, YDirt, 13f, 12, mTank, -1, 4f);
            Cone(sx, sz, 3.3f, 13f, 2.2f, 12, mHvac);
            nBuildings++;
        }
        // farmhouse
        float hx0 = x0 + 6f, hz0 = z0 + 24f;
        House(hx0, hz0, hx0 + 11f, hz0 + 9f, true);
        for (int k = 0; k < 5; k++) Broadleaf(x0 + R(-8f, yw + 8f), z0 + yd + R(1f, 10f), 0f, R(8f, 14f));
    }

    // ── HILL FORESTS ────────────────────────────────────────────────────────
    static float TerrainY(float x, float z)
    {
        if (terrain != null) return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        return WorldBuilder.SampleGroundY(x, z);
    }

    static void BuildHillForests()
    {
        const float step = 17f;
        for (float z = -5800f; z < 5800f; z += step)
            for (float x = -5800f; x < 5800f; x += step)
            {
                float px = x + R(-step * 0.45f, step * 0.45f), pz = z + R(-step * 0.45f, step * 0.45f);
                float rr = Mathf.Sqrt(px * px + pz * pz);
                if (rr < 2550f) continue;
                if (Mathf.Abs(px) < 340f && pz < 0f) continue;   // keep the approach corridor clear
                float mask = Mathf.PerlinNoise(px * 0.0011f + 5.3f, pz * 0.0011f + 9.1f) * 0.75f
                           + Mathf.PerlinNoise(px * 0.006f + 1.7f, pz * 0.006f + 3.3f) * 0.25f;
                if (mask < 0.47f) continue;
                float gy = WorldBuilder.SampleGroundY(px, pz);
                if (gy < 4f || gy > 700f) continue;
                float gx = WorldBuilder.SampleGroundY(px + 8f, pz) - gy, gz = WorldBuilder.SampleGroundY(px, pz + 8f) - gy;
                if (Mathf.Sqrt(gx * gx + gz * gz) / 8f > 0.75f) continue;
                if (R() < 0.15f) continue;
                float ty = TerrainY(px, pz);
                float h = R(10f, 18f) * (1f - gy / 1600f);
                bool conifer = R() < Mathf.Lerp(0.25f, 0.9f, Mathf.InverseLerp(80f, 450f, gy));
                if (conifer) Conifer(px, pz, ty, h * 1.15f);
                else Broadleaf(px, pz, ty, h);
            }
    }
}
