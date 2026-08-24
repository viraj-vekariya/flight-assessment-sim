using UnityEngine;

/// <summary>
/// First-person cockpit camera at the pilot's eye (child of the aircraft).
/// Default view looks slightly down (basePitch) so the instrument panel is
/// visible without effort. Hold RIGHT MOUSE to look around; release to recentre.
/// </summary>
public class CockpitCamera : MonoBehaviour
{
    public float basePitch = 4f;      // slight downward tilt — panel sits low, window fills the view
    public float lookSpeed = 2.5f;
    public float maxYaw = 100f;
    public float maxPitch = 60f;
    public float recenterSpeed = 360f; // deg/sec

    float yaw, pitch;

    void LateUpdate()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSpeed;
            pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
            yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        }
        else
        {
            yaw = Mathf.MoveTowards(yaw, 0f, recenterSpeed * Time.deltaTime);
            pitch = Mathf.MoveTowards(pitch, 0f, recenterSpeed * Time.deltaTime);
        }
        transform.localRotation = Quaternion.Euler(basePitch + pitch, yaw, 0f);
    }
}
