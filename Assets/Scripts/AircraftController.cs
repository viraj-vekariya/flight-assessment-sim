using UnityEngine;

/// <summary>
/// THE SINGLE WRITER of the aircraft's control inputs. Every input source — keyboard,
/// mouse cockpit interaction, VR cockpit interaction — goes through this class, and
/// nothing else may write CessnaPhysics.pitchInput / rollInput / yawInput / throttle /
/// flaps / trim / braking while a mission is running.
///
/// ═══════════════════════════════════════════════════════════════════════════════
/// CONTROL OWNERSHIP — and the latch bug this design exists to prevent
/// ═══════════════════════════════════════════════════════════════════════════════
/// The override API (SetPitch/SetRoll/...) used to work by LATCHING a `hasX` flag that
/// only `ClearOverrides()` could unset. That was safe when the only caller was the mouse
/// interaction, which cleared and re-set every frame. It is NOT safe once a second
/// source exists: a VR component that calls SetPitch() once — because the participant
/// grabbed the yoke for a moment — would own pitch FOREVER, and the keyboard would go
/// dead with no visible cause.
///
/// The fix is to make ownership EXPIRE rather than latch. Each axis records the frame
/// it was last written. An override is honoured only on the frame it was set (and the
/// one after, to absorb script-execution-order differences); after that the axis falls
/// back to the keyboard automatically. A source that wants continuous control simply
/// keeps writing every frame, which is what a held grab does anyway.
///
/// Consequences that matter:
///   * a source that stops writing releases the axis — no explicit release call needed,
///     and a crashed/disabled source cannot strand the aircraft;
///   * two sources writing the same axis in one frame: last writer wins, deterministically;
///   * ClearOverrides() still exists and still works, and is still called on reset.
///
/// ═══════════════════════════════════════════════════════════════════════════════
/// DESKTOP KEYS
/// ═══════════════════════════════════════════════════════════════════════════════
///   W/S pitch · A/D roll · Q/E rudder · Shift/Ctrl throttle · F flaps · B brakes
///   [ / ] elevator trim (nose down / nose up)
///   X       spoiler — DEBUG ONLY, and locked out while a trial is recording
/// </summary>
[RequireComponent(typeof(CessnaPhysics))]
public class AircraftController : MonoBehaviour
{
    public float throttleRate = 0.5f;    // per second, keyboard
    public float trimRate = 0.25f;       // per second, keyboard — a trim wheel is slow
    public float brakeRate = 4f;         // per second, keyboard brake ramp
    // RUDDER, keyboard. Q/E used to write +-1 the instant they were touched, which made
    // the rudder the ONLY flight control in the aeroplane that was a switch: pitch and roll
    // come through Input.GetAxis and ramp over about a third of a second, throttle and trim
    // are rate-based, and even the BRAKE ramps on brakeRate. Only the rudder slammed to full
    // deflection and back.
    //
    // That was survivable while the rudder only swung the nose. It stopped being survivable
    // once the dihedral term went in, because full rudder now rolls the aeroplane through
    // 64 degrees — so a tap of Q was a step input straight into a large roll, with no way to
    // ask for a small amount of rudder at all. For an experiment that is worse than
    // unpleasant: a participant could not make a proportional rudder correction, so their
    // rudder trace was a square wave no matter how gently they meant to fly.
    //
    // 3 per second matches Unity's default axis sensitivity/gravity, which is what Horizontal
    // and Vertical use — so the rudder now builds and releases at the same rate as the
    // ailerons and elevator, and the three primary axes finally feel like one aeroplane.
    public float yawRate = 3f;           // per second, keyboard rudder ramp (and self-centre)

    CessnaPhysics phys;
    float flapsTarget;
    /// <summary>The flap detent the pilot has SELECTED (0 / 0.5 / 1), regardless of
    /// whether the flap motor can actually get there. Telemetry logs both, so a failed
    /// flap motor shows up as selected != actual.</summary>
    public float FlapsSelected => flapsTarget;
    bool flapKeyDown;
    float spoilerTarget;
    bool spoilerKeyDown;
    float keyBrake;
    float keyYaw;                        // ramped keyboard rudder, see yawRate

    // ── override channel ──────────────────────────────────────────────────────
    // Value + the frame it was written. Honoured for GraceFrames frames, then it lapses.
    const int GraceFrames = 1;
    int fPitch = -99, fRoll = -99, fYaw = -99, fThrottle = -99, fBrake = -99, fTrim = -99;
    float pitchCmd, rollCmd, yawCmd, throttleCmd, brakeCmd, trimCmd;

    bool Live(int frame) => Time.frameCount - frame <= GraceFrames;

    public void SetPitch(float v)    { pitchCmd    = Mathf.Clamp(v, -1f, 1f); fPitch = Time.frameCount; }
    public void SetRoll(float v)     { rollCmd     = Mathf.Clamp(v, -1f, 1f); fRoll = Time.frameCount; }
    public void SetYaw(float v)      { yawCmd      = Mathf.Clamp(v, -1f, 1f); fYaw = Time.frameCount; }
    public void SetThrottle(float v) { throttleCmd = Mathf.Clamp01(v);        fThrottle = Time.frameCount; }
    /// <summary>Analog brake pressure, 0..1.</summary>
    public void SetBrake(float v)    { brakeCmd    = Mathf.Clamp01(v);        fBrake = Time.frameCount; }
    /// <summary>Absolute trim position, +1 nose up .. -1 nose down.</summary>
    public void SetTrim(float v)     { trimCmd     = Mathf.Clamp(v, -1f, 1f); fTrim = Time.frameCount; }

    /// <summary>Drop every override immediately. Kept for explicit release and reset;
    /// normal operation no longer depends on it being called.</summary>
    public void ClearOverrides()
    { fPitch = fRoll = fYaw = fThrottle = fBrake = fTrim = -99; }

    // ---- which source currently owns each axis (for telemetry / debugging) ----
    public bool PitchOverridden    => Live(fPitch);
    public bool RollOverridden     => Live(fRoll);
    public bool YawOverridden      => Live(fYaw);
    public bool ThrottleOverridden => Live(fThrottle);
    public bool BrakeOverridden    => Live(fBrake);
    public bool TrimOverridden     => Live(fTrim);
    /// <summary>True when any axis is currently being driven by something other than
    /// the keyboard — i.e. the cockpit is being flown by hand.</summary>
    public bool AnyOverride => PitchOverridden || RollOverridden || YawOverridden ||
                               ThrottleOverridden || BrakeOverridden || TrimOverridden;

    // ── flap detents ──────────────────────────────────────────────────────────
    /// <summary>The three detents the simulated flap system supports.</summary>
    public static readonly float[] FlapDetents = { 0f, 0.5f, 1f };
    public static readonly string[] FlapLabels = { "UP", "10°", "FULL" };

    public void StepFlaps()
    {
        int i = NearestDetentIndex(flapsTarget);
        SetFlapDetent((i + 1) % FlapDetents.Length);
    }

    /// <summary>Select a flap detent by index. The one entry point for flaps, so the
    /// keyboard, the mouse lever and the VR lever cannot drift apart.</summary>
    public void SetFlapDetent(int index)
    {
        index = Mathf.Clamp(index, 0, FlapDetents.Length - 1);
        if (Mathf.Approximately(flapsTarget, FlapDetents[index])) return;
        flapsTarget = FlapDetents[index];
        CockpitEvents.RaiseFlapSelected(index, FlapLabels[index]);
    }

    public int FlapDetentIndex => NearestDetentIndex(flapsTarget);

    static int NearestDetentIndex(float v)
    {
        int best = 0; float bd = 99f;
        for (int i = 0; i < FlapDetents.Length; i++)
        { float d = Mathf.Abs(FlapDetents[i] - v); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    /// <summary>Set the spoiler to a detent (0 UP / 1 HALF / 2 FULL) from the cockpit
    /// lever. Honours the same recorded-trial lockout as the X key: during a trial the
    /// spoiler is held retracted, because no mission specifies spoiler use and letting a
    /// participant change drag and lift mid-trial would confound the workload contrast.</summary>
    public void SetSpoilerDetent(int detent)
    {
        bool trialRecording = GameManager.Instance != null
                           && GameManager.Instance.ScenarioRunner != null
                           && GameManager.Instance.ScenarioRunner.Active;
        if (trialRecording) { spoilerTarget = 0f; return; }
        spoilerTarget = Mathf.Clamp01(detent * 0.5f);
    }

    /// <summary>Which spoiler detent is selected (0 UP / 1 HALF / 2 FULL).</summary>
    public int SpoilerDetentIndex =>
        spoilerTarget > 0.75f ? 2 : spoilerTarget > 0.25f ? 1 : 0;

    public void StepSpoiler()
    {
        spoilerTarget = Mathf.Approximately(spoilerTarget, 0f) ? 0.5f
                      : Mathf.Approximately(spoilerTarget, 0.5f) ? 1f : 0f;
    }

    /// <summary>Return every pilot-selectable control to its clean start state.
    ///
    /// THIS MATTERS FOR THE EXPERIMENT. flapsTarget, trim and the brake are fields that
    /// used to survive a trial change, so a participant who selected flap or wound in
    /// trim in one mission began the NEXT mission with it still set — silently, in a
    /// mission whose specification says otherwise. That is a between-trial carry-over of
    /// handling, drag and trim as a function of what the participant happened to do
    /// earlier, which a within-subject design cannot absorb. ScenarioEngine and
    /// BaselineRunner call this at the start of every trial.</summary>
    public void ResetConfiguration(float flaps01 = 0f)
    {
        flapsTarget = Mathf.Clamp01(flaps01);
        spoilerTarget = 0f;
        flapKeyDown = spoilerKeyDown = false;
        keyBrake = 0f;
        pitchCmd = rollCmd = yawCmd = brakeCmd = trimCmd = 0f;
        throttleCmd = 0f;
        ClearOverrides();
        if (phys == null) phys = GetComponent<CessnaPhysics>();
        if (phys != null)
        {
            phys.flaps = flapsTarget;
            phys.spoiler = 0f;
            phys.braking = false;
            phys.brakeInput01 = 0f;
            phys.trim = 0f;
        }
    }

    /// <summary>One step of the keyboard rudder ramp: move toward <paramref name="target"/>
    /// at <paramref name="rate"/> per second, which also self-centres when the key is let go
    /// because the target falls to zero.
    ///
    /// Pure and public so the control battery can measure the ramp directly — a batch test
    /// cannot press a key, and a feel change nobody can test is a feel change that silently
    /// regresses.</summary>
    public static float RampRudder(float current, float target, float rate, float dt)
        => Mathf.MoveTowards(current, target, Mathf.Max(0f, rate) * Mathf.Max(0f, dt));

    void Awake() => phys = GetComponent<CessnaPhysics>();

    void Update()
    {
        bool flying = GameManager.Instance != null && GameManager.Instance.State == GameState.Flying;
        if (!flying)
        {
            phys.pitchInput = phys.rollInput = phys.yawInput = 0f;
            phys.braking = false; phys.brakeInput01 = 0f;
            keyYaw = 0f;             // no rudder may survive into the next trial
            spoilerTarget = 0f;      // start each flight with spoilers retracted
            return;
        }

        // ── primary flight controls ───────────────────────────────────────────
        phys.pitchInput = PitchOverridden ? pitchCmd : Input.GetAxis("Vertical");
        phys.rollInput  = RollOverridden  ? rollCmd  : Input.GetAxis("Horizontal");

        // RUDDER. Held keys RAMP toward full deflection and fall back to centre when
        // released, at the same rate the aileron and elevator axes use — see yawRate. A tap
        // of Q is now a touch of left rudder instead of all of it.
        float yawKeys = 0f;
        if (Input.GetKey(KeyCode.Q)) yawKeys -= 1f;
        if (Input.GetKey(KeyCode.E)) yawKeys += 1f;
        keyYaw = RampRudder(keyYaw, Mathf.Clamp(yawKeys, -1f, 1f), yawRate, Time.deltaTime);
        phys.yawInput = YawOverridden ? yawCmd : keyYaw;

        // ── throttle ──────────────────────────────────────────────────────────
        // A physical lever is POSITION-based, so an override sets the value directly.
        // The keyboard has no position, so it stays rate-based.
        if (ThrottleOverridden) phys.throttle = throttleCmd;
        else
        {
            float t = phys.throttle;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) t += throttleRate * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) t -= throttleRate * Time.deltaTime;
            phys.throttle = Mathf.Clamp01(t);
        }

        // ── elevator trim ─────────────────────────────────────────────────────
        // A trim wheel is a POSITION, so an override sets it absolutely. The keyboard
        // winds it, slowly, like the real wheel.
        if (TrimOverridden) SetTrimValue(trimCmd);
        else
        {
            float d = 0f;
            if (Input.GetKey(KeyCode.RightBracket)) d += 1f;   // ]  nose up
            if (Input.GetKey(KeyCode.LeftBracket))  d -= 1f;   // [  nose down
            if (d != 0f) SetTrimValue(phys.trim + d * trimRate * Time.deltaTime);
        }

        // ── flaps ─────────────────────────────────────────────────────────────
        if (Input.GetKey(KeyCode.F)) { if (!flapKeyDown) { flapKeyDown = true; StepFlaps(); } }
        else flapKeyDown = false;
        // Flaps move smoothly, but never past what the flap MOTOR can deliver.
        // flapAuthority01 is 1 when healthy and is pulled down by AircraftSystems on a
        // flap-motor / low-bus-voltage failure, so a stuck flap really is stuck — and the
        // SELECTED detent still moves, so telemetry shows selected != actual.
        // A dead motor STICKS the flaps where they are — it does not merely cap them.
        float flapCmd = phys.flapMotorLocked ? phys.flapAuthority01
                                             : Mathf.Min(flapsTarget, phys.flapAuthority01);
        phys.flaps = Mathf.MoveTowards(phys.flaps, flapCmd, Time.deltaTime);

        // ── spoilers: DEBUG ONLY ──────────────────────────────────────────────
        // Not a C172 control and deliberately not exposed in the cockpit. Kept on the
        // X key so the simulation capability can still be exercised in testing.
        // SPOILERS: a debug aid, and LOCKED OUT during a recorded trial.
        //
        // A 172 has no spoilers, so there is deliberately no cockpit spoiler control.
        // But leaving the debug key live would mean a participant who brushed X mid-
        // trial silently changed the aircraft's drag — and the trial would still look
        // perfectly normal in the data. The column stays in the telemetry so its value
        // is provably zero rather than merely assumed to be.
        bool trialRecording = GameManager.Instance != null
                           && GameManager.Instance.ScenarioRunner != null
                           && GameManager.Instance.ScenarioRunner.Active;
        if (!trialRecording && Input.GetKey(KeyCode.X))
        { if (!spoilerKeyDown) { spoilerKeyDown = true; StepSpoiler(); } }
        else spoilerKeyDown = false;
        if (trialRecording) spoilerTarget = 0f;
        phys.spoiler = Mathf.MoveTowards(phys.spoiler, spoilerTarget, Time.deltaTime * 2f);

        // ── wheel brakes ──────────────────────────────────────────────────────
        // RESTORED. These were hard-disabled (`phys.braking = false`), which meant the
        // aeroplane could not be stopped, taxi speed could not be controlled, and the
        // BrakeFailure abnormality was unobservable because brakes never worked anyway.
        keyBrake = Mathf.MoveTowards(keyBrake, Input.GetKey(KeyCode.B) ? 1f : 0f, brakeRate * Time.deltaTime);
        float brake = BrakeOverridden ? brakeCmd : keyBrake;
        // Brakes are wheel brakes: they only exist on the ground. Guarding here rather
        // than trusting every caller means an airborne brake input can never bleed speed.
        if (!phys.Grounded) brake = 0f;
        phys.brakeInput01 = brake;
        phys.braking = brake > 0.02f;
        // A failed brake system still shows the pilot's INPUT in telemetry but produces
        // no deceleration — AircraftSystems clears phys.braking after this.
    }

    void SetTrimValue(float v)
    {
        float nv = Mathf.Clamp(v, -1f, 1f);
        if (Mathf.Abs(nv - phys.trim) < 0.0005f) return;
        phys.trim = nv;
        CockpitEvents.RaiseTrimChanged(nv);
    }
}
