// CockpitInteractorVR — VR hands. Reach for a control, squeeze grip, move your hand.
//
// The VR half of "one simulation, two input modalities". It obtains a world point from
// the controller pose and feeds the SAME PhysicalControl.BeginGrab/UpdateGrab/EndGrab
// calls the mouse interactor uses, so the aircraft cannot behave differently between
// modalities.
//
// ═══════════════════════════════════════════════════════════════════════════════
// INTERACTION MODEL — chosen for an experiment, not for a game
// ═══════════════════════════════════════════════════════════════════════════════
//   * CONTINUOUS controls (yoke, throttle, trim, flap lever, brake) are GRABBED with the
//     grip button and driven by HAND DISPLACEMENT. Position in, position out.
//
//   * DISCRETE controls (carb heat, fuel selector, load shed, alternate static) are
//     PRESSED with the trigger. No precision drag, no grab-and-hold. A switch that
//     needed a careful drag would be a dexterity test; what the experiment wants to know
//     is WHEN the pilot decided to move it, not whether they can operate a fiddly
//     virtual object.
//
//   * WRIST ROTATION IS NOT USED for the yoke. Mapping wrist orientation to pitch/roll
//     is the standard VR shortcut and it is wrong here: it couples aircraft control to
//     forearm posture, drifts with arm fatigue over a 300 s trial, and adds a motor
//     difficulty that varies between participants — precisely the confound this
//     experiment is trying to keep out of the EEG.
//
//   * ONE HAND OWNS A CONTROL AT A TIME. Both hands can work different controls (fly
//     with the right, throttle with the left), which is how the aeroplane is actually
//     flown, but two hands cannot fight over one control.
//
// Acknowledgement (probes, decisions, checklist items) is a CONTROLLER BUTTON, routed
// straight to ScenarioEngine.ExternalAck(). That path is latency-sensitive experimental
// data and must not depend on aiming a hand at a small virtual button.

using UnityEngine;

[DefaultExecutionOrder(-70)]   // before PhysicalControl (-60) and AircraftController (0)
public class CockpitInteractorVR : MonoBehaviour
{
    /// <summary>Grip value above which a grab is held. Hysteresis below.</summary>
    public float gripOn = 0.65f, gripOff = 0.35f;
    public float triggerPress = 0.7f;

    PhysicalControl heldRight, heldLeft;
    bool prevRightTrigger, prevLeftTrigger;

    /// <summary>Below this the left trigger is ignored, so resting a finger on it does not
    /// drag the brakes on during a take-off roll.</summary>
    public float brakeDeadZone = 0.08f;
    Transform rig;          // the transform controller poses are expressed relative to

    void Awake() => rig = transform;   // controller space is tracked-space; see HandWorld()

    void Update()
    {
        if (!VRRuntime.Active) { ReleaseBoth(); return; }
        var gm = GameManager.Instance;
        bool usable = gm != null && (gm.State == GameState.Flying || ControlCheckMode.Active);
        if (!usable) { ReleaseBoth(); return; }

        Hand(true,  ref heldRight, ref prevRightTrigger);
        Hand(false, ref heldLeft,  ref prevLeftTrigger);

        // Acknowledge — the one action that is a button, on purpose.
        if (VRRuntime.AckPressed)
        {
            var eng = gm.ScenarioRunner;
            if (eng != null && eng.Active) { eng.ExternalAck(); VRRuntime.Haptic(true, 0.5f, 0.05f); }
        }

        Brake();
    }

    /// <summary>WHEEL BRAKES on the LEFT controller's trigger, as an analog axis.
    ///
    /// The brake's visual is the aeroplane's own rudder/brake pedal assembly, which is
    /// correct and is left alone — but it lives in the footwell, measured at 45 deg below
    /// the eye line and largely tucked under the panel. There is no foot tracking on Touch
    /// controllers, so the only way to work it as a grabbed control is to reach a HAND down
    /// into the footwell during a taxi: awkward, unreliable, and a motor confound the
    /// experiment does not want.
    ///
    /// The left trigger is an analog axis, so it gives genuinely PROGRESSIVE braking rather
    /// than an on/off button — which is what a toe brake does. The pedals still tilt with
    /// applied pressure, so the cockpit tells the truth about what the brake is doing.
    ///
    /// The LEFT trigger is free for this because discrete cockpit presses are handled per
    /// hand only when a hand is actually near a control, and the left hand is not the one
    /// that works the quadrant. If a control IS in the left hand, that hand keeps it and
    /// the brake stands down, so one input can never do two things at once.</summary>
    void Brake()
    {
        var ac = GameManager.Instance != null ? GameManager.Instance.Aircraft : null;
        if (ac == null) return;
        var ctl = ac.GetComponent<AircraftController>();
        if (ctl == null) return;

        if (heldLeft != null) return;               // that hand is busy holding something

        float t = VRRuntime.LeftTrigger;
        if (t <= brakeDeadZone) return;             // silent when not asked for, so the
                                                    // keyboard and the pedals still work
        // Rescale past the dead zone so the full trigger throw maps to full pressure.
        ctl.SetBrake(Mathf.Clamp01((t - brakeDeadZone) / (1f - brakeDeadZone)));
    }

    void Hand(bool rightHand, ref PhysicalControl held, ref bool prevTrigger)
    {
        bool valid = rightHand ? VRRuntime.RightValid : VRRuntime.LeftValid;
        if (!valid) { Release(ref held); return; }

        Vector3 hand = HandWorld(rightHand);
        float grip = rightHand ? VRRuntime.RightGrip : VRRuntime.LeftGrip;
        float trig = rightHand ? VRRuntime.RightTrigger : VRRuntime.LeftTrigger;

        // ---- holding ----
        if (held != null)
        {
            if (grip > gripOff) { held.UpdateGrab(hand); return; }
            Release(ref held);
            return;
        }

        var near = Nearest(hand, rightHand);

        // hover feedback: a faint tick the first time the hand enters a control's volume
        foreach (var c in Rig().Controls) if (c != null) c.Hovered = (c == near);

        if (near == null) { prevTrigger = trig > triggerPress; return; }

        bool discrete = near.spec.kind == ControlKind.Toggle || near.spec.kind == ControlKind.Rotary;
        bool trigDown = trig > triggerPress && !prevTrigger;
        prevTrigger = trig > triggerPress;

        if (discrete)
        {
            if (trigDown) { near.Click(); VRRuntime.Haptic(rightHand, 0.6f, 0.03f); }
            return;
        }

        if (grip > gripOn)
        {
            held = near;
            held.BeginGrab(hand);
            VRRuntime.Haptic(rightHand, 0.35f, 0.03f);
        }
    }

    void Release(ref PhysicalControl held)
    {
        if (held == null) return;
        held.EndGrab();
        held = null;
    }

    void ReleaseBoth() { Release(ref heldRight); Release(ref heldLeft); }

    /// <summary>Controller position in WORLD space. Poses come from the XR runtime in
    /// tracked space, which is the VR camera rig's space, so they are transformed by the
    /// rig — this is what keeps the cockpit controls in the right place when the
    /// aeroplane manoeuvres.</summary>
    Vector3 HandWorld(bool rightHand)
    {
        Vector3 local = rightHand ? VRRuntime.RightPos : VRRuntime.LeftPos;
        var origin = VRCameraRig.TrackingSpace;
        return origin != null ? origin.TransformPoint(local) : transform.TransformPoint(local);
    }

    /// <summary>The nearest control within its own capture radius, excluding one already
    /// held by the other hand.</summary>
    PhysicalControl Nearest(Vector3 hand, bool rightHand)
    {
        var rig = Rig();
        if (rig == null) return null;
        PhysicalControl other = rightHand ? heldLeft : heldRight;
        PhysicalControl best = null; float bd = float.MaxValue;
        foreach (var c in rig.Controls)
        {
            if (c == null || c == other) continue;
            float d = c.DistanceTo(hand);
            if (d > c.spec.captureRadius) continue;
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    static CockpitControlRig Rig() => CockpitControlRig.Instance;
}
