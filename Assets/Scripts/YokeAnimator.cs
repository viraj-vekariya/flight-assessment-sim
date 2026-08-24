using UnityEngine;

/// <summary>
/// Drives the visible yoke from the live inputs through TWO nested pivots, the way
/// a real control column moves (this replaces the old single-transform version that
/// faked it):
///   pitchPivot — a hinge low on the column; pull/push tilts the WHOLE column about X.
///   wheel      — the ram's-horn wheel; roll rotates ONLY the wheel about the column axis.
/// Pure visual feedback (the flight input itself comes from keys or the mouse-grabbed
/// yoke via CockpitInteraction/AircraftController).
/// </summary>
public class YokeAnimator : MonoBehaviour
{
    public CessnaPhysics phys;
    public Transform pitchPivot;   // tilts about local X (pitch)
    public Transform wheel;        // rotates about local Z (roll — wheel only)
    public float pitchDeg = 12f;
    public float rollAngle = 45f;

    void LateUpdate()
    {
        if (phys == null) return;
        if (pitchPivot != null)
            pitchPivot.localRotation = Quaternion.Euler(phys.pitchInput * pitchDeg, 0f, 0f);
        if (wheel != null)
            wheel.localRotation = Quaternion.Euler(0f, 0f, -phys.rollInput * rollAngle);
    }
}
