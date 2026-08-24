using UnityEngine;

/// <summary>
/// Small helpers shared by the runtime builders so the same code works whether
/// it runs at Play time or from the editor "Build Scene" menu.
/// </summary>
public static class SimUtil
{
    /// Destroy() is illegal in edit mode (Unity requires DestroyImmediate there),
    /// so pick the right one automatically.
    public static void Destroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }
}
