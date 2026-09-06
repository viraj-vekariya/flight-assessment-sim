// CockpitControlRig — builds the operable controls onto the real GLB cockpit.
//
// WHAT IT DOES
//   Creates one PhysicalControl per cockpit control, positions it against the model's
//   OWN measured geometry where that geometry exists (yoke, throttle quadrant), and
//   builds simple, clearly-shaped geometry where it does not (flap lever, trim wheel,
//   switch panel). Everything ends up on the cockpit layer so the external camera culls
//   it and the interactors can find it.
//
// WHY BUILD GEOMETRY AT ALL
//   The GLB is a static art asset: it has a throttle quadrant and a panel, but no
//   separable flap lever, trim wheel, fuel selector or switch bodies that can be moved
//   independently. Rather than pretend a fused mesh is operable — which would give the
//   pilot a control that does not visibly respond — the rig builds a small, honest,
//   clearly-labelled physical control at the right place. It reads as a cockpit control
//   and, crucially, its VISIBLE POSITION IS THE ACTUAL CONTROL STATE.
//
// GEOMETRY REFERENCE (holder-local, from the verified mesh map)
//   panel face          z ≈ 0.755
//   pilot eye point     (0, 0.58, 0.52)
//   yoke column axis    on the centreline, measured by RealCockpit.RigYoke
//   throttle quadrant   Object_107, below the panel centre
//   Unity holder x      = −(glTF x);  +Z points at the panel
//
// PLACEMENT PRINCIPLE
//   Controls sit where a 172's controls sit, and — more importantly for this experiment
//   — they sit FAR ENOUGH APART that reaching for one cannot grab another. Capture radii
//   are set per control against the actual spacing, not copied from the old VR project.

using UnityEngine;

public class CockpitControlRig : MonoBehaviour
{
    public static CockpitControlRig Instance { get; private set; }

    /// <summary>Every control built, in a stable order.</summary>
    public PhysicalControl[] Controls { get; private set; } = new PhysicalControl[0];

    /// <summary>True when any physical cockpit control is currently in the pilot's hand.
    /// Used by the control-activity covariate so "hands on" time is measured rather than
    /// inferred from whether the inputs happen to be changing.</summary>
    public static bool AnyGrabbed
    {
        get
        {
            var r = Instance;
            if (r == null) return false;
            foreach (var c in r.Controls) if (c != null && c.Grabbed) return true;
            return false;
        }
    }

    // Panel-relative anchors (holder-local metres). Chosen so nothing overlaps:
    // the nearest two controls are 9 cm apart and the largest capture radius is 4.5 cm.
    // Layout, revised in the visual pass. Every control is now MOUNTED — on the lower
    // panel face, on the centre pedestal, or on a sub-panel — instead of hanging in space.
    // Spacing is checked against capture radii below: the closest pair is 5.5 cm apart and
    // the radii that meet there sum to less than that, so no reach can grab two controls.
    const float PanelZ    = 0.750f;   // lower panel face, just proud of the GLB panel

    // ── LAYOUT ─────────────────────────────────────────────────────────────────────
    // Two tiers, because the cockpit has two kinds of control and they should not look
    // alike or sit together:
    //
    //   PRIMARY — touched continuously, must be found without looking:
    //       yoke (GLB) · throttle quadrant · flap lever · brake handle
    //   SYSTEMS — memory items, touched a handful of times in three missions, grouped
    //   into ONE tidy block on the left so they read as a switch panel rather than as
    //   controls scattered round the cabin:
    //       carb heat · fuel selector · load shed · alternate static
    //
    // The systems four are NOT decoration and cannot be dropped: ChecklistLibrary gates
    // real DO items on them — Electrical/H2 on LoadShed, StaticBlock/H3 on
    // AlternateStaticOpen, EngineFailure/H4 on CarbHeatOn and Selector != Both. Remove
    // any of them and a HIGH mission's drill can never be completed.
    //
    // Spacing is checked against capture radii: the closest pair is 55 mm apart and the
    // radii that meet there sum to 54 mm, so one reach can never take two controls.

    // Layout follows the reference: levers in a quadrant to the RIGHT of the yoke, the
    // engine/systems group to the LEFT, trim and alternate static on the pedestal, and the
    // brakes on the model's OWN rudder pedals (Object_52, no longer hidden).
    //
    // The furniture is kept clear of the pedal volume (x +-0.127, y 0.270..0.306,
    // z 0.755..0.801): the sub-panel now starts at y 0.315 and the pedestal is pulled aft
    // to z 0.630..0.730, so nothing intersects them.
    //
    // Capture radii may overlap freely: CockpitInteractor*.Nearest() takes the CLOSEST
    // control inside its own radius, so a tight quadrant resolves to whichever lever the
    // hand is actually nearest rather than becoming ambiguous.

    // ── THE CONTROL AREA ───────────────────────────────────────────────────────────
    // Three SLIDE controls in one compact block on the lower panel, in the order a hand
    // meets them moving outboard:
    //
    //        SPOILER      THROTTLE      FLAPS
    //
    // They slide VERTICALLY on the panel face. That is not a stylistic choice — it is what
    // lets them be compact and mounted flat on the panel below the displays, instead of
    // three tall poles standing up off a pedestal, and it agrees with 14 CFR 23.779, which
    // accepts up/forward for "increase" and down/rearward for flaps and speed brakes to
    // extend.
    //
    // X = 0.115 is set by what the YOKE occludes: at 0.072 and again at 0.092 the inboard
    // (spoiler) control rendered behind the right yoke horn and could not be seen or
    // reached. SPACING 48 mm centre-to-centre sits in the band compact GA quadrants use
    // (40-50 mm); MIL-STD-1472F Fig.18 wants 50 mm for one-hand RANDOM access, but
    // 5.4.3.2.1.2 requires handle CODING when controls are grouped, and all three grips are
    // shape-coded (ball / flat paddle / ribbed bar), which is what buys the separation back.
    //
    // Y and Z: the block sits on the panel face at z = 0.7485 (6 mm proud), spanning
    // y 0.322..0.452 — under the displays, whose lower edge is y 0.462, and above the
    // sub-panel lip. It is BELOW the seated forward view (everything under y = 0.461 is, in
    // this cockpit, at 55 deg field of view) and is a glance-down control, as the throttle
    // is in a real 172.
    // POSITION, measured against the REAL pilot view (the cockpit camera is 78 deg
    // vertical — CockpitBuilder — not the 55 deg the verification tool used to force).
    // Every bound below was read off that view, not estimated:
    //
    //   yoke horns span      x -0.052 .. +0.048   -> the group must start outboard of 0.048
    //   panel's right edge   x  0.159             -> and end inboard of it
    //   MFD's lower edge     y  0.4856            -> travel must top out below that
    //
    // That gives a 111 mm window, which comfortably takes three controls at 45 mm centres.
    // 45 mm is in the band compact GA quadrants use (40-50 mm); MIL-STD-1472F Fig.18 asks
    // 50 mm for one-hand RANDOM access, but 5.4.3.2.1.2 requires handle CODING when
    // controls are grouped, and all three grips are shape-coded per 14 CFR 23.781 (ball /
    // flat paddle / ribbed bar), which is what buys the separation back.
    const float SlideX = 0.105f, SlideSpacing = 0.045f;
    // y = 0.432 with 44 mm of travel puts the whole group at viewport y ~0.02..0.19 —
    // fully inside the pilot's view, and with the top of the ESCUTCHEON below the MFD's
    // lower edge (viewport 0.24) so no display is occluded.
    //
    // Both bounds were measured, and both were wrong at first. Sizing against the TRAVEL
    // rather than the escutcheon put the plate through the bottom of the MFD; and the
    // spoiler sits directly under the MFD in x, so it is the control that has to clear it.
    // Dropping the group instead pushed the placards off the bottom of the frame, which is
    // why they now sit ABOVE their controls.
    const float SlideY = 0.432f, SlideZ = 0.7485f;
    /// <summary>Vertical travel of a slide handle, metres. The handle's position IS the
    /// value: 0 % of travel = 0.0, 50 % = 0.5, 100 % = 1.0.</summary>
    const float SlideTravel = 0.044f;
    static readonly Vector3 SpoilerLeverPos = new Vector3(SlideX - SlideSpacing, SlideY, SlideZ);
    static readonly Vector3 ThrottlePos     = new Vector3(SlideX,                SlideY, SlideZ);

    // ── THE THROTTLE QUADRANT LEVER ────────────────────────────────────────────────
    // The throttle is a LEVER ON A PIVOT, not a carriage in a slot: a 65 mm arm that
    // sweeps 44 deg, pointing aft out of a casing on the panel so the knob stands 60 mm
    // proud where a hand meets it.
    //
    // The pivot sits ON the panel at ThrottlePos; the KNOB, at rest length along the arm,
    // is what the pilot sees and reaches for. Those are 65 mm apart, and which of the two
    // the capture sphere is centred on decides whether the control can be grabbed at all —
    // see ThrottleGrabPos.
    const float LeverArm      = 0.065f;   // pivot to knob centre
    const float LeverSweepDeg = 44f;      // idle to full power, total
    const float KnobDia       = 0.024f;

    /// <summary>The arm's angle ABOVE HORIZONTAL at idle — the rest rake, carried by the
    /// pivot. 20 deg, and it is the parameter that decides whether the control can be seen
    /// at all, so it is derived from the seat rather than picked for symmetry.
    ///
    /// A lever that rests pointing straight aft (0 deg) or below it hangs its knob the full
    /// arm length toward the pilot and 20-odd mm down. Built that way, at 22 deg below
    /// horizontal, the ball came out at 41.6 deg below the eye line — BELOW the bottom edge
    /// of the pilot's default view, with 76% of it off the frame (measured: viewport y
    /// -0.16 to +0.05). It worked perfectly and could not be seen.
    ///
    /// Raking the whole sweep up puts the knob higher AND closer to the panel at both ends,
    /// because the knob rides a circle: cos shortens the standoff as sin lifts it. At 20 deg
    /// the idle ball sits 36.4 deg below the eye line, which is where the previous straight
    /// slide handle sat, and it is in frame.
    ///
    /// It is not free. Vertical knob travel falls from 48.7 mm (symmetric) to 36.2 mm,
    /// because the span is L*(sin(a+44) - sin a) and that shrinks as the rake grows. The
    /// knob makes up the difference fore-and-aft, so the total movement is still the full
    /// 48.7 mm chord, and idle-aft/full-forward is the 14 CFR 23.779 sense of the control
    /// anyway.
    ///
    /// 32 deg, not 20, and the 12 deg between them is the whole budget. At 20 deg the ball's
    /// lowest point measured viewport y = -0.023 — still clipped by the bottom of the frame,
    /// barely — while the top of the group measured y = 0.4878, 2.2 mm THROUGH the MFD's
    /// lower edge. Squeezed from both ends at once, because the knob's vertical span and the
    /// gap between the frame floor and the display ceiling are within a few mm of each other.
    ///
    /// Raking further buys at the bottom faster than it costs at the top: the span
    /// L*(sin(a+44) - sin a) shrinks as a grows, so the idle end rises more than the power
    /// end does. 20 -> 32 deg lifts idle by 7.8 mm and full by only 2.2 mm, and shortens the
    /// idle standoff by 6 mm as well, which lifts the apparent elevation again.
    const float LeverIdleDeg = 32f;
    static readonly float LeverMidDeg = LeverIdleDeg + LeverSweepDeg * 0.5f;


    /// <summary>The pivot, on the panel. y is set by the ceiling: the knob's highest point
    /// at full power must stay below the MFD's lower edge at y = 0.4856, which with a 32 deg
    /// rake puts the pivot at 0.4075 and leaves 3 mm of clearance. Checked in the control
    /// battery across the whole sweep, because this bound was measured and was wrong on the
    /// first attempt.</summary>
    static readonly Vector3 ThrottlePivotPos = new Vector3(SlideX, 0.4075f, SlideZ);

    /// <summary>WHERE THE CAPTURE SPHERE GOES: on the KNOB's mid-travel position, one arm
    /// length aft of the pivot — NOT on the pivot.
    ///
    /// The knob is 65 mm from the pivot at every point in the sweep, so a sphere centred on
    /// the pivot would need a 77 mm radius just to contain the thing being aimed at, and
    /// would swallow half the panel doing it. Centred on the knob's mid position the knob
    /// is never more than 25 mm away — the arc's own sagitta and half-chord — and a modest
    /// radius covers both stops. This is the arc equivalent of the straight slot's problem,
    /// where the sphere sat on the middle of the channel while the pilot aimed at a handle
    /// 22 mm away at either end.</summary>
    static readonly Vector3 ThrottleGrabPos = ThrottlePivotPos + new Vector3(
        0f,
         LeverArm * Mathf.Sin(LeverMidDeg * Mathf.Deg2Rad),
        -LeverArm * Mathf.Cos(LeverMidDeg * Mathf.Deg2Rad));

    /// <summary>Pivot expressed in the CONTROL's frame — the exact inverse of the offset
    /// above, so the two can never drift apart.</summary>
    static readonly Vector3 PivotFromGrab = ThrottlePivotPos - ThrottleGrabPos;
    static readonly Vector3 FlapLeverPos    = new Vector3(SlideX + SlideSpacing, SlideY, SlideZ);
    // left-hand engine / systems group
    static readonly Vector3 CarbHeatPos     = new Vector3(-0.068f, 0.385f, PanelZ);
    static readonly Vector3 FuelSelPos      = new Vector3(-0.068f, 0.335f, PanelZ);
    // LoadShedPos / AltStaticPos retired 23 Aug 2026 with the switches themselves.
    // pedestal
    static readonly Vector3 TrimWheelPos    = new Vector3(-0.152f, 0.318f, 0.7425f);
    // BRAKE HANDLE. Under the LEFT edge of the panel — which is where a 172's brake
    // handle actually is ("under the left side of the instrument panel", POH Fig. 7-2) and,
    // more to the point, somewhere a seated hand can reach without groping into the
    // footwell.
    //
    // The pedals were the brake control until now, and they were unusable: measured at 45
    // deg below the eye line, tucked under the panel, and reachable only by putting a hand
    // into the footwell mid-taxi. Worse, Object_52 was REPARENTED under the control, so
    // the control and an unrelated piece of aircraft geometry moved and hid together —
    // exactly the coupling that must not exist. The pedals are now driven by a separate
    // follower that only READS brake pressure (see PedalBrakeVisual), and the brake
    // control is its own object with its own geometry.
    // BRAKE HANDLE, lower-LEFT panel — where a 172's brake handle actually is ("under the
    // left side of the instrument panel", POH Fig. 7-2), and reachable from the seat.
    //
    // y = 0.435 was set against the REAL pilot view (78 deg): at 0.345 the handle sat below
    // the bottom edge of the frame and the participant never saw it. 0.435 puts it at
    // viewport y ~0.11, inboard of the panel's left edge and clear of the PFD, whose lower
    // edge is y = 0.462.
    static readonly Vector3 BrakePos        = new Vector3(-0.130f, 0.435f, 0.7485f);

    // ── LABEL SCALE ────────────────────────────────────────────────────────────────
    // Cockpit placards, not captions. The previous pass drew ~5 cm letters: measured from
    // the pilot's eye the CARB HEAT label was 0.449 of screen width against the PFD's
    // 0.167, i.e. the label was 2.7x wider than the primary flight instrument. Real
    // placard lettering is 4-6 mm. PlacardText is the cap height in metres; everything
    // else is expressed as a multiple of it so the hierarchy cannot drift again.
    const float PlacardText = 0.0045f;   // primary control placards
    const float DetentText  = 0.0036f;   // detent marks (L / BOTH / R, flap gates)

    static readonly Color Black  = new Color(0.075f, 0.075f, 0.085f);
    static readonly Color Steel  = new Color(0.62f, 0.63f, 0.66f);
    static readonly Color Red    = new Color(0.72f, 0.10f, 0.08f);
    static readonly Color Blue   = new Color(0.16f, 0.34f, 0.68f);
    static readonly Color White  = new Color(0.97f, 0.97f, 0.98f);   // placard legend
    static readonly Color Orange = new Color(0.88f, 0.45f, 0.06f);
    static readonly Color PanelDark = new Color(0.255f, 0.260f, 0.272f);  // cockpit furniture
    static readonly Color PanelEdge = new Color(0.335f, 0.342f, 0.355f);  // bezel / escutcheon
    static readonly Color PedestalGrey = new Color(0.285f, 0.292f, 0.303f); // pedestal body

    // ── QUADRANT PALETTE ───────────────────────────────────────────────────────────
    // Restrained and desaturated. The previous levers were a pure blue cube and a pure
    // white cube, which read as untextured debug primitives rather than aircraft parts.
    // Real cockpit furniture is near-neutral dark grey; only the HANDLES carry colour,
    // and only where an aviation convention calls for it.
    static readonly Color QuadBody   = new Color(0.185f, 0.190f, 0.200f);  // quadrant casing
    static readonly Color QuadFace   = new Color(0.235f, 0.240f, 0.250f);  // faceplate the slots cut through
    static readonly Color QuadEdge   = new Color(0.310f, 0.318f, 0.330f);  // cheeks, bosses, detent teeth
    static readonly Color SlotDark   = new Color(0.030f, 0.030f, 0.035f);  // inside of a slot
    static readonly Color LeverSteel = new Color(0.255f, 0.262f, 0.280f);  // lever blades
    static readonly Color KnobBlack  = new Color(0.048f, 0.048f, 0.052f);  // throttle knob (FAA: black)
    static readonly Color FlapWhite  = new Color(0.760f, 0.762f, 0.772f);  // flap paddle
    static readonly Color SpoilBlue  = new Color(0.125f, 0.185f, 0.325f);  // spoiler grip, dark navy
    static readonly Color Placard    = new Color(0.780f, 0.790f, 0.805f);  // engraved legend

    Transform model;
    CessnaPhysics phys;
    AircraftController ctl;
    AircraftSystems sys;

    public void Build(Transform glbModel, CessnaPhysics physics)
    {
        Instance = this;
        model = glbModel;
        phys = physics;
        ctl = phys.GetComponent<AircraftController>();
        sys = phys.GetComponent<AircraftSystems>();

        // ═══════════════════════════════════════════════════════════════════════
        // MINIMAL COCKPIT — 3 September 2026, on request.
        // ═══════════════════════════════════════════════════════════════════════
        //
        // The cockpit contains exactly three things: the CONTROL YOKE, and the two glass
        // displays (built by LivePFD and LiveMFD, not here). Nothing else is generated —
        // no throttle, no flap lever, no brake, no trim wheel, no carburettor heat, no
        // fuel selector, no sub-panel, no systems bay, no placards.
        //
        // WHAT THIS DOES NOT CHANGE
        //   The aeroplane still has every one of those inputs. They live in
        //   CessnaPhysics / AircraftController / AircraftSystems exactly as before, they
        //   are still driven by the keyboard, still logged in telemetry, and still set by
        //   ScenarioEngine at the start of each trial. Only the grabbable cockpit OBJECTS
        //   are gone. No mission, no checklist and no telemetry column changed.
        //
        // THE CONSEQUENCE, STATED PLAINLY
        //   Those inputs become KEYBOARD-ONLY. On the desktop the simulator is unchanged
        //   and fully flyable. In a headset there is no keyboard, so a participant in VR
        //   can move the yoke and nothing else — they cannot set power, flaps, brakes or
        //   trim by hand, and the systems drills in the HIGH missions cannot be performed.
        //   That is a deliberate decision recorded here, not an oversight.
        //
        //   The builders below are all retained and unreferenced, so restoring any single
        //   control is one line in this list.
        //
        // ═══════════════════════════════════════════════════════════════════════
        // AMENDED 6 September 2026 — THE THROTTLE IS BACK. Nothing else is.
        // ═══════════════════════════════════════════════════════════════════════
        //
        // The cockpit now contains the YOKE, the THROTTLE, and the two glass displays.
        // Everything in the paragraphs above still stands for every OTHER control: flaps,
        // brake, trim, carburettor heat and the fuel selector remain keyboard-only, and a
        // participant in a headset still cannot perform the HIGH missions' systems drills.
        //
        // WHY THE THROTTLE AND NOT THE REST
        //   Power is the one input that is set continuously through every phase of every
        //   mission — take-off, climb, cruise, descent, go-around — where the others are
        //   set a handful of times at known moments. Restoring it is what makes the
        //   aeroplane flyable by hand; restoring the others would rebuild the cluttered
        //   panel that was deliberately removed.
        //
        // NO NEW FURNITURE
        //   BuildStructure() is deliberately NOT called. It would draw the sub-panel, its
        //   lip and the systems bay — backing plates for controls that no longer exist.
        //   BuildQuadrantHousing() IS called, but it builds no geometry at all: it is a
        //   naming root with no renderer, and exists only so the throttle has a parent.

        var list = new System.Collections.Generic.List<PhysicalControl>();
        BuildQuadrantHousing();
        list.Add(BuildYoke());
        list.Add(BuildThrottle());

        list.RemoveAll(c => c == null);
        Controls = list.ToArray();

        // The interactors that can drive these. Both are always present; each no-ops
        // when its hardware is not the active modality, so there is exactly one
        // simulation and no build-time branching.
        if (phys.GetComponent<CockpitInteractorMouse>() == null)
            phys.gameObject.AddComponent<CockpitInteractorMouse>();
        if (phys.GetComponent<CockpitInteractorVR>() == null)
            phys.gameObject.AddComponent<CockpitInteractorVR>();

        Debug.Log("[CockpitRig] built " + Controls.Length + " physical controls on the GLB cockpit.");
    }

    /// <summary>Reset every control to the state a fresh trial specifies. Called from
    /// ScenarioEngine so a control the participant moved cannot survive into the next
    /// mission — the same class of leak that flaps had.</summary>
    public void ResetAll(float flaps01)
    {
        int flapDetent = flaps01 > 0.75f ? 2 : flaps01 > 0.25f ? 1 : 0;
        foreach (var c in Controls)
        {
            if (c == null) continue;
            switch (c.spec.target)
            {
                case ControlTarget.Flaps:      c.SetSilently(AircraftController.FlapDetents[flapDetent], flapDetent); break;
                case ControlTarget.Throttle:   c.SetSilently(phys != null ? phys.Throttle01 : 0f); break;
                case ControlTarget.Spoiler:      c.SetSilently(0f, 0); break;   // always stowed
                case ControlTarget.FuelSelector: c.SetSilently(0.5f, 1); break;  // BOTH
                case ControlTarget.CarbHeat:
                case ControlTarget.LoadShed:
                case ControlTarget.AlternateStatic: c.SetSilently(0f, -1, false); break;
                case ControlTarget.PitchRoll:
                case ControlTarget.Trim:
                case ControlTarget.Rudder:     c.SetSilently(0f); break;
                default:                       c.SetSilently(0f, -1, false); break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // THE CONTROL QUADRANT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>The quadrant the three levers are mounted in.
    ///
    /// This exists so the levers are visibly ATTACHED to the aeroplane. The previous
    /// build drew each lever as a bare primitive standing on a flat deck with a painted
    /// black rectangle for a slot, which is why they read as debug objects: there was no
    /// casing, no cheeks, no faceplate, and nothing for a lever to pivot IN.
    ///
    /// The casing is one shared piece of furniture; the levers are separate objects
    /// parented to it, so each keeps its own collider, animation and telemetry.</summary>
    Transform quadrant;

    void BuildQuadrantHousing()
    {
        // A NAMING ROOT ONLY — no geometry.
        //
        // The first version of this put a 168 x 130 mm backing plate behind the three
        // controls to "group" them. From the seat that read as a large blank panel bolted
        // to the aeroplane, which is precisely the extra panel this cockpit is not supposed
        // to grow. The three controls are grouped by being 48 mm apart in a row with
        // matching hardware; they do not need a slab behind them to say so.
        //
        // Nothing is parented to this that owns geometry, so it cannot move, hide or
        // otherwise affect a control.
        var g = new GameObject("ControlArea");
        g.transform.SetParent(model, false);
        g.transform.localPosition = new Vector3(SlideX, SlideY, SlideZ);
        g.transform.localRotation = Quaternion.identity;
        quadrant = g.transform;
    }



    /// <summary>The FIXED half of a quadrant lever: the slot it runs in and the pivot
    /// boss it turns on. Built per lever, so every lever owns its own mounting.</summary>
    /// <summary>The FIXED half of a slide control: the recessed channel the handle runs
    /// in, plus its end stops. Built per control, so every control owns its own mounting
    /// and nothing is shared between neighbours.</summary>
    /// <summary>The FIXED half of a slide control: a small escutcheon, the channel the
    /// handle runs in, and its end stops. Built per control, so every control owns its own
    /// mounting and nothing is shared with a neighbour.
    ///
    /// NOTE ON SIGN: the pilot's eye is at z = 0.52 and the panel face at z = 0.755, so
    /// +Z points INTO the panel, away from the pilot. Everything that should stand proud
    /// of the panel is therefore at NEGATIVE local z. Building this with +Z buried the
    /// whole control inside the panel — visible in the control tests as a perfectly
    /// working slider that could not be seen.</summary>
    void SlideMount(Transform t)
    {
        float half = SlideTravel * 0.5f;
        // Escutcheon: the plate the control is let into, so it is mounted rather than
        // stuck on. Small — it hugs this control only.
        // Height is travel + 18 mm, not + 28 mm: the taller plate reached y = 0.492 and
        // overlapped the MFD's lower edge at 0.4856. The ESCUTCHEON, not the travel, is the
        // tallest part of the control and is what has to clear the display.
        Gloss(Box(t, new Vector3(0f, 0f, -0.002f),
                  new Vector3(0.030f, SlideTravel + 0.018f, 0.005f), QuadBody), 0.16f);
        // The channel itself, recessed into that plate.
        Gloss(Box(t, new Vector3(0f, 0f, -0.005f),
                  new Vector3(0.015f, SlideTravel + 0.016f, 0.004f), SlotDark), 0.05f);
        // Machined side rails: what makes it read as a channel and not a painted stripe.
        foreach (float sx in new[] { -0.0105f, 0.0105f })
            Gloss(Box(t, new Vector3(sx, 0f, -0.007f),
                      new Vector3(0.005f, SlideTravel + 0.016f, 0.007f), QuadEdge), 0.34f);
        // End stops, top and bottom.
        foreach (float sy in new[] { -(half + 0.009f), half + 0.009f })
            Gloss(Box(t, new Vector3(0f, sy, -0.007f),
                      new Vector3(0.026f, 0.004f, 0.007f), QuadEdge), 0.34f);
    }



    /// <summary>The MOVING half: pivot -> arm -> blade. Returns the ARM, which handles
    /// hang off; the pivot (arm.parent) is what the spec animates.
    ///
    /// The blade is thin across the cockpit and deep fore-and-aft — the proportions of a
    /// real quadrant lever, which is a flat blade rather than a round rod, so its travel
    /// direction is legible edge-on from the seat.</summary>
    /// <param name="biasDeg">Static rake of the arm at rest. It must be the OPPOSITE SIGN
    /// of the direction the spec animates, so the lever ends up swinging symmetrically
    /// +-22 deg about vertical.
    ///
    /// Getting this wrong is not cosmetic and it happened here: the flap and spoiler specs
    /// animate about Vector3.left while the throttle animates about Vector3.right, so a
    /// shared -22 deg bias sent those two from -22 deg to -66 deg — a 66 deg sweep that
    /// laid the handles flat across the front of the quadrant and over their own placards,
    /// while the throttle swung a correct +-22 deg. The travel DIRECTIONS were right all
    /// along (14 CFR 23.779: power forward to increase, flaps and speed brakes aft to
    /// extend); it was the rest position that was wrong.</param>
    /// <summary>The MOVING half of a slide control: a carriage that rides the channel and
    /// carries a shaped grip. Returns the carriage, which the spec translates.
    ///
    /// The carriage TRANSLATES; it never rotates. That is the behaviour asked for — grab,
    /// slide, release, and the handle stays where it was put — and it also means the
    /// handle's position along its slot IS the control's value, with no second animation
    /// path that could disagree with the aircraft.
    ///
    /// <paramref name="startAtTop"/> places the carriage at the t = 0 end of the channel.
    /// Throttle starts at the BOTTOM (0 = idle, slide up for power); flaps and spoiler
    /// start at the TOP (0 = retracted, slide down to extend, per 14 CFR 23.779).</summary>
    /// <summary>The MOVING half: a carriage that rides the channel and carries a shaped
    /// grip. Returns the carriage, which the spec translates.
    ///
    /// The carriage TRANSLATES and never rotates — grab, slide, release, and the handle
    /// stays where it was put. Its position along the channel IS the control's value, so
    /// there is no second animation path that could disagree with the aeroplane.
    ///
    /// <paramref name="startAtTop"/> puts the carriage at the t = 0 end. Throttle starts at
    /// the BOTTOM (0 = idle, slide up for power); flaps and spoiler start at the TOP
    /// (0 = retracted, slide down to extend, per 14 CFR 23.779).</summary>
    Transform SlideHandle(Transform t, bool startAtTop)
    {
        var carriage = new GameObject("Handle").transform;
        carriage.SetParent(t, false);
        carriage.localPosition = new Vector3(0f, startAtTop ? SlideTravel * 0.5f : -SlideTravel * 0.5f, 0f);
        // Carriage body: spans the rails and sits in the channel, so the grip above it
        // never reads as floating in front of the panel.
        Metal(Gloss(Box(carriage, new Vector3(0f, 0f, -0.009f),
                        new Vector3(0.024f, 0.016f, 0.010f), LeverSteel), 0.55f), 0.6f);
        return carriage;
    }



    static Transform Metal(Transform t, float m)
    {
        var r = t.GetComponent<Renderer>();
        if (r != null) r.material.SetFloat("_Metallic", m);
        return t;
    }

    static Transform Gloss(Transform t, float g)
    {
        var r = t.GetComponent<Renderer>();
        if (r != null) r.material.SetFloat("_Glossiness", g);
        return t;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // THE CONTROLS
    // ═══════════════════════════════════════════════════════════════════════════

    PhysicalControl BuildYoke()
    {
        // The yoke geometry is already rigged by RealCockpit (YokeRoot/YokeVisual) and
        // already follows pitchInput/rollInput, so grabbing it and writing input makes
        // the visual agree automatically — the "visual 50% = aircraft 50%" requirement
        // is satisfied by construction rather than by a second animation path.
        var yokeVisual = FindDeep(model, "YokeVisual");
        Vector3 pos = yokeVisual != null ? yokeVisual.position
                                         : model.TransformPoint(new Vector3(0f, 0.46f, 0.66f));

        var c = Make("yoke", pos);
        c.spec = new ControlSpec
        {
            id = "yoke", label = "YOKE", kind = ControlKind.Yoke, target = ControlTarget.PitchRoll,
            // Fore/aft = pitch, left/right = roll, in the cockpit's own frame.
            axis = Vector3.forward, secondaryAxis = Vector3.right,
            // 16 cm of fore/aft travel and 18 cm across. A 172's yoke moves roughly this
            // far, and it is large enough that a participant's hand tremor is a few
            // percent of full deflection rather than a few tens of percent.
            travel = 0.16f, secondaryTravel = 0.18f,
            centred = true,
            // 65 mm, REDUCED FROM 130 mm when the throttle came back. Measured, not guessed.
            //
            // CockpitInteractorMouse picks the eligible control NEAREST ALONG THE RAY, and
            // the yoke sits 45 mm closer to the pilot than the panel. A mouse ray aimed at
            // the throttle knob passes 93 mm from the yoke's centre — inside a 130 mm
            // capture sphere — so the yoke was selected instead and the throttle, though
            // built, visible and correctly wired, could not be grabbed with a mouse at all.
            // ControlTestHarness.ThrottleIsWhatTheMouseGrabs is what caught it and is what
            // will catch it again.
            //
            // 65 mm keeps a 28 mm margin against that worst case (93 mm, at idle; the
            // margin only grows as power comes up) and still leaves the yoke by far the
            // largest target in the cockpit: at the 185 mm the hub sits from the eye, 65 mm
            // subtends about 19 deg, roughly a quarter of the frame's width.
            //
            // VR was never affected — CockpitInteractorVR picks the control NEAREST THE
            // HAND, and a hand on the knob is 0 mm from the throttle against 140 mm from
            // the yoke. This is a mouse-only defect, which is exactly why aiming, and not
            // just grabbing, has to be tested.
            captureRadius = 0.065f,
            smoothingTau = 0.045f,
            // Slight softening around neutral so small hand jitter is not full-scale
            // aileron, without making large deflections feel dead.
            responseExponent = 1.25f,
            deadZone = 0.02f,
        };
        Configure(c);
        return c;
    }

    PhysicalControl BuildThrottle()
    {
        // THROTTLE — the control that must be identifiable without looking. Shape does that
        // work, not colour: a round ball grip, the 14 CFR 23.781(b) powerplant shape, and
        // the only spherical grip in the cockpit.
        //
        // A QUADRANT LEVER: a 65 mm arm on a pivot mounted on the panel, sweeping 44 deg in
        // the fore/aft plane. The knob rises for power (23.779: "forward to increase forward
        // thrust"; up is the accepted equivalent on a panel-mounted control) and its angle
        // along the arc IS the value — bottom stop = idle, top stop = full.
        //
        // The capture sphere is centred on the KNOB's mid-travel position, not on the pivot;
        // ThrottleGrabPos explains why that distinction decides whether it can be grabbed.
        var c = Make("throttle", model.TransformPoint(ThrottleGrabPos));
        c.transform.SetParent(quadrant, true);

        // The pivot, in the control's own frame. +Z points AT the panel, so the casing sits
        // forward and above the capture centre and the lever reaches down and back toward
        // the pilot at idle.
        LeverCasing(c.transform, PivotFromGrab);

        // ═══════════════════════════════════════════════════════════════════════════
        // THE REST ANGLE LIVES ON THE PIVOT. IT IS NEVER ADDED TO THE ANIMATED VALUE.
        // ═══════════════════════════════════════════════════════════════════════════
        //
        // Two separate reasons, and both have already cost this project a build.
        //
        // ONE — the Awake() capture bug, rotation edition. PhysicalControl reads its
        // visual's rest pose in Awake(), which Unity runs at AddComponent, inside Make(),
        // BEFORE spec.visual is assigned. The captured rest is therefore ALWAYS the
        // identity, whatever the arm's real rest pose is, and UpdateVisual writes an
        // absolute localRotation of AngleAxis(t * visualTravel, axis). An arm carrying its
        // own -22 deg rake would be driven from 0 to +44 deg instead of -22 to +22, i.e.
        // hard against one stop at idle and 22 deg past the other at full power. The
        // translating side of this is worked around the same way, on HandleBase.
        //
        // TWO — the +-22 -> -66 deg bug, which actually happened. An earlier build gave the
        // levers a SHARED rest-angle bias while their specs animated about Vector3.left and
        // Vector3.right respectively. For the two that animated the other way the bias and
        // the travel added instead of cancelling, swinging those handles through 66 deg and
        // laying them flat across their own placards. The travel DIRECTIONS were correct
        // throughout; the rest position was what was wrong.
        //
        // Both disappear if the rest pose is a fixed parent transform and the animated
        // child starts from identity. The pivot rakes; the arm animates; they cannot
        // interact, and no sign convention has to agree with anything.
        var pivot = new GameObject("LeverPivot").transform;
        pivot.SetParent(c.transform, false);
        pivot.localPosition = PivotFromGrab;
        pivot.localRotation = Quaternion.Euler(LeverIdleDeg, 0f, 0f);

        var arm = new GameObject("Arm").transform;
        arm.SetParent(pivot, false);
        arm.localPosition = Vector3.zero;
        arm.localRotation = Quaternion.identity;   // MUST be identity — see above

        // Blade: thin across the cockpit and deep in the swing plane, so its travel is
        // legible edge-on from the seat the way a real quadrant lever is.
        Metal(Gloss(Box(arm, new Vector3(0f, 0f, -LeverArm * 0.5f),
                        new Vector3(0.009f, 0.016f, LeverArm), LeverSteel), 0.55f), 0.6f);
        // Collar where the blade meets the grip, so the ball is mounted rather than stuck on.
        var collar = Cylinder(arm, new Vector3(0f, 0f, -LeverArm + 0.010f), 0.0075f, 0.005f, QuadEdge);
        collar.localRotation = Quaternion.Euler(90f, 0f, 0f);      // axis along the blade
        Gloss(collar, 0.4f);

        var knob = Sphere(arm, new Vector3(0f, 0f, -LeverArm), KnobDia, KnobBlack);
        Gloss(knob, 0.30f);

        // ── GEARING: MEASURED FROM THE BUILT LEVER, NOT COMPUTED FROM THE DESIGN ────
        //
        // spec.travel is the distance THE HAND moves, in WORLD metres, resolved onto
        // spec.axis. Two things make that easy to get wrong here, and both would show up
        // only as a lever that feels heavy rather than as a failure:
        //
        //   The arc. The knob's straight-line movement between the stops is the chord,
        //   48.7 mm, but only its VERTICAL component, 28.6 mm, lies along spec.axis. Gear
        //   to the chord and the hand has to travel 49 mm to drag the ball 29 mm — the ball
        //   visibly lagging the hand that is holding it, which is the exact failure the
        //   position-based control model exists to avoid.
        //
        //   The scale. Every constant in this file is in the GLB's frame, and the model is
        //   not at unit scale, so a design distance is not a world distance.
        //
        // Rather than reason about either, swing the finished lever between its stops and
        // measure where the knob actually goes. That is correct by construction for any
        // arm, sweep, rake or model scale.
        Vector3 axisWorld = c.transform.TransformDirection(Vector3.up).normalized;
        arm.localRotation = Quaternion.identity;
        Vector3 knobAtIdle = knob.position;
        arm.localRotation = Quaternion.AngleAxis(LeverSweepDeg, Vector3.right);
        Vector3 knobAtFull = knob.position;
        arm.localRotation = Quaternion.identity;
        float handTravel = Vector3.Dot(knobAtFull - knobAtIdle, axisWorld);
        Debug.Log("[CockpitRig] throttle gearing: the knob rises " + (handTravel * 1000f).ToString("F1")
                  + " mm in world metres between the stops; the hand is geared to that.");

        // NO PLACARD. Labels were removed from this cockpit on 3 September and the brief
        // for restoring the throttle was "nothing else". The ball grip is the only
        // spherical control in the cabin and the only thing on the lower panel, so there
        // is nothing it can be confused with. Restoring the legend is one line:
        //   Label(c.transform, PivotFromGrab + new Vector3(0f, 0.040f, -0.006f),
        //         "THROTTLE", PlacardText, Placard);

        c.spec = new ControlSpec
        {
            id = "throttle", label = "THROTTLE", kind = ControlKind.Lever, target = ControlTarget.Throttle,
            axis = Vector3.up,          // the hand moves UP for power
            // Measured above by swinging the built lever stop to stop — see the note there.
            travel = handTravel,
            centred = false,
            // 55 mm, MEASURED at both stops rather than reasoned about.
            //
            // ControlTestHarness.ThrottleIsWhatTheMouseGrabs aims at the BALL and reports
            // what reaching it costs. On an arc the two ends are not symmetric — the knob
            // swings toward the pilot as well as up, so perspective moves it further off
            // the capture axis at one end than the other:
            //
            //     idle        25 mm off axis + 16 mm ball radius = 41 mm needed
            //     full power  21 mm off axis + 16 mm ball radius = 37 mm needed
            //
            // Sized against IDLE, the worse end, with 14 mm of margin so a click that lands
            // near the ball rather than dead on it still takes. Which end is worse moved
            // when the rake changed, which is the argument for measuring both every run
            // rather than reasoning about one.
            //
            // It cannot steal the yoke: a ray aimed at the yoke passes 111 mm from this
            // centre, twice the radius. That direction is now worth checking, because the
            // knob standing 60 mm proud of the panel put it NEARER the pilot than the yoke
            // hub, which reverses who wins nearest-along-the-ray.
            captureRadius = 0.055f,
            smoothingTau = 0.035f,
            visual = arm, visualIsRotation = true,
            visualAxis = Vector3.right, visualTravel = LeverSweepDeg,
        };
        Configure(c);
        c.SetSilently(phys != null ? phys.Throttle01 : 0f);
        return c;
    }

    /// <summary>The FIXED half of the quadrant: the escutcheon on the panel, the cheeks the
    /// blade swings between, the slot behind it and the two end stops.
    ///
    /// Built on the CONTROL, deliberately not on the pivot — furniture that rotated with the
    /// lever would be the same coupling that once made a rudder pedal move and hide together
    /// with the control that was supposed to be independent of it.
    ///
    /// The cheeks are a frame, not a block: the blade sweeps between them, so the casing has
    /// to be open down the middle or the lever passes through its own housing.</summary>
    void LeverCasing(Transform t, Vector3 pivot)
    {
        // Escutcheon, flat on the panel. Half-height 36 mm brackets the blade where it
        // leaves the casing at both ends of the sweep, and keeps the top of the casing at
        // model y 0.448 — clear of the MFD's lower edge at 0.4856. That bound is asserted
        // across the whole sweep in the control battery rather than trusted here, because it
        // was measured and it was wrong on the first attempt.
        Gloss(Box(t, pivot + new Vector3(0f, 0f, 0.002f),
                  new Vector3(0.034f, 0.072f, 0.005f), QuadBody), 0.16f);
        // Slot: the dark recess the blade runs out of.
        Gloss(Box(t, pivot + new Vector3(0f, 0f, -0.001f),
                  new Vector3(0.020f, 0.064f, 0.004f), SlotDark), 0.05f);
        // Cheeks either side of the blade. Inner faces at x +-9.5 mm against a 9 mm blade,
        // so the lever swings clear.
        foreach (float sx in new[] { -0.0125f, 0.0125f })
            Gloss(Box(t, pivot + new Vector3(sx, 0f, -0.012f),
                      new Vector3(0.006f, 0.068f, 0.024f), QuadEdge), 0.34f);
        // End stops, bracketing the sweep.
        foreach (float sy in new[] { -0.034f, 0.034f })
            Gloss(Box(t, pivot + new Vector3(0f, sy, -0.012f),
                      new Vector3(0.030f, 0.005f, 0.024f), QuadEdge), 0.34f);
        // Pivot boss, axis across the cockpit.
        var boss = Cylinder(t, pivot + new Vector3(0f, 0f, -0.004f), 0.008f, 0.013f, QuadEdge);
        boss.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Gloss(boss, 0.34f);
    }

    PhysicalControl BuildTrimWheel()
    {
        // A 172's trim wheel is a vertical wheel on the pedestal, wound fore/aft.
        var c = Make("trim", model.TransformPoint(TrimWheelPos));
        var wheel = Cylinder(c.transform, Vector3.zero, 0.026f, 0.007f, new Color(0.22f, 0.22f, 0.24f));
        wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);          // axis across the cockpit
        for (int i = 0; i < 8; i++)                                    // rim ribs, so rotation reads
        {
            float a = i * Mathf.PI * 2f / 8f;
            Box(wheel, new Vector3(0f, Mathf.Sin(a) * 0.025f, Mathf.Cos(a) * 0.025f),
                new Vector3(0.010f, 0.005f, 0.005f), Steel);
        }
        // Housing so the wheel is recessed in the pedestal with only its rim exposed,
        // which is how a 172 trim wheel actually presents.
        // Housing sits BEHIND the wheel only, so the rim stands proud and can be read and
        // reached. The first pass boxed the wheel in and hid it completely.
        // Backing boss into the pedestal side; the wheel itself stands proud of it.
        // Flat escutcheon on the panel, not a pedestal cheek: the wheel now lives on the
        // left panel and an 88 mm backing box there read as a slab.
        Box(c.transform, new Vector3(0f, 0f, 0.008f), new Vector3(0.062f, 0.062f, 0.010f), PanelEdge);
        Label(c.transform, new Vector3(0f, 0.026f, -0.004f), "TRIM", DetentText, White);
        // No legend. The ribbed wheel on the pedestal side is self-evident, and a
        // participant who needs to know is told by the drill text, not by the cockpit.

        c.spec = new ControlSpec
        {
            id = "trim", label = "TRIM", kind = ControlKind.TrimWheel, target = ControlTarget.Trim,
            axis = Vector3.forward,     // wind forward = nose down, back = nose up
            // 12 cm end-to-end for the FULL trim range. Deliberately long: trim is a fine
            // adjustment, and a short travel would make it a precision task, which is
            // exactly the unnecessary motor difficulty this build is meant to avoid.
            travel = 0.12f,
            centred = true,
            captureRadius = 0.030f,
            smoothingTau = 0.06f,
            visual = wheel, visualIsRotation = true,
            visualAxis = Vector3.forward, visualTravel = 160f,        // degrees at full trim
        };
        // Wind FORWARD for nose DOWN: displacement +Z must give trim −1.
        c.spec.axis = Vector3.back;
        Configure(c);
        return c;
    }

    PhysicalControl BuildFlapLever()
    {
        // FLAPS — shape-coded as a flat PADDLE, the CS-23/14 CFR 23.781(a) flap shape (the
        // handle is shaped like the surface it drives, exactly as a landing-gear handle is
        // shaped like a wheel). Flat where the throttle is round, so the two are told apart
        // by touch alone, in a headset, without looking.
        //
        // Slides DOWN to extend (23.779: "flaps — rearward/down to extend"), and stops at
        // three gates. THREE, because the simulation has exactly three flap states; giving
        // the handle continuous travel would let the cockpit disagree with the aeroplane.
        var c = Make("flaps", model.TransformPoint(FlapLeverPos));
        c.transform.SetParent(quadrant, true);

        SlideMount(c.transform);
        var h = SlideHandle(c.transform, startAtTop: true);

        // Flat paddle: thick leading edge tapering to a thin trailing edge — an aerofoil
        // section in miniature, which is what 23.781(a) depicts.
        Gloss(Box(h, new Vector3(0f, 0f, -0.018f), new Vector3(0.030f, 0.013f, 0.011f), FlapWhite), 0.18f);
        Gloss(Box(h, new Vector3(0f, 0f, -0.027f), new Vector3(0.030f, 0.009f, 0.008f), FlapWhite), 0.18f);

        // Gate teeth beside the channel: the three positions are visible BEFORE the handle
        // is touched, which is what a gate is for. No legends — the teeth say "three
        // positions" and the handle resting against one says which is selected.
        for (int i2 = 0; i2 < 3; i2++)
            Gloss(Box(c.transform, new Vector3(0.018f, SlideTravel * (0.5f - i2 * 0.5f), -0.007f),
                      new Vector3(0.008f, 0.0035f, 0.007f), QuadEdge), 0.40f);

        Label(c.transform, new Vector3(0f, SlideTravel * 0.5f + 0.003f, -0.006f),
              "FLAPS", PlacardText, Placard);

        c.spec = new ControlSpec
        {
            id = "flaps", label = "FLAPS", kind = ControlKind.DetentLever, target = ControlTarget.Flaps,
            axis = Vector3.down,        // slide down through the gates for more flap
            travel = SlideTravel,
            centred = false,
            captureRadius = 0.020f,   // < half the 45 mm spacing
            smoothingTau = 0.05f,
            detents = AircraftController.FlapDetents,
            detentLabels = AircraftController.FlapLabels,
            visual = h, visualIsRotation = false,
            visualAxis = Vector3.down, visualTravel = SlideTravel,
        };
        Configure(c);
        c.SetSilently(0f, 0);
        return c;
    }


    PhysicalControl BuildSpoilerLever()
    {
        // SPOILER — inboard control, shape-coded as a RIBBED BAR: neither round like the
        // throttle nor flat like the flap paddle, so all three are distinct by touch. Dark
        // navy, the glider convention for an airbrake, deliberately desaturated so it does
        // not become the brightest thing in a dark cockpit.
        //
        // Slides DOWN to extend (23.779: "speed brakes — aft to extend").
        //
        // HONEST LIMITATION: a 172 has no spoilers — this is a simulator control, and no
        // mission or checklist references it. AircraftController holds the spoiler
        // retracted for the whole of a recorded trial, so a participant cannot alter drag
        // and lift mid-mission. It works in FREE FLIGHT and CONTROL CHECK and parks itself
        // at UP during the twelve missions, reading the actual spoiler back each frame so
        // it shows the truth rather than a stale selection.
        var c = Make("spoiler", model.TransformPoint(SpoilerLeverPos));
        c.transform.SetParent(quadrant, true);

        SlideMount(c.transform);
        var h = SlideHandle(c.transform, startAtTop: true);

        Gloss(Box(h, new Vector3(0f, 0f, -0.020f), new Vector3(0.017f, 0.012f, 0.016f), SpoilBlue), 0.20f);
        for (int i2 = 0; i2 < 2; i2++)
            Gloss(Box(h, new Vector3(0f, -0.003f + i2 * 0.006f, -0.027f),
                      new Vector3(0.019f, 0.0025f, 0.006f), QuadBody), 0.15f);

        for (int i2 = 0; i2 < 3; i2++)
            Gloss(Box(c.transform, new Vector3(-0.018f, SlideTravel * (0.5f - i2 * 0.5f), -0.007f),
                      new Vector3(0.008f, 0.0035f, 0.007f), QuadEdge), 0.40f);

        Label(c.transform, new Vector3(0f, SlideTravel * 0.5f + 0.003f, -0.006f),
              "SPOILER", PlacardText, Placard);

        c.spec = new ControlSpec
        {
            id = "spoiler", label = "SPOILER", kind = ControlKind.DetentLever, target = ControlTarget.Spoiler,
            axis = Vector3.down,
            travel = SlideTravel,
            centred = false,
            captureRadius = 0.020f,   // < half the 45 mm spacing
            smoothingTau = 0.05f,
            detents = new[] { 0f, 0.5f, 1f },
            detentLabels = new[] { "UP", "HALF", "FULL" },
            visual = h, visualIsRotation = false,
            visualAxis = Vector3.down, visualTravel = SlideTravel,
        };
        Configure(c);
        c.SetSilently(0f, 0);
        return c;
    }


    PhysicalControl BuildBrakePedals()
    {
        // A pull handle: escutcheon on the panel underside, a shaft, and a T-grip that
        // comes AFT toward the pilot as pressure goes on. Spring-loaded, so releasing it
        // releases the brakes — a brake that stayed on would be both wrong and dangerous.
        //
        // Deliberately NOT a floating button, and deliberately not sharing geometry with
        // anything: every part below is created here, parented here, and nothing else in
        // the cockpit is reparented under it.
        var c = Make("brake", model.TransformPoint(BrakePos));

        // Fixed: mounting escutcheon and the barrel the shaft runs in.
        Gloss(Box(c.transform, new Vector3(0f, 0f, -0.002f), new Vector3(0.042f, 0.034f, 0.005f), QuadBody), 0.18f);
        var barrel = Cylinder(c.transform, new Vector3(0f, 0f, -0.010f), 0.0090f, 0.010f, QuadEdge);
        barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Gloss(barrel, 0.20f);

        // Moving: shaft + T-grip. Travels AFT (toward the pilot, -Z) as the brake comes on.
        var pull = new GameObject("Pull").transform;
        pull.SetParent(c.transform, false);
        Metal(Gloss(Cylinder(pull, new Vector3(0f, 0f, -0.022f), 0.0055f, 0.014f, LeverSteel), 0.6f), 0.7f);
        var shaft = pull.GetChild(0);
        shaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Gloss(Box(pull, new Vector3(0f, 0f, -0.038f), new Vector3(0.038f, 0.014f, 0.012f), KnobBlack), 0.28f);

        Label(c.transform, new Vector3(0f, -0.026f, -0.004f), "BRAKE", PlacardText, Placard);

        c.spec = new ControlSpec
        {
            id = "brake", label = "BRAKE", kind = ControlKind.SpringLever, target = ControlTarget.WheelBrake,
            axis = Vector3.back,        // pull it toward you to brake
            travel = 0.045f,
            centred = false,
            // 30 mm. The nearest neighbour is the carb-heat knob 62 mm away with a 26 mm
            // radius; 45 mm here would have summed to 71 mm and overlapped it, so a reach
            // for carb heat could have taken the brake.
            captureRadius = 0.030f,
            smoothingTau = 0.03f,       // brakes must feel immediate
            visual = pull, visualIsRotation = false,
            visualAxis = Vector3.back, visualTravel = 0.030f,
        };
        Configure(c);

        // The PEDALS still show the brake, but as an INDEPENDENT follower.
        AttachPedalFollower();
        return c;
    }

    /// <summary>Tilt the aeroplane's own rudder/brake pedals with applied brake pressure.
    ///
    /// This is a one-way READ of `phys.brakeInput01`. The pedals are not parented to the
    /// brake control and the brake control does not own them, so hiding, moving or
    /// rebuilding either one cannot affect the other. That independence is the whole point:
    /// the previous build reparented Object_52 under the control, which coupled an
    /// interactable to an unrelated piece of the airframe.</summary>
    void AttachPedalFollower()
    {
        var pedals = FindDeep(model, "Object_52");
        if (pedals == null)
        {
            Debug.LogWarning("[CockpitRig] Object_52 (pedals) not found — brake handle still works, pedals just will not animate.");
            return;
        }
        var pivot = new GameObject("PedalPivot").transform;
        pivot.SetParent(pedals.parent, false);
        pivot.localPosition = pedals.localPosition;
        pivot.localRotation = pedals.localRotation;
        pedals.SetParent(pivot, true);          // keeps the mesh exactly where it is

        var f = pivot.gameObject.AddComponent<PedalBrakeVisual>();
        f.phys = phys;
        f.maxTiltDeg = 20f;                     // top of the 15-20 deg band for a GA toe brake
    }

    PhysicalControl BuildCarbHeat()
    {
        // Small pull knob on the left systems bay, as the reference shows. Orange is the
        // conventional carb-heat colour. Pulled out = ON.
        var c = Make("carb_heat", model.TransformPoint(CarbHeatPos));

        var boss = Cylinder(c.transform, new Vector3(0f, 0f, 0.004f), 0.010f, 0.006f, PanelEdge);
        boss.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var plunger = new GameObject("Plunger").transform;
        plunger.SetParent(c.transform, false);
        plunger.localPosition = Vector3.zero;
        var shaft = Cylinder(plunger, new Vector3(0f, 0f, -0.012f), 0.003f, 0.014f, Steel);
        shaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var knob = Cylinder(plunger, new Vector3(0f, 0f, -0.026f), 0.0075f, 0.007f, Orange);
        knob.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Label(c.transform, new Vector3(0f, 0.019f, -0.004f), "CARB HEAT", DetentText, Orange);

        c.spec = new ControlSpec
        {
            id = "carb_heat", label = "CARB HEAT", kind = ControlKind.Toggle, target = ControlTarget.CarbHeat,
            axis = Vector3.back, travel = 0.03f, centred = false,
            captureRadius = 0.026f, smoothingTau = 0.05f,
            visual = plunger, visualAxis = Vector3.back, visualTravel = 0.024f,
        };
        Configure(c);
        return c;
    }

    PhysicalControl BuildFuelSelector()
    {
        // Three-position rotary: LEFT — BOTH — RIGHT, exactly the states
        // AircraftSystems.Selector supports. Red, as the reference shows.
        var c = Make("fuel_selector", model.TransformPoint(FuelSelPos));
        Box(c.transform, new Vector3(0f, 0f, 0.008f), new Vector3(0.048f, 0.048f, 0.010f), PanelEdge);
        var body = new GameObject("Dial").transform;      // spun by the control
        body.SetParent(c.transform, false);
        var disc = Cylinder(body, Vector3.zero, 0.017f, 0.005f, Red);
        disc.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Box(body, new Vector3(0f, 0.011f, -0.007f), new Vector3(0.005f, 0.016f, 0.005f), White);

        Label(c.transform, new Vector3(-0.021f, 0.006f, -0.004f), "L",    DetentText, White);
        Label(c.transform, new Vector3(0f,      0.021f, -0.004f), "BOTH", DetentText, White);
        Label(c.transform, new Vector3( 0.021f, 0.006f, -0.004f), "R",    DetentText, White);

        c.spec = new ControlSpec
        {
            id = "fuel_selector", label = "FUEL SELECTOR", kind = ControlKind.Rotary,
            target = ControlTarget.FuelSelector,
            axis = Vector3.right, travel = 0.09f, centred = false,
            captureRadius = 0.026f, smoothingTau = 0.06f,
            detents = new[] { 0f, 0.5f, 1f },
            detentLabels = new[] { "LEFT", "BOTH", "RIGHT" },
            visual = body, visualIsRotation = true,
            visualAxis = Vector3.forward, visualTravel = 90f,   // -45 L .. 0 BOTH .. +45 R
        };
        Configure(c);
        c.SetSilently(0.5f, 1);
        return c;
    }

    PhysicalControl BuildToggle(string id, string label, Vector3 localPos, ControlTarget target, Color col)
    {
        var c = Make(id, model.TransformPoint(localPos));
        Box(c.transform, new Vector3(0f, 0f, 0.006f), new Vector3(0.034f, 0.026f, 0.010f), PanelEdge);
        var body = Box(c.transform, new Vector3(0f, 0f, -0.002f), new Vector3(0.020f, 0.015f, 0.012f), Black);
        var lever = Box(body, new Vector3(0f, 0.010f, -0.006f), new Vector3(0.007f, 0.020f, 0.007f), col);
        Label(c.transform, new Vector3(0f, 0.020f, -0.004f), label, DetentText, White);

        c.spec = new ControlSpec
        {
            id = id, label = label, kind = ControlKind.Toggle, target = target,
            axis = Vector3.up, travel = 0.03f, centred = false,
            captureRadius = 0.024f,
            smoothingTau = 0.04f,
            visual = lever, visualIsRotation = true,
            visualAxis = Vector3.right, visualTravel = -46f,   // flicks up when ON
        };
        Configure(c);
        return c;
    }

    PhysicalControl Make(string name, Vector3 worldPos)
    {
        var go = new GameObject("Ctl_" + name);
        go.transform.SetParent(model, true);
        go.transform.position = worldPos;
        go.transform.localRotation = Quaternion.identity;
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.05f;                    // overwritten in Configure from the spec
        var pc = go.AddComponent<PhysicalControl>();
        return pc;
    }

    void Configure(PhysicalControl c)
    {
        c.controller = ctl; c.phys = phys; c.systems = sys;
        var col = c.GetComponent<SphereCollider>();
        if (col != null) col.radius = c.spec.captureRadius;
        SetLayer(c.transform, CockpitBuilder.CockpitLayer);
    }

    /// <summary>Centre of a named GLB node in model-local space, or a fallback if the
    /// node is missing — so a model revision that renames a mesh degrades to a sensible
    /// position instead of putting a control at the origin.</summary>
    Vector3 MeasuredCentre(string node, Vector3 fallback)
    {
        var t = FindDeep(model, node);
        if (t == null) return fallback;
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return fallback;
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return model.InverseTransformPoint(b.center);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
        return null;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
    }

    static Transform Box(Transform parent, Vector3 lp, Vector3 size, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = size;
        Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g.transform;
    }

    static Transform Cylinder(Transform parent, Vector3 lp, float radius, float halfHeight, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = new Vector3(radius * 2f, halfHeight, radius * 2f);
        Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g.transform;
    }

    static Transform Sphere(Transform parent, Vector3 lp, float diameter, Color col)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = Vector3.one * diameter;
        Destroy(g.GetComponent<Collider>());
        Paint(g, col);
        return g.transform;
    }

    /// <summary>A cockpit PLACARD: small engraved lettering that identifies the control it
    /// is attached to, and is beaten in visual weight by the control itself.
    ///
    /// capHeight is the real height of a capital letter IN METRES. Unity's TextMesh sizes
    /// glyphs as characterSize * fontSize / 10, so characterSize is derived from the height
    /// the cockpit actually wants rather than tuned by eye.
    ///
    /// DEPTH: TextMesh's built-in font material is the GUI text shader, which is
    /// ZTest Always — the lettering draws straight THROUGH solid geometry, so a placard
    /// printed itself across the front of the very knob that should have been hiding it.
    /// Sprites/Default is depth-tested and tints by vertex colour (which is where TextMesh
    /// puts tm.color), so the label is occluded by the cockpit like any other object while
    /// keeping its colour. The font atlas is rebuilt as new glyphs appear, so the material's
    /// texture is refreshed on that event or the placards would blank out mid-session.</summary>
    void Label(Transform parent, Vector3 lp, string text, float capHeight, Color col)
        => Label(parent, lp, text, capHeight, col, Vector3.zero);

    /// <summary>As above, but laid at an angle — used for legends engraved FLAT on a
    /// faceplate (euler ~78 deg about X) rather than standing up facing the pilot.</summary>
    void Label(Transform parent, Vector3 lp, string text, float capHeight, Color col, Vector3 euler)
    {
        const int Pt = 72;
        var tm = new GameObject("Lbl").AddComponent<TextMesh>();
        tm.transform.SetParent(parent, false);
        tm.transform.localPosition = lp;
        tm.text = text;
        tm.characterSize = capHeight * 10f / Pt;
        tm.fontSize = Pt;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = col;
        tm.transform.localRotation = Quaternion.Euler(euler);

        var mr = tm.GetComponent<MeshRenderer>();
        mr.sharedMaterial = PlacardMaterial(tm.font);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    Material placardMat;
    Font placardFont;
    System.Action<Font> placardRebuild;

    Material PlacardMaterial(Font font)
    {
        if (placardMat != null && placardFont == font) return placardMat;
        placardFont = font;

        // The font atlas is an alpha-coverage texture: its RGB is zero, so any shader that
        // multiplies texture RGB by the vertex colour (Sprites/Default, UI/Default) renders
        // the lettering BLACK. What is needed is a shader that takes coverage from the
        // texture's ALPHA and colour from the vertex — and that is depth-tested, unlike the
        // built-in font material (GUI/Text Shader is ZTest Always, which is what let a
        // placard print itself across the front of the knob that should have hidden it).
        // GUI/3D Text Shader is exactly that pairing where the build ships it.
        var sh = Shader.Find("GUI/3D Text Shader");
        if (sh != null)
        {
            placardMat = new Material(sh) { name = "PlacardText", mainTexture = font.material.mainTexture };
        }
        else
        {
            // Fall back to the stock font material. Placards are seated on clear panel
            // beside their control precisely so that ZTest Always stays invisible, but say
            // so in the log rather than shipping a silent downgrade.
            Debug.LogWarning("[CockpitRig] GUI/3D Text Shader unavailable — placards fall back "
                           + "to the ZTest-Always font material.");
            placardMat = new Material(font.material) { name = "PlacardText(fallback)" };
        }

        placardRebuild = f =>
        {
            if (f == placardFont && placardMat != null)
                placardMat.mainTexture = placardFont.material.mainTexture;
        };
        Font.textureRebuilt += placardRebuild;
        Debug.Log("[CockpitRig] placard shader = " + placardMat.shader.name);
        return placardMat;
    }

    void OnDestroy()
    {
        if (placardRebuild != null) Font.textureRebuilt -= placardRebuild;
    }

    /// <summary>Structural, non-interactive cockpit furniture: the thing a control is
    /// mounted ON. Without these the controls read as objects floating in the cabin.</summary>
    Transform Structure(string name, Vector3 localPos, Vector3 size, Color col, Vector3 euler)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        var col0 = g.GetComponent<Collider>(); if (col0 != null) Destroy(col0);
        g.transform.SetParent(transform, false);
        g.transform.position = model.TransformPoint(localPos);
        g.transform.rotation = model.rotation * Quaternion.Euler(euler);
        g.transform.localScale = size;
        Paint(g, col);
        g.layer = CockpitBuilder.CockpitLayer;
        return g.transform;
    }

    /// <summary>The cockpit furniture the controls are mounted on: a sub-panel across the
    /// lower instrument panel, and a centre pedestal running down and aft from it. Built
    /// once, before the controls, so every control lands ON something.</summary>
    void BuildStructure()
    {
        // Lower sub-panel: the strip of panel the plungers and switches live on. Sits a
        // few millimetres proud of the GLB panel face so it reads as a separate bay.
        Structure("SubPanel", new Vector3(0f, 0.360f, PanelZ + 0.004f),
                  new Vector3(0.400f, 0.090f, 0.012f), PanelDark, Vector3.zero);
        // A lighter bezel line under it, so the bay has an edge instead of dissolving.
        Structure("SubPanelLip", new Vector3(0f, 0.316f, PanelZ + 0.002f),
                  new Vector3(0.400f, 0.008f, 0.016f), PanelEdge, Vector3.zero);

        // Centre pedestal: from under the sub-panel, down and aft toward the pilot.
        // Tapered by stacking two boxes rather than a wedge mesh — cheap and reads right.
        // The centre pedestal was REMOVED (23 Aug 2026). It carried only the trim wheel and
        // filled the footwell directly ahead of the seat, breaking the pilot's visual path
        // from seat to pedals. The trim wheel now mounts on the left panel.

        BuildQuadrantHousing();
        // left-hand engine/systems bay, so that group reads as its own panel
        // Systems bay: sized to the two controls that remain (carb heat, fuel selector)
        // rather than the four it used to hold, so removing the switches does not leave a
        // bare plate behind. It is a BACKING PLATE ONLY — no control is parented to it,
        // which is what guarantees that changing it cannot move or hide a control.
        Structure("SystemsBay", new Vector3(-0.068f, 0.360f, PanelZ - 0.001f),
                  new Vector3(0.062f, 0.086f, 0.008f), PanelEdge, Vector3.zero);
    }

    static void Paint(GameObject g, Color c)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", 0.35f);
        g.GetComponent<Renderer>().material = m;
    }
}
