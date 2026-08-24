// HardwareInput — physical yoke, throttle, rudder pedals and toe brakes.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHY THIS EXISTS
// ═══════════════════════════════════════════════════════════════════════════════
// The laboratory build is meant to be flown with real controls. Before this file the
// only paths into the flight model were the keyboard, the mouse-driven cockpit and the
// VR hand grab — so a yoke plugged into the lab PC would have moved nothing, and the
// only axes Unity's default input map defines are a gamepad's two stick axes wired to
// pitch and roll. There was no rudder axis, no throttle axis and no toe brakes at all.
//
// That matters beyond convenience. Control response is an uncontrolled between-subject
// variable if some participants fly with a keyboard's on/off inputs and others with a
// proportional yoke: a keyboard cannot hold 30% aileron, so keyboard tracking error and
// control-activity covariates are not comparable with hardware ones. The modality is
// recorded per session for exactly that reason, and must not be pooled in analysis.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE TWO RULES THAT KEEP IT SAFE
// ═══════════════════════════════════════════════════════════════════════════════
// 1. IT STANDS DOWN WHEN IT IS NOT WANTED. No joystick connected, the axis not
//    explicitly enabled, or a headless harness currently driving (SimDriver) => this
//    component writes NOTHING. An unplugged or uncalibrated axis reads a constant
//    non-zero value on many devices, and an axis that wrote unconditionally would fly
//    the aeroplane into the ground during a battery run with the log showing a
//    perfectly innocent in_pitch.
//
// 2. A PRESENT AXIS WRITES EVERY FRAME. This is the opposite of the rule for the
//    cockpit's virtual levers (PhysicalControl writes only while grabbed, and otherwise
//    mirrors the aircraft) and the difference is not arbitrary. A virtual lever is a
//    picture of the throttle; a real lever IS the throttle. Its physical position is
//    the commanded value whether or not a hand is on it, so it must write continuously
//    or the pilot could never command idle. Enabling a hardware axis therefore takes
//    that axis away from the keyboard, deliberately and visibly.
//
// This is only possible at all because the unconditional per-frame
// `AircraftController.ClearOverrides()` in CockpitInteraction was removed: it ran at
// execution order -50 and wiped any override written by a later script, so a hardware
// layer would have been silently ineffective in the GLB-fallback path.
//
// ═══════════════════════════════════════════════════════════════════════════════
// CALIBRATION
// ═══════════════════════════════════════════════════════════════════════════════
// Per axis: enable, source axis name, invert, dead zone, expo, and for sliders a
// min/max learned from the observed travel. Stored in PlayerPrefs so a lab PC is set up
// once, and written into session.json so an analysis can tell what the participant
// actually flew with. Nothing here is guessed at runtime: an axis that has not been
// calibrated stays disabled rather than being assumed to be centred.

using System.Text;
using UnityEngine;

public enum HwAxisRole { Pitch, Roll, Yaw, Throttle, BrakeLeft, BrakeRight }

[System.Serializable]
public class HwAxis
{
    public HwAxisRole role;
    /// <summary>Name of the axis in Unity's Input Manager, e.g. "HwPitch".</summary>
    public string axisName = "";
    public bool enabled;
    public bool invert;
    /// <summary>Fraction of travel around centre that is treated as zero. Real hardware
    /// does not return exactly to centre; without this the aeroplane creeps.</summary>
    [Range(0f, 0.3f)] public float deadZone = 0.04f;
    /// <summary>Response curve. 1 = linear. >1 = finer control near centre, which is
    /// what a yoke wants and a throttle does not.</summary>
    [Range(1f, 3f)] public float expo = 1f;
    /// <summary>True for a lever with no centre (throttle, toe brake): the raw -1..1
    /// value is rescaled to 0..1 rather than treated as a signed deflection.</summary>
    public bool slider;

    public float Read()
    {
        if (!enabled || string.IsNullOrEmpty(axisName)) return 0f;
        float v;
        try { v = Input.GetAxisRaw(axisName); }
        catch { return 0f; }              // axis not defined in the Input Manager
        if (invert) v = -v;

        if (slider) return Mathf.Clamp01((v + 1f) * 0.5f);

        float a = Mathf.Abs(v);
        if (a <= deadZone) return 0f;
        // Rescale so the value just outside the dead zone is 0, not a step to deadZone.
        a = (a - deadZone) / (1f - deadZone);
        if (expo > 1.001f) a = Mathf.Pow(a, expo);
        return Mathf.Sign(v) * Mathf.Clamp01(a);
    }
}

[DefaultExecutionOrder(-55)]   // after the cockpit interactors, before AircraftController (0)
public class HardwareInput : MonoBehaviour
{
    public static HardwareInput Instance { get; private set; }

    /// <summary>True when at least one axis is enabled AND a device is connected.
    /// This is what `input_modality` reports, so it must not be optimistic.</summary>
    public static bool Active { get; private set; }
    /// <summary>Names of the connected devices, for the session record.</summary>
    public static string Devices { get; private set; } = "";

    public HwAxis[] axes =
    {
        new HwAxis { role = HwAxisRole.Pitch,      axisName = "HwPitch",      expo = 1.4f },
        new HwAxis { role = HwAxisRole.Roll,       axisName = "HwRoll",       expo = 1.4f },
        new HwAxis { role = HwAxisRole.Yaw,        axisName = "HwYaw",        expo = 1.2f, deadZone = 0.08f },
        new HwAxis { role = HwAxisRole.Throttle,   axisName = "HwThrottle",   slider = true },
        new HwAxis { role = HwAxisRole.BrakeLeft,  axisName = "HwBrakeLeft",  slider = true },
        new HwAxis { role = HwAxisRole.BrakeRight, axisName = "HwBrakeRight", slider = true },
    };

    AircraftController ctl;
    CessnaPhysics phys;
    float lastDeviceScan;

    const string PrefPrefix = "hw_axis_";

    void Awake()
    {
        Instance = this;
        Load();
        ScanDevices();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Bind(CessnaPhysics aircraft)
    {
        phys = aircraft;
        ctl = aircraft != null ? aircraft.GetComponent<AircraftController>() : null;
    }

    static void ScanDevices()
    {
        var names = Input.GetJoystickNames();
        var sb = new StringBuilder();
        int live = 0;
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;          // an empty slot is a REMOVED device
            if (live++ > 0) sb.Append("; ");
            sb.Append(n);
        }
        Devices = live > 0 ? sb.ToString() : "none";
    }

    void Update()
    {
        // Re-scan occasionally so plugging a yoke in mid-session is noticed, but not
        // every frame — GetJoystickNames allocates.
        if (Time.unscaledTime - lastDeviceScan > 3f) { lastDeviceScan = Time.unscaledTime; ScanDevices(); }

        bool anyEnabled = false;
        foreach (var a in axes) if (a.enabled) { anyEnabled = true; break; }
        Active = anyEnabled && Devices != "none";

        if (!Active) return;
        // A headless harness owning the controls must not be fought. This is the same
        // one-driver rule the batteries obey.
        if (!SimDriver.Free) return;
        if (ctl == null || phys == null)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Aircraft == null) return;
            Bind(gm.Aircraft);
            if (ctl == null) return;
        }
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Flying) return;

        // Toe brakes: either pedal commands braking, and the larger wins. Differential
        // braking is not modelled by the flight model (one brake value), so the pair is
        // reduced to a magnitude rather than pretending to steer with it.
        float bl = 0f, br = 0f;
        bool haveBrake = false;

        foreach (var a in axes)
        {
            if (!a.enabled) continue;
            float v = a.Read();
            switch (a.role)
            {
                case HwAxisRole.Pitch:      ctl.SetPitch(v); break;
                case HwAxisRole.Roll:       ctl.SetRoll(v); break;
                case HwAxisRole.Yaw:        ctl.SetYaw(v); break;
                case HwAxisRole.Throttle:   ctl.SetThrottle(v); break;
                case HwAxisRole.BrakeLeft:  bl = v; haveBrake = true; break;
                case HwAxisRole.BrakeRight: br = v; haveBrake = true; break;
            }
        }
        if (haveBrake) ctl.SetBrake(Mathf.Max(bl, br));
    }

    // ── persistence ───────────────────────────────────────────────────────────

    public void Save()
    {
        foreach (var a in axes)
        {
            string k = PrefPrefix + a.role;
            PlayerPrefs.SetInt(k + "_en", a.enabled ? 1 : 0);
            PlayerPrefs.SetString(k + "_name", a.axisName ?? "");
            PlayerPrefs.SetInt(k + "_inv", a.invert ? 1 : 0);
            PlayerPrefs.SetFloat(k + "_dz", a.deadZone);
            PlayerPrefs.SetFloat(k + "_ex", a.expo);
            PlayerPrefs.SetInt(k + "_sl", a.slider ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    public void Load()
    {
        foreach (var a in axes)
        {
            string k = PrefPrefix + a.role;
            if (!PlayerPrefs.HasKey(k + "_en")) continue;   // never calibrated => stays off
            a.enabled = PlayerPrefs.GetInt(k + "_en") == 1;
            a.axisName = PlayerPrefs.GetString(k + "_name", a.axisName);
            a.invert = PlayerPrefs.GetInt(k + "_inv") == 1;
            a.deadZone = PlayerPrefs.GetFloat(k + "_dz", a.deadZone);
            a.expo = PlayerPrefs.GetFloat(k + "_ex", a.expo);
            a.slider = PlayerPrefs.GetInt(k + "_sl") == 1;
        }
    }

    /// <summary>One line per enabled axis, for session.json and the control-check bench.
    /// Reports the DEVICE and the CALIBRATION, because "flown with hardware" is not a
    /// sufficient record — two yokes with different dead zones are two different
    /// interfaces.</summary>
    public static string StatusLine()
    {
        var inst = Instance;
        if (inst == null) return "hardware input: not initialised";
        if (Devices == "none") return "hardware input: no device connected";
        var sb = new StringBuilder("devices [" + Devices + "]  axes:");
        int n = 0;
        foreach (var a in inst.axes)
        {
            if (!a.enabled) continue;
            n++;
            sb.Append(' ').Append(a.role).Append('=').Append(a.axisName)
              .Append(a.invert ? "(inv)" : "")
              .Append(a.slider ? "(slider)" : "(dz " + a.deadZone.ToString("F2") + ", expo " + a.expo.ToString("F1") + ")");
        }
        if (n == 0) sb.Append(" none enabled");
        return sb.ToString();
    }

    /// <summary>Live values of every axis, for the calibration bench.</summary>
    public static string LiveLine()
    {
        var inst = Instance;
        if (inst == null) return "";
        var sb = new StringBuilder();
        foreach (var a in inst.axes)
            sb.Append(a.role).Append(' ').Append(a.enabled ? a.Read().ToString("F2") : "  -  ").Append("   ");
        return sb.ToString();
    }
}
