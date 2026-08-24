// VRRuntime — the single source of truth for "is a headset actually driving this?".
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHY THIS EXISTS, AND THE MISTAKE IT IS BUILT TO AVOID
// ═══════════════════════════════════════════════════════════════════════════════
// The previous VR attempt shipped the OpenXR package and a set of VR scripts but never
// configured an XR loader. The result: the scripts compiled, looked complete, and never
// ran. Nothing in the code said so, so the project appeared VR-capable when its VR
// capability was zero.
//
// This class makes that state VISIBLE and CHECKABLE. It reports, separately:
//
//     XrModulePresent  — the engine's XR module is compiled in
//     LoaderActive     — an XR loader actually initialised (this is what was missing)
//     HmdPresent       — a head-mounted display is connected and tracking
//     Active           — all of the above AND VR input is the chosen modality
//
// `Active` is what the rest of the codebase asks. Everything else exists so a failure
// can be diagnosed instead of guessed at.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHY CORE XR INPUT RATHER THAN THE XR INTERACTION TOOLKIT
// ═══════════════════════════════════════════════════════════════════════════════
// XRI provides grab interaction, interactor/interactable state machines, socket and
// ray interactors, and a large amount of behaviour this project does not want. The
// cockpit already has its own interaction model (PhysicalControl), deliberately shaped
// around EXPERIMENTAL requirements — position-based, spec-driven, identical between
// mouse and VR — and XRI's model would either have to be bent into that shape or would
// impose its own.
//
// What is actually needed from the XR stack is small and stable: controller POSE,
// a GRIP value, a couple of BUTTONS, and haptics. `UnityEngine.XR.InputDevices` gives
// exactly that, is part of the engine rather than a package that churns, and keeps the
// dependency footprint to the XR module plus a loader.
//
// Decision: core XR input. Revisit only if hand-tracking meshes or complex grab
// affordances become a research requirement.
//
// ═══════════════════════════════════════════════════════════════════════════════
// PLATFORM REALITY
// ═══════════════════════════════════════════════════════════════════════════════
// OpenXR is supported on Windows and Android (Quest), NOT on macOS. This project is
// developed on a Mac, so on the development machine `LoaderActive` will be false and
// the sim runs desktop — correctly, and by design. VR verification happens on the
// Windows machine that drives the Quest over Link. See VR_INTERACTION.md.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public enum InputModality { Desktop, VR }

public class VRRuntime : MonoBehaviour
{
    public static VRRuntime Instance { get; private set; }

    /// <summary>True only when a headset is present AND VR is the selected modality.
    /// Everything that must behave differently in VR asks this.</summary>
    public static bool Active { get; private set; }

    /// <summary>Recorded into session metadata. VR and desktop sessions must never be
    /// pooled in one analysis — the interface is a between-subject factor, not noise.</summary>
    public static InputModality Modality => Active ? InputModality.VR : InputModality.Desktop;

    // ---- diagnosis ----
    public static bool XrModulePresent { get; private set; }
    public static bool LoaderActive { get; private set; }
    public static bool HmdPresent { get; private set; }
    public static string StatusLine { get; private set; } = "VR: not initialised";

    // ---- controller state, refreshed each frame ----
    public static bool LeftValid, RightValid;
    public static Vector3 LeftPos, RightPos;
    public static Quaternion LeftRot, RightRot;
    public static float LeftGrip, RightGrip;
    public static float LeftTrigger, RightTrigger;
    public static bool LeftPrimary, RightPrimary;       // A / X
    public static bool LeftSecondary, RightSecondary;   // B / Y

    static bool prevRightPrimary, prevLeftPrimary;
    /// <summary>Rising edge of either primary button — the acknowledge action.</summary>
    public static bool AckPressed { get; private set; }

    InputDevice hmd, left, right;
    readonly List<InputDevice> scratch = new List<InputDevice>();
    float nextScan;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        XrModulePresent = true;   // this file compiles only when UnityEngine.XR exists
        Scan();
    }

    void Update()
    {
        // Rescanning is cheap but not free; twice a second is plenty to notice a headset
        // being put on or a controller waking up.
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.5f; Scan(); }
        if (Active) PollControllers(); else ClearControllers();
    }

    void Scan()
    {
        LoaderActive = XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName);

        hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        HmdPresent = hmd.isValid;
        // A headset that is present but not being worn should not hijack the session
        // mid-experiment, so presence AND tracking are both required.
        bool userPresent = true;
        if (hmd.isValid && hmd.TryGetFeatureValue(CommonUsages.userPresence, out bool up)) userPresent = up;

        bool wasActive = Active;
        Active = LoaderActive && HmdPresent && userPresent;

        if (Active != wasActive)
            Debug.Log("[VR] modality -> " + (Active ? "VR" : "DESKTOP") + "   " + Describe());
        StatusLine = Describe();
    }

    string Describe()
    {
        if (!XrModulePresent) return "VR: engine XR module absent";
        if (!LoaderActive)
            return "VR: no XR loader active (desktop). " +
                   "Expected on macOS — OpenXR is Windows/Android only. On Windows, check " +
                   "Project Settings > XR Plug-in Management > OpenXR is ticked.";
        if (!HmdPresent) return "VR: loader active (" + XRSettings.loadedDeviceName + ") but no HMD detected";
        return "VR: active — " + XRSettings.loadedDeviceName +
               (right.isValid ? ", right controller" : ", NO right controller") +
               (left.isValid ? ", left controller" : ", NO left controller");
    }

    void PollControllers()
    {
        RightValid = right.isValid; LeftValid = left.isValid;
        Read(right, out RightPos, out RightRot, out RightGrip, out RightTrigger, out RightPrimary, out RightSecondary);
        Read(left, out LeftPos, out LeftRot, out LeftGrip, out LeftTrigger, out LeftPrimary, out LeftSecondary);

        AckPressed = (RightPrimary && !prevRightPrimary) || (LeftPrimary && !prevLeftPrimary);
        prevRightPrimary = RightPrimary; prevLeftPrimary = LeftPrimary;
    }

    static void Read(InputDevice d, out Vector3 pos, out Quaternion rot,
                     out float grip, out float trigger, out bool primary, out bool secondary)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        grip = 0f; trigger = 0f; primary = false; secondary = false;
        if (!d.isValid) return;
        d.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
        d.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
        d.TryGetFeatureValue(CommonUsages.grip, out grip);
        d.TryGetFeatureValue(CommonUsages.trigger, out trigger);
        d.TryGetFeatureValue(CommonUsages.primaryButton, out primary);
        d.TryGetFeatureValue(CommonUsages.secondaryButton, out secondary);
    }

    void ClearControllers()
    {
        RightValid = LeftValid = false;
        RightGrip = LeftGrip = RightTrigger = LeftTrigger = 0f;
        RightPrimary = LeftPrimary = RightSecondary = LeftSecondary = false;
        AckPressed = false;
    }

    /// <summary>Short haptic tick — a detent, a grab, a switch. Silently does nothing on
    /// hardware without haptics, so no caller has to check.</summary>
    public static void Haptic(bool rightHand, float amplitude = 0.4f, float duration = 0.04f)
    {
        if (!Active) return;
        var d = InputDevices.GetDeviceAtXRNode(rightHand ? XRNode.RightHand : XRNode.LeftHand);
        if (!d.isValid) return;
        if (d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            d.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
    }
}
