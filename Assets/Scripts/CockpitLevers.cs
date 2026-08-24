using UnityEngine;

/// <summary>
/// Animates the visible cockpit throttle and flaps levers from the live state,
/// so you can see throttle/flap position in the panel (also shown on the HUD).
/// Levers tilt forward as the value increases.
/// </summary>
public class CockpitLevers : MonoBehaviour
{
    public CessnaPhysics phys;
    public Transform spoilerLever;
    public Transform throttleLever;
    public Transform flapLever;
    public float minDeg = 35f;    // value 0
    public float maxDeg = -35f;   // value 1

    void LateUpdate()
    {
        if (phys == null) return;
        if (spoilerLever)
            spoilerLever.localRotation = Quaternion.Euler(Mathf.Lerp(minDeg, maxDeg, phys.Spoiler01), 0f, 0f);
        if (throttleLever)
            throttleLever.localRotation = Quaternion.Euler(Mathf.Lerp(minDeg, maxDeg, phys.Throttle01), 0f, 0f);
        if (flapLever)
            flapLever.localRotation = Quaternion.Euler(Mathf.Lerp(minDeg, maxDeg, phys.Flaps01), 0f, 0f);
    }
}
