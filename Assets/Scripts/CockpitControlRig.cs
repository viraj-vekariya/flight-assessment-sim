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
    // SlideX 0.105 -> 0.075: the whole right-hand group moves 30 mm inboard. At 0.105/0.150
    // the flap ran out to x 0.165, which reads as sitting against the door rather than on
    // the instrument panel. The usable strip is bounded by the yoke's rim at x 0.047 and the
    // door skin at x 0.138, so 0.075 and 0.120 sit two 30 mm escutcheons inside it with
    // 13 mm clear of the wheel and 3 mm clear of the door. Spacing stays 45 mm, which is
    // what the capture radii are sized against.
    const float SlideX = 0.075f, SlideSpacing = 0.045f;
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

    // FLAPS — OUTBOARD of the throttle. One CLICK steps UP -> 10 -> FULL -> UP.
    //
    // It was at x = 0.060 and that was too close to the yoke. The pilot's wheel is 94 mm
    // across (measured from the GLB: the twin mesh's pilot half spans x -0.1105..-0.0165,
    // so +-0.047 once CentreGroup slides it to the centreline), and a 30 mm escutcheon at
    // 0.060 spans 0.045..0.075 — its inboard edge INSIDE the wheel's envelope. The wheel
    // swept across the flap handle every time the aeroplane was rolled.
    //
    // 0.150 puts it in the third slot of the original quadrant layout: 45 mm outboard of
    // the throttle (which the capture radii are already sized for) and 88 mm clear of the
    // wheel. It fits, because the panel at this height actually reaches x = 0.1736
    // (Object_81, measured) — an old comment in this file claimed 0.159 and that is what
    // made this slot look unusable.
    static readonly Vector3 FlapPos  = new Vector3(0.120f, SlideY, SlideZ);
    // BRAKE — lower LEFT panel, which is where a 172's brake handle is (POH Fig. 7-2), and
    // outboard of the PFD's left edge at x -0.113 so it cannot cover the display.
    // Left side mirrors the right: two controls at 45 mm centres in the 127 mm between the
    // yoke's rim (x -0.047) and the panel edge (x -0.1736).  trim | brake | PFD .. MFD | throttle | flap
    static readonly Vector3 BrakePullPos = new Vector3(-0.105f, 0.435f, SlideZ);
    static readonly Vector3 TrimPos      = new Vector3(-0.150f, SlideY,  SlideZ);
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
        list.Add(BuildFlapButton());
        list.Add(BuildBrakePull());
        list.Add(BuildTrimWheelSmall());

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
            // 58 mm, reduced from 65 when the right-hand group moved 30 mm inboard: a ray
            // aimed at the throttle knob then passed only 69 mm from the yoke hub, leaving
            // 4 mm before the yoke would start stealing those clicks again. 58 keeps 11 mm
            // and still leaves the yoke by far the largest target in the cockpit.
            captureRadius = 0.058f,
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
        // the only spherical grip in the cockpit. It is also the largest of the three.
        //
        // Slides UP for power (23.779: "forward to increase forward thrust"; up is the
        // accepted equivalent on a panel-mounted control). Handle position IS the value —
        // bottom of travel = idle, halfway = ~0.5, top = full.
        var c = Make("throttle", model.TransformPoint(ThrottlePos));
        c.transform.SetParent(quadrant, true);

        SlideMount(c.transform);

        // THE CARRIAGE MUST REST AT LOCAL ZERO. This is not a style choice.
        //
        // PhysicalControl captures its visual's rest position in Awake(), and Unity runs
        // Awake() at AddComponent — which happens inside Make(), BEFORE spec.visual is
        // assigned. visualBase is therefore ALWAYS Vector3.zero, whatever the carriage's
        // real rest position is, and UpdateVisual drives the handle from
        //     zero  ->  zero + up * travel
        // i.e. from the CENTRE of the channel to 22 mm PAST the top end stop, instead of
        // from stop to stop.
        //
        // Putting the -22 mm offset on a PARENT and leaving the carriage itself at local
        // zero makes the animation correct for either value of visualBase, and keeps the
        // repair in this file rather than in PhysicalControl, which several other things
        // depend on. The underlying Awake() bug is recorded here because it will bite the
        // next control that gets restored with a translating visual.
        var handleBase = new GameObject("HandleBase").transform;
        handleBase.SetParent(c.transform, false);
        handleBase.localPosition = new Vector3(0f, -SlideTravel * 0.5f, 0f);

        var h = SlideHandle(handleBase, startAtTop: false);
        h.localPosition = Vector3.zero;      // the offset lives on handleBase, see above

        // Ball grip on a short neck. The neck keeps the ball off the carriage so a hand
        // (or a VR controller) has something to close around.
        Gloss(Cylinder(h, new Vector3(0f, 0f, -0.017f), 0.0060f, 0.005f, LeverSteel), 0.5f);
        var knob = Sphere(h, new Vector3(0f, 0f, -0.029f), 0.024f, KnobBlack);
        knob.localScale = new Vector3(0.024f, 0.024f, 0.019f);   // slightly flattened, not a gearstick
        Gloss(knob, 0.30f);

        // NO PLACARD. Labels were removed from this cockpit on 3 September and the brief
        // for restoring the throttle was "nothing else". The ball grip is the only
        // spherical control in the cabin and the only thing on the lower panel, so there
        // is nothing it can be confused with. Restoring the legend is one line:
        //   Label(c.transform, new Vector3(0f, SlideTravel * 0.5f + 0.003f, -0.006f),
        //         "THROTTLE", PlacardText, Placard);

        c.spec = new ControlSpec
        {
            id = "throttle", label = "THROTTLE", kind = ControlKind.Lever, target = ControlTarget.Throttle,
            axis = Vector3.up,          // slide up = more power
            travel = SlideTravel,
            centred = false,
            // 45 mm. The capture sphere is centred on the CONTROL — the middle of the
            // channel — but what the pilot aims at is the KNOB, which is 22 mm away at
            // either end of travel and is itself 12 mm in radius. The old 20 mm radius was
            // sized against neighbouring levers that no longer exist, and left the visible
            // ball sitting on the very edge of its own grab volume: the measured ray
            // distance was 18 mm at idle and 20 mm at full power, against a 20 mm radius.
            // 40 mm. It must cover the ball (18 mm off the capture axis + a 16 mm ball = 34 mm
            // needed) while staying INSIDE the 42 mm that separates it from the flap paddle's
            // aim ray — otherwise the two fight over every click in this corner of the panel,
            // and the flap wins because it sits nearer the pilot along the ray. Measured at
            // both ends of travel in ThrottleIsWhatTheMouseGrabs.
            captureRadius = 0.040f,
            smoothingTau = 0.035f,
            visual = h, visualIsRotation = false,
            visualAxis = Vector3.up, visualTravel = SlideTravel,
        };
        Configure(c);
        c.SetSilently(phys != null ? phys.Throttle01 : 0f);
        return c;
    }


    /// <summary>FLAPS — one button. Click it and the flaps step UP -> 10 -> FULL -> UP;
    /// it can also be dragged down the channel like a lever. Either way the handle's
    /// position IS the selected detent, because PhysicalControl snaps a DetentLever to the
    /// nearest detent on release and mirrors the aeroplane when nobody is holding it.
    ///
    /// Shape-coded as a flat PADDLE — 14 CFR 23.781(a) asks for a flap handle shaped like
    /// the surface it drives, and it makes this unmistakable against the throttle's ball
    /// without needing a placard.</summary>
    PhysicalControl BuildFlapButton()
    {
        var c = Make("flaps", model.TransformPoint(FlapPos));
        c.transform.SetParent(quadrant, true);
        SlideMount(c.transform);

        // Rest at the TOP of the channel = flaps UP. The offset lives on the parent for the
        // same reason it does on the throttle: PhysicalControl captures its visual's rest
        // pose in Awake(), which runs before spec.visual is assigned, so the captured rest
        // is always zero and the carriage must actually BE at zero.
        var handleBase = new GameObject("HandleBase").transform;
        handleBase.SetParent(c.transform, false);
        handleBase.localPosition = new Vector3(0f, SlideTravel * 0.5f, 0f);

        var h = SlideHandle(handleBase, startAtTop: true);
        h.localPosition = Vector3.zero;

        // Flat paddle, wide and thin — the shape of the flap itself.
        Gloss(Box(h, new Vector3(0f, 0f, -0.016f), new Vector3(0.026f, 0.005f, 0.020f), FlapWhite), 0.30f);

        c.spec = new ControlSpec
        {
            id = "flaps", label = "FLAPS", kind = ControlKind.DetentLever, target = ControlTarget.Flaps,
            axis = Vector3.down,              // 23.779: flaps extend DOWN/aft
            travel = SlideTravel,
            centred = false,
            // 38 mm: enough for the paddle (22 mm up the channel + 13 mm of half-width) and
            // under the 42 mm gap to the throttle's aim ray, so neither steals the other.
            captureRadius = 0.038f,
            smoothingTau = 0.04f,
            detents = AircraftController.FlapDetents,
            detentLabels = AircraftController.FlapLabels,
            visual = h, visualIsRotation = false,
            visualAxis = Vector3.down, visualTravel = SlideTravel,
        };
        Configure(c);
        c.SetSilently(0f, 0);
        return c;
    }

    /// <summary>BRAKE — a pull handle under the left panel edge, and a SPRING lever.
    ///
    /// HOW IT WORKS, and why this shape:
    ///   * How far you pull IS how hard you brake. brakeInput01 is analog 0..1, so half a
    ///     pull is half braking — you can hold a taxi speed instead of stamping on and off.
    ///   * It SPRINGS BACK the moment you let go (ControlKind.SpringLever sets the value to
    ///     zero on release), so the brakes can never be left dragging after a grab. That is
    ///     the one behaviour a brake must have that a throttle must not.
    ///   * The keyboard B key still works and is unchanged.
    ///
    /// It is deliberately NOT on the pedals. Toe brakes would be correct for a 172, but the
    /// pedals sit below the pilot's default view (measured: viewport y -0.21..-0.02), so a
    /// brake down there is a control you cannot see yourself operate.</summary>
    PhysicalControl BuildBrakePull()
    {
        var c = Make("brake", model.TransformPoint(BrakePullPos));
        c.transform.SetParent(quadrant, true);

        // ── THE FIXED HALF: a round escutcheon and a raised bushing the rod runs in ──
        // NEGATIVE local z stands proud of the panel, toward the pilot.
        var plate = Cylinder(c.transform, new Vector3(0f, 0f, 0.001f), 0.016f, 0.002f, QuadBody);
        plate.localRotation = Quaternion.Euler(90f, 0f, 0f);       // disc facing the pilot
        Gloss(plate, 0.16f);
        var bush = Cylinder(c.transform, new Vector3(0f, 0f, -0.006f), 0.009f, 0.005f, QuadEdge);
        bush.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Metal(Gloss(bush, 0.40f), 0.5f);

        var handleBase = new GameObject("HandleBase").transform;
        handleBase.SetParent(c.transform, false);
        handleBase.localPosition = Vector3.zero;

        var pull = new GameObject("Pull").transform;
        pull.SetParent(handleBase, false);
        pull.localPosition = Vector3.zero;

        // ── THE ROD IS LONG ENOUGH TO STAY IN ITS BUSHING ───────────────────────
        //
        // The first version hung a 20 mm stub off the knob. The stub travels WITH the
        // knob, so at full pull both had left the panel behind and the handle floated in
        // front of the cockpit on nothing — the knob looked like it had come off in your
        // hand. A real pull-rod does not get shorter; it slides, and the part you cannot
        // see stays in its bushing.
        //
        // So the rod is 51 mm long and positioned so its INBOARD end is still behind the
        // panel face at full extension (local z >= 0), while its outboard end always ends
        // at the knob. Nothing scales and nothing is animated separately: one long rod
        // parented to the moving handle does it, and the far end is buried inside the panel
        // where it cannot be seen. The control battery asserts the no-gap condition at full
        // brake rather than trusting these numbers.
        var rod = Cylinder(pull, new Vector3(0f, 0f, -0.0005f), 0.0035f, 0.0255f, LeverSteel);
        rod.name = "Rod";
        rod.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Metal(Gloss(rod, 0.55f), 0.7f);

        // Chrome collar where the rod meets the grip, so the knob reads as fitted to the
        // rod rather than skewered by it.
        var collar = Cylinder(pull, new Vector3(0f, 0f, -0.019f), 0.008f, 0.003f, QuadEdge);
        collar.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Metal(Gloss(collar, 0.45f), 0.5f);

        // Red mushroom knob — flattened along the rod so it reads as a knob to pull rather
        // than a ball to push.
        var knob = Sphere(pull, new Vector3(0f, 0f, -0.026f), 0.022f, Red);
        knob.localScale = new Vector3(0.022f, 0.022f, 0.016f);
        Gloss(knob, 0.28f);

        c.spec = new ControlSpec
        {
            id = "brake", label = "BRAKE", kind = ControlKind.SpringLever, target = ControlTarget.WheelBrake,
            // THE HAND MOVES DOWN; THE KNOB MOVES OUT. These are deliberately different
            // axes, and the control is unusable if they are the same.
            //
            // A mouse grab point is the closest point on the view RAY to the control, so
            // dragging the mouse slides that point ACROSS the view — it barely moves along
            // the ray at all. Give a pull-knob an axis of Vector3.back and you have asked
            // the pilot to drag the mouse into the screen: the hand displacement resolved
            // onto that axis stays near zero however far they drag, and the knob never
            // moves. That is exactly how this shipped, and it looked like a dead control.
            //
            // spec.axis is what the HAND is measured along; spec.visualAxis is where the
            // GEOMETRY goes. Dragging down now pulls the knob out toward the pilot.
            axis = Vector3.down,              // drag DOWN to brake
            travel = 0.024f,
            centred = false,
            captureRadius = 0.038f,           // 45 mm to the trim wheel; must not reach it
            smoothingTau = 0.03f,             // brakes must feel immediate
            visual = pull, visualIsRotation = false,
            visualAxis = Vector3.back, visualTravel = 0.024f,
        };
        Configure(c);
        c.SetSilently(0f);
        return c;
    }

    /// <summary>ELEVATOR TRIM — a wheel on the left panel that holds the stick force so the
    /// pilot does not have to.
    ///
    /// WHY IT IS THE ONE CONTROL THIS COCKPIT COULD NOT DO WITHOUT. The yoke HOLDS ITS
    /// POSITION when released (PhysicalControl.EndGrab, deliberately — a real yoke does not
    /// spring to neutral). Trim is therefore the only way to set an attitude and take your
    /// hands off: you wind in trim until the aeroplane flies the pitch you want by itself.
    /// Without it the pilot must hold the yoke for the whole flight. It is not a spoiler and
    /// not a brake — it changes nothing about drag or stopping; it only moves where "hands
    /// off" sits.
    ///
    /// TWO TRAPS, both avoided here:
    ///   * The HAND axis is vertical, not fore/aft. A mouse grab point slides across the
    ///     view, barely along it, so a fore/aft axis is undraggable — the same fault that
    ///     made the brake knob look dead.
    ///   * UpdateVisual writes an ABSOLUTE localRotation, so it wipes any orientation stored
    ///     on the animated transform. The wheel's 90-degree tilt therefore lives on a CHILD,
    ///     and the animated pivot starts at identity.</summary>
    PhysicalControl BuildTrimWheelSmall()
    {
        var c = Make("trim", model.TransformPoint(TrimPos));
        c.transform.SetParent(quadrant, true);

        Gloss(Box(c.transform, new Vector3(0f, 0f, 0.001f),
                  new Vector3(0.040f, 0.040f, 0.005f), QuadBody), 0.16f);

        var pivot = new GameObject("TrimPivot").transform;   // animated: MUST stay identity
        pivot.SetParent(c.transform, false);
        pivot.localPosition = new Vector3(0f, 0f, -0.013f);
        pivot.localRotation = Quaternion.identity;

        // Hub, tilted on a CHILD so the animation cannot overwrite the orientation.
        var hub = Cylinder(pivot, Vector3.zero, 0.0165f, 0.006f, new Color(0.10f, 0.10f, 0.11f));
        hub.localRotation = Quaternion.Euler(0f, 0f, 90f);   // axis across the cockpit
        Gloss(hub, 0.22f);
        // Knurled rubber rim: many fine ribs in the Y-Z plane, so the wheel reads as a
        // moulded trim wheel (not a cog) and rotation about X still visibly winds it.
        for (int i = 0; i < 28; i++)
        {
            float a = i * Mathf.PI * 2f / 28f;
            var rib = Box(pivot, new Vector3(0f, Mathf.Sin(a) * 0.0165f, Mathf.Cos(a) * 0.0165f),
                          new Vector3(0.0118f, 0.0019f, 0.0019f), new Color(0.07f, 0.07f, 0.075f));
            rib.localRotation = Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f);
            Gloss(rib, 0.15f);
        }
        // White index stripe across the rim. At neutral it faces the pilot's EYE — which is
        // ~35° above the wheel's axis — not straight aft, so the whole ±90° travel stays on
        // the visible face (straight-aft placement hid it under the wheel at full nose-up).
        var stripe = Box(pivot, Quaternion.Euler(35f, 0f, 0f) * new Vector3(0f, 0f, -0.0172f),
                         new Vector3(0.0120f, 0.0024f, 0.0012f), White);
        stripe.localRotation = Quaternion.Euler(35f, 0f, 0f);
        Gloss(stripe, 0.3f);
        // which way is which, engraved above and below the wheel
        Label(c.transform, new Vector3(0f,  0.0245f, -0.004f), "NOSE DN", DetentText, White);
        Label(c.transform, new Vector3(0f, -0.0245f, -0.004f), "NOSE UP", DetentText, White);

        c.spec = new ControlSpec
        {
            id = "trim", label = "TRIM", kind = ControlKind.TrimWheel, target = ControlTarget.Trim,
            axis = Vector3.down,        // wind DOWN for nose UP, like pulling the yoke back
            // Deliberately long: trim is a fine adjustment, and a short travel would turn it
            // into a precision task — an unnecessary motor demand in a workload experiment.
            travel = 0.12f,
            centred = true,
            captureRadius = 0.038f,     // 45 mm to the brake; must not reach it
            smoothingTau = 0.06f,
            visual = pivot, visualIsRotation = true,
            // ±90° = 180° lock to lock (was ±160° = 320°). At ±160° the white index
            // stripe went round the BACK of the wheel and out of sight, so the pilot could
            // not tell nose-up trim from nose-down. Within ±90° it always stays on the
            // visible face: stripe BELOW centre = nose UP, ABOVE centre = nose DOWN.
            // visualAxis is LEFT so the face rolls DOWN for nose-up: the same way the hand
            // drags (axis = down) and the way a real 172 wheel rolls. With Vector3.right the
            // stripe rose while the hand pulled down — the wheel turned against the hand.
            visualAxis = Vector3.left, visualTravel = 90f,     // degrees at full trim, each way
        };
        Configure(c);
        c.SetSilently(0f);
        return c;
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
