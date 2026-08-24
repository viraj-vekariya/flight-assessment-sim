using UnityEngine;

/// <summary>
/// Builds a Cessna-style high-wing trainer from primitives: fuselage, wing,
/// struts, tail, hinged ailerons/elevator/rudder/flaps, landing gear and a
/// propeller. Attaches Rigidbody + CessnaPhysics + AircraftController +
/// ControlSurfaces, and builds the cockpit (panel, gauges, yoke, camera).
/// Stylized placeholder art — a real model can replace it later.
/// </summary>
public static class AircraftBuilder
{
    public static CessnaPhysics Build(Vector3 pos, Quaternion rot, Transform parent)
    {
        var root = new GameObject("Cessna");
        root.transform.SetParent(parent);
        root.transform.SetPositionAndRotation(pos, rot);

        Color white = new Color(0.93f, 0.93f, 0.95f);
        Color trim = new Color(0.15f, 0.35f, 0.75f);
        Color metal = new Color(0.6f, 0.6f, 0.62f);
        Color dark = new Color(0.12f, 0.12f, 0.13f);

        // Fuselage
        var body = Capsule(root.transform, new Vector3(0f, 0f, -0.2f),
                           Quaternion.Euler(90f, 0f, 0f), new Vector3(1.1f, 2.4f, 1.1f), white);

        // High wing + struts
        Box(root.transform, new Vector3(0f, 0.95f, -0.2f), Quaternion.identity,
            new Vector3(11f, 0.18f, 1.7f), white);
        Box(root.transform, new Vector3(0f, 0.92f, -0.2f), Quaternion.identity,
            new Vector3(11f, 0.04f, 0.35f), trim);                          // wing stripe
        Cyl(root.transform, new Vector3(-1.6f, 0.5f, -0.2f), Quaternion.Euler(0f, 0f, 28f),
            new Vector3(0.07f, 0.55f, 0.07f), metal);                       // L strut
        Cyl(root.transform, new Vector3(1.6f, 0.5f, -0.2f), Quaternion.Euler(0f, 0f, -28f),
            new Vector3(0.07f, 0.55f, 0.07f), metal);                       // R strut

        // Tail
        Box(root.transform, new Vector3(0f, 0.75f, -2.7f), Quaternion.identity,
            new Vector3(0.16f, 1.1f, 1.1f), white);                         // vertical fin
        Box(root.transform, new Vector3(0f, 0.4f, -2.7f), Quaternion.identity,
            new Vector3(3.6f, 0.14f, 0.9f), white);                         // horizontal stab

        // Control surfaces (each on a hinge pivot)
        Transform ailL = Hinge(root.transform, new Vector3(-4.0f, 0.95f, -1.0f),
                               new Vector3(2.4f, 0.12f, 0.5f), white);
        Transform ailR = Hinge(root.transform, new Vector3(4.0f, 0.95f, -1.0f),
                               new Vector3(2.4f, 0.12f, 0.5f), white);
        Transform flpL = Hinge(root.transform, new Vector3(-1.6f, 0.95f, -1.0f),
                               new Vector3(2.4f, 0.12f, 0.5f), trim);
        Transform flpR = Hinge(root.transform, new Vector3(1.6f, 0.95f, -1.0f),
                               new Vector3(2.4f, 0.12f, 0.5f), trim);
        Transform elev = Hinge(root.transform, new Vector3(0f, 0.4f, -3.1f),
                               new Vector3(3.4f, 0.12f, 0.5f), white);
        Transform rudd = HingeYaw(root.transform, new Vector3(0f, 0.85f, -3.1f),
                                  new Vector3(0.14f, 0.95f, 0.5f), trim);

        // Landing gear
        Wheel(root.transform, new Vector3(0f, -0.9f, 1.6f), dark);
        Wheel(root.transform, new Vector3(-1.2f, -0.9f, -0.2f), dark);
        Wheel(root.transform, new Vector3(1.2f, -0.9f, -0.2f), dark);

        // Propeller (spins about the nose Z axis)
        var propPivot = new GameObject("PropPivot");
        propPivot.transform.SetParent(root.transform);
        propPivot.transform.localPosition = new Vector3(0f, 0f, 2.55f);
        propPivot.transform.localRotation = Quaternion.identity;
        Box(propPivot.transform, Vector3.zero, Quaternion.identity, new Vector3(0.12f, 2.0f, 0.12f), dark);
        Cyl(root.transform, new Vector3(0f, 0f, 2.45f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.25f, 0.12f, 0.25f), metal);                       // spinner

        // Body collider rides ABOVE the wheels (the suspension carries the plane),
        // so the tail can't strike the runway during rotation. Used for crashes/scenery.
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.2f, -0.2f);
        col.size = new Vector3(2.0f, 1.0f, 5.0f);
        col.material = LowFriction();

        root.AddComponent<Rigidbody>();
        var phys = root.AddComponent<CessnaPhysics>();
        root.AddComponent<AircraftController>();
        root.AddComponent<StallHorn>().phys = phys;   // procedural stall-warning horn
        root.AddComponent<AircraftAudio>().phys = phys; // procedural engine/wind/ground/touchdown/crash SFX

        var surfaces = root.AddComponent<ControlSurfaces>();
        surfaces.phys = phys;
        surfaces.aileronLeft = ailL; surfaces.aileronRight = ailR;
        surfaces.flapLeft = flpL; surfaces.flapRight = flpR;
        surfaces.elevator = elev; surfaces.rudder = rudd;
        surfaces.propeller = propPivot.transform;

        // Hide the whole exterior from the cockpit camera so no wings/fuselage/
        // surfaces appear in the first-person view (keeps the v4/v5 cockpit shape,
        // just removes the geometry that was clipping into view).
        SetLayer(root.transform, ExteriorLayer);

        // Cockpit interior + gauges + yoke + camera (stays on the default layer,
        // so the cockpit camera DOES render it).
        CockpitBuilder.Build(root.transform, phys);

        return phys;
    }

    public const int ExteriorLayer = 10;

    static void SetLayer(Transform t, int layer)
    {
        foreach (Transform c in t) { c.gameObject.layer = layer; SetLayer(c, layer); }
    }

    // ---- primitive helpers ----

    static GameObject Box(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color c)
        => Prim(PrimitiveType.Cube, parent, pos, rot, scale, c);

    static GameObject Cyl(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color c)
        => Prim(PrimitiveType.Cylinder, parent, pos, rot, scale, c);

    static GameObject Capsule(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color c)
        => Prim(PrimitiveType.Capsule, parent, pos, rot, scale, c);

    static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(t);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        SimUtil.Destroy(g.GetComponent<Collider>());
        Paint(g, c);
        return g;
    }

    static void Wheel(Transform parent, Vector3 pos, Color c)
    {
        var strut = Cyl(parent, pos + new Vector3(0f, 0.3f, 0f), Quaternion.identity,
                        new Vector3(0.06f, 0.3f, 0.06f), new Color(0.5f, 0.5f, 0.5f));
        Cyl(parent, pos, Quaternion.Euler(0f, 0f, 90f), new Vector3(0.36f, 0.12f, 0.36f), c);
    }

    /// Hinge that rotates about local X (ailerons/elevator/flaps). Surface extends behind the hinge.
    static Transform Hinge(Transform parent, Vector3 hingePos, Vector3 surfaceScale, Color c)
    {
        var pivot = new GameObject("Hinge");
        pivot.transform.SetParent(parent);
        pivot.transform.localPosition = hingePos;
        pivot.transform.localRotation = Quaternion.identity;
        Box(pivot.transform, new Vector3(0f, 0f, -surfaceScale.z * 0.5f), Quaternion.identity, surfaceScale, c);
        return pivot.transform;
    }

    /// Hinge that rotates about local Y (rudder).
    static Transform HingeYaw(Transform parent, Vector3 hingePos, Vector3 surfaceScale, Color c)
    {
        var pivot = new GameObject("HingeYaw");
        pivot.transform.SetParent(parent);
        pivot.transform.localPosition = hingePos;
        pivot.transform.localRotation = Quaternion.identity;
        Box(pivot.transform, new Vector3(0f, 0f, -surfaceScale.z * 0.5f), Quaternion.identity, surfaceScale, c);
        return pivot.transform;
    }

    static void Paint(GameObject g, Color c)
    {
        var m = new Material(Shader.Find("Standard"));
        m.color = c;
        g.GetComponent<Renderer>().material = m;
    }

    static PhysicsMaterial LowFriction()
    {
        var pm = new PhysicsMaterial();
        pm.name = "wheel";
        pm.dynamicFriction = 0.05f;
        pm.staticFriction = 0.05f;
        pm.bounciness = 0f;
        pm.frictionCombine = PhysicsMaterialCombine.Minimum;
        pm.bounceCombine = PhysicsMaterialCombine.Minimum;
        return pm;
    }
}
