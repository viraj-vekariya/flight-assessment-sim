using UnityEngine;

/// <summary>
/// Gives the ocean a subtle sense of motion: slowly scrolls the water surface
/// texture offset. Combined with the material's high smoothness this produces a
/// moving sun glint instead of a dead flat blue fill.
/// </summary>
public class WaterAnimator : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.010f, 0.006f);
    Material mat;
    Vector2 off;

    void Start()
    {
        var r = GetComponent<Renderer>();
        if (r != null) mat = r.material;
    }

    void Update()
    {
        if (mat == null) return;
        off += speed * Time.deltaTime;
        mat.mainTextureOffset = off;
    }
}
