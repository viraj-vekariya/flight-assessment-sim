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

    // ═══════════════════════════════════════════════════════════════════════════
    // LAYOUT
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // MEASURED, NOT ESTIMATED. Every anchor below was read out of the GLB by the design
    // probe (CockpitDesignShots.WriteGeometryReport), in the cockpit's own axes:
    //
    //     panel face                z = 0.755      (Object_50 / Object_81 / Object_83)
    //     panel width               x = +-0.176    (Object_50)
    //     display band              y = 0.474 .. 0.547
    //     centre pedestal           x = +-0.047,  y = 0.237 .. 0.445,  front z = 0.667  (Object_58)
    //     rudder pedals             x = +-0.127,  y = 0.270 .. 0.306,  z = 0.755 .. 0.801  (Object_52)
    //     pilot eye                 (0, 0.580, 0.520)
    //
    // The model is about 0.352x life size (CockpitHardware.ModelScale, measured four
    // independent ways), so all HARDWARE dimensions are written in real millimetres and
    // scaled by MM. Positions stay in model units because that is what the anchors are in.
    //
    // THE ARRANGEMENT IS A 172'S, not a set of sliders in a row:
    //
    //     lower panel, left to right:   PARK BRAKE | switches | CARB HEAT  THROTTLE  MIXTURE | FLAPS
    //     centre pedestal, top to bottom:                       TRIM WHEEL / FUEL VALVE
    //     rudder pedals:                                        TOE BRAKES
    //     panel outboard columns:                               engine + electrical gauges
    //
    // Why the engine controls sit close together in the centre rather than spread across
    // the panel: on the aeroplane they are one cluster under the radio stack, reached with
    // one hand without looking, and the FLAP lever is deliberately outboard of them so it
    // cannot be grabbed by mistake in place of the throttle. Separating controls by
    // FUNCTION and by SHAPE is what makes a cockpit learnable; spacing them evenly does not.

    // MEASURED SURFACES (design probe, drawn-triangle bounds + depth map):
    //
    //     upper panel  Object_50   z = 0.755   y 0.470 .. 0.560, full width
    //     LOWER PANEL  Object_81   z = 0.760   y 0.270 .. 0.470, x +-0.176
    //     PEDESTAL     Object_58   z = 0.667   y 0.250 .. 0.450, x +-0.047   (stands 93 mm proud)
    //     yoke wheel   Object_90   z = 0.710   y 0.417 .. 0.474, x +-0.047   (94 mm across, centred)
    //     pedals       Object_52   z = 0.755   y 0.270 .. 0.310, x +-0.110
    //     PFD glass    centre (-0.0709, 0.4921, 0.7535), 85 x 56
    //     MFD glass    centre ( 0.0461, 0.4921, 0.7535), 85 x 56
    //
    // The pedestal is the fact that drives the whole arrangement: it occupies the centre
    // strip x +-0.047 and stands 93 mm CLOSER to the pilot than the panel, so anything
    // mounted on the lower panel behind it is simply invisible. The first version of this
    // layout put the engine controls at x = -0.012 .. +0.052 — directly behind the pedestal —
    // which is why they rendered as knobs floating in the footwell with a gap above them.
    //
    // So the panel is used as the aeroplane uses it:
    //
    //     LEFT of the pedestal    engine and electrical gauges, park brake, switch bank
    //     ON the pedestal         trim wheel, and the fuel valve below it
    //     RIGHT of the pedestal   carb heat / throttle / mixture in a row, flap lever outboard
    //
    // That also happens to put the throttle 100 mm right of the pilot's centreline, which is
    // 284 mm at full scale — where a 172's throttle actually is.

    const float PanelZ = 0.7585f;  // lower panel face (0.760), hardware mounts just proud of it
    const float PedZ   = 0.6655f;  // pedestal front face (0.667), same treatment

    /// <summary>THE SEATED VIEW CUTS OFF AT y = 0.358 ON THE PANEL.
    ///
    /// Worked from the camera rather than guessed: the eye is at (0, 0.580, 0.520), the
    /// cockpit camera is 78 deg vertical with a 4 deg down-tilt (CockpitBuilder,
    /// CockpitCamera.basePitch), and the panel face is 0.2385 in front of the eye. The
    /// bottom of the frame is therefore 43 deg below the eye, which on the panel is
    ///     y = 0.580 - 0.2385 * tan(43 deg) = 0.358.
    ///
    /// Anything below that is invisible to a seated desktop participant unless they hold
    /// right-mouse and look down. In a headset it is simply a glance. So:
    ///
    ///   ABOVE 0.358  everything used continuously — throttle, flaps, brake, the gauges.
    ///   BELOW 0.358  the memory items — switch bank, fuel valve — which are touched a
    ///                handful of times per session, have keyboard equivalents on the
    ///                desktop, and are exactly where a 172 keeps them.
    ///
    /// The pedestal is closer to the eye (z = 0.6655), so the same angle cuts it off much
    /// higher, at y = 0.444. Its top is 0.450, so the pedestal is essentially never in the
    /// seated forward view — which is also true of the real aeroplane's.</summary>
    const float SeatedViewFloorY = 0.358f;

    /// <summary>The engine-control row. One height, because on the aeroplane they are one
    /// row and a hand sweeping along it should meet them all.</summary>
    const float EngineRowY = 0.430f;

    // 34 mm centres = 97 mm at full scale, which is a 172's spacing and, more to the point,
    // more than the two capture radii sum to. At 30 mm the two overlapped by a millimetre
    // and a hand between them could have taken either.
    static readonly Vector3 CarbHeatPos = new Vector3(0.072f, EngineRowY, PanelZ);
    static readonly Vector3 ThrottlePos = new Vector3(0.106f, EngineRowY, PanelZ);
    static readonly Vector3 MixturePos  = new Vector3(0.140f, EngineRowY, PanelZ);
    /// <summary>Flaps: outboard of the engine cluster and well below it, so it is neither in
    /// the row a hand sweeps for power nor at the same height as it. Mistaking the flap
    /// lever for the throttle on short final is a real accident category; separating them by
    /// position AND by shape is the standard defence.</summary>
    static readonly Vector3 FlapLeverPos = new Vector3(0.154f, 0.394f, PanelZ);
    /// <summary>Park/wheel brake: the small black T-pull under the LEFT panel edge, which is
    /// where a 172's parking brake is.</summary>
    static readonly Vector3 BrakePos = new Vector3(-0.072f, 0.394f, PanelZ);

    /// <summary>Trim and fuel go on the PEDESTAL, which is real GLB geometry rather than a
    /// plate invented to hold them. Both sit about 50 deg below the eye — a glance down,
    /// exactly as in the aeroplane, well inside the 64 deg the cockpit camera pitches to
    /// (CockpitCamera.maxPitch) and trivially inside a headset's.</summary>
    static readonly Vector3 TrimWheelPos = new Vector3(0f, 0.393f, PedZ);
    static readonly Vector3 FuelSelPos   = new Vector3(0f, 0.310f, PedZ);

    /// <summary>Two interactive toggles in the left switch bank. They exist because
    /// ChecklistLibrary gates HIGH missions on them (Electrical/H2 on LoadShed,
    /// StaticBlock/H3 on AlternateStaticOpen) and a headset has no keyboard, so without a
    /// cockpit object those drills are unperformable in the modality the study runs in.</summary>
    static readonly Vector3 LoadShedPos  = new Vector3(-0.150f, 0.332f, PanelZ);
    static readonly Vector3 AltStaticPos = new Vector3(-0.128f, 0.332f, PanelZ);

    /// <summary>The engine and electrical gauges, on the left panel where the yoke and the
    /// pedestal cannot cover them. Two columns of three.</summary>
    const float GaugeLeftX  = -0.152f;
    const float GaugeRightX = -0.112f;
    static readonly float[] GaugeRowY = { 0.452f, 0.414f, 0.376f };

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

        CabinTrim.Build(model);    // the space first, so the furniture sits IN somewhere
        BuildStructure();          // furniture next, so every control lands ON something

        var list = new System.Collections.Generic.List<PhysicalControl>();

        // PRIMARY FLIGHT CONTROLS — touched continuously, found without looking.
        list.Add(BuildYoke());
        list.Add(BuildBrakePedals());   // also builds the toe pads on the rudder pedals

        // ENGINE CONTROL CLUSTER — one row on the lower centre panel, as on the aeroplane.
        list.Add(BuildCarbHeat());
        list.Add(BuildThrottle());
        BuildMixtureVisual();           // geometry only, deliberately not interactive
        list.Add(BuildFlapLever());

        // PEDESTAL — trim above, fuel valve below, on the GLB's own centre console.
        list.Add(BuildTrimWheel());
        list.Add(BuildFuelSelector());

        // SYSTEMS SWITCHES. LOAD SHED and ALTERNATE STATIC are RESTORED as cockpit objects.
        // They were dropped on 23 Aug as panel clutter, but the criterion is whether the
        // experiment needs them and it does: ChecklistLibrary gates real DO items on their
        // state — Electrical/H2 on LoadShed and StaticBlock/H3 on AlternateStaticOpen.
        // Keyboard K and L cover the desktop modality, but a headset has no keyboard, so
        // without these two objects those drills are unperformable in the modality the
        // study is actually run in. They are small toggles inside the left switch bank.
        list.Add(BuildToggle("load_shed", "LOAD SHED", LoadShedPos, ControlTarget.LoadShed, Red));
        list.Add(BuildToggle("alt_static", "ALT STATIC", AltStaticPos, ControlTarget.AlternateStatic, White));

        // NO SPOILER LEVER. A Cessna 172 has no spoilers, and this one served no
        // experimental purpose: no mission and no checklist referenced it, and
        // AircraftController held it retracted for the whole of every recorded trial — an
        // unrealistic control, in the most-looked-at part of the panel, that a participant
        // could see and reach and that did nothing. The simulation capability is untouched:
        // the spoiler still exists in the flight model, is still on the X key for
        // development, is still locked out during a recorded trial, and its telemetry column
        // still proves it stayed at zero rather than being assumed to have.

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
        CheckSeatedVisibility();
    }


    /// <summary>Report which controls a SEATED participant can actually see without moving
    /// their head, and complain if a continuously-used one cannot.
    ///
    /// This check exists because its absence cost a whole build. The flap lever, the brake
    /// and the switch bank were all placed on perfectly good panel, mounted on real
    /// geometry, correctly wired and verified by the control battery — and all three were
    /// below the bottom edge of the frame. Every test passed and a participant would have
    /// seen none of them. "Is it wired correctly" and "can the person see it" are different
    /// questions and only one of them was being asked.
    ///
    /// The angles come from the camera as built (CockpitBuilder: 78 deg vertical, 4 deg
    /// down-tilt) rather than from a constant repeated here, so if the camera changes this
    /// check changes with it.</summary>
    void CheckSeatedVisibility()
    {
        var cam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None),
                                    c2 => c2.name == "CockpitCamera");
        if (cam == null) { Debug.LogWarning("[CockpitRig] no CockpitCamera — cannot check visibility."); return; }

        float halfV = cam.fieldOfView * 0.5f;
        float halfH = Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * Mathf.Max(1f, cam.aspect)) * Mathf.Rad2Deg;
        var cc = cam.GetComponent<CockpitCamera>();
        float tilt = cc != null ? cc.basePitch : 0f;

        // The controls a pilot uses continuously. A memory item below the frame is a design
        // choice; a primary flight control below the frame is a defect.
        var primary = new System.Collections.Generic.HashSet<string> { "yoke", "throttle", "flaps", "brake" };

        foreach (var c in Controls)
        {
            if (c == null) continue;
            Vector3 local = cam.transform.InverseTransformPoint(c.transform.position);
            if (local.z <= 0.001f) continue;
            float elev = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            float azim = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            bool inFrame = Mathf.Abs(elev) <= halfV && Mathf.Abs(azim) <= halfH;
            string line = string.Format("[CockpitRig] view: {0,-14} elev {1,6:0.0}  azim {2,6:0.0}  {3}",
                                        c.spec.id, elev, azim, inFrame ? "in frame" : "OUT OF FRAME");
            if (!inFrame && primary.Contains(c.spec.id))
                Debug.LogError(line + "  <-- PRIMARY CONTROL NOT VISIBLE FROM THE SEAT "
                             + "(frame is +-" + halfV.ToString("0.0") + " deg vertical about a "
                             + tilt.ToString("0.0") + " deg down-tilt)");
            else
                Debug.Log(line);
        }
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
    // THE CONTROLS
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // Each control is built out of CockpitHardware, which knows the SHAPES a 172 uses.
    // The rule this file now follows, and did not before: a control's shape is part of its
    // identity, so no two controls with different jobs may share one. A pilot finds the
    // throttle in the dark because it is the only knurled black plunger; if the flaps were
    // also a plunger, they could not.

    /// <summary>A physical placard: a dark plate with engraved-looking legend, sitting ON
    /// the panel beside its control. Replaces the floating white text that read as debug
    /// annotation, and at a size a placard actually is rather than three times it.</summary>
    void PlacardPlate(Transform parent, Vector3 lp, string text, float widthMm, float capMm = 9f)
    {
        CockpitHardware.Box(parent, lp + new Vector3(0f, 0f, 0.0004f),
                            new Vector3(widthMm * CockpitHardware.MM,
                                        (capMm + 4f) * CockpitHardware.MM,
                                        1.2f * CockpitHardware.MM),
                            CockpitHardware.Bezel, 0.10f);
        Label(parent, lp + new Vector3(0f, 0f, -0.0009f), text,
              capMm * CockpitHardware.MM, CockpitHardware.Placard);
    }

    PhysicalControl BuildYoke()
    {
        // The yoke MESH is the GLB's own (RealCockpit cuts the twin-yoke mesh down to one
        // and rigs it to pitch/roll), so this control only supplies the grab volume and the
        // input mapping. Its visual is null on purpose: adding a second animation path here
        // would let the collider and the wheel disagree about where the yoke is.
        var yokeVisual = FindDeep(model, "YokeVisual");
        Vector3 pos = yokeVisual != null ? yokeVisual.position
                                         : model.TransformPoint(new Vector3(0f, 0.44f, 0.72f));

        var c = Make("yoke", pos);
        c.spec = new ControlSpec
        {
            id = "yoke", label = "YOKE", kind = ControlKind.Yoke, target = ControlTarget.PitchRoll,
            axis = Vector3.forward, secondaryAxis = Vector3.right,
            // Model units: 0.16 and 0.18 are 455 and 511 real mm, which is about a 172's
            // fore/aft column travel and wheel throw. Large travel is deliberate — it makes
            // hand tremor a few percent of full deflection instead of a few tens.
            travel = 0.16f, secondaryTravel = 0.18f,
            centred = true,
            // 55 mm = 156 mm at full scale, which still comfortably contains both grips of
            // a 300 mm wheel. It was 130 mm, chosen when the engine controls were somewhere
            // else entirely; at that size the yoke's capture volume swallowed the carb-heat
            // knob 64 mm away, so a hand reaching for carb heat would have taken the yoke.
            // Found by the pairwise reach check, not by anyone noticing.
            captureRadius = 0.055f,
            smoothingTau = 0.045f,
            responseExponent = 1.25f,
            deadZone = 0.02f,
        };
        Configure(c);
        return c;
    }

    /// <summary>THROTTLE — a push-pull plunger with a knurled black knob, which is what a
    /// 172 has. IN is full power and OUT is idle, so the knob's protrusion IS the power
    /// setting and can be read from the corner of the eye.
    ///
    /// The previous build made this a black ball on a vertical slider. Both halves of that
    /// were wrong: the aeroplane has no vertical throttle, and a sphere is the one shape
    /// 14 CFR 23.781 reserves for a control this is not.</summary>
    PhysicalControl BuildThrottle()
    {
        var c = Make("throttle", model.TransformPoint(ThrottlePos));
        // 32 mm knob, 55 mm of shaft: a 172's throttle, to the millimetre.
        var plunger = CockpitHardware.Plunger(c.transform, 32f, 46f, CockpitHardware.KnobBlack);
        PlacardPlate(c.transform, new Vector3(0f, -0.0165f, -0.0006f), "THROTTLE", 40f);

        const float Travel = 55f * CockpitHardware.MM;
        c.spec = new ControlSpec
        {
            id = "throttle", label = "THROTTLE", kind = ControlKind.Lever, target = ControlTarget.Throttle,
            axis = Vector3.forward,     // push IN for power
            travel = Travel,
            centred = false,
            captureRadius = 0.016f,     // half the 34 mm gap to carb heat and mixture
            smoothingTau = 0.05f,
            visual = plunger, visualIsRotation = false,
            visualAxis = Vector3.forward, visualTravel = Travel,
        };
        Configure(c);
        c.SetSilently(0f);
        return c;
    }

    /// <summary>MIXTURE — geometry only, deliberately NOT interactive.
    ///
    /// A 172 has three engine controls and a cockpit with two of them looks wrong at a
    /// glance. But the flight model has no mixture, so an interactive mixture would either
    /// do nothing (a control that lies) or would have to be invented — and inventing one
    /// adds a variable to a workload experiment that nothing in the design controls for.
    /// So it is built, in the right place, in the right shape and the right colour, and it
    /// does not move and cannot be grabbed. That is the honest version.</summary>
    void BuildMixtureVisual()
    {
        var g = new GameObject("MixtureVisual").transform;
        g.SetParent(model, false);
        g.localPosition = MixturePos;
        g.localRotation = Quaternion.identity;
        var p = CockpitHardware.Plunger(g, 28f, 46f, CockpitHardware.KnobRed);
        // Full rich: pushed all the way in, which is where it sits for every phase of
        // flight this experiment simulates.
        p.localPosition = new Vector3(0f, 0f, 46f * CockpitHardware.MM * 0.55f);
        PlacardPlate(g, new Vector3(0f, -0.0165f, -0.0006f), "MIXTURE", 36f);
        SetLayer(g, CockpitBuilder.CockpitLayer);
    }

    /// <summary>CARB HEAT — a smaller plunger, LEFT of the throttle, pulled OUT for heat
    /// ON. Same family as the throttle because on the aeroplane it is the same family; told
    /// apart by size, position and placard rather than by being a different kind of
    /// object.</summary>
    PhysicalControl BuildCarbHeat()
    {
        var c = Make("carb_heat", model.TransformPoint(CarbHeatPos));
        var plunger = CockpitHardware.Plunger(c.transform, 24f, 34f, CockpitHardware.KnobBlack, knurled: false);
        PlacardPlate(c.transform, new Vector3(0f, -0.0165f, -0.0006f), "CARB HEAT", 42f);

        const float Travel = 30f * CockpitHardware.MM;
        c.spec = new ControlSpec
        {
            id = "carb_heat", label = "CARB HEAT", kind = ControlKind.Toggle, target = ControlTarget.CarbHeat,
            axis = Vector3.back,        // pull OUT for heat ON
            travel = Travel,
            centred = false,
            captureRadius = 0.015f,
            smoothingTau = 0.05f,
            visual = plunger, visualIsRotation = false,
            visualAxis = Vector3.back, visualTravel = Travel,
        };
        Configure(c);
        return c;
    }

    /// <summary>FLAPS — the 172's small gated lever on the lower right panel. Three gates,
    /// because the aeroplane has three flap settings; a continuous slider would let the
    /// cockpit show a setting the aeroplane cannot hold.</summary>
    PhysicalControl BuildFlapLever()
    {
        var c = Make("flaps", model.TransformPoint(FlapLeverPos));
        Transform[] gates;
        const float TravelMm = 52f;
        const float Travel = TravelMm * CockpitHardware.MM;
        var lever = CockpitHardware.FlapSelector(c.transform, TravelMm, 3, out gates);
        // UP is the top gate; the lever starts there and slides DOWN to extend, per
        // 14 CFR 23.779.
        lever.localPosition = new Vector3(0f, Travel * 0.5f, 0f);

        PlacardPlate(c.transform, new Vector3(0f, Travel * 0.5f + 0.010f, -0.0006f), "FLAPS", 30f);
        // Gate legends, beside the teeth they belong to.
        string[] marks = { "UP", "10", "FULL" };
        for (int i = 0; i < 3; i++)
            Label(c.transform,
                  new Vector3(0.0235f, Travel * (0.5f - i * 0.5f), -0.0014f),
                  marks[i], 7f * CockpitHardware.MM, CockpitHardware.Placard);

        c.spec = new ControlSpec
        {
            id = "flaps", label = "FLAPS", kind = ControlKind.DetentLever, target = ControlTarget.Flaps,
            axis = Vector3.down,        // slide down through the gates for more flap
            travel = Travel,
            centred = false,
            captureRadius = 0.018f,
            smoothingTau = 0.05f,
            detents = AircraftController.FlapDetents,
            detentLabels = AircraftController.FlapLabels,
            visual = lever, visualIsRotation = false,
            visualAxis = Vector3.down, visualTravel = Travel,
        };
        Configure(c);
        c.SetSilently(0f, 0);
        return c;
    }

    /// <summary>ELEVATOR TRIM — a large wheel edge-on in the centre pedestal, with a
    /// separate position pointer beside it. The wheel spins; the pointer says where the
    /// trim actually is, which the wheel cannot because it turns many times.</summary>
    PhysicalControl BuildTrimWheel()
    {
        var c = Make("trim", model.TransformPoint(TrimWheelPos));
        Transform pointer;
        var wheel = CockpitHardware.TrimWheel(c.transform, 128f, out pointer);
        // The pointer beside the wheel shows ABSOLUTE trim. The wheel itself cannot: it
        // turns through 220 degrees per unit and would be ambiguous, which is exactly why
        // the real aeroplane has a separate indicator with a TAKEOFF band on it.
        var ti = c.gameObject.AddComponent<TrimIndicator>();
        ti.pointer = pointer;
        ti.phys = phys;
        ti.span = 56f * CockpitHardware.MM;
        PlacardPlate(c.transform, new Vector3(0f, 0.0245f, -0.0006f), "TRIM", 26f, 6f);
        Label(c.transform, new Vector3(-0.0046f, 0.0205f, -0.0016f), "NOSE UP",
              6f * CockpitHardware.MM, CockpitHardware.Placard);
        Label(c.transform, new Vector3(-0.0046f, -0.0205f, -0.0016f), "DN",
              6f * CockpitHardware.MM, CockpitHardware.Placard);

        c.spec = new ControlSpec
        {
            id = "trim", label = "TRIM", kind = ControlKind.TrimWheel, target = ControlTarget.Trim,
            // THE RIM FACES THE PILOT, so the hand moves UP and DOWN across it — not fore
            // and aft as it would on a wheel exposed at the side of a pedestal. Rolling the
            // exposed rim UP carries the top of the wheel AWAY from the pilot, which is the
            // 172's "wind forward for nose down"; rolling it DOWN brings the top back, for
            // nose up. So a downward hand gives positive trim.
            axis = Vector3.down,
            travel = 0.12f,             // long: trim is a fine adjustment, not a precision task
            centred = true,
            captureRadius = 0.028f,
            smoothingTau = 0.06f,
            visual = wheel, visualIsRotation = true,
            // ABOUT -X, not +X. A rotation about +X lifts the near rim, so with the hand
            // moving down the wheel would have visibly rolled the opposite way to the
            // fingers on it — the one thing a direct-manipulation control must never do.
            visualAxis = Vector3.left, visualTravel = 220f,   // degrees across the full range
        };
        Configure(c);
        return c;
    }

    /// <summary>FUEL SELECTOR — a red rotary valve on the pedestal below the trim wheel,
    /// which is where a 172's is. The handle is a BAR, so its direction is the reading.</summary>
    PhysicalControl BuildFuelSelector()
    {
        var c = Make("fuel_selector", model.TransformPoint(FuelSelPos));
        var handle = CockpitHardware.FuelValve(c.transform, 62f, 58f);

        Label(c.transform, new Vector3(-0.0092f, -0.0055f, -0.0012f), "L",
              8f * CockpitHardware.MM, CockpitHardware.Placard);
        Label(c.transform, new Vector3(0f, 0.0118f, -0.0012f), "BOTH",
              7f * CockpitHardware.MM, CockpitHardware.Placard);
        Label(c.transform, new Vector3(0.0092f, -0.0055f, -0.0012f), "R",
              8f * CockpitHardware.MM, CockpitHardware.Placard);
        PlacardPlate(c.transform, new Vector3(0f, -0.0148f, -0.0006f), "FUEL", 26f, 6f);

        c.spec = new ControlSpec
        {
            id = "fuel_selector", label = "FUEL SELECTOR", kind = ControlKind.Rotary,
            target = ControlTarget.FuelSelector,
            axis = Vector3.right, travel = 0.07f, centred = false,
            captureRadius = 0.024f, smoothingTau = 0.06f,
            detents = new[] { 0f, 0.5f, 1f },
            detentLabels = new[] { "LEFT", "BOTH", "RIGHT" },
            visual = handle, visualIsRotation = true,
            // -55 L .. 0 BOTH .. +55 R, about the valve's own axis. Negative because a
            // handle pointing LEFT selects the left tank.
            visualAxis = Vector3.back, visualTravel = 110f,
        };
        Configure(c);
        c.SetSilently(0.5f, 1);
        return c;
    }

    /// <summary>THE BRAKE.
    ///
    /// A 172 brakes with TOE PADS on the rudder pedals, and this build now has them: they
    /// are on the pedals, they carry the tread, and they tilt with applied pressure.
    ///
    /// It ALSO keeps a hand control, and that is a deliberate accessibility decision rather
    /// than an oversight. No headset in this lab has rudder pedals, and a participant in VR
    /// has no keyboard either — so with toe brakes alone there would be no way to stop the
    /// aeroplane in the modality the experiment is actually run in. The hand control is
    /// therefore shaped and placed as the thing a 172 really does have within reach of the
    /// left hand: the small black parking-brake T-pull under the left panel edge. It is
    /// small, it is where that handle lives, and it is no longer a black bar the size of a
    /// forearm sitting in the middle of the panel.</summary>
    PhysicalControl BuildBrakePedals()
    {
        var c = Make("brake", model.TransformPoint(BrakePos));
        var pull = CockpitHardware.BrakePull(c.transform);
        PlacardPlate(c.transform, new Vector3(0f, -0.0135f, -0.0006f), "BRAKE", 30f, 6f);

        const float Travel = 26f * CockpitHardware.MM;
        c.spec = new ControlSpec
        {
            id = "brake", label = "BRAKE", kind = ControlKind.SpringLever, target = ControlTarget.WheelBrake,
            axis = Vector3.back,        // pull it toward you to brake
            travel = Travel,
            centred = false,
            captureRadius = 0.020f,
            smoothingTau = 0.03f,       // brakes must feel immediate
            visual = pull, visualIsRotation = false,
            visualAxis = Vector3.back, visualTravel = Travel,
        };
        Configure(c);

        AttachPedalFollower();
        return c;
    }

    /// <summary>Tilt the aeroplane's own rudder/brake pedals with applied brake pressure,
    /// and give them the toe pads that make them read as brakes.
    ///
    /// This is a one-way READ of `phys.brakeInput01`. The pedals are not parented to the
    /// brake control and the brake control does not own them, so hiding, moving or
    /// rebuilding either one cannot affect the other.</summary>
    void AttachPedalFollower()
    {
        var pedals = FindDeep(model, "Object_52");
        if (pedals == null)
        {
            Debug.LogWarning("[CockpitRig] Object_52 (pedals) not found — brake handle still works, pedals just will not animate.");
            return;
        }

        // THE HINGE MUST BE ON THE PEDAL, NOT ON THE NODE ORIGIN.
        //
        // This previously put the pivot at `pedals.localPosition`. In a glTF the mesh's
        // transform is usually identity with the geometry baked into the vertices, so that
        // "pivot" sat at the model datum — roughly a metre away from the pedals and well
        // below them. Tilting about a point a metre from the part throws it through a huge
        // arc, which is what put the pedals under the floor whenever the brake was applied.
        // That is a pivot-placement error, not a fault in the brake logic.
        var rends = pedals.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
        {
            Debug.LogWarning("[CockpitRig] pedals have no renderer — not animating them.");
            return;
        }
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        Vector3 hingeWorld = new Vector3(b.center.x, b.min.y, b.center.z);

        var pivot = new GameObject("PedalPivot").transform;
        pivot.SetParent(pedals.parent, false);
        pivot.position = hingeWorld;
        // Axis alignment, not node alignment: the pivot's X must be the aeroplane's LATERAL
        // axis, or a "14 degree" tilt would be 14 degrees about whatever the glTF node
        // happened to be rotated to.
        pivot.rotation = model.rotation;
        pedals.SetParent(pivot, true);          // keeps the mesh exactly where it is

        // TOE PADS on the pilot's two pedals. Purely visual, parented to the pedal so they
        // move with it, and with no collider — they are what the pedal IS, not a control.
        Vector3 padLocal = pivot.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
        foreach (float sx in new[] { -0.048f, 0.048f })
            CockpitHardware.ToeBrakePad(pivot,
                padLocal + new Vector3(sx, -0.004f, -0.008f), 62f, 34f);
        SetLayer(pivot, CockpitBuilder.CockpitLayer);

        var f = pivot.gameObject.AddComponent<PedalBrakeVisual>();
        f.phys = phys;
        f.maxTiltDeg = 14f;
        f.pedalHalfHeight = Mathf.Max(0.001f, b.extents.y);

        // A pedal that moves further than its own size is not hinged, it is thrown. The
        // check is cheap and it is exactly what was missing when this shipped broken.
        float worst = Vector3.Distance(hingeWorld, b.max) * f.maxTiltDeg * Mathf.Deg2Rad;
        if (worst > 0.12f)
            Debug.LogError(string.Format(
                "[CockpitRig] PEDAL HINGE BAD: full brake would move the pedal {0:0.000} m " +
                "(pedal is only {1:0.000} m tall). Hinge at {2}, bounds {3}.",
                worst, b.size.y, hingeWorld, b));
        else
            Debug.Log(string.Format("[CockpitRig] pedal hinge ok: {0:0.000} m sweep at full brake (pedal {1:0.000} m tall).",
                                    worst, b.size.y));
    }

    /// <summary>A panel toggle switch that actually does something. Used for the two
    /// systems items a HIGH mission's drill requires.</summary>
    PhysicalControl BuildToggle(string id, string label, Vector3 localPos, ControlTarget target, Color col)
    {
        var c = Make(id, model.TransformPoint(localPos));
        var lever = CockpitHardware.Toggle(c.transform, Vector3.zero, col);
        PlacardPlate(c.transform, new Vector3(0f, -0.0115f, -0.0006f), label, 22f, 5f);

        c.spec = new ControlSpec
        {
            id = id, label = label, kind = ControlKind.Toggle, target = target,
            axis = Vector3.up, travel = 0.02f, centred = false,
            captureRadius = 0.009f,
            smoothingTau = 0.04f,
            visual = lever, visualIsRotation = true,
            visualAxis = Vector3.right, visualTravel = 44f,   // flicks up when ON
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
        // The visual's REST transform, recorded now that the spec exists and the geometry
        // has been built. Every builder assigns `spec` and then calls Configure, so this is
        // the one place that is guaranteed to run after both.
        c.CaptureVisualRest();
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
    /// <summary>The cockpit FURNITURE: the panel hardware a control is mounted on or sits
    /// beside, none of it interactive.
    ///
    /// This is not decoration. The strongest single cue that the old cockpit was generated
    /// rather than real was the acre of empty dark panel around two glass displays — an
    /// aeroplane's panel is dense, and a bare one reads as a menu screen. It also matters
    /// experimentally: a participant told they are flying an aeroplane and shown an empty
    /// slab has been given a reason to disbelieve the whole scene, and disbelief is not a
    /// controlled variable.</summary>
    void BuildStructure()
    {
        var furn = new GameObject("PanelFurniture").transform;
        furn.SetParent(model, false);
        furn.localPosition = Vector3.zero;
        furn.localRotation = Quaternion.identity;

        // NO DISPLAY BEZELS. The GLB already models them (Object_83 is a bezel plate that
        // hugs both screens, with the radio stack in the 32 mm between them), and the pair
        // this file built earlier were 160 mm slabs that cut across the windscreen and
        // covered the bottom of the PFD. Adding furniture to a model without first measuring
        // what it already has is how that happens.

        // ── ENGINE-CONTROL SUB-PANEL, right of the pedestal ───────────────────────
        CockpitHardware.Box(furn, new Vector3(0.106f, EngineRowY, PanelZ + 0.0018f),
                            new Vector3(0.084f, 0.038f, 0.0035f), CockpitHardware.PanelSub, 0.20f);
        CockpitHardware.Box(furn, new Vector3(0.106f, EngineRowY - 0.0205f, PanelZ + 0.0010f),
                            new Vector3(0.084f, 0.004f, 0.006f), CockpitHardware.PanelLight, 0.35f);

        // ── LEFT SWITCH BANK ──────────────────────────────────────────────────────
        // Six toggles. Two are real controls built elsewhere (LOAD SHED, ALT STATIC); the
        // other four are the aeroplane's electrical switches, built as hardware and honestly
        // inert. A 172 has them and their absence is conspicuous, but inventing behaviour for
        // them would add uncontrolled variables to a workload study.
        CockpitHardware.Box(furn, new Vector3(-0.128f, 0.332f, PanelZ + 0.0018f),
                            new Vector3(0.092f, 0.026f, 0.0035f), CockpitHardware.PanelSub, 0.20f);
        string[] swNames = { "MSTR", "ALT", "AVN", "BCN" };
        for (int i = 0; i < swNames.Length; i++)
        {
            var g = new GameObject("Sw_" + swNames[i]).transform;
            g.SetParent(furn, false);
            g.localPosition = new Vector3(-0.106f + i * 0.0140f, 0.332f, PanelZ);
            CockpitHardware.Toggle(g, Vector3.zero, CockpitHardware.KnobWhite);
        }

        // ── CIRCUIT BREAKER FIELD, right lower panel below the flap lever ─────────
        CockpitHardware.Box(furn, new Vector3(0.090f, 0.332f, PanelZ + 0.0018f),
                            new Vector3(0.062f, 0.036f, 0.0035f), CockpitHardware.PanelSub, 0.20f);
        for (int r = 0; r < 3; r++)
            for (int col = 0; col < 5; col++)
                CockpitHardware.Breaker(furn, new Vector3(0.066f + col * 0.0120f,
                                                          0.320f + r * 0.0110f, PanelZ));

        BuildEngineGauges(furn);

        SetLayer(furn, CockpitBuilder.CockpitLayer);
    }

    /// <summary>Six live round gauges on the left panel: the engine and electrical
    /// instruments a 172 has, driven by AircraftSystems.
    ///
    /// These are NOT decoration, and adding them is not a neutral change. Three HIGH
    /// missions turn on diagnosing a systems failure — a rough engine, a failing alternator,
    /// a blocked static port — and until now the cockpit displayed none of the evidence a
    /// pilot would use to diagnose them. A participant could only guess, or follow the
    /// checklist text blindly. With the gauges present the diagnosis becomes a real
    /// perceptual task, which is what the mission was designed to be.
    ///
    /// It does change the information available in those missions, so it changes what they
    /// measure. That is recorded in the change report rather than slipped in quietly: the
    /// three affected missions should be re-piloted before their workload predictions are
    /// treated as calibrated.</summary>
    void BuildEngineGauges(Transform parent)
    {
        var specs = new[]
        {
            new PanelGauge.Spec { label = "RPM",   kind = PanelGauge.Kind.RPM,        min = 0f,    max = 3000f },
            new PanelGauge.Spec { label = "FUEL",  kind = PanelGauge.Kind.FuelTotal,  min = 0f,    max = 180f  },
            new PanelGauge.Spec { label = "OIL P", kind = PanelGauge.Kind.OilPress,   min = 0f,    max = 115f  },
            new PanelGauge.Spec { label = "OIL T", kind = PanelGauge.Kind.OilTemp,    min = 20f,   max = 120f  },
            new PanelGauge.Spec { label = "VOLTS", kind = PanelGauge.Kind.BusVolts,   min = 0f,    max = 32f   },
            new PanelGauge.Spec { label = "AMPS",  kind = PanelGauge.Kind.LoadAmps,   min = -20f,  max = 60f   },
        };

        for (int i = 0; i < specs.Length; i++)
        {
            float x = (i % 2 == 0) ? GaugeLeftX : GaugeRightX;
            float y = GaugeRowY[i / 2];
            var g = new GameObject("Gauge_" + specs[i].label.Replace(" ", "")).transform;
            g.SetParent(parent, false);
            g.localPosition = new Vector3(x, y, PanelZ);
            g.localRotation = Quaternion.identity;

            var face = CockpitHardware.GaugeCan(g, Vector3.zero, 46f);
            var pg = g.gameObject.AddComponent<PanelGauge>();
            pg.spec = specs[i];
            pg.systems = sys;
            pg.phys = phys;
            pg.Build(face, 46f);
            Label(g, new Vector3(0f, -0.0110f, -0.0053f), specs[i].label,
                  6.5f * CockpitHardware.MM, CockpitHardware.Placard);
        }
    }

    static void Paint(GameObject g, Color c)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", 0.35f);
        g.GetComponent<Renderer>().material = m;
    }
}
