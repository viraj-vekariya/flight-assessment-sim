// PanelGauge — one live round engine/electrical instrument on the cockpit panel.
//
// WHY THESE EXIST
//   Three HIGH missions turn on diagnosing a systems failure: a rough-running engine
//   (carburettor ice), a failing alternator, a blocked static port. Until now the cockpit
//   showed the pilot NONE of the evidence those diagnoses are made from — no tachometer,
//   no oil pressure, no ammeter, no fuel quantity. The participant could only follow the
//   checklist text and hope, which turns a diagnostic task into a reading task and
//   measures something other than what the mission was designed to measure.
//
//   Every value below already existed in AircraftSystems and was already being logged.
//   Nothing here invents a number; it only shows one the aeroplane already had.
//
// HONESTY
//   The needle shows the SIMULATED value, not a smoothed or prettified one, so a gauge
//   that flickers is telling the truth about the model. The only filtering is the same
//   frame-rate-independent easing every other cockpit visual uses, standing in for needle
//   inertia.

using UnityEngine;

public class PanelGauge : MonoBehaviour
{
    public enum Kind { RPM, OilPress, OilTemp, FuelTotal, BusVolts, LoadAmps }

    [System.Serializable]
    public struct Spec
    {
        public string label;
        public Kind kind;
        public float min, max;
    }

    public Spec spec;
    public AircraftSystems systems;
    public CessnaPhysics phys;

    Transform needle;
    float shown;

    /// <summary>Sweep from the 7-o'clock position to 5-o'clock, the 270 degrees a round
    /// instrument conventionally uses.</summary>
    const float Sweep = 270f;

    public void Build(Transform face, float diaMm)
    {
        float mm = CockpitHardware.MM;
        float r = diaMm * 0.5f * mm;

        // Tick marks round the dial: without them a needle has nothing to be read against
        // and the instrument is a decoration.
        for (int i = 0; i <= 8; i++)
        {
            float a = (-Sweep * 0.5f + Sweep * i / 8f) * Mathf.Deg2Rad;
            bool major = (i % 2) == 0;
            var t = CockpitHardware.Box(face,
                new Vector3(Mathf.Sin(a) * r * 0.78f, Mathf.Cos(a) * r * 0.78f, -0.2f * mm),
                new Vector3((major ? 1.4f : 0.9f) * mm, (major ? 4.5f : 2.8f) * mm, 0.5f * mm),
                CockpitHardware.Placard, 0.20f);
            t.localRotation = Quaternion.Euler(0f, 0f, -(-Sweep * 0.5f + Sweep * i / 8f));
        }

        var pivot = new GameObject("Needle").transform;
        pivot.SetParent(face, false);
        pivot.localPosition = new Vector3(0f, 0f, -0.6f * mm);
        // The needle is offset so it pivots at its tail, not its middle.
        CockpitHardware.Box(pivot, new Vector3(0f, r * 0.34f, 0f),
                            new Vector3(1.3f * mm, r * 0.72f, 0.6f * mm),
                            new Color(0.88f, 0.86f, 0.30f), 0.30f);
        CockpitHardware.Barrel(pivot, new Vector3(0f, 0f, -0.3f * mm), 2.0f * mm, 1.4f * mm,
                               CockpitHardware.KnobBlack, 0.30f);
        needle = pivot;

        SetLayerDeep(face, CockpitBuilder.CockpitLayer);
    }

    static void SetLayerDeep(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerDeep(t.GetChild(i), layer);
    }

    void LateUpdate()
    {
        if (needle == null) return;
        float v = Read();
        float t01 = Mathf.InverseLerp(spec.min, spec.max, v);
        float k = 1f - Mathf.Exp(-Time.deltaTime / 0.12f);      // needle inertia
        shown = Mathf.Lerp(shown, Mathf.Clamp01(t01), k);
        needle.localRotation = Quaternion.Euler(0f, 0f, -(-Sweep * 0.5f + Sweep * shown));
    }

    float Read()
    {
        if (systems == null) return spec.min;
        switch (spec.kind)
        {
            case Kind.RPM:       return systems.RPM;
            case Kind.OilPress:  return systems.OilPressurePsi;
            case Kind.OilTemp:   return systems.OilTempC;
            case Kind.FuelTotal: return systems.FuelTotalL;
            case Kind.BusVolts:  return systems.BusVolts;
            // Charge/discharge: positive when the alternator is carrying the load, negative
            // when the battery is. This is the instrument that shows an alternator failure,
            // which is exactly the point of having it.
            case Kind.LoadAmps:  return systems.AlternatorOn ? systems.LoadA : -systems.LoadA;
            default:             return spec.min;
        }
    }
}
