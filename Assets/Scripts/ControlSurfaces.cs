using UnityEngine;

/// <summary>
/// Visibly deflects the aircraft's control surfaces from the live control inputs:
/// ailerons (differential), elevator, rudder, and flaps. Pure visual — the forces
/// come from CessnaPhysics. Hinge pivots are created by AircraftBuilder.
/// </summary>
public class ControlSurfaces : MonoBehaviour
{
    public CessnaPhysics phys;
    public Transform aileronLeft;
    public Transform aileronRight;
    public Transform elevator;
    public Transform rudder;
    public Transform flapLeft;
    public Transform flapRight;
    public Transform propeller;

    [Header("Max deflections (deg)")]
    public float aileronMax = 20f;
    public float elevatorMax = 22f;
    public float rudderMax = 25f;
    public float flapMax = 35f;
    public float propSpinMax = 2000f;   // deg/sec at full throttle

    void LateUpdate()
    {
        if (phys == null) return;

        if (aileronLeft) aileronLeft.localRotation = Quaternion.Euler(phys.rollInput * aileronMax, 0f, 0f);
        if (aileronRight) aileronRight.localRotation = Quaternion.Euler(-phys.rollInput * aileronMax, 0f, 0f);
        if (elevator) elevator.localRotation = Quaternion.Euler(-phys.pitchInput * elevatorMax, 0f, 0f);
        if (rudder) rudder.localRotation = Quaternion.Euler(0f, phys.yawInput * rudderMax, 0f);

        float flapDeg = phys.Flaps01 * flapMax;
        if (flapLeft) flapLeft.localRotation = Quaternion.Euler(flapDeg, 0f, 0f);
        if (flapRight) flapRight.localRotation = Quaternion.Euler(flapDeg, 0f, 0f);

        if (propeller)
            propeller.Rotate(0f, 0f, propSpinMax * phys.Throttle01 * Time.deltaTime, Space.Self);
    }
}
