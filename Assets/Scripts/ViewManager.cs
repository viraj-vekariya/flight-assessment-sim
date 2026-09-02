using UnityEngine;

/// <summary>
/// Switches between three camera views with <b>C</b>:
///   Cockpit — the Phase-1 first-person camera (built by CockpitBuilder).
///   Chase   — third-person behind & above, smoothly following; the aircraft
///             visibly pitches / rolls / yaws against a world-stable horizon.
///   Orbit   — free camera: hold right-mouse to orbit, scroll to zoom.
/// The cockpit camera culls the plane's exterior; the external camera renders
/// everything, so the aircraft is visible from outside.
/// </summary>
public class ViewManager : MonoBehaviour
{
    public enum View { Cockpit, Chase, Orbit }

    [Header("Chase")]
    public float chaseDistance = 14f;
    public float chaseHeight = 4.5f;
    public float chasePosDamp = 0.10f;     // SmoothDamp time (snappy so it stays behind the tail)
    public float chaseRotLerp = 12f;

    [Header("Orbit")]
    public float orbitSensitivity = 3f;
    public float orbitMinDist = 6f;
    public float orbitMaxDist = 60f;

    View view = View.Cockpit;
    Transform target;
    Camera cockpitCam;
    CockpitCamera cockpitLook;
    AudioListener cockpitAudio;
    Camera extCam;
    AudioListener extAudio;

    Vector3 followVel;
    float orbitYaw, orbitPitch = 18f, orbitDist = 18f;

    public View Current => view;
    public Camera ActiveCamera => view == View.Cockpit ? cockpitCam : extCam;

    public void Init(Transform aircraft, Camera cockpit)
    {
        target = aircraft;
        cockpitCam = cockpit;
        cockpitLook = cockpit != null ? cockpit.GetComponent<CockpitCamera>() : null;
        cockpitAudio = cockpit != null ? cockpit.GetComponent<AudioListener>() : null;

        var go = new GameObject("ExternalCamera");
        extCam = go.AddComponent<Camera>();
        extCam.fieldOfView = 60f;
        extCam.nearClipPlane = 0.1f;
        extCam.farClipPlane = 12000f;
        extCam.clearFlags = CameraClearFlags.Skybox;
        // render everything (incl. the exterior plane) EXCEPT the cockpit interior,
        // so the panel/yoke don't float inside the fuselage in chase/orbit views.
        // Not the cockpit interior, and not the displays' symbology layers: from
        // outside, those glyphs would otherwise hang in the air beside the aeroplane.
        extCam.cullingMask = ~((1 << CockpitBuilder.CockpitLayer) | CockpitBuilder.DisplayOnlyMask);
        extAudio = go.AddComponent<AudioListener>();

        Apply();
    }

    void Update()
    {
        if (target == null) return;
        if (Input.GetKeyDown(KeyCode.C))
        {
            view = (View)(((int)view + 1) % 3);
            Apply();
        }
    }

    void Apply()
    {
        bool cockpit = view == View.Cockpit;
        if (cockpitCam != null) cockpitCam.enabled = cockpit;
        if (cockpitLook != null) cockpitLook.enabled = cockpit;
        if (cockpitAudio != null) cockpitAudio.enabled = cockpit;
        if (extCam != null) extCam.enabled = !cockpit;
        if (extAudio != null) extAudio.enabled = !cockpit;

        if (view == View.Orbit)
        {
            orbitYaw = target.eulerAngles.y;          // start behind the plane
            orbitPitch = 18f;
        }
        if (view == View.Chase && extCam != null)
            extCam.transform.position = DesiredChasePos();   // snap on entry, then smooth
    }

    void LateUpdate()
    {
        if (target == null || view == View.Cockpit || extCam == null) return;
        if (view == View.Chase) UpdateChase();
        else UpdateOrbit();
    }

    Vector3 DesiredChasePos()
    {
        // Sit behind + above in the AIRCRAFT'S OWN frame, so the camera always
        // stays behind the tail and follows yaw, pitch and roll with the plane.
        return target.position + target.rotation * new Vector3(0f, chaseHeight, -chaseDistance);
    }

    void UpdateChase()
    {
        Vector3 desired = DesiredChasePos();
        extCam.transform.position = Vector3.SmoothDamp(extCam.transform.position, desired, ref followVel, chasePosDamp);

        // Look forward along the body (aim a little ahead of + above the plane),
        // using the aircraft's own up so a bank is shown — a true behind-the-tail
        // chase that banks/pitches/yaws with the aircraft.
        Vector3 lookAt = target.position + target.forward * 6f + target.up * 1.2f;
        Quaternion look = Quaternion.LookRotation(lookAt - extCam.transform.position, target.up);
        extCam.transform.rotation = Quaternion.Slerp(extCam.transform.rotation, look, 1f - Mathf.Exp(-chaseRotLerp * Time.deltaTime));
    }

    void UpdateOrbit()
    {
        if (Input.GetMouseButton(1))
        {
            orbitYaw += Input.GetAxis("Mouse X") * orbitSensitivity;
            orbitPitch -= Input.GetAxis("Mouse Y") * orbitSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch, -20f, 80f);
        }
        orbitDist = Mathf.Clamp(orbitDist - Input.GetAxis("Mouse ScrollWheel") * 12f, orbitMinDist, orbitMaxDist);

        Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 pos = target.position + Vector3.up * 2f + rot * (Vector3.back * orbitDist);
        extCam.transform.position = pos;
        extCam.transform.rotation = Quaternion.LookRotation((target.position + Vector3.up * 1.5f) - pos, Vector3.up);
    }
}
