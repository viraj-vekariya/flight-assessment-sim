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
    public Transform trimTab;     // child of the elevator, on its trailing edge
    public Transform rudder;
    public Transform flapLeft;
    public Transform flapRight;
    public Transform propeller;

    [Header("Max deflections (deg)")]
    public float aileronMax = 20f;
    public float elevatorMax = 22f;
    public float rudderMax = 25f;
    public float flapMax = 35f;
    public float trimTabMax = 25f;
    public float propSpinMax = 2000f;   // deg/sec at full throttle

    float propRate;

    void LateUpdate()
    {
        if (phys == null) return;

        if (aileronLeft) aileronLeft.localRotation = Quaternion.Euler(phys.rollInput * aileronMax, 0f, 0f);
        if (aileronRight) aileronRight.localRotation = Quaternion.Euler(-phys.rollInput * aileronMax, 0f, 0f);
        // The elevator shows the TOTAL command the physics applies — yoke plus trim
        // (CessnaPhysics.ElevatorCmd). It used to follow the yoke alone, so trimming with
        // [ and ] moved nothing on the outside of the aeroplane.
        if (elevator) elevator.localRotation = Quaternion.Euler(-phys.ElevatorCmd * elevatorMax, 0f, 0f);
        // The trim tab deflects OPPOSITE to the elevator position it holds — that is how a
        // real tab works: nose-up trim puts the tab's trailing edge DOWN, the airflow on it
        // pushes the elevator's trailing edge UP. It is a child of the elevator, so this
        // angle is relative to the elevator.
        if (trimTab) trimTab.localRotation = Quaternion.Euler(-phys.trim * trimTabMax, 0f, 0f);
        if (rudder) rudder.localRotation = Quaternion.Euler(0f, phys.yawInput * rudderMax, 0f);

        float flapDeg = phys.Flaps01 * flapMax;
        if (flapLeft) flapLeft.localRotation = Quaternion.Euler(flapDeg, 0f, 0f);
        if (flapRight) flapRight.localRotation = Quaternion.Euler(flapDeg, 0f, 0f);

        // Same behaviour as the cockpit's propeller: still with the throttle closed, eased
        // spin-up/run-down, stopped by an engine failure.
        if (propeller)
        {
            var sys = AircraftSystems.Instance;
            float target = (sys != null && sys.Engine == EngineState.Failed) ? 0f : propSpinMax * phys.Throttle01;
            float accel = target > propRate ? propSpinMax / 2.5f : propSpinMax / 5f;
            propRate = Mathf.MoveTowards(propRate, target, accel * Time.deltaTime);
            if (propRate > 0.01f) propeller.Rotate(0f, 0f, propRate * Time.deltaTime, Space.Self);
        }
    }
}
