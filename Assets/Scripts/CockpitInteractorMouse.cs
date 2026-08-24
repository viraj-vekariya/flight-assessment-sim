// CockpitInteractorMouse — desktop hands. Left-drag operates the cockpit.
//
// This is the DESKTOP half of the "one simulation, two input modalities" rule. It
// produces exactly the same PhysicalControl calls the VR interactor produces, so the
// aircraft cannot behave differently between modalities: the difference is only in how
// a world point is obtained.
//
//   mouse:  screen ray -> point on the control's grab plane
//   VR:     controller/hand position directly
//
// Both then feed BeginGrab / UpdateGrab / EndGrab.
//
// WHY A PLANE PROJECTION RATHER THAN A RAYCAST HIT POINT
//   A raycast hit only exists while the ray is on the collider. Once the pilot drags the
//   throttle away from where they clicked, the ray leaves the (small) collider and the
//   hit point vanishes. Projecting the ray onto a fixed plane through the grab point
//   gives a continuous world point for as long as the button is held — which is what a
//   position-based control needs.
//
// Right-drag is left alone: that is the existing look-around control.

using UnityEngine;

[DefaultExecutionOrder(-70)]   // before PhysicalControl (-60) and AircraftController (0)
public class CockpitInteractorMouse : MonoBehaviour
{
    public float reach = 1.6f;        // metres from the eye — the cockpit is within arm's length

    Camera cam;
    PhysicalControl held;
    Plane dragPlane;
    PhysicalControl hovered;

    void Awake() => cam = GetComponentInChildren<Camera>();

    void Update()
    {
        // VR owns the cockpit when a headset is driving it; the mouse stands down so the
        // two can never fight for the same control.
        if (VRRuntime.Active) { ReleaseIfHeld(); return; }

        var gm = GameManager.Instance;
        bool usable = gm != null && (gm.State == GameState.Flying || ControlCheckMode.Active);
        if (!usable) { ReleaseIfHeld(); return; }
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam == null || !cam.enabled) { ReleaseIfHeld(); return; }

        // ---- dragging ----
        if (held != null)
        {
            if (Input.GetMouseButton(0))
            {
                Ray r = cam.ScreenPointToRay(Input.mousePosition);
                if (dragPlane.Raycast(r, out float d)) held.UpdateGrab(r.GetPoint(d));
                return;
            }
            ReleaseIfHeld();
            return;
        }

        // ---- hover ----
        var near = Nearest(out Vector3 point);
        if (hovered != near)
        {
            if (hovered != null) hovered.Hovered = false;
            hovered = near;
            if (hovered != null) hovered.Hovered = true;
        }

        // ---- grab / click ----
        if (near != null && Input.GetMouseButtonDown(0))
        {
            // Discrete controls are CLICKED, not dragged. A switch that needed a precise
            // drag would be a dexterity test, which is not what this experiment measures.
            if (near.spec.kind == ControlKind.Toggle || near.spec.kind == ControlKind.Rotary)
            {
                near.Click();
                return;
            }
            held = near;
            dragPlane = new Plane(-cam.transform.forward, point);
            held.BeginGrab(point);
        }
    }

    void ReleaseIfHeld()
    {
        if (held == null) return;
        held.EndGrab();
        held = null;
    }

    /// <summary>The control the cursor is pointing at, if the ray passes within its
    /// capture radius and it is within reach.</summary>
    PhysicalControl Nearest(out Vector3 point)
    {
        point = Vector3.zero;
        var rig = CockpitControlRig.Instance;
        if (rig == null) return null;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        PhysicalControl best = null;
        float bestT = float.MaxValue;

        foreach (var c in rig.Controls)
        {
            if (c == null) continue;
            // Closest approach of the ray to the control centre.
            Vector3 toC = c.transform.position - ray.origin;
            float t = Vector3.Dot(toC, ray.direction);
            if (t < 0f || t > reach) continue;
            float dist = Vector3.Distance(ray.GetPoint(t), c.transform.position);
            if (dist > c.spec.captureRadius) continue;
            if (t < bestT) { bestT = t; best = c; point = ray.GetPoint(t); }
        }
        return best;
    }
}
