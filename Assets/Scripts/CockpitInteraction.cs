using UnityEngine;

/// <summary>
/// In-cockpit mouse interaction. While in the cockpit view and flying, LEFT-drag
/// the yoke to fly (X = roll, Y = pitch), drag the throttle lever, press the rudder
/// pedals to yaw, hold the brake lever, and click the flap / spoiler levers and the
/// switches. It raycasts from the cockpit camera against the cockpit-layer trigger
/// colliders tagged with <see cref="CockpitControl"/> and routes the result through
/// <see cref="AircraftController"/>'s override API, so the keyboard still works and
/// the two never fight. (RIGHT mouse stays "look around" — see CockpitCamera.)
///
/// Runs before AircraftController each frame so the overrides it sets are read the
/// same frame. Colliders are triggers, so they never disturb the aircraft physics.
/// This is also the seam a VR rig would plug into later (grab replaces raycast).
/// </summary>
// STATUS: FALLBACK ONLY. This drives the CODE-BUILT cockpit, which is used only when
// the GLB model fails to load. When the real cockpit loads, RealCockpit destroys these
// grab boxes and disables this component, and CockpitInteractorMouse + PhysicalControl
// take over. Two mouse-interaction paths exist because there are two cockpits; exactly
// one is ever live.
[DefaultExecutionOrder(-50)]
public class CockpitInteraction : MonoBehaviour
{
    public Camera cam;                 // the cockpit camera (set by CockpitBuilder)
    public AircraftController controller;
    public CessnaPhysics phys;

    public float pitchRange = 150f;    // pixels of drag for full deflection
    public float rollRange = 150f;
    public float throttleRange = 200f;
    public float reach = 4f;           // metres

    CockpitControl grabbed;
    Vector2 grabMouse;
    float grabValue;

    void Update()
    {
        // DO NOT call controller.ClearOverrides() unconditionally here.
        //
        // It used to be called every frame, at execution order -50, i.e. AFTER every
        // other input source has written and BEFORE AircraftController reads. That
        // wiped the override of anything running at order >= -50 — the scenario
        // engine, the test harnesses, and any external control-hardware layer — so
        // those inputs silently never reached the aeroplane. It was invisible in
        // normal use only because RealCockpit disables this component once the GLB
        // cockpit finishes loading (~14 s in). In the FALLBACK path, where the GLB
        // fails to load and this component stays enabled for the whole session, no
        // programmatic or hardware input could reach the aircraft at all.
        //
        // The unconditional clear was also vestigial: it dates from the old model in
        // which an override LATCHED until explicitly cleared. Ownership now expires on
        // its own after one frame (AircraftController.GraceFrames), so nothing needs
        // clearing except this component's own grab, on release.
        var gm = GameManager.Instance;
        bool active = gm != null && gm.State == GameState.Flying
                      && cam != null && cam.enabled && controller != null;
        if (!active)
        {
            if (grabbed != null) { grabbed = null; controller?.ClearOverrides(); }
            return;
        }

        if (Input.GetMouseButtonDown(0)) TryGrab();
        if (Input.GetMouseButton(0) && grabbed != null) DriveHeld();
        if (Input.GetMouseButtonUp(0) && grabbed != null) { grabbed = null; controller.ClearOverrides(); }
    }

    void TryGrab()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, reach,
                             1 << CockpitBuilder.CockpitLayer, QueryTriggerInteraction.Collide))
            return;
        var cc = hit.collider.GetComponent<CockpitControl>();
        if (cc == null) return;

        switch (cc.kind)
        {
            // discrete — one action per click
            case CockpitControlKind.Flaps:   controller.StepFlaps();   break;
            case CockpitControlKind.Spoiler: controller.StepSpoiler(); break;
            case CockpitControlKind.Switch:  cc.FlipSwitch();          break;

            // continuous — grab and drive while the button is held
            default:
                grabbed = cc;
                grabMouse = Input.mousePosition;
                grabValue = cc.kind == CockpitControlKind.Throttle && phys != null ? phys.Throttle01 : 0f;
                break;
        }
    }

    void DriveHeld()
    {
        Vector2 dm = (Vector2)Input.mousePosition - grabMouse;
        switch (grabbed.kind)
        {
            case CockpitControlKind.Yoke:
                controller.SetPitch(-dm.y / pitchRange);   // push (drag down) = nose down
                controller.SetRoll(dm.x / rollRange);      // drag right = roll right
                break;
            case CockpitControlKind.Throttle:
                controller.SetThrottle(grabValue + dm.y / throttleRange);  // drag up = more power
                break;
            case CockpitControlKind.Brake:
                controller.SetBrake(1f);   // legacy code-cockpit lever is on/off
                break;
            case CockpitControlKind.YawLeft:
                controller.SetYaw(-1f);
                break;
            case CockpitControlKind.YawRight:
                controller.SetYaw(1f);
                break;
        }
    }
}
