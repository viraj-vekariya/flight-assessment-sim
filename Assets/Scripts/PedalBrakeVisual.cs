// PedalBrakeVisual — tilts the aeroplane's own rudder/brake pedals with brake pressure.
//
// WHY THIS IS A SEPARATE COMPONENT
//   The pedals are aircraft geometry, not a control. The brake CONTROL is a handle under
//   the left panel edge with its own object, collider and travel. Earlier the pedal mesh
//   was reparented underneath the brake control, which meant the two moved and hid
//   together — so hiding or moving one piece of cockpit furniture could disable the brake,
//   and moving the brake dragged part of the airframe with it.
//
//   This component only READS `CessnaPhysics.brakeInput01`. It writes nothing, owns
//   nothing, and holds no reference to the control. Delete it and the brake still works;
//   delete the brake and the pedals simply stop tilting.
//
// The pedal face is hinged near its LOWER edge on a real 172 — the master cylinders sit
// immediately forward of the pedals, so pressing the TOP of the pedal strokes the cylinder.
// The pivot this component sits on is placed accordingly.

using UnityEngine;

public class PedalBrakeVisual : MonoBehaviour
{
    public CessnaPhysics phys;
    public float maxTiltDeg = 20f;
    public float tau = 0.05f;

    Quaternion rest;
    float shown;

    void Awake() { rest = transform.localRotation; }

    void LateUpdate()
    {
        if (phys == null) return;
        // Frame-rate independent, like every other smoothed thing in this cockpit.
        float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, tau));
        shown = Mathf.Lerp(shown, Mathf.Clamp01(phys.brakeInput01), k);
        transform.localRotation = rest * Quaternion.Euler(shown * maxTiltDeg, 0f, 0f);
    }
}
