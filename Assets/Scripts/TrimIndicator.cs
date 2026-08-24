// TrimIndicator — the pointer beside the elevator trim wheel.
//
// The wheel turns through more than half a revolution per unit of trim, so its angle is
// not a reading. A 172 solves that the same way: a separate pointer running in a slot
// beside the wheel, with a marked TAKEOFF band. This drives that pointer from the
// aeroplane's actual trim, so it stays truthful whether the trim was set by the wheel,
// by the keyboard, or by a mission's starting configuration.

using UnityEngine;

public class TrimIndicator : MonoBehaviour
{
    public Transform pointer;
    public CessnaPhysics phys;
    /// <summary>Full travel of the pointer, metres, end to end.</summary>
    public float span = 0.017f;

    Vector3 rest;
    bool captured;
    float shown;

    void LateUpdate()
    {
        if (pointer == null || phys == null) return;
        if (!captured) { rest = pointer.localPosition; captured = true; }
        // trim is -1 (nose down) .. +1 (nose up); the pointer runs UP for nose up.
        float k = 1f - Mathf.Exp(-Time.deltaTime / 0.08f);
        shown = Mathf.Lerp(shown, Mathf.Clamp(phys.trim, -1f, 1f), k);
        pointer.localPosition = rest + new Vector3(0f, shown * span * 0.5f, 0f);
    }
}
