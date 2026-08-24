// TrafficAircraft — scripted other traffic the pilot can actually see.
//
// WHY IT EXISTS
//   The experiment's requirements call for "another aircraft on/near the runway" at
//   MEDIUM and "multiple/conflicting aircraft situations" at HIGH. Until now this
//   simulator had no traffic at all, so a conflict could only be delivered as a radio
//   call — the pilot never had to LOOK for anything. That removed the visual-search
//   and outside-scan component, which is a large part of why traffic conflicts are
//   cognitively expensive, and it was the largest documented fidelity gap in the
//   HIGH set.
//
// WHAT IT IS
//   A low-poly aeroplane shape (fuselage, wings, tail, fin) that follows a scripted
//   path at a scripted speed. It is a MOVING PROP, not a simulated aircraft: no
//   flight model, no collision response, no pilot logic, no TCAS. It exists to be
//   seen, to be looked for, and to force a decision.
//
// WHAT IT IS NOT
//   It does not detect the player, does not manoeuvre to avoid, and cannot collide.
//   Conflict is scripted geometry, not emergent. This is a deliberate choice: an
//   emergent-collision model would make the trigger time vary between participants,
//   which would destroy the event-locked EEG epoching the whole design depends on.
//   Every participant meets the same traffic at the same (jittered) instant.
//
// LOGGED
//   The engine emits TRAFFIC_ONSET when the traffic is spawned/becomes relevant, and
//   the telemetry carries the slant range to the nearest traffic each sample, so
//   "how close did it actually get" is measurable rather than asserted.

using UnityEngine;

public enum TrafficBehaviour
{
    /// <summary>Sitting on the runway, not moving — the classic "another aircraft on
    /// the runway" that blocks a take-off clearance.</summary>
    HoldingOnRunway,
    /// <summary>Crosses the runway ahead, left to right, then clears.</summary>
    CrossingRunway,
    /// <summary>Taxies along the parallel taxiway toward the hold-short point.</summary>
    TaxiingParallel,
    /// <summary>Airborne, converging on the runway centreline at a similar level —
    /// the approach conflict.</summary>
    ConvergingApproach,
    /// <summary>Airborne, crossing the departure path from the right.</summary>
    CrossingDeparture,
}

public class TrafficAircraft : MonoBehaviour
{
    public TrafficBehaviour Behaviour { get; private set; }
    public string Callsign { get; private set; } = "TRAFFIC";
    public bool Active { get; private set; }

    Vector3 from, to;
    float speed;          // m/s
    float t;              // 0..1 along the path
    float pathLength;
    float age;

    static Mesh sharedBody;

    /// <summary>Build a traffic aircraft and start it running.</summary>
    public static TrafficAircraft Spawn(Transform parent, TrafficBehaviour behaviour,
                                        string callsign, float scale = 1f)
    {
        var go = new GameObject("Traffic_" + callsign);
        go.transform.SetParent(parent);
        var t = go.AddComponent<TrafficAircraft>();
        t.Callsign = callsign;
        t.Behaviour = behaviour;
        t.BuildShape(scale);
        t.SetPath(behaviour);
        t.Active = true;
        return t;
    }

    void SetPath(TrafficBehaviour b)
    {
        switch (b)
        {
            case TrafficBehaviour.HoldingOnRunway:
                // Parked across the runway at the far end of the take-off roll.
                from = to = new Vector3(0f, 1.4f, 60f);
                transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // across the runway
                speed = 0f;   // sits there for the whole trial
                break;

            case TrafficBehaviour.CrossingRunway:
                from = new Vector3(-70f, 1.4f, 40f);
                to = new Vector3(70f, 1.4f, 40f);
                speed = 9f;
                transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                break;

            case TrafficBehaviour.TaxiingParallel:
                from = new Vector3(Aerodrome.TaxiwayX, 1.4f, -200f);
                to = new Vector3(Aerodrome.TaxiwayX, 1.4f, Aerodrome.HoldShortZ + 8f);
                speed = 6f;
                transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                break;

            case TrafficBehaviour.ConvergingApproach:
                // Comes in from the right and joins the extended centreline ahead of
                // the player, at a similar height — visible in the forward field of view.
                from = new Vector3(1400f, 300f, -4200f);
                to = new Vector3(60f, 150f, -1200f);
                speed = 55f;
                break;

            case TrafficBehaviour.CrossingDeparture:
                from = new Vector3(900f, 420f, 900f);
                to = new Vector3(-900f, 380f, 1500f);
                speed = 55f;
                break;
        }
        pathLength = Vector3.Distance(from, to);
        transform.position = from;
        if (pathLength > 1f) AimAlongPath();
    }

    void AimAlongPath()
    {
        Vector3 d = (to - from).normalized;
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }

    void Update()
    {
        if (!Active) return;
        age += Time.deltaTime;
        if (speed <= 0f || pathLength < 1f) return;

        t += speed * Time.deltaTime / pathLength;
        if (t >= 1f)
        {
            t = 1f;
            Active = false;                     // reached the end of its path
            gameObject.SetActive(false);
            return;
        }
        transform.position = Vector3.Lerp(from, to, t);
    }

    /// <summary>Slant range from a point to this traffic, or -1 when inactive.</summary>
    public float RangeFrom(Vector3 p) => Active ? Vector3.Distance(p, transform.position) : -1f;

    /// <summary>True while this traffic physically blocks the runway.</summary>
    public bool BlocksRunway =>
        Active && Behaviour != TrafficBehaviour.ConvergingApproach
               && Behaviour != TrafficBehaviour.CrossingDeparture
               && Aerodrome.OnRunway(transform.position);

    // ── the shape ───────────────────────────────────────────────────────────────
    // A recognisable light-aircraft silhouette from primitives. It only has to read
    // as "an aeroplane" at 100-2000 m; there is no cockpit detail and no livery.
    void BuildShape(float scale)
    {
        var col = new Color(0.88f, 0.88f, 0.92f);
        var trim = new Color(0.15f, 0.32f, 0.65f);

        Part(PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f),
             new Vector3(1.1f, 3.6f, 1.1f) * scale, col);                       // fuselage
        Part(PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.4f) * scale, Quaternion.identity,
             new Vector3(10.8f, 0.18f, 1.5f) * scale, col);                     // high wing
        Part(PrimitiveType.Cube, new Vector3(0f, 0.15f, -3.2f) * scale, Quaternion.identity,
             new Vector3(3.6f, 0.15f, 0.9f) * scale, col);                      // tailplane
        Part(PrimitiveType.Cube, new Vector3(0f, 0.9f, -3.3f) * scale, Quaternion.identity,
             new Vector3(0.15f, 1.5f, 1.0f) * scale, trim);                     // fin
        Part(PrimitiveType.Cube, new Vector3(0f, 0f, 3.4f) * scale, Quaternion.identity,
             new Vector3(2.2f, 0.12f, 0.12f) * scale, new Color(0.2f, 0.2f, 0.2f));  // prop disc
    }

    void Part(PrimitiveType type, Vector3 lp, Quaternion lr, Vector3 ls, Color c)
    {
        var g = GameObject.CreatePrimitive(type);
        g.transform.SetParent(transform);
        g.transform.localPosition = lp;
        g.transform.localRotation = lr;
        g.transform.localScale = ls;
        // No colliders: this is a prop. Nothing may bounce off it, and it must never
        // disturb the player's flight model.
        SimUtil.Destroy(g.GetComponent<Collider>());
        var m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", 0.4f);
        g.GetComponent<Renderer>().material = m;
    }
}
