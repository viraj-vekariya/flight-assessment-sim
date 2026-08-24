using UnityEngine;

/// <summary>
/// Light-aircraft flight model + per-wheel suspension ground handling for a
/// Cessna-style trainer. Airborne: thrust, lift (with stall), drag, flaps,
/// airspeed-scaled control torques, gentle weathervane. On the ground: three
/// spring/damper wheels hold the plane up (so it rolls and can ROTATE on the
/// mains to take off), with lateral tyre grip, light rolling resistance, brakes
/// and nose-wheel steering. Units exposed to HUD/gauges are METRIC (km/h, m, m/s).
/// </summary>
public enum LandingTier { None, Perfect, Good, Acceptable, HardLanding, Destroyed, Ditched }

[RequireComponent(typeof(Rigidbody))]
public class CessnaPhysics : MonoBehaviour
{
    [Header("Mass / geometry")]
    public float mass = 1100f;          // kg
    public float wingArea = 16.2f;      // m^2

    [Header("Engine")]
    public float maxThrust = 2800f;     // N — sized so a C172 tops out ~250 km/h and climbs ~3-4 m/s
    [Range(0f, 1f)] public float throttle = 0f;

    [Header("Aerodynamics")]
    public float airDensity = 1.225f;
    public float cl0 = 0.30f;
    public float clAlpha = 5.7f;
    public float stallAngleDeg = 16f;
    public float cd0 = 0.040f;           // parasitic drag (raised so top speed is realistic)
    public float inducedK = 0.045f;
    public float flapsLiftBonus = 0.5f;
    public float flapsDragBonus = 0.04f;
    // Spoilers / speedbrakes: extra drag to slow down + descend, and a lift dump so
    // the aircraft settles onto the runway (real wing spoilers do both).
    public float spoilerDragBonus = 0.055f;   // added drag coefficient at full spoiler
    public float spoilerLiftLoss = 0.40f;     // fraction of lift dumped at full spoiler

    [Header("Controls")]
    public float pitchPower = 13000f;
    public float rollPower = 16000f;
    public float yawPower = 7000f;
    public float controlRefSpeed = 38f; // m/s where control authority is full
    public bool invertPitch = false;
    public bool invertRoll = false;

    [Header("Stability")]
    [Range(0f, 1f)] public float weathervane = 0.30f;
    /// <summary>DIRECTIONAL STABILITY (weathercock stability, Cn_beta). Effective fin
    /// volume in m^3: yaw moment = 0.5 * rho * V * V_lateral * finVolume, which is the
    /// standard q * S_v * l_v * a_v * beta with beta ~ V_lateral / V.
    ///
    /// WHY IT IS A SEPARATE TERM FROM `weathervane`
    ///   `weathervane` rotates the whole aircraft toward its velocity vector on ALL
    ///   THREE axes and is tuned for pitch — it is what makes the nose drop in a stall.
    ///   At 0.30 it delivers about 2 N.m per radian of yaw misalignment, roughly three
    ///   thousand times too little to model a fin, so the aeroplane would fly through a
    ///   crosswind almost entirely SIDEWAYS instead of weathercocking into it. Measured:
    ///   with an 11 m/s crosswind at 55 m/s, the aircraft reached 2 degrees of drift in
    ///   8 s where the trigonometry says 11.3. Raising `weathervane` instead would have
    ///   changed the verified stall and pitch behaviour, so directional stability gets
    ///   its own yaw-only term and `weathervane` is left exactly as it was.
    ///
    /// SIZING. S_v ~ 1.6 m^2, l_v ~ 4.2 m, fin lift slope ~ 2.5 /rad gives ~17 m^3.
    /// With Izz ~ 1800 kg.m^2 that puts the Dutch-roll frequency near 0.67 Hz at
    /// cruise against a real C172's ~0.4-0.5 Hz, and the damping ratio near 0.24
    /// against a real ~0.1-0.2 — the right order, slightly over-damped, which is the
    /// safe direction to err in for an experiment (predictable beats twitchy).
    ///
    /// It applies ON THE GROUND TOO, and that is the point: it is what makes a
    /// crosswind take-off roll require rudder. Before this term existed a 15 kt
    /// crosswind produced no yawing tendency at all.</summary>
    public float finVolume = 17f;

    [Header("Aerodynamic rate damping")]
    // The tail/wings resist angular RATE, so a held control input settles at a steady rate
    // instead of accelerating without limit (aircraft feel, not a spacecraft). Scaled with
    // dynamic pressure, so damping grows with airspeed just like real aerodynamic damping.
    public float pitchDamp = 0.85f;
    public float rollDamp = 0.55f;
    public float yawDamp = 0.12f;   // gentle — must NOT lock the nose from yawing into a turn
    public float turnCoordination = 0.045f;   // bank -> heading change (coordinated turn)

    [Header("Landing gear (suspension)")]
    public Vector3[] wheels =
    {
        new Vector3(0f, -0.2f, 1.6f),    // nose
        new Vector3(-1.2f, -0.2f, -0.2f),// main left
        new Vector3(1.2f, -0.2f, -0.2f)  // main right
    };
    public float suspensionRest = 0.7f;  // wheel reach below the attach point
    public float suspensionTravel = 0.35f;
    public float springK = 32000f;
    public float damperK = 5000f;
    public float tyreGrip = 2600f;       // lateral grip per wheel (N per m/s slip)
    public float rollResistAccel = 0.35f;// light constant decel on the ground
    public float brakeAccel = 9f;        // wheel brakes (B) — firm enough to stop on rollout
    public float groundSteer = 9000f;
    public LayerMask groundMask = ~0;

    [Header("Ground & crash limits (all tunable)")]
    // A contact is judged by the CLOSING SPEED ALONG THE SURFACE NORMAL, not the
    // total relative speed — so rolling/taxiing along the runway (fast, but no
    // speed INTO the surface) never counts as an impact. The takeoff roll and
    // gentle landings are normal; only the cases below are crashes.
    public float maxSafeDescentRate = 3.5f;   // sink rate at touchdown (m/s) above which it's a hard landing
    public float impactSpeedLimit = 4f;       // closing speed into an obstacle/terrain (m/s) that destroys the plane
    public float maxTouchdownBank = 30f;      // bank angle (deg) at touchdown above which a wing strikes
    public float maxTouchdownNoseDown = 20f;  // nose-down pitch (deg) at touchdown = nose-first strike
    // Off-runway (grass/terrain) touchdown is a VALID landing, judged like the
    // runway by sink/bank/attitude — it just scores lower. Only hard sink, bad
    // attitude, water or a high-speed impact crash. Landing-quality tiers:
    public float perfectSink = 1.5f, goodSink = 2.5f;   // m/s
    public float perfectBank = 8f, goodBank = 18f;      // deg

    [Header("Stall & stability")]
    public float stallWarnMargin = 4f;     // deg of AoA before the stall to sound the warning
    public float stallRecovery = 1.6f;     // extra nose->velocity alignment in a stall (hands-off self-recovery)

    [Header("Ground effect")]
    public float groundEffectHeight = 6f;  // m above ground where extra lift begins (the flare)
    public float groundEffectGain = 0.35f; // max extra lift fraction in full ground effect

    // --- inputs (-1..1), set by AircraftController ---
    [HideInInspector] public float pitchInput;
    [HideInInspector] public float rollInput;
    [HideInInspector] public float yawInput;
    [HideInInspector] public float flaps;
    [HideInInspector] public float spoiler;   // 0..1 speedbrake / lift-dumper (X key)
    [HideInInspector] public bool braking;

    // ---- ELEVATOR TRIM ---------------------------------------------------------
    // PILOT CONVENTION: +1 = full nose UP, -1 = full nose DOWN. (Note this is the
    // OPPOSITE sign to pitchInput, where negative is nose up — the pilot-facing value
    // is the intuitive one and the conversion happens in one place, below.)
    //
    // WHY THIS EXISTS. Without trim the pilot must hold a sustained elevator force for
    // the whole of a 300 s mission to stay on altitude. That is a real, continuous
    // PHYSICAL effort which (a) differs between missions purely by how far the aircraft
    // is from its hands-off trim speed, and (b) is exactly the motor activity that
    // contaminates an EEG workload measure. Trim lets the pilot null that force out,
    // which is what a real pilot does within seconds of levelling off.
    /// <summary>Elevator trim, +1 nose up .. -1 nose down.</summary>
    [HideInInspector] public float trim = 0f;
    /// <summary>Trim authority as a fraction of full elevator deflection. 0.35 means a
    /// fully-trimmed aircraft holds an attitude that would otherwise need 35% elevator —
    /// enough to trim out cruise, climb and approach, not enough to fly on trim alone.</summary>
    public float trimAuthority = 0.35f;
    /// <summary>The total elevator command actually applied this frame (stick + trim),
    /// before control-authority scaling. Logged, so "how much were they holding?" is
    /// answerable separately from "how much did they move?".</summary>
    public float ElevatorCmd { get; private set; }

    // ---- ANALOG WHEEL BRAKES ---------------------------------------------------
    /// <summary>Brake pressure 0..1. `braking` stays as the on/off flag every existing
    /// caller uses; this scales how hard. Toe brakes and a VR trigger are analog, a
    /// keyboard is not, so the model has to accept both.</summary>
    [HideInInspector] public float brakeInput01 = 0f;

    // --- readouts (metric) ---
    public float AirspeedKmh { get; private set; }
    public float AirspeedMs { get; private set; }
    // ---- WIND-RELATIVE READOUTS -----------------------------------------------
    // AirspeedMs is the speed through the AIR (what the wing feels and what the ASI
    // shows). GroundSpeedMs is the speed over the ground (what the GPS/MFD shows and
    // what determines whether you reach the runway). They are equal only in calm air,
    // and the difference between them IS the crosswind/headwind task.
    /// <summary>Speed over the ground, m/s.</summary>
    public float GroundSpeedMs { get; private set; }
    /// <summary>The air-mass velocity the aircraft is currently in, m/s world axes.</summary>
    public Vector3 WindVel { get; private set; }
    /// <summary>Track minus heading, degrees. Positive = drifting right of the nose.
    /// A pilot holding a crab into a left crosswind flies with a POSITIVE drift angle.
    /// Zero in calm air; this is the primary observable of the crosswind task.</summary>
    public float DriftAngleDeg { get; private set; }
    /// <summary>Aerodynamic sideslip, degrees. Positive = relative wind from the right.</summary>
    public float SideslipDeg { get; private set; }
    public float AltitudeM { get; private set; }
    public float HeadingDeg { get; private set; }
    public float VerticalSpeedMs { get; private set; }
    public float AoADeg { get; private set; }
    public float RollDeg { get; private set; }
    public float PitchDeg { get; private set; }
    public float YawRateDps { get; private set; }
    public bool Stalled { get; private set; }
    public bool StallWarning { get; private set; }      // AoA nearing the stall (horn/HUD)
    public LandingTier Landing { get; private set; }    // quality of the last touchdown
    public float TouchdownSink { get; private set; }    // sink rate at the last touchdown (m/s)
    public float TouchdownBank { get; private set; }    // bank angle at the last touchdown (deg)
    public bool Grounded { get; private set; }
    public bool Crashed { get; private set; }
    public string CrashReason { get; private set; } = "";
    public float Throttle01 => throttle;
    public float Flaps01 => flaps;
    public float Spoiler01 => spoiler;
    public Rigidbody Body => rb;   // for external systems (scenario turbulence)

    // ---- FAILURE / SYSTEMS HOOKS (cognitive-load experiment) ------------------
    // AircraftSystems writes these each FixedUpdate. They are the ONLY way the
    // systems/failure layer touches the flight model, so normal flight with every
    // system healthy is bit-identical to this model without them (1/0/0/0/1).
    /// <summary>Fraction of commanded thrust actually delivered (engine health).
    /// 1 = normal, 0 = engine dead. Rough running is a small ripple on this.</summary>
    [HideInInspector] public float powerAvailable01 = 1f;
    /// <summary>Extra parasite drag coefficient (open door and similar).</summary>
    [HideInInspector] public float extraCd = 0f;
    /// <summary>Constant body-axis roll torque (N·m) — split/asymmetric flap.</summary>
    [HideInInspector] public float extraRollTorque = 0f;
    /// <summary>Constant body-axis yaw torque (N·m) — asymmetric drag, open door.</summary>
    [HideInInspector] public float extraYawTorque = 0f;
    /// <summary>Upper clamp on the flap setting the flap MOTOR can actually reach
    /// (1 = healthy). AircraftController honours it.</summary>
    [HideInInspector] public float flapAuthority01 = 1f;
    /// <summary>True when the flap motor is dead. The flaps are then STUCK at
    /// flapAuthority01 — they can neither extend nor RETRACT.
    ///
    /// The retract half matters. Previously authority was recomputed every frame as
    /// "wherever the flaps are now", so selecting UP after a motor failure let the
    /// flaps run all the way back to zero, one frame at a time, with authority chasing
    /// them down. A pilot could therefore undo a flap failure by selecting UP — which
    /// silently defeats H2, the mission whose entire decision is whether to spend the
    /// remaining battery on flaps you cannot take back.</summary>
    [HideInInspector] public bool flapMotorLocked = false;
    /// <summary>Thrust actually produced this frame (N) — for telemetry.</summary>
    public float ThrustN { get; private set; }

    Rigidbody rb;
    bool wasGroundedLast = true;
    SurfaceKind groundSurface = SurfaceKind.Runway;   // surface currently under the wheels

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 1.2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = 6f;
        rb.centerOfMass = new Vector3(0f, -0.2f, 0.0f);
    }

    void FixedUpdate()
    {
        // ---- WIND ------------------------------------------------------------
        // Every aerodynamic term below uses the AIR-RELATIVE velocity; the ground
        // handling (wheels, brakes, steering) uses the ground-relative one. That one
        // distinction is the whole wind model as far as the flight dynamics are
        // concerned. WindModel.Sample() returns exactly zero when no wind is
        // configured, so a calm mission is bit-identical to this model before the wind
        // layer existed — the same contract AircraftSystems' five hooks have.
        Vector3 groundVel = rb.linearVelocity;
        WindVel = WindModel.Sample(transform.position);
        Vector3 vel = groundVel - WindVel;          // air-relative: what the wing feels
        float speed = vel.magnitude;

        GroundSpeedMs = groundVel.magnitude;
        AirspeedMs = speed;
        AirspeedKmh = speed * 3.6f;
        AltitudeM = transform.position.y;
        VerticalSpeedMs = groundVel.y;              // the VSI is inertial, not air-relative
        HeadingDeg = NormalizeHeading(transform.eulerAngles.y);
        YawRateDps = rb.angularVelocity.y * Mathf.Rad2Deg;
        ComputeAttitude();

        // Drift = where you are GOING minus where you are POINTING. The single number
        // that says whether the pilot is compensating for the crosswind.
        Vector3 track = groundVel; track.y = 0f;
        DriftAngleDeg = track.sqrMagnitude > 1f
            ? Mathf.DeltaAngle(HeadingDeg, NormalizeHeading(Mathf.Atan2(track.x, track.z) * Mathf.Rad2Deg))
            : 0f;
        Vector3 lv = transform.InverseTransformDirection(vel);
        SideslipDeg = speed > 1.5f ? Mathf.Atan2(lv.x, Mathf.Max(0.5f, Mathf.Abs(lv.z))) * Mathf.Rad2Deg : 0f;

        // CRASHED -> wreck & fall: the engine is dead and there is no lift or
        // control, so the aircraft CANNOT keep flying. Gravity + the ground bring
        // it down to rest; the flight is over. (No thrust / aero / control below.)
        if (Crashed)
        {
            throttle = 0f;
            pitchInput = rollInput = yawInput = 0f;
            rb.linearDamping = 0.35f;     // bleed off speed so the wreck settles
            rb.angularDamping = 0.8f;
            GroundSuspension();           // still rest/tumble on the ground
            wasGroundedLast = Grounded;
            return;
        }

        // Thrust along the nose
        // Stick + trim. pitchInput is negative-for-nose-up; trim is positive-for-nose-up,
        // so it is subtracted. Clamped to the raw elevator range so trim can never buy
        // MORE authority than the stick has.
        //
        // Computed OUTSIDE the airspeed gate: it is a CONTROL POSITION, not an
        // aerodynamic force, and telemetry needs it on the ground too (during taxi, and
        // in the control-check bench where the aeroplane is parked). It used to sit
        // inside `if (speed > 1.5f)` and read 0.00 whenever the aircraft was stationary.
        ElevatorCmd = Mathf.Clamp(pitchInput - trim * trimAuthority, -1f, 1f);

        ThrustN = maxThrust * throttle * Mathf.Clamp01(powerAvailable01);
        rb.AddForce(transform.forward * ThrustN);

        // Aerodynamics + controls — active whenever there is meaningful airflow.
        Vector3 localVel = transform.InverseTransformDirection(vel);
        if (speed > 1.5f)
        {
            // Angle of attack in the pitch plane. Guard the forward component so it
            // stays well-defined at very low / backward forward-speed (deep stall)
            // instead of snapping to 0 or wrapping.
            float fz = localVel.z;
            float vy = -localVel.y;
            float aoaRad = (fz > 0.3f)
                ? Mathf.Atan2(vy, fz)
                : (Mathf.Abs(vy) < 0.001f ? 0f : Mathf.Sign(vy)) * (Mathf.PI * 0.5f);
            AoADeg = Mathf.Clamp(aoaRad * Mathf.Rad2Deg, -90f, 90f);

            float q = 0.5f * airDensity * speed * speed;

            float cl;
            float aoaAbs = Mathf.Abs(AoADeg);
            float clBase = cl0 + flaps * flapsLiftBonus;
            if (aoaAbs <= stallAngleDeg)
            {
                cl = clBase + clAlpha * aoaRad;
                Stalled = false;
            }
            else
            {
                float clPeak = clBase + clAlpha * (stallAngleDeg * Mathf.Deg2Rad);
                float fade = Mathf.Clamp01(1f - (aoaAbs - stallAngleDeg) / 30f);
                cl = clPeak * fade * Mathf.Sign(AoADeg);
                Stalled = true;
            }

            // spoilers dump lift (settle onto the runway / steepen the descent)
            cl *= (1f - spoiler * spoilerLiftLoss);

            // stall warning a few degrees of AoA before the actual stall
            StallWarning = aoaAbs >= (stallAngleDeg - stallWarnMargin);

            // ground effect: extra lift within ~one wingspan of the ground (the flare)
            float ge = 0f;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit gh,
                                groundEffectHeight * 2f, groundMask, QueryTriggerInteraction.Ignore)
                && !gh.collider.transform.IsChildOf(transform))
                ge = groundEffectGain * Mathf.Clamp01(1f - gh.distance / groundEffectHeight);

            float cd = cd0 + extraCd + flaps * flapsDragBonus + spoiler * spoilerDragBonus + inducedK * cl * cl;
            Vector3 liftDir = Vector3.Cross(vel.normalized, transform.right).normalized;
            rb.AddForce(liftDir * (q * wingArea * cl * (1f + ge)) - vel.normalized * (q * wingArea * cd));

            // Asymmetry torques from failed systems (split flap, open door). Scaled by
            // dynamic pressure so they vanish at a standstill, like a real aero moment.
            if (extraRollTorque != 0f || extraYawTorque != 0f)
            {
                float qScale = Mathf.Clamp01(q / 900f);
                rb.AddRelativeTorque(0f, extraYawTorque * qScale, -extraRollTorque * qScale, ForceMode.Force);
            }

            float authority = Mathf.Clamp01((speed * speed) / (controlRefSpeed * controlRefSpeed));
            if (!Grounded) authority = Mathf.Max(authority, 0.15f); // never fully uncontrollable in the air


            float ps = invertPitch ? -1f : 1f;
            float rs = invertRoll ? -1f : 1f;
            rb.AddRelativeTorque(
                ElevatorCmd * pitchPower * ps * authority,
                yawInput * yawPower * authority,
                -rollInput * rollPower * rs * authority,
                ForceMode.Force);

            // Aerodynamic rate damping: oppose the current angular RATE (scaled by dynamic
            // pressure). This gives a control input a bounded steady response — deflect to a
            // rate, release to stop — instead of an ever-accelerating rotation.
            Vector3 wLocal = transform.InverseTransformDirection(rb.angularVelocity);
            float dq = q * wingArea;
            rb.AddRelativeTorque(
                -wLocal.x * pitchDamp * dq,
                -wLocal.y * yawDamp * dq,
                -wLocal.z * rollDamp * dq,
                ForceMode.Force);

            // Coordinated turn: a bank makes the aircraft change heading. The horizontal
            // component of lift curves the flight path; here we yaw the NOSE to follow it so
            // heading actually changes in a bank (right bank -> turn right). Scaled by dynamic
            // pressure and cut off near/after the stall (no lift = no turn).
            if (!Grounded && aoaAbs < stallAngleDeg)
            {
                float bank = Mathf.Clamp(RollDeg, -75f, 75f) * Mathf.Deg2Rad;
                rb.AddRelativeTorque(0f, Mathf.Sin(bank) * turnCoordination * dq, 0f, ForceMode.Force);
            }

            // FIN / DIRECTIONAL STABILITY. Always on, airborne and on the ground.
            // Sign: localVel.x > 0 means the relative wind comes from the RIGHT, and the
            // nose must swing right (positive yaw) to point into it.
            if (Mathf.Abs(localVel.x) > 0.01f)
                rb.AddRelativeTorque(0f, 0.5f * airDensity * speed * localVel.x * finVolume, 0f,
                                     ForceMode.Force);

            // Static stability: align the nose toward the velocity vector. In a
            // stall this pulls the nose DOWN toward the airflow, so the aircraft
            // self-recovers hands-off (nose drops, speed builds) — recovery with
            // correct inputs is reliable, not a fight.
            if (weathervane > 0f && !Grounded)
            {
                Quaternion target = Quaternion.LookRotation(vel.normalized, transform.up);
                Quaternion delta = target * Quaternion.Inverse(transform.rotation);
                delta.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                float wv = weathervane;
                if (aoaAbs > stallAngleDeg)                       // stronger recovery once stalled
                    wv *= 1f + stallRecovery * Mathf.Clamp01((aoaAbs - stallAngleDeg) / 20f);
                if (!float.IsInfinity(axis.x))
                    rb.AddTorque(axis.normalized * (ang * Mathf.Deg2Rad) * wv * q * 0.02f, ForceMode.Force);
            }
        }
        else
        {
            AoADeg = 0f;
            Stalled = false;
            StallWarning = false;
        }

        GroundSuspension();

        // Touchdown moment: the wheels just met a surface — judge it.
        if (Grounded && !wasGroundedLast) EvaluateTouchdown();
        wasGroundedLast = Grounded;
    }

    // Body-collider contact (e.g. flying into a mountain/building, or a wingtip/
    // tail grazing the runway during rotation). Judged by closing speed ALONG THE
    // CONTACT NORMAL, so sliding/rolling parallel to a surface is never an impact.
    void OnCollisionEnter(Collision c) => EvaluateContact(c);
    void OnCollisionStay(Collision c) => EvaluateContact(c);

    void EvaluateContact(Collision c)
    {
        if (Crashed) return;
        SurfaceKind kind = SurfaceOf(c.collider);
        float closing = Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal));

        switch (kind)
        {
            case SurfaceKind.Water:
                SetCrash("Ditched in water"); Landing = LandingTier.Ditched;
                break;
            case SurfaceKind.Obstacle:
                if (closing > impactSpeedLimit) { SetCrash("Destroyed (impact)"); Landing = LandingTier.Destroyed; }
                break;
            case SurfaceKind.Ground:
                // a gentle terrain graze is NOT a crash; only a genuine high-speed
                // impact into terrain destroys the plane.
                if (closing > impactSpeedLimit) { SetCrash("Destroyed (terrain impact)"); Landing = LandingTier.Destroyed; }
                break;
            case SurfaceKind.Runway:
                // tail/wingtip graze while rolling has ~0 normal closing speed -> safe;
                // only a genuine slam into the runway is a hard landing.
                if (closing > maxSafeDescentRate) { SetCrash("Hard landing"); Landing = LandingTier.HardLanding; }
                break;
        }
    }

    // The wheels just touched down — judge the landing by sink rate, attitude and
    // which surface we landed on. The takeoff roll never reaches here (the plane
    // stays grounded, so there is no air->ground transition).
    void EvaluateTouchdown()
    {
        if (Crashed) return;
        float sink = -VerticalSpeedMs;                                   // +ve = descending
        bool inverted = Vector3.Dot(transform.up, Vector3.up) < 0.3f;
        TouchdownSink = Mathf.Max(0f, sink);
        TouchdownBank = Mathf.Abs(RollDeg);

        switch (groundSurface)
        {
            case SurfaceKind.Water:
                SetCrash("Ditched in water"); Landing = LandingTier.Ditched;
                break;
            case SurfaceKind.Obstacle:
                SetCrash("Destroyed (impact)"); Landing = LandingTier.Destroyed;
                break;
            default: // Runway OR Ground (grass/terrain) — both are valid landing
                     // surfaces, judged the same way; off-runway just scores lower.
                LandJudge(groundSurface == SurfaceKind.Runway, sink, inverted);
                break;
        }
    }

    // Crash only on hard sink / bad attitude; otherwise grade the landing quality.
    void LandJudge(bool onRunway, float sink, bool inverted)
    {
        if (sink > maxSafeDescentRate) { SetCrash("Hard landing"); Landing = LandingTier.HardLanding; }
        else if (inverted || TouchdownBank > maxTouchdownBank) { SetCrash("Wing strike (excess bank)"); Landing = LandingTier.HardLanding; }
        else if (PitchDeg < -maxTouchdownNoseDown) { SetCrash("Nose-first touchdown"); Landing = LandingTier.HardLanding; }
        else if (onRunway && sink <= perfectSink && TouchdownBank <= perfectBank) Landing = LandingTier.Perfect;
        else if (sink <= goodSink && TouchdownBank <= goodBank) Landing = onRunway ? LandingTier.Good : LandingTier.Acceptable;
        else Landing = LandingTier.Acceptable;
    }

    static SurfaceKind SurfaceOf(Collider col)
    {
        var t = col.GetComponentInParent<SurfaceTag>();
        return t != null ? t.Kind : SurfaceKind.Obstacle;   // unknown solid = treat as obstacle
    }

    void SetCrash(string reason)
    {
        if (Crashed) return;
        Crashed = true;
        CrashReason = reason;
    }

    void GroundSuspension()
    {
        int contacts = 0;
        Vector3 up = transform.up;
        SurfaceKind frameSurface = SurfaceKind.Ground;   // what the wheels are rolling on this frame
        bool surfaceSet = false;

        foreach (var local in wheels)
        {
            Vector3 wp = transform.TransformPoint(local);
            Vector3 origin = wp + up * suspensionTravel;
            float maxDist = suspensionRest + suspensionTravel;

            if (Physics.Raycast(origin, -up, out RaycastHit hit, maxDist, groundMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                contacts++;

                // surface under this wheel; Runway wins if any wheel is on it
                var st = hit.collider.GetComponentInParent<SurfaceTag>();
                SurfaceKind k = st != null ? st.Kind : SurfaceKind.Ground;
                if (!surfaceSet || k == SurfaceKind.Runway) { frameSurface = k; surfaceSet = true; }

                float dist = hit.distance - suspensionTravel;          // attach -> ground
                float compression = Mathf.Clamp(suspensionRest - dist, 0f, suspensionRest);

                Vector3 wheelVel = rb.GetPointVelocity(wp);
                float springF = compression * springK;
                float damperF = -Vector3.Dot(wheelVel, up) * damperK;
                float normalF = Mathf.Max(0f, springF + damperF);
                rb.AddForceAtPosition(up * normalF, wp);

                // lateral tyre grip
                float slip = Vector3.Dot(wheelVel, transform.right);
                rb.AddForceAtPosition(-transform.right * (slip * tyreGrip), wp);
            }
        }

        Grounded = contacts > 0;
        if (Grounded) groundSurface = frameSurface;
        if (!Grounded) return;

        // forward rolling resistance + brakes (gentle, only while actually rolling)
        float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (Mathf.Abs(fwdSpeed) > 0.3f)
        {
            float decel = rollResistAccel + (braking ? brakeAccel * Mathf.Clamp01(brakeInput01) : 0f);
            rb.AddForce(-transform.forward * (Mathf.Sign(fwdSpeed) * decel * mass));
        }

        // nose-wheel steering (needs roll speed to bite)
        float steerBite = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 6f);
        rb.AddRelativeTorque(0f, yawInput * groundSteer * steerBite, 0f, ForceMode.Force);

        // parking hold: with no throttle and barely moving, damp out any drift so
        // the plane sits still on the runway instead of creeping.
        if (throttle < 0.05f && AirspeedMs < 3f)
        {
            Vector3 horiz = rb.linearVelocity; horiz.y = 0f;
            rb.AddForce(-horiz * 10f, ForceMode.Acceleration);
        }
    }

    void ComputeAttitude()
    {
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        PitchDeg = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
        RollDeg = Mathf.Asin(Mathf.Clamp(-right.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    public void ResetTo(Vector3 position, Quaternion rotation, bool startAirborne, float startSpeed)
    {
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        rb.linearVelocity = startAirborne ? rotation * Vector3.forward * startSpeed : Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        throttle = startAirborne ? 0.65f : 0f;
        flaps = 0f;
        spoiler = 0f;
        pitchInput = rollInput = yawInput = 0f;
        braking = false; brakeInput01 = 0f;
        trim = 0f; ElevatorCmd = 0f;
        flapAuthority01 = 1f; flapMotorLocked = false;
        Crashed = false;
        CrashReason = "";
        wasGroundedLast = true;
        Landing = LandingTier.None;
        StallWarning = false;
        TouchdownSink = 0f; TouchdownBank = 0f;
        rb.linearDamping = 0f;        // undo the wreck-and-fall damping
        rb.angularDamping = 1.2f;
    }

    static float NormalizeHeading(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }
}
