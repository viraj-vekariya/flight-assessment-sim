using UnityEngine;

/// <summary>
/// Entry point. Runs automatically when you press Play — no scene setup needed.
/// Clears any default camera/light/listener a blank scene ships with, then spawns
/// the GameManager, which builds the whole world from code.
/// </summary>
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Spawn()
    {
        if (Object.FindFirstObjectByType<GameManager>() != null) return;

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Object.Destroy(cam.gameObject);
        foreach (var lis in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            Object.Destroy(lis);
        foreach (var lgt in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            Object.Destroy(lgt.gameObject);

        new GameObject("FlightSim").AddComponent<GameManager>();
    }
}
