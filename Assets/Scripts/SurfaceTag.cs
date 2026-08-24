using UnityEngine;

/// <summary>
/// Marks what kind of surface a collider is, so the flight model can decide
/// whether a contact is a normal landing/taxi or a crash. Attached to world
/// surfaces by WorldBuilder; read via GetComponentInParent on the contact.
/// </summary>
public enum SurfaceKind
{
    Runway,    // safe rolling surface — taxi / takeoff roll / gentle landing
    Ground,    // non-runway terrain (grass, dirt) — landing here is off-field
    Obstacle,  // mountains, buildings — ramming them is a crash
    Water      // ocean — ditching is a crash
}

public class SurfaceTag : MonoBehaviour
{
    public SurfaceKind Kind = SurfaceKind.Obstacle;

    public static SurfaceTag Add(GameObject go, SurfaceKind kind)
    {
        var t = go.AddComponent<SurfaceTag>();
        t.Kind = kind;
        return t;
    }
}
