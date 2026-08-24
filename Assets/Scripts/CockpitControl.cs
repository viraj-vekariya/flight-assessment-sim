using UnityEngine;

/// <summary>
/// Marks a cockpit object the pilot can operate with the mouse in the cockpit
/// view. A trigger collider on the same GameObject is the click/grab target;
/// CockpitInteraction raycasts for these and drives the matching flight input.
///
/// Continuous controls (Yoke, Throttle, YawLeft/Right, Brake) act while the mouse
/// is held; discrete controls (Flaps, Spoiler, Switch) act once per click.
/// </summary>
public enum CockpitControlKind
{
    Yoke,       // drag: X = roll, Y = pitch (held)
    Throttle,   // drag Y: throttle 0..1 (held)
    Flaps,      // click: step flap detent
    Spoiler,    // click: step spoiler detent
    Brake,      // hold: wheel brakes
    YawLeft,    // hold: left rudder
    YawRight,   // hold: right rudder
    Switch      // click: flip a toggle (cosmetic state)
}

public class CockpitControl : MonoBehaviour
{
    public CockpitControlKind kind;

    // For Switch: the little toggle to flip up/down when clicked.
    public Transform toggle;
    public float switchOnDeg = -24f;
    public float switchOffDeg = 24f;
    public bool on;

    public void FlipSwitch()
    {
        on = !on;
        if (toggle != null)
            toggle.localRotation = Quaternion.Euler(on ? switchOnDeg : switchOffDeg, 0f, 0f);
    }
}
