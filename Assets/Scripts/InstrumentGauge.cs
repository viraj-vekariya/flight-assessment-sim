using UnityEngine;

/// <summary>
/// Drives a single round gauge needle from a live CessnaPhysics value.
/// The needle points up at the gauge's "zero" and sweeps as the value changes.
/// </summary>
public class InstrumentGauge : MonoBehaviour
{
    public CessnaPhysics phys;
    public GaugeType type;
    public Transform needle;     // pivot at gauge centre; rotates about local Z

    // sweep of a typical round dial
    const float Sweep = 135f;    // +/- degrees from top

    void LateUpdate()
    {
        if (phys == null || needle == null) return;
        needle.localRotation = Quaternion.Euler(0f, 0f, AngleFor());
    }

    float AngleFor()
    {
        switch (type)
        {
            case GaugeType.Airspeed:
                return Map(phys.AirspeedKmh, 0f, 250f);
            case GaugeType.Altimeter:
                // single needle, wraps every 1000 m
                return Map(Mathf.Repeat(phys.AltitudeM, 1000f), 0f, 1000f);
            case GaugeType.Heading:
                return -phys.HeadingDeg;                 // compass card: N up at 0
            case GaugeType.VerticalSpeed:
                return -Mathf.Clamp(phys.VerticalSpeedMs / 10f, -1f, 1f) * Sweep;
            case GaugeType.TurnRate:
                return -Mathf.Clamp(phys.YawRateDps / 30f, -1f, 1f) * 45f;
            default:
                return 0f;
        }
    }

    // value range -> [+Sweep .. -Sweep], clockwise
    static float Map(float v, float min, float max)
    {
        float t = Mathf.Clamp01((v - min) / (max - min));
        return Mathf.Lerp(Sweep, -Sweep, t);
    }
}
