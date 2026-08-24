using UnityEngine;

/// <summary>
/// Places CC0 Kenney models (loaded from Resources/Models via the editor's import)
/// onto the terrain. Each instance is auto-scaled to a target height from its
/// renderer bounds, so the kit's native units don't matter, then seated on the
/// ground. If the models aren't present/imported (e.g. headless), every Try*
/// returns false and WorldBuilder falls back to its primitive buildings/trees.
/// </summary>
public static class ModelLibrary
{
    static readonly string[] City = { "building-a", "building-b", "building-c", "building-d", "building-e", "building-f" };
    static readonly string[] Houses = { "building-type-a", "building-type-b", "building-type-c", "building-type-d", "building-type-e" };
    static readonly string[] Trees = { "tree_default", "tree_cone", "tree_blocks", "tree_default_dark", "tree_cone_dark" };
    static readonly string[] Rocks = { "cliff_block_rock", "cliff_blockSlope_rock", "cliff_blockHalf_rock" };

    static int available = -1;
    public static bool Available
    {
        get
        {
            if (available < 0) available = Resources.Load<GameObject>("Models/city/building-a") != null ? 1 : 0;
            return available == 1;
        }
    }

    public static bool TryBuilding(Transform parent, float x, float z, float gy, float targetH, bool house)
        => Place(parent, house ? "houses" : "city", house ? Houses : City, x, z, gy, targetH, true) != null;

    public static bool TryTree(Transform parent, float x, float z, float gy, float targetH)
        => Place(parent, "nature", Trees, x, z, gy, targetH, false) != null;

    public static bool TryRock(Transform parent, float x, float z, float gy, float targetH)
        => Place(parent, "nature", Rocks, x, z, gy, targetH, true) != null;

    static GameObject Place(Transform parent, string cat, string[] names, float x, float z, float gy, float targetH, bool obstacle)
    {
        if (!Available) return null;
        var prefab = Resources.Load<GameObject>("Models/" + cat + "/" + names[Random.Range(0, names.Length)]);
        if (prefab == null) return null;

        var inst = Object.Instantiate(prefab, parent);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        // local bounds at scale 1 (object at world origin, identity)
        if (!CombinedBounds(inst, out Bounds b0)) { Object.Destroy(inst); return null; }
        float scale = targetH / Mathf.Max(0.01f, b0.size.y);

        if (obstacle)
        {
            var bc = inst.AddComponent<BoxCollider>();   // local box -> scales/rotates with the transform
            bc.center = b0.center;
            bc.size = b0.size;
            SurfaceTag.Add(inst, SurfaceKind.Obstacle);
        }

        inst.transform.localScale = Vector3.one * scale;
        inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // seat the base on the ground and centre it at (x, z)
        CombinedBounds(inst, out Bounds bw);
        inst.transform.position += new Vector3(x - bw.center.x, gy - bw.min.y, z - bw.center.z);
        inst.name = cat + "_model";
        return inst;
    }

    static bool CombinedBounds(GameObject g, out Bounds b)
    {
        var rends = g.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { b = new Bounds(Vector3.zero, Vector3.zero); return false; }
        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return true;
    }
}
