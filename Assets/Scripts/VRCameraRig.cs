// VRCameraRig — puts the headset at the pilot's eye point without letting VR change
// where the pilot's eye IS.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE RULE THIS ENFORCES
// ═══════════════════════════════════════════════════════════════════════════════
// The cockpit camera is already positioned at the model's measured pilot eye point by
// RealCockpit. VR must not move it, re-seat it, or add its own offset — otherwise the
// VR pilot and the desktop pilot are looking from different places, the panel subtends
// a different angle, and the two modalities stop being comparable.
//
// So: a TRACKING SPACE transform is created AT the existing cockpit camera pose, and
// the headset moves the camera WITHIN that space. Seated origin. The pilot's head is
// where the pilot's head is, and leaning forward leans forward.
//
// ═══════════════════════════════════════════════════════════════════════════════
// MOTION SAFETY
// ═══════════════════════════════════════════════════════════════════════════════
// The camera is parented to the aircraft, so the aeroplane's attitude carries the view —
// which is correct and unavoidable in a cockpit. What is NOT added:
//
//   * no head bob, no camera shake, no g-force lean, no artificial roll
//   * no vignette or comfort tunnelling (it would occlude the instruments)
//   * no snap-turn or artificial locomotion — the pilot is seated and strapped in
//
// Those effects make simulators sick, and nausea alters EEG and heart rate. The only
// motion is the aeroplane's own, which is the thing being simulated.
//
// Recentre is available (both grip + both trigger) because a seated origin drifts and a
// participant should not have to take the headset off mid-session.

using UnityEngine;

public class VRCameraRig : MonoBehaviour
{
    /// <summary>The transform controller and headset poses are expressed relative to.
    /// Null when VR is not running.</summary>
    public static Transform TrackingSpace { get; private set; }

    public static VRCameraRig Instance { get; private set; }

    Camera cockpitCam;
    Transform space;
    Vector3 camBaseLocalPos;
    Quaternion camBaseLocalRot;
    Vector3 recentreOffset;
    bool built;
    bool prevRecentreHeld;

    void Awake() { Instance = this; }

    void Update()
    {
        if (!VRRuntime.Active) { Teardown(); return; }
        if (!built) Build();
        if (!built) return;

        // Seated recentre: hold both grips and both triggers for half a second.
        bool held = VRRuntime.LeftGrip > 0.7f && VRRuntime.RightGrip > 0.7f &&
                    VRRuntime.LeftTrigger > 0.7f && VRRuntime.RightTrigger > 0.7f;
        if (held && !prevRecentreHeld) Recentre();
        prevRecentreHeld = held;
    }

    void Build()
    {
        // Find the cockpit camera as RealCockpit left it.
        var gm = GameManager.Instance;
        if (gm == null || gm.Aircraft == null) return;
        cockpitCam = System.Array.Find(gm.Aircraft.GetComponentsInChildren<Camera>(true),
                                       c => c.name == "CockpitCamera");
        if (cockpitCam == null) return;

        camBaseLocalPos = cockpitCam.transform.localPosition;
        camBaseLocalRot = cockpitCam.transform.localRotation;

        // Tracking space sits exactly where the eye point is, parented to whatever the
        // camera was parented to. The camera then becomes a child of it, so the headset
        // pose is applied AROUND the pilot's eye rather than replacing it.
        space = new GameObject("VRTrackingSpace").transform;
        space.SetParent(cockpitCam.transform.parent, false);
        space.localPosition = camBaseLocalPos;
        space.localRotation = camBaseLocalRot;

        cockpitCam.transform.SetParent(space, false);
        cockpitCam.transform.localPosition = Vector3.zero;
        cockpitCam.transform.localRotation = Quaternion.identity;

        // Let the XR runtime drive the camera. TrackedPoseDriver lives in the input
        // system package; using the XR node directly keeps the dependency at the module.
        if (cockpitCam.GetComponent<VRHeadPose>() == null)
            cockpitCam.gameObject.AddComponent<VRHeadPose>();

        TrackingSpace = space;
        built = true;
        Debug.Log("[VR] camera rig built at the pilot eye point " + camBaseLocalPos.ToString("F3"));
    }

    void Teardown()
    {
        if (!built) return;
        // Put the camera back exactly where it was, so leaving VR mid-session restores
        // the desktop view rather than leaving it inside the tracking space.
        if (cockpitCam != null && space != null)
        {
            cockpitCam.transform.SetParent(space.parent, false);
            cockpitCam.transform.localPosition = camBaseLocalPos;
            cockpitCam.transform.localRotation = camBaseLocalRot;
            var hp = cockpitCam.GetComponent<VRHeadPose>();
            if (hp != null) Destroy(hp);
        }
        if (space != null) Destroy(space.gameObject);
        TrackingSpace = null;
        built = false;
        Debug.Log("[VR] camera rig torn down — desktop view restored");
    }

    /// <summary>Zero the seated origin on the pilot's current head position.</summary>
    public void Recentre()
    {
        if (!built) return;
        var hp = cockpitCam != null ? cockpitCam.GetComponent<VRHeadPose>() : null;
        if (hp != null) hp.Recentre();
        VRRuntime.Haptic(true, 0.5f, 0.08f);
        VRRuntime.Haptic(false, 0.5f, 0.08f);
        Debug.Log("[VR] recentred");
    }
}

/// <summary>Applies the headset pose to the camera inside the tracking space, with a
/// recentring offset. Deliberately minimal — no prediction, no smoothing, no comfort
/// effects: head tracking must be 1:1 or it induces sickness.</summary>
public class VRHeadPose : MonoBehaviour
{
    Vector3 originOffset;

    void LateUpdate()
    {
        if (!VRRuntime.Active) return;
        var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
        if (!head.isValid) return;
        if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 p))
            transform.localPosition = p - originOffset;
        if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion r))
            transform.localRotation = r;
    }

    public void Recentre()
    {
        var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
        if (head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 p))
            originOffset = p;
    }
}
