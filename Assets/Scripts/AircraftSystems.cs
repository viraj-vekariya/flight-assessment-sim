// AircraftSystems — the simulated systems layer for the cognitive-load experiment.
//
// WHAT THIS IS
//   A light, deterministic model of the SUBSYSTEMS of a Cessna 172-class trainer
//   (carburetted engine, fixed-pitch prop, FIXED tricycle gear, ELECTRIC flaps,
//   single alternator + battery, pitot-static instruments). It exists so that the
//   abnormal/emergency missions change what the pilot actually sees and feels,
//   instead of just printing a warning banner.
//
// WHAT IT IS NOT
//   It is NOT an engineering-grade systems simulation. Every quantity here is a
//   MINIMUM DEFENSIBLE ABSTRACTION chosen to reproduce the *cues and decisions*
//   a pilot faces, at the right timescale — not the physics of a magneto or a
//   carburettor venturi. Every abstraction is documented in TELEMETRY_SCHEMA.md
//   and MISSION_IMPLEMENTATION.md. Nothing here should be reported as a
//   validated systems model.
//
// AIRCRAFT DEFINITION (fixed for the whole experiment — see EXPERIMENT_PROTOCOL.md)
//   The study aircraft is a C172-class high-wing single: CARBURETTED engine with
//   carburettor heat (so induction icing is in scope), FIXED gear (so "gear will
//   not extend" is NOT in scope and is deliberately absent from all 12 missions),
//   ELECTRIC flaps on the main bus (so flap failure and alternator failure
//   interact), and conventional pitot-static instruments.
//
// HOW IT TOUCHES THE FLIGHT MODEL
//   Only through the four hooks on CessnaPhysics: powerAvailable01, extraCd,
//   extraRollTorque, extraYawTorque (+ flapAuthority01 read by AircraftController).
//   With every system healthy those are 1/0/0/0/1 and the flight model behaves
//   exactly as it did before this file existed.
//
// INDICATED vs TRUE
//   Displays and telemetry must be able to disagree with reality (that is the
//   whole point of a blocked static port). AircraftSystems therefore publishes
//   IndicatedAirspeedKmh / IndicatedAltitudeM / IndicatedVSpeedMs, which equal the
//   true values until a pitot-static failure is armed. Telemetry logs BOTH.

using UnityEngine;

public enum EngineState { Normal, Rough, PartialPower, Failed }
public enum FuelSelector { Left, Right, Both }

/// <summary>Every abnormality the mission set can arm. One enum so missions,
/// checklists, telemetry and the test harness all speak the same language.</summary>
public enum FailureKind
{
    None,
    CarbIce,            // gradual partial power loss; cured by carb heat
    EngineRoughness,    // rough running, small power loss, ambiguous cause
    EngineFailure,      // complete power loss -> forced landing
    FuelStarvation,     // selected tank dry -> power loss; cured by tank change
    AlternatorFailure,  // bus on battery; volts decay; electric flaps at risk
    StaticBlocked,      // altimeter/ASI/VSI lie (AFH ch.18 signature)
    PitotBlocked,       // ASI behaves like an altimeter
    AttitudeFailure,    // attitude/heading reference lost (partial panel)
    FlapMotorFailure,   // flaps stuck where they are -> no-flap landing
    SplitFlap,          // asymmetric flap -> roll + yaw, cross-controlled
    DoorOpen,           // loud, distracting, barely affects flying (AFH ch.18)
    RadioFailure,       // COM lost -> no ATC channel
    BrakeFailure        // no wheel braking on rollout
}

[RequireComponent(typeof(CessnaPhysics))]
public class AircraftSystems : MonoBehaviour
{
    public static AircraftSystems Instance { get; private set; }

    // ---------------- ENGINE ----------------
    public EngineState Engine { get; private set; } = EngineState.Normal;
    /// <summary>Engine health 0..1 before roughness ripple. Drives thrust.</summary>
    public float EnginePower01 { get; private set; } = 1f;
    public float RPM { get; private set; }
    public float OilPressurePsi { get; private set; } = 75f;
    public float OilTempC { get; private set; } = 85f;
    /// <summary>Pilot control: carburettor heat ON removes induction ice (and costs
    /// a little power, as it does in the real aeroplane).</summary>
    public bool CarbHeatOn { get; set; }
    /// <summary>0..1 how much induction ice has accreted. Only grows when armed.</summary>
    public float CarbIce01 { get; private set; }

    // ---------------- FUEL ----------------
    public float FuelLeftL { get; private set; } = 90f;
    public float FuelRightL { get; private set; } = 90f;
    public FuelSelector Selector { get; set; } = FuelSelector.Both;
    public float FuelFlowLph { get; private set; }
    public float FuelTotalL => FuelLeftL + FuelRightL;

    // ---------------- ELECTRICAL ----------------
    public bool AlternatorOn { get; private set; } = true;
    /// <summary>Bus volts. ~28 V with the alternator on line, decaying on battery.</summary>
    public float BusVolts { get; private set; } = 28f;
    /// <summary>Remaining battery charge, amp-hours.</summary>
    public float BatteryAh { get; private set; } = 24f;
    /// <summary>Electrical load the pilot has NOT shed (amps).</summary>
    public float LoadA { get; private set; } = 22f;
    /// <summary>Pilot action: shed non-essential loads (AFH ch.18 first item).</summary>
    public bool LoadShed { get; set; }
    public bool BusDead => BusVolts < 18f;

    // ---------------- PITOT-STATIC / AVIONICS ----------------
    public bool StaticBlocked { get; private set; }
    public bool PitotBlocked { get; private set; }
    /// <summary>Pilot action: alternate static source restores the static system.</summary>
    public bool AlternateStaticOpen { get; set; }
    public bool AttitudeOk { get; private set; } = true;
    public bool RadioOk { get; private set; } = true;
    public bool BrakesOk { get; private set; } = true;

    // ---------------- AIRFRAME ----------------
    public bool DoorOpen { get; private set; }
    public bool FlapMotorOk { get; private set; } = true;
    /// <summary>-1..1 asymmetry: +1 = right flap down / left up (rolls LEFT).</summary>
    public float FlapAsymmetry { get; private set; }

    // ---------------- INDICATED (what the pilot's instruments show) ----------------
    public float IndicatedAirspeedKmh { get; private set; }
    public float IndicatedAltitudeM { get; private set; }
    public float IndicatedVSpeedMs { get; private set; }

    /// <summary>Set by the engine when a failure is armed, so the checklist system and
    /// the test harness can ask "is this abnormality still present?".</summary>
    public FailureKind Armed { get; private set; } = FailureKind.None;

    /// <summary>Raised the first time a newly armed failure produces a cue the pilot
    /// could actually perceive (used for the CUE_ONSET event marker — the epoch zero
    /// for EEG, which must be the CUE, not the code call).</summary>
    public System.Action<FailureKind> OnCuePerceptible;

    /// <summary>Raised whenever the pilot moves a systems control (carb heat, fuel
    /// selector, load shed, alternate static). ScenarioEngine turns this into a
    /// CONFIGURATION_CHANGE marker, which is both an EEG epoch boundary and the
    /// behavioural record of what the pilot actually DID during a drill.</summary>
    public System.Action<string> OnConfigChange;

    /// <summary>Pilot switch actions. Routed through here rather than set directly so
    /// every one of them is timestamped exactly once.</summary>
    public void SetCarbHeat(bool on)
    { if (CarbHeatOn == on) return; CarbHeatOn = on; OnConfigChange?.Invoke("carb_heat=" + (on ? "ON" : "OFF")); }
    public void SetSelector(FuelSelector sel)
    { if (Selector == sel) return; Selector = sel; OnConfigChange?.Invoke("fuel_selector=" + sel); }
    public void SetLoadShed(bool on)
    { if (LoadShed == on) return; LoadShed = on; OnConfigChange?.Invoke("load_shed=" + (on ? "ON" : "OFF")); }
    public void SetAlternateStatic(bool on)
    { if (AlternateStaticOpen == on) return; AlternateStaticOpen = on; OnConfigChange?.Invoke("alt_static=" + (on ? "OPEN" : "CLOSED")); }

    CessnaPhysics ac;
    float staticFrozenAlt, pitotFrozenQ;         // frozen references for blocked systems
    float roughPhase, carbRate, iceTarget;
    bool cueRaised;
    float armTime;

    void Awake()
    {
        Instance = this;
        ac = GetComponent<CessnaPhysics>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Full reset to a serviceable aeroplane. Called at the start of every
    /// trial so no failure can ever leak across missions.</summary>
    public void ResetAll()
    {
        Engine = EngineState.Normal; EnginePower01 = 1f;
        OilPressurePsi = 75f; OilTempC = 85f;
        CarbHeatOn = false; CarbIce01 = 0f; carbRate = 0f; iceTarget = 0f;
        FuelLeftL = FuelRightL = 90f; Selector = FuelSelector.Both;
        AlternatorOn = true; BusVolts = 28f; BatteryAh = 24f; LoadA = 22f; LoadShed = false;
        StaticBlocked = PitotBlocked = false; AlternateStaticOpen = false;
        if (ac != null) { ac.flapMotorLocked = false; ac.flapAuthority01 = 1f; }
        AttitudeOk = RadioOk = BrakesOk = true;
        DoorOpen = false; FlapMotorOk = true; FlapAsymmetry = 0f;
        Armed = FailureKind.None; cueRaised = false; armTime = 0f;
        roughPhase = 0f;
        ApplyToPhysics();
    }

    /// <summary>Arm an abnormality. `severity` 0..1 scales how bad it is; `param` is
    /// kind-specific (e.g. the flap position a split flap freezes at).</summary>
    public void Arm(FailureKind kind, float severity = 1f, float param = 0f)
    {
        Armed = kind; cueRaised = false; armTime = Time.time;
        severity = Mathf.Clamp01(severity);

        switch (kind)
        {
            case FailureKind.CarbIce:
                // Ice accretes over ~40-70 s, costing up to ~35% power. Deliberately
                // GRADUAL and ambiguous: the diagnostic burden is the point.
                carbRate = Mathf.Lerp(0.010f, 0.025f, severity);
                iceTarget = Mathf.Lerp(0.45f, 1f, severity);
                break;
            case FailureKind.EngineRoughness:
                Engine = EngineState.Rough;
                EnginePower01 = Mathf.Lerp(0.92f, 0.75f, severity);
                break;
            case FailureKind.EngineFailure:
                Engine = EngineState.Failed; EnginePower01 = 0f;
                OilPressurePsi = 0f;
                break;
            case FailureKind.FuelStarvation:
                // Drain the SELECTED side only; the other tank still has fuel, so the
                // fix is a tank change, not a forced landing.
                if (Selector == FuelSelector.Right) FuelRightL = 0f;
                else FuelLeftL = 0f;
                break;
            case FailureKind.AlternatorFailure:
                AlternatorOn = false;
                break;
            case FailureKind.StaticBlocked:
                StaticBlocked = true; staticFrozenAlt = ac.AltitudeM;
                break;
            case FailureKind.PitotBlocked:
                PitotBlocked = true;
                pitotFrozenQ = ac.AirspeedMs * ac.AirspeedMs;
                staticFrozenAlt = ac.AltitudeM;
                break;
            case FailureKind.AttitudeFailure: AttitudeOk = false; break;
            case FailureKind.RadioFailure:    RadioOk = false; break;
            case FailureKind.BrakeFailure:    BrakesOk = false; break;
            case FailureKind.DoorOpen:        DoorOpen = true; break;
            case FailureKind.FlapMotorFailure: FlapMotorOk = false; break;
            case FailureKind.SplitFlap:
                FlapMotorOk = false;
                FlapAsymmetry = Mathf.Lerp(0.4f, 1f, severity) * (param >= 0f ? 1f : -1f);
                break;
        }
    }

    /// <summary>Clear one abnormality (a successful pilot action, or mission reset).</summary>
    public void Clear(FailureKind kind)
    {
        switch (kind)
        {
            case FailureKind.CarbIce: CarbIce01 = 0f; carbRate = 0f; iceTarget = 0f; break;
            case FailureKind.EngineRoughness: Engine = EngineState.Normal; EnginePower01 = 1f; break;
            case FailureKind.EngineFailure: Engine = EngineState.Normal; EnginePower01 = 1f; OilPressurePsi = 75f; break;
            case FailureKind.AlternatorFailure: AlternatorOn = true; break;
            case FailureKind.StaticBlocked: StaticBlocked = false; break;
            case FailureKind.PitotBlocked: PitotBlocked = false; break;
            case FailureKind.AttitudeFailure: AttitudeOk = true; break;
            case FailureKind.RadioFailure: RadioOk = true; break;
            case FailureKind.BrakeFailure: BrakesOk = true; break;
            case FailureKind.DoorOpen: DoorOpen = false; break;
            case FailureKind.FlapMotorFailure: FlapMotorOk = true; break;
            case FailureKind.SplitFlap: FlapAsymmetry = 0f; FlapMotorOk = true; break;
        }
        if (Armed == kind) Armed = FailureKind.None;
    }

    /// <summary>True when the armed abnormality has been correctly dealt with. The
    /// mission's FAILURE_RESOLVED marker fires on the rising edge of this.</summary>
    public bool IsResolved(FailureKind kind)
    {
        switch (kind)
        {
            case FailureKind.CarbIce:          return CarbIce01 < 0.05f;
            case FailureKind.FuelStarvation:   return SelectedTankHasFuel && EnginePower01 > 0.9f;
            case FailureKind.AlternatorFailure:return LoadShed || AlternatorOn;
            case FailureKind.StaticBlocked:    return AlternateStaticOpen || !StaticBlocked;
            case FailureKind.PitotBlocked:     return !PitotBlocked;
            case FailureKind.DoorOpen:         return !DoorOpen;
            case FailureKind.EngineFailure:    return EnginePower01 > 0.9f;
            case FailureKind.None:             return true;
            // Un-fixable in flight — the pilot flies the consequence, not a cure.
            case FailureKind.FlapMotorFailure:
            case FailureKind.SplitFlap:
            case FailureKind.AttitudeFailure:
            case FailureKind.RadioFailure:
            case FailureKind.BrakeFailure:     return false;
            default: return false;
        }
    }

    bool SelectedTankHasFuel =>
        Selector == FuelSelector.Both ? (FuelLeftL > 0.5f || FuelRightL > 0.5f)
      : Selector == FuelSelector.Left ? FuelLeftL > 0.5f
                                      : FuelRightL > 0.5f;

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        UpdateEngine(dt);
        UpdateFuel(dt);
        UpdateElectrical(dt);
        UpdateIndications();
        ApplyToPhysics();
        RaiseCueIfPerceptible();
    }

    void UpdateEngine(float dt)
    {
        // --- carburettor icing ---------------------------------------------------
        // Grows while armed and carb heat is OFF; melts out in ~8 s with heat ON.
        // First symptom of ice in a fixed-pitch aeroplane is an RPM drop, then
        // roughness (FAA PHAK / NTSB SA-029) — reproduced below via EnginePower01.
        if (carbRate > 0f)
        {
            if (CarbHeatOn) CarbIce01 = Mathf.MoveTowards(CarbIce01, 0f, dt * 0.125f);
            else            CarbIce01 = Mathf.MoveTowards(CarbIce01, iceTarget, dt * carbRate);
        }

        float health = 1f;
        if (Engine == EngineState.Failed) health = 0f;
        else
        {
            health = EnginePower01;
            health *= 1f - 0.35f * CarbIce01;           // ice costs up to 35% power
            if (CarbHeatOn) health *= 0.95f;            // hot air costs ~5%, as in life
            if (!SelectedTankHasFuel) health = 0f;      // starvation = no power
            if (CarbIce01 > 0.35f || Engine == EngineState.Rough)
            {
                roughPhase += dt * 11f;
                health *= 1f + 0.055f * Mathf.Sin(roughPhase) * Mathf.Clamp01(CarbIce01 * 2f + (Engine == EngineState.Rough ? 1f : 0f));
            }
        }
        EngineHealthNow = Mathf.Clamp01(health);

        // RPM: fixed-pitch, so RPM tracks power and airspeed loosely.
        float target = Engine == EngineState.Failed
            ? Mathf.Lerp(0f, 700f, Mathf.Clamp01(ac.AirspeedMs / 45f))   // windmilling
            : Mathf.Lerp(700f, 2700f, ac.Throttle01 * EngineHealthNow);
        RPM = Mathf.MoveTowards(RPM, target, dt * 900f);

        // Oil: pressure follows RPM, temperature drifts toward a load-dependent value.
        float oilTarget = Engine == EngineState.Failed ? 0f : Mathf.Lerp(25f, 85f, Mathf.Clamp01(RPM / 2400f));
        OilPressurePsi = Mathf.MoveTowards(OilPressurePsi, oilTarget, dt * 30f);
        float tTarget = Mathf.Lerp(60f, 105f, ac.Throttle01) - (CarbHeatOn ? 0f : 0f);
        OilTempC = Mathf.MoveTowards(OilTempC, tTarget, dt * 1.5f);
    }
    /// <summary>Engine health actually applied to thrust this frame (incl. roughness ripple).</summary>
    public float EngineHealthNow { get; private set; } = 1f;

    void UpdateFuel(float dt)
    {
        // ~34 L/h at full power for a 172-class trainer; scaled by throttle.
        FuelFlowLph = Engine == EngineState.Failed ? 0f : Mathf.Lerp(6f, 34f, ac.Throttle01);
        float burn = FuelFlowLph / 3600f * dt;
        switch (Selector)
        {
            case FuelSelector.Left:  FuelLeftL = Mathf.Max(0f, FuelLeftL - burn); break;
            case FuelSelector.Right: FuelRightL = Mathf.Max(0f, FuelRightL - burn); break;
            default:
                FuelLeftL = Mathf.Max(0f, FuelLeftL - burn * 0.5f);
                FuelRightL = Mathf.Max(0f, FuelRightL - burn * 0.5f);
                break;
        }
    }

    void UpdateElectrical(float dt)
    {
        LoadA = LoadShed ? 7f : 22f;
        if (AlternatorOn)
        {
            BusVolts = Mathf.MoveTowards(BusVolts, 28f, dt * 6f);
            BatteryAh = Mathf.Min(24f, BatteryAh + dt * 0.002f);
        }
        else
        {
            // Battery only. AFH ch.18: a 40 A load can flatten a battery in 10-15 min;
            // shedding load is the first item. Volts sag as charge is used.
            BatteryAh = Mathf.Max(0f, BatteryAh - LoadA * dt / 3600f);
            float soc = Mathf.Clamp01(BatteryAh / 24f);
            BusVolts = Mathf.MoveTowards(BusVolts, Mathf.Lerp(16f, 24.5f, soc), dt * 1.5f);
        }
        // Electric flaps live on the main bus: a flat battery means no flaps.
        if (BusDead) FlapMotorOk = false;
    }

    void UpdateIndications()
    {
        // Healthy case: indicated == true.
        IndicatedAirspeedKmh = ac.AirspeedKmh;
        IndicatedAltitudeM = ac.AltitudeM;
        IndicatedVSpeedMs = ac.VerticalSpeedMs;

        bool staticBad = StaticBlocked && !AlternateStaticOpen;
        if (staticBad)
        {
            // AFH ch.18: with the static source blocked the altimeter freezes at the
            // blockage altitude; in a descent the ASI over-reads and the VSI reads ~0.
            // Modelled as a partial (severe but not total) restriction, which is the
            // "insidious" case the handbook singles out.
            float lag = ac.AltitudeM - staticFrozenAlt;                 // + = climbed since
            IndicatedAltitudeM = staticFrozenAlt + lag * 0.15f;
            IndicatedVSpeedMs = ac.VerticalSpeedMs * 0.15f;
            // ASI errs opposite to the altitude error: descending below the blockage
            // altitude -> reads FAST; climbing above it -> reads SLOW.
            IndicatedAirspeedKmh = Mathf.Max(0f, ac.AirspeedKmh - lag * 0.12f);
        }
        if (PitotBlocked)
        {
            // Blocked pitot with a clear drain: the ASI behaves like an altimeter —
            // indication rises in a climb and falls in a descent regardless of speed.
            float refKmh = Mathf.Sqrt(Mathf.Max(0f, pitotFrozenQ)) * 3.6f;
            IndicatedAirspeedKmh = Mathf.Max(0f, refKmh + (ac.AltitudeM - staticFrozenAlt) * 0.35f);
        }
    }

    void ApplyToPhysics()
    {
        if (ac == null) return;
        ac.powerAvailable01 = EngineHealthNow;

        // Rebuilt from zero every frame so a cleared failure always leaves the flight
        // model in its pristine state (no stale torque can survive a Clear/ResetAll).
        float cd = 0f, roll = 0f, yaw = 0f;

        // Open door: loud and distracting, MILD aerodynamic effect. FAA-H-8083-3C
        // ch.18: "There may be some handling effects, such as roll and/or yaw, but in
        // most instances these can be easily overcome" and a door "seldom if ever
        // compromises the airplane's ability to fly".
        //
        // CALIBRATION NOTE. These numbers were 0.012 / 220 N·m on the first pass, and
        // the automated battery caught that as wrong: this airframe has only Unity's
        // angular drag (1.2 /s) resisting a constant yaw torque, so 220 N·m settled
        // at a PERSISTENT ~4 °/s yaw, which coupled into bank and spiralled the
        // aeroplane into the ground every time. That is the opposite of what the
        // handbook describes, and it would have turned the study's designed
        // "startling but harmless" mission into its most lethal one — silently
        // invalidating the startle-vs-danger dissociation the whole design rests on.
        // Retuned to ~0.7 °/s uncorrected yaw and +13% parasite drag: noticeable,
        // trivially held with rudder, not dangerous.
        if (DoorOpen) { cd += 0.004f; yaw += 30f; }

        // Split flap: pronounced roll toward the wing with the LESS-deflected flap,
        // plus yaw from the extra drag on the deflected side (AFH ch.18). Sized so
        // that at full asymmetry it takes most of the available aileron to hold the
        // wings level, matching the handbook's "almost full aileron may be required".
        //
        // NOT USED BY ANY OF THE TWELVE MISSIONS as it stands — it is implemented and
        // available, but it has not been flown in a mission or calibrated against a
        // participant, so treat these numbers as untested if a future mission adopts it.
        if (Mathf.Abs(FlapAsymmetry) > 0.01f)
        {
            float k = FlapAsymmetry * Mathf.Clamp01(ac.Flaps01 + 0.35f);
            roll += -k * 13000f;   // +asymmetry (right flap down) rolls LEFT
            yaw  +=  k * 1400f;
        }

        ac.extraCd = cd; ac.extraRollTorque = roll; ac.extraYawTorque = yaw;
        // Freeze the flap surface where it was WHEN THE MOTOR DIED, and keep it there.
        // Recomputing this as "wherever the flaps are now" let a failed motor retract.
        bool motorDead = !FlapMotorOk || BusDead;
        if (motorDead && !ac.flapMotorLocked)
        {
            ac.flapMotorLocked = true;
            ac.flapAuthority01 = ac.Flaps01;      // stuck here, both directions
        }
        else if (!motorDead && ac.flapMotorLocked)
        {
            ac.flapMotorLocked = false;
            ac.flapAuthority01 = 1f;
        }
        if (!BrakesOk) ac.braking = false;
    }

    void RaiseCueIfPerceptible()
    {
        if (cueRaised || Armed == FailureKind.None) return;
        bool perceptible;
        switch (Armed)
        {
            // Gradual failures: the cue exists once the symptom is above a threshold a
            // pilot could plausibly notice — NOT at the instant the code armed it.
            case FailureKind.CarbIce:        perceptible = CarbIce01 > 0.18f; break;
            case FailureKind.FuelStarvation: perceptible = EngineHealthNow < 0.5f; break;
            case FailureKind.AlternatorFailure: perceptible = BusVolts < 25.5f; break;
            // Abrupt failures: immediate.
            default: perceptible = true; break;
        }
        if (perceptible)
        {
            cueRaised = true;
            OnCuePerceptible?.Invoke(Armed);
        }
    }

    /// <summary>Seconds since the armed failure was injected (0 if none).</summary>
    public float TimeSinceArm => Armed == FailureKind.None ? 0f : Time.time - armTime;

    /// <summary>Compact systems word for telemetry: one row per subsystem state.</summary>
    public string TelemetryFields()
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return string.Join(",", new[]
        {
            Engine.ToString(),
            EngineHealthNow.ToString("F3", ci),
            RPM.ToString("F0", ci),
            OilPressurePsi.ToString("F1", ci),
            OilTempC.ToString("F1", ci),
            CarbIce01.ToString("F3", ci),
            CarbHeatOn ? "1" : "0",
            FuelLeftL.ToString("F1", ci),
            FuelRightL.ToString("F1", ci),
            FuelFlowLph.ToString("F1", ci),
            Selector.ToString(),
            AlternatorOn ? "1" : "0",
            BusVolts.ToString("F1", ci),
            BatteryAh.ToString("F2", ci),
            LoadShed ? "1" : "0",
            StaticBlocked ? "1" : "0",
            PitotBlocked ? "1" : "0",
            AlternateStaticOpen ? "1" : "0",
            AttitudeOk ? "1" : "0",
            RadioOk ? "1" : "0",
            FlapMotorOk ? "1" : "0",
            FlapAsymmetry.ToString("F2", ci),
            DoorOpen ? "1" : "0",
            BrakesOk ? "1" : "0",
            Armed.ToString()
        });
    }

    public const string TelemetryHeader =
        "engine_state,engine_power_01,rpm,oil_press_psi,oil_temp_c,carb_ice_01,carb_heat," +
        "fuel_left_l,fuel_right_l,fuel_flow_lph,fuel_selector,alternator,bus_volts,battery_ah,load_shed," +
        "static_blocked,pitot_blocked,alt_static_open,attitude_ok,radio_ok,flap_motor_ok,flap_asym," +
        "door_open,brakes_ok,armed_failure";
}
