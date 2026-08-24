using UnityEngine;

/// <summary>
/// Artificial horizon: a horizon bar that rolls (about Z) and pitches (moves up/
/// down) behind a fixed miniature-aircraft symbol — the standard way to read
/// attitude. Driven by CessnaPhysics roll & pitch.
/// </summary>
public class AttitudeIndicator : MonoBehaviour
{
    public CessnaPhysics phys;
    public Transform horizonBar;     // rolls + translates
    public float pitchPixelsPerDeg = 0.0016f;
    public float maxPitchOffset = 0.07f;

    void LateUpdate()
    {
        if (phys == null || horizonBar == null) return;
        float pitchOffset = Mathf.Clamp(-phys.PitchDeg * pitchPixelsPerDeg, -maxPitchOffset, maxPitchOffset);
        horizonBar.localPosition = new Vector3(0f, pitchOffset, 0f);
        horizonBar.localRotation = Quaternion.Euler(0f, 0f, phys.RollDeg);
    }
}
