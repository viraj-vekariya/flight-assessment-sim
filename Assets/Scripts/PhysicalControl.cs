// PhysicalControl — one operable object in the cockpit, and the binding that says what
// it does to the aeroplane.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE POINT OF THIS FILE
// ═══════════════════════════════════════════════════════════════════════════════
// Every cockpit control — yoke, throttle, flap lever, trim wheel, carb heat, fuel
// selector, load shed, alternate static, brake — is the SAME object with a different
// ControlSpec. There is one grab model, one sensitivity pipeline, one release rule and
// one place where a control writes to the aircraft. Adding a control is data, not code.
//
// This matters more here than in a game. In an experiment the interface has to be
// IDENTICAL across all twelve missions and predictable enough that operating it is not
// itself the thing being measured. A pile of bespoke per-control scripts drifts; one
// spec-driven implementation does not.
//
// ═══════════════════════════════════════════════════════════════════════════════
// INPUT IS HARDWARE-AGNOSTIC
// ═══════════════════════════════════════════════════════════════════════════════
// A control knows nothing about mice, controllers or hands. An interactor calls:
//
//     BeginGrab(worldPoint)  ->  UpdateGrab(worldPoint)  ->  EndGrab()
//     Click()                                                (discrete controls)
//
// The mouse interactor supplies a point derived from a screen ray; the VR interactor
// supplies the controller/hand position. Both produce the same aircraft behaviour,
// which is what keeps desktop and VR one simulation rather than two.
//
// ═══════════════════════════════════════════════════════════════════════════════
// POSITION-BASED, NOT RATE-BASED
// ═══════════════════════════════════════════════════════════════════════════════
// Continuous controls map HAND DISPLACEMENT to CONTROL POSITION:
//
//     hand moves half the control's travel  ->  control shows ~50%  ->  aircraft gets ~50%
//
// Not "hold the hand off-centre and the value keeps winding". A real lever is a
// position. Rate-based mapping is what makes VR flight controls feel disconnected, and
// it makes the control value depend on how long the participant held their hand there —
// i.e. on their reaction time, which is data we are trying to measure separately.

using UnityEngine;

public enum ControlKind
{
    /// <summary>Two-axis yoke: fore/aft = pitch, left/right = roll.</summary>
    Yoke,
    /// <summary>Continuous single-axis lever (throttle).</summary>
    Lever,
    /// <summary>Single-axis lever that settles into discrete detents (flaps).</summary>
    DetentLever,
    /// <summary>Continuous wheel wound along an axis (elevator trim).</summary>
    TrimWheel,
    /// <summary>Two-state switch or pull knob (carb heat, load shed, alternate static).</summary>
    Toggle,
    /// <summary>Multi-position rotary (fuel selector).</summary>
    Rotary,
    /// <summary>Analog lever that springs back to zero on release (wheel brakes).</summary>
    SpringLever,
}

/// <summary>What a control is wired to. One enum, resolved in one place
/// (<see cref="PhysicalControl.Apply"/>), so a control's effect is never hidden in a
/// side-effect somewhere else in the codebase.</summary>
public enum ControlTarget
{
    PitchRoll, Throttle, Flaps, Trim, WheelBrake, Rudder, Spoiler,
    CarbHeat, FuelSelector, LoadShed, AlternateStatic,
}

[System.Serializable]
public class ControlSpec
{
    /// <summary>Stable id used in markers and telemetry ("yoke", "throttle", ...).</summary>
    public string id = "control";
    public string label = "CONTROL";
    public ControlKind kind = ControlKind.Lever;
    public ControlTarget target = ControlTarget.Throttle;

    /// <summary>Local axis the hand moves along to work this control. For the yoke this
    /// is the PITCH axis; roll uses <see cref="secondaryAxis"/>.</summary>
    public Vector3 axis = Vector3.forward;
    public Vector3 secondaryAxis = Vector3.right;

    /// <summary>Physical travel in metres, end to end. Hand displacement is divided by
    /// this, so it sets the gearing directly: bigger travel = less sensitive.</summary>
    public float travel = 0.10f;
    public float secondaryTravel = 0.10f;

    /// <summary>Where 0 sits within the travel. 0.5 = centred (yoke, trim);
    /// 0 = one end (throttle, flaps, brake).</summary>
    public bool centred = false;

    /// <summary>How close the hand must be to grab it, metres. Generous enough not to
    /// frustrate, small enough not to steal a neighbouring control.</summary>
    public float captureRadius = 0.09f;

    /// <summary>Exponential smoothing time constant, seconds. 0 = raw. Kept small:
    /// smoothing that is visible as lag makes the aeroplane feel disconnected, and any
    /// lag becomes an unmeasured component of the pilot's control loop.</summary>
    public float smoothingTau = 0.05f;

    /// <summary>Applied to the normalised value before it reaches the aircraft.
    /// 1 = linear. >1 softens around neutral (yoke).</summary>
    public float responseExponent = 1f;

    /// <summary>Fractional dead zone around neutral. Deliberately tiny or zero — a dead
    /// zone on a POSITION control is a lie about where the control is.</summary>
    public float deadZone = 0f;

    /// <summary>Detent positions in normalised 0..1 (DetentLever / Rotary).</summary>
    public float[] detents;
    public string[] detentLabels;

    /// <summary>Visual: the transform moved/rotated to show the control's state.</summary>
    public Transform visual;
    /// <summary>Visual travel in metres (levers) or degrees (wheels/rotaries).</summary>
    public float visualTravel = 0.06f;
    public Vector3 visualAxis = Vector3.forward;
    public bool visualIsRotation = false;
}

[DefaultExecutionOrder(-60)]   // writes overrides before AircraftController reads them
public class PhysicalControl : MonoBehaviour
{
    public ControlSpec spec = new ControlSpec();
    public AircraftController controller;
    public CessnaPhysics phys;
    public AircraftSystems systems;

    /// <summary>Normalised control position. 0..1 for one-ended controls,
    /// -1..1 for centred ones. This IS the control — the aircraft reads it.</summary>
    public float Value { get; private set; }
    public float Secondary { get; private set; }
    public bool Grabbed { get; private set; }
    public bool Hovered { get; set; }

    Vector3 grabOrigin;          // world point where the hand took hold
    float valueAtGrab, secondaryAtGrab;
    float smoothed, smoothedSecondary;

    /// <summary>The value actually written to the aircraft, after smoothing.
    /// `Value` is the raw hand position; this is what the aeroplane sees. Read-only,
    /// and used by the control bench and the control tests — anything measuring
    /// control response must measure THIS, not `Value`.</summary>
    public float Smoothed => smoothed;
    int detentIndex;
    bool toggleState;

    public int DetentIndex => detentIndex;
    public bool ToggleState => toggleState;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Set the control to a state without the pilot touching it (trial reset).
    /// Deliberately does NOT raise an interaction event — nobody interacted.</summary>
    public void SetSilently(float value, int detent = -1, bool toggle = false)
    {
        writeOnce = true;      // a programmatic set must reach the aircraft too
        Value = smoothed = value;
        Secondary = smoothedSecondary = 0f;
        if (detent >= 0) detentIndex = detent;
        toggleState = toggle;
        Grabbed = false;
        UpdateVisual();
    }

    // ── interaction ───────────────────────────────────────────────────────────

    public void BeginGrab(Vector3 worldPoint)
    {
        if (Grabbed) return;
        Grabbed = true;
        grabOrigin = worldPoint;
        valueAtGrab = Value;
        secondaryAtGrab = Secondary;
        CockpitEvents.RaiseGrab(spec.id);
    }

    public void UpdateGrab(Vector3 worldPoint)
    {
        if (!Grabbed) return;
        Vector3 d = worldPoint - grabOrigin;

        // Hand displacement resolved onto the control's own axes, in ITS local frame —
        // so the mapping is right whatever attitude the aeroplane is in.
        float along = Vector3.Dot(d, transform.TransformDirection(spec.axis).normalized);
        float dv = along / Mathf.Max(0.001f, spec.travel) * (spec.centred ? 2f : 1f);
        Value = ClampToRange(valueAtGrab + dv);

        if (spec.kind == ControlKind.Yoke)
        {
            float across = Vector3.Dot(d, transform.TransformDirection(spec.secondaryAxis).normalized);
            float ds = across / Mathf.Max(0.001f, spec.secondaryTravel) * 2f;
            Secondary = Mathf.Clamp(secondaryAtGrab + ds, -1f, 1f);
        }
    }

    public void EndGrab()
    {
        if (!Grabbed) return;
        Grabbed = false;
        CockpitEvents.RaiseRelease(spec.id);

        switch (spec.kind)
        {
            case ControlKind.DetentLever:
            case ControlKind.Rotary:
                SnapToNearestDetent();
                writeOnce = true;              // commit the detent the pilot let go on
                break;

            case ControlKind.SpringLever:
                Value = 0f;                       // brakes release when you let go
                break;

            case ControlKind.Yoke:
                // A REAL yoke does not spring to neutral when released — it settles where
                // the aerodynamic force and the trim balance. Modelling that properly needs
                // a force model the flight model does not have, so this is the closest
                // defensible abstraction: the yoke HOLDS ITS POSITION on release, and the
                // pilot uses TRIM to make that position the one they want. Snapping to
                // neutral would make the yoke behave like a keyboard key and would remove
                // the entire reason trim exists. Documented in COCKPIT_CONTROLS.md.
                break;
        }
        UpdateVisual();
    }

    /// <summary>Discrete press: flips a toggle, advances a rotary, steps a detent lever.
    /// The safe interaction for small controls — no grab precision required.</summary>
    public void Click()
    {
        switch (spec.kind)
        {
            case ControlKind.Toggle:
                toggleState = !toggleState;
                Value = toggleState ? 1f : 0f;
                break;
            case ControlKind.Rotary:
            case ControlKind.DetentLever:
                if (spec.detents != null && spec.detents.Length > 0)
                {
                    detentIndex = (detentIndex + 1) % spec.detents.Length;
                    Value = spec.detents[detentIndex];
                }
                break;
            default:
                return;   // continuous controls are not clickable
        }
        smoothed = Value;
        writeOnce = true;                      // push the new state to the aircraft once
        UpdateVisual();
        CockpitEvents.RaiseGrab(spec.id);      // a press is a discrete interaction
        CockpitEvents.RaiseRelease(spec.id);
    }

    float ClampToRange(float v) => spec.centred ? Mathf.Clamp(v, -1f, 1f) : Mathf.Clamp01(v);

    void SnapToNearestDetent()
    {
        if (spec.detents == null || spec.detents.Length == 0) return;
        int best = 0; float bd = 99f;
        for (int i = 0; i < spec.detents.Length; i++)
        { float d = Mathf.Abs(spec.detents[i] - Value); if (d < bd) { bd = d; best = i; } }
        detentIndex = best;
        Value = spec.detents[best];
        smoothed = Value;
    }

    // ── per-frame ─────────────────────────────────────────────────────────────

    /// <summary>Set by Click() so a discrete control writes its new state once, then
    /// goes back to following the aircraft like everything else.</summary>
    bool writeOnce;

    void Update()
    {
        // FRAME-RATE INDEPENDENT smoothing. `1 - exp(-dt/tau)` gives the same physical
        // response at 30, 60 or 90 fps; `Lerp(a, b, k)` with a constant k does not, and
        // would make the control feel different on a different machine — an
        // uncontrolled between-participant variable.
        float k = spec.smoothingTau <= 0.0001f ? 1f
                : 1f - Mathf.Exp(-Time.deltaTime / spec.smoothingTau);
        smoothed = Mathf.Lerp(smoothed, Value, k);
        smoothedSecondary = Mathf.Lerp(smoothedSecondary, Secondary, k);

        // ═══════════════════════════════════════════════════════════════════════
        // WRITE ONLY WHILE HELD — otherwise FOLLOW the aircraft.
        // ═══════════════════════════════════════════════════════════════════════
        // A lever that writes its position to the aeroplane every frame would own that
        // axis permanently, which has two bad consequences:
        //
        //   1. The KEYBOARD stops working. A desktop participant pressing Shift for
        //      power would be overwritten by the throttle lever sitting at idle — and
        //      the cause would be invisible.
        //   2. Ownership never lapses, so `AnyOverride` is always true and the
        //      expiry model that fixes the latch bug is defeated by the very controls
        //      it exists to serve.
        //
        // A real cockpit lever IS the throttle: if something else moves the throttle,
        // the lever moves. So when it is not in the pilot's hand, the control mirrors
        // the aircraft rather than commanding it. The visual therefore stays truthful
        // whichever input moved the value.
        if (Grabbed || writeOnce)
        {
            Apply(smoothed, smoothedSecondary);
            writeOnce = false;
        }
        else FollowAircraft();

        UpdateVisual();
    }

    /// <summary>Mirror the aircraft's current state into this control, so the lever
    /// shows the truth when the pilot is not holding it.</summary>
    void FollowAircraft()
    {
        if (phys == null) return;
        switch (spec.target)
        {
            case ControlTarget.Throttle: Value = smoothed = phys.Throttle01; break;
            case ControlTarget.Trim:     Value = smoothed = phys.trim; break;
            case ControlTarget.WheelBrake:
                // A spring lever must return to rest, not mirror a brake the keyboard
                // is holding — otherwise releasing the virtual lever would not release.
                Value = 0f;
                break;
            case ControlTarget.Flaps:
                if (controller != null)
                {
                    detentIndex = controller.FlapDetentIndex;
                    Value = smoothed = AircraftController.FlapDetents[detentIndex];
                }
                break;
            case ControlTarget.Spoiler:
                // Reads the ACTUAL spoiler, so when a recorded trial forces it away the
                // lever visibly parks at UP instead of lying about the aeroplane's state.
                if (phys != null)
                {
                    Value = smoothed = phys.spoiler;
                    detentIndex = phys.spoiler > 0.75f ? 2 : phys.spoiler > 0.25f ? 1 : 0;
                }
                break;
            case ControlTarget.PitchRoll:
            case ControlTarget.Rudder:
                // The yoke's VISUAL is driven by RealCockpit from the live control
                // inputs, so it already shows whatever is flying the aeroplane. Its own
                // Value is left where the pilot released it (see EndGrab).
                break;
            case ControlTarget.CarbHeat:
                if (systems != null) { toggleState = systems.CarbHeatOn; Value = smoothed = toggleState ? 1f : 0f; }
                break;
            case ControlTarget.LoadShed:
                if (systems != null) { toggleState = systems.LoadShed; Value = smoothed = toggleState ? 1f : 0f; }
                break;
            case ControlTarget.AlternateStatic:
                if (systems != null) { toggleState = systems.AlternateStaticOpen; Value = smoothed = toggleState ? 1f : 0f; }
                break;
            case ControlTarget.FuelSelector:
                if (systems != null)
                {
                    detentIndex = systems.Selector == FuelSelector.Left ? 0
                                : systems.Selector == FuelSelector.Both ? 1 : 2;
                    Value = smoothed = spec.detents != null && detentIndex < spec.detents.Length
                                     ? spec.detents[detentIndex] : Value;
                }
                break;
        }
    }

    /// <summary>THE ONE PLACE a control touches the aeroplane. Everything goes through
    /// AircraftController's override API or AircraftSystems' Set* methods — never
    /// directly at the Rigidbody, and never at CessnaPhysics fields the controller owns.</summary>
    void Apply(float v, float secondary)
    {
        if (controller == null) return;

        switch (spec.target)
        {
            case ControlTarget.PitchRoll:
                // Only while held: releasing the yoke must hand pitch/roll back to the
                // keyboard rather than freezing the aeroplane at the last hand position.
                if (!Grabbed) return;
                controller.SetPitch(Shape(v));
                controller.SetRoll(Shape(secondary));
                break;

            case ControlTarget.Throttle:
                controller.SetThrottle(v);
                if (Grabbed) CockpitEvents.RaiseThrottleMoved(v);
                break;

            case ControlTarget.Trim:
                controller.SetTrim(v);
                if (Grabbed) CockpitEvents.RaiseTrimChanged(v);
                break;

            case ControlTarget.Rudder:
                if (!Grabbed) return;
                controller.SetYaw(Shape(v));
                break;

            case ControlTarget.WheelBrake:
                controller.SetBrake(v);
                break;

            case ControlTarget.Flaps:
                controller.SetFlapDetent(detentIndex);
                break;

            case ControlTarget.Spoiler:
                controller.SetSpoilerDetent(detentIndex);
                break;

            // ---- systems: discrete state, written only when it actually changes ----
            case ControlTarget.CarbHeat:
                if (systems != null && systems.CarbHeatOn != toggleState) systems.SetCarbHeat(toggleState);
                break;
            case ControlTarget.LoadShed:
                if (systems != null && systems.LoadShed != toggleState) systems.SetLoadShed(toggleState);
                break;
            case ControlTarget.AlternateStatic:
                if (systems != null && systems.AlternateStaticOpen != toggleState) systems.SetAlternateStatic(toggleState);
                break;
            case ControlTarget.FuelSelector:
                if (systems == null) break;
                var want = detentIndex == 0 ? FuelSelector.Left
                         : detentIndex == 1 ? FuelSelector.Both : FuelSelector.Right;
                if (systems.Selector != want) systems.SetSelector(want);
                break;
        }
    }

    /// <summary>Dead zone + response curve. Both default to "do nothing", because on a
    /// position-based control they are distortions and have to be justified per control.</summary>
    float Shape(float v)
    {
        float a = Mathf.Abs(v);
        if (spec.deadZone > 0f)
        {
            if (a < spec.deadZone) return 0f;
            a = (a - spec.deadZone) / (1f - spec.deadZone);
        }
        if (!Mathf.Approximately(spec.responseExponent, 1f)) a = Mathf.Pow(a, spec.responseExponent);
        return a * Mathf.Sign(v);
    }

    void UpdateVisual()
    {
        if (spec.visual == null) return;
        float t = spec.centred ? smoothed : smoothed;   // -1..1 or 0..1, as configured
        if (spec.visualIsRotation)
            spec.visual.localRotation = Quaternion.AngleAxis(t * spec.visualTravel, spec.visualAxis);
        else
            spec.visual.localPosition = visualBase + spec.visualAxis.normalized * (t * spec.visualTravel);
    }

    Vector3 visualBase;
    void Awake()
    {
        if (spec.visual != null) visualBase = spec.visual.localPosition;
        if (controller == null) controller = GetComponentInParent<AircraftController>();
    }

    /// <summary>Distance from a point to this control's grab centre.</summary>
    public float DistanceTo(Vector3 worldPoint) => Vector3.Distance(worldPoint, transform.position);
    public bool InRange(Vector3 worldPoint) => DistanceTo(worldPoint) <= spec.captureRadius;
}
