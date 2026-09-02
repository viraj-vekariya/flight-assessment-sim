using System.Collections.Generic;
using System.IO;
using UnityEngine;
using GLTFast;

/// <summary>
/// (EXTRA) Replaces the code-built cockpit with a real photoreal Cessna 172 glass
/// cockpit model (loaded at runtime via glTFast from StreamingAssets), and RIGS its
/// yoke so it moves with the flight controls — the whole point: a realistic cockpit
/// whose handle actually turns when you roll and pushes when you pitch.
///
/// The model draws BOTH control yokes as one twin mesh (Object_90) flat against the
/// panel, with a short column hub for each (Object_94 pilot / Object_92 co-pilot).
/// That twin mesh is cut to the pilot half, slid to the centreline and hung under a
/// pivot on the column axis, so the model's OWN wheel turns with roll and slides
/// along the column with pitch. No geometry is built — it is the Blender mesh.
///
/// If the model fails to load, the code cockpit stays visible (graceful fallback).
/// Model: "FREE Cessna 172SP" by NLM, CC-BY 4.0 — attribution required (see CREDITS.txt).
/// </summary>
public class RealCockpit : MonoBehaviour
{
    public CessnaPhysics phys;
    public Transform cockpitRoot;
    public Camera cockpitCam;

    public string fileName = "Cockpit/cessna_g1000.glb";
    public float scale = 1.0f;                    // the model already frames naturally at 1:1 from the seat
    // The pilot eye point IN THE MODEL's own space. Centred (x=0) so the pilot sits in the
    // middle of the cockpit. The model is placed so this maps to the cockpit camera.
    public Vector3 eyePointInModel = new Vector3(0f, 0.58f, 0.52f);

    // SINGLE-PILOT cockpit. Mesh map, re-verified against the GLB (node transforms + mesh
    // bounds) and against a headless render of the pilot's view:
    //   Doors/windows      = Object_32/34/35 (left) + Object_120/122/123 (right)   -> visible
    //   Door armrest+catch = Object_33 (left) + Object_121 (right)                 -> visible
    //   Seat body (twin)   = Object_96 + Object_98 + Object_100, cut to the pilot half
    //   CONTROL YOKES      = Object_90, ONE twin mesh holding BOTH ram's-horn wheels,
    //                        centred at x = -0.064 (pilot) and +0.064 (co-pilot)
    //   Yoke column hubs   = Object_94 (pilot) and Object_92 (co-pilot)
    //   Object_104/105     = the 3 analog dials  -> hidden (the glass panel replaces them)
    // NOTE: an earlier pass had Object_33/121 recorded as the yoke wheels. They are not —
    // they sit ON the door skin (x = +-0.170), behind the pilot's eye, and are never in view.
    // Hide the dials and the CO-PILOT column hub. The co-pilot WHEEL is not hidden here: it
    // lives in the twin mesh and is cut away by RigYoke (KeepHalf), same as the seat.
    public string[] hideNodes = {
        "Object_104", "Object_105", "Object_92",
        // Object_52 (the rudder/brake pedal assembly) is NO LONGER HIDDEN. The reference
        // layout puts the brakes on the real pedals, and the model already has them at
        // x +-0.127, y 0.270..0.306, z 0.755..0.801 — so the cockpit furniture is kept
        // clear of that volume and CockpitControlRig anchors the brake control to it.
        "Object_15",  // landing gear legs/wheel assembly (78cm wide, y 0.07-0.245) — NOT pedal pads;
                      //   below the cabin floor and on the cockpit layer, so hiding it changes nothing
        "Object_17",  // REVERT_CANDIDATE: rudder bar — wide floor element spanning both sides; check in-sim and remove if it exposes raw floor geometry
        "Object_60",  // center console
        // MINIMAL COCKPIT (3 Sep 2026): the moulded radio / avionics button stack in the
        // strip between the two displays. Three separate nodes, all at x -0.016..-0.008
        // and y 0.472..0.519 — i.e. exactly the gap between the PFD (ends x -0.028) and
        // the MFD (starts x +0.004) — so hiding them removes the buttons and leaves the
        // panel, the bezel (Object_83) and both displays untouched.
        "Object_84", "Object_85", "Object_86",
        "Object_64",  // rear seats
    };
    // The seat BODY (cushion + back + headrest) is a single TWIN mesh spanning both seats,
    // centred at x=0. Cut it to the pilot (left) half, then re-centre -> one seat in the middle.
    public string[] seatBodyTwinNodes = { "Object_96", "Object_98", "Object_100" };

    // ---- the model's OWN control yoke, made dynamic ---------------------------------
    // Both wheels live in one mesh, so the pilot half is cut out of it (KeepHalf) and slid
    // to the centreline (CentreGroup) together with its column hub — then hung under a pivot
    // whose local +Z runs along the column, toward the panel.
    public string yokeTwinNode = "Object_90";   // twin mesh: both yoke wheels
    public string yokeHubNode  = "Object_94";   // pilot column hub (the stub at the panel)
    // Dynamic limits + smoothing (bounded, and self-centring because the inputs return to 0).
    public float yokeMaxRoll   = 55f;      // deg of wheel turn at full roll input
    public float yokeMaxTravel = 0.028f;   // metres of pull/push at full pitch input
    public float yokeSmooth    = 10f;      // higher = snappier; lower = smoother
    public float rollSign      = -1f;      // VERIFIED in-sim: -1 makes roll-right turn the wheel
                                           //   clockwise from the seat (flip to +1 to mirror it)
    public float pitchSign     = 1f;       // flip if the DOWN key doesn't pull the wheel toward you
    // The column hub (Object_94) can either RIDE with the wheel (default — it stays tucked
    // behind the wheel's boss in every position, so nothing odd is ever exposed) or stay
    // anchored in the panel and stretch along the column like a shaft in its bushing. The
    // stretch is more literal but the model's hub is a pale chrome stub, and it reads as a
    // bright blob poking out above the wheel when the yoke is pulled — hence ride-along.
    // TRUE (23 Aug 2026): the yoke needs a VISIBLE central shaft. With this off the column
    // hub rode along with the wheel, so when the yoke was pulled aft nothing connected it to
    // the panel and the wheel appeared to float — the missing centre attachment. With it on
    // the hub stays anchored in the panel and stretches along the column to meet the wheel,
    // which is what a real control column does.
    public bool  yokeShaftStretch = true;

    // The propeller + spinner (front fan) to spin with throttle.
    public string[] propNodes = { "Object_39", "Object_40", "Object_30" };
    public float propSpinMax = 2200f;   // deg/sec at full throttle

    Transform yokeRoot;      // on the column axis; slides along it with pitch
    Transform yokeVisual;    // holds the wheel mesh; twists about the column with roll
    Transform yokeShaft;     // the column hub, anchored in the panel, stretched to the wheel
    Vector3 yokeRootBasePos; // neutral position (roll = pitch = 0)
    float shaftLen = 0.036f; // hub length along the column, for the stretch
    float curRoll, curTravel;   // smoothed state
    Transform propPivot;

    async void Start()
    {
        // remember the existing (code-built) cockpit renderers so we can hide them
        // ONLY after the real model has loaded successfully.
        var codeRenderers = new List<Renderer>(cockpitRoot.GetComponentsInChildren<Renderer>(true));

        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        var import = new GltfImport();
        bool ok = await import.Load("file://" + path);
        if (!ok)
        {
            Debug.LogWarning("RealCockpit: could not load model at " + path + " — keeping code cockpit.");
            return;
        }

        var holder = new GameObject("RealCockpitModel");
        holder.transform.SetParent(cockpitRoot, false);
        holder.transform.localScale = Vector3.one * scale;
        // Place the model so its eye-point sits exactly at the cockpit camera.
        Vector3 camLocal = cockpitCam != null ? cockpitCam.transform.localPosition : new Vector3(0f, 0.64f, 0.42f);
        holder.transform.localPosition = camLocal - eyePointInModel * scale;
        await import.InstantiateMainSceneAsync(holder.transform);

        SetLayer(holder.transform, CockpitBuilder.CockpitLayer);

        // Single-pilot conversion: remove the co-pilot copies + the model's yoke meshes.
        // Hide the WHOLE subtree of each node — the yoke wheels have CHILD meshes, and only
        // disabling the parent's own renderer left those children (the static yokes) visible.
        foreach (var n in hideNodes)
        {
            var t = FindDeep(holder.transform, n);
            if (t != null)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        // Seat BODY is a twin mesh: cut to the pilot (left, x<0) half, then centre that half.
        foreach (var n in seatBodyTwinNodes)
        {
            var t = FindDeep(holder.transform, n);
            if (t != null) KeepHalf(holder.transform, t);
        }
        CentreGroup(holder.transform, seatBodyTwinNodes);

        // Yoke: the model's OWN wheel — kept exactly as Blender has it, just cut to one,
        // centred in front of the pilot, and driven by the live controls.
        RigYoke(holder.transform);

        // Spin the propeller (front fan) with throttle.
        RigProp(holder.transform);

        // hide the code cockpit visuals now that the real one is in place
        foreach (var r in codeRenderers) if (r != null) r.enabled = false;

        // ── retire the code cockpit's interaction surface ─────────────────────────
        // The code cockpit's CockpitControl grab boxes are trigger colliders that stay
        // COLLIDABLE after their geometry is hidden. Merely disabling the mouse
        // interaction component (which is what used to happen here) left eight invisible
        // interaction targets floating inside the real cockpit — harmless for a mouse
        // raycast that is switched off, but unacceptable for VR, where a hand reaching
        // for the real throttle can enter an invisible box that belongs to a control
        // that is not there. Destroy them outright: the real GLB cockpit is now the only
        // interaction surface, and having exactly one is the point.
        int retired = 0;
        foreach (var cc in cockpitRoot.GetComponentsInChildren<CockpitControl>(true))
        {
            foreach (var col in cc.GetComponents<Collider>()) Destroy(col);
            Destroy(cc.gameObject);
            retired++;
        }
        var ci = GetComponentInParent<CockpitInteraction>();
        if (ci != null) ci.enabled = false;
        Debug.Log("[Cockpit] retired " + retired + " code-cockpit interaction targets; " +
                  "the GLB cockpit is now the only interaction surface.");

        // Build the REAL cockpit's physical controls on the loaded model.
        var rig = holder.gameObject.AddComponent<CockpitControlRig>();
        rig.Build(holder.transform, phys);

        // Make the left glass screen a LIVE PFD (artificial horizon from real attitude).
        var pfd = holder.AddComponent<LivePFD>();
        pfd.Build(phys, holder.transform,
            // MEASURED off the model's own screen mesh (Object_88, two flat quads):
            //   glass face z = 0.7596, dead flat and VERTICAL (zero z-extent -> no tilt),
            //   recessed inside a bezel (Object_83) whose front face is at z = 0.7546.
            //   left screen centre  x = -0.0709, y = 0.4921
            // The quad is 85 mm wide but the glass opening is only 63.5 mm, so at z = 0.758 (behind
            // the bezel rim) the outer 11 mm each side — where the SPD and ALT readouts live — was
            // hidden by the bezel, and numbers got clipped as soon as they grew past two digits.
            // z = 0.7535 puts it 1.1 mm PROUD of the rim (0.7546) so the whole screen shows,
            // overlapping the bezel buttons. (0.778 is 18 mm inside the panel — invisible.)
            new Vector3(-0.0709f, 0.4921f, 0.7535f),  // left PFD centre, over the panel glass
            Quaternion.identity,                       // the glass is vertical — no tilt
            new Vector2(0.085f, 0.056f),
            CockpitBuilder.CockpitLayer);

        // Make the right glass screen a LIVE moving map (top-down world view).
        var mfd = holder.AddComponent<LiveMFD>();
        mfd.Build(phys.transform, holder.transform,
            new Vector3(0.0461f, 0.4921f, 0.7535f),   // right MFD centre, same treatment
            Quaternion.identity,
            new Vector2(0.085f, 0.056f),
            CockpitBuilder.CockpitLayer);

        // Live keyboard tuner for the two screen quads: F9 arms it, then the arrows / PageUp /
        // PageDown / +- / [ ] nudge whichever screen TAB has selected, and P prints both in
        // paste-ready form for the two Build() calls above.
        var tuner = holder.AddComponent<ScreenTuner>();
        tuner.pfdQuad = pfd.screenQuad;
        tuner.mfdQuad = mfd.screenQuad;

        Debug.Log("RealCockpit: model loaded + dynamic model yoke + live PFD + live MFD + screen tuner (F9).");
    }

    // Rig the model's OWN control yoke so it moves with the flight controls.
    // Nothing is built and nothing is scaled: the Blender mesh is cut to one wheel, slid to
    // the centreline and re-parented under a pivot that sits ON the column axis.
    //
    //   YokeRoot   (on the column axis; slides along +Z/-Z  ->  pitch push/pull)
    //     YokeVisual  (twists about the column, +Z  ->  roll)
    //       Object_90 (pilot half of the twin wheel mesh — untouched geometry)
    //       Object_94 (the column hub, riding along behind the wheel's boss)
    // With yokeShaftStretch the hub instead stays anchored in the panel, under its own
    // YokeShaft node, and stretches along the column to reach the wheel.
    void RigYoke(Transform model)
    {
        var wheel = FindDeep(model, yokeTwinNode);
        if (wheel == null) { Debug.LogWarning("RealCockpit: yoke mesh " + yokeTwinNode + " not found — yoke stays static."); return; }
        var hub = FindDeep(model, yokeHubNode);

        // ONE yoke: cut the twin mesh down to the pilot (left, x<0) half — that removes the
        // co-pilot wheel — then slide the pilot wheel + its hub to the centreline as one pair.
        KeepHalf(model, wheel);
        CentreGroup(model, hub != null ? new[] { yokeTwinNode, yokeHubNode } : new[] { yokeTwinNode });

        Bounds wb, hb;
        if (!DrawnBoundsInModel(model, wheel, out wb))
        { Debug.LogWarning("RealCockpit: yoke mesh has no drawn triangles."); return; }
        hb = wb;                // no hub in the model -> spin about the wheel's own centre
        bool hasHub = false;
        if (hub != null)
        {
            Bounds tmp;
            if (DrawnBoundsInModel(model, hub, out tmp)) { hb = tmp; hasHub = true; }
        }

        // The column axis = the hub's own centre line, on the centreline, pointing at the panel.
        Vector3 axis = new Vector3(0f, hb.center.y, hb.center.z);

        yokeRoot = new GameObject("YokeRoot").transform;
        yokeRoot.SetParent(model, false);
        yokeRoot.localPosition = axis;
        yokeRoot.localRotation = Quaternion.identity;      // local +Z = along the column, toward the panel

        yokeVisual = new GameObject("YokeVisual").transform;
        yokeVisual.SetParent(yokeRoot, false);
        yokeVisual.localPosition = Vector3.zero;
        yokeVisual.localRotation = Quaternion.identity;
        wheel.SetParent(yokeVisual, true);                 // keeps the mesh exactly where it is

        // The hub rides with the wheel by default. The stretch alternative is only safe while
        // the hub sits square to the model axes — a non-uniform scale would otherwise shear it.
        if (hasHub)
        {
            float skew = Quaternion.Angle(Quaternion.Inverse(model.rotation) * hub.rotation, Quaternion.identity);
            if (yokeShaftStretch && skew < 1f)
            {
                shaftLen = Mathf.Max(0.002f, hb.size.z);
                yokeShaft = new GameObject("YokeShaft").transform;
                yokeShaft.SetParent(model, false);
                yokeShaft.localPosition = new Vector3(0f, hb.center.y, hb.max.z);   // origin AT the panel face
                yokeShaft.localRotation = Quaternion.identity;
                hub.SetParent(yokeShaft, true);
                SetLayer(yokeShaft, CockpitBuilder.CockpitLayer);
            }
            else
            {
                hub.SetParent(yokeVisual, true);            // slides with the wheel, no stretch
            }
        }

        yokeRootBasePos = yokeRoot.localPosition;
        SetLayer(yokeRoot, CockpitBuilder.CockpitLayer);
        Debug.Log($"RealCockpit: yoke rigged — wheel size {wb.size:F3} centre {wb.center:F3}, "
                + $"column axis {axis:F3}, shaft {(yokeShaft != null ? shaftLen.ToString("F3") : "rides along")}");
    }

    // Bounds (in model space) of the geometry that is ACTUALLY DRAWN — i.e. of the vertices the
    // current triangles reference. Renderer.bounds / RecalculateBounds cover the whole vertex
    // array, which after KeepHalf still spans the half that was cut away.
    static bool DrawnBoundsInModel(Transform model, Transform node, out Bounds b)
    {
        b = default;
        var mf = node.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        var verts = mf.sharedMesh.vertices;
        var tris = mf.sharedMesh.triangles;
        bool has = false;
        for (int i = 0; i < tris.Length; i++)
        {
            Vector3 p = model.InverseTransformPoint(node.TransformPoint(verts[tris[i]]));
            if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
        }
        return has;
    }

    // Group the propeller + spinner under a pivot at their centre and spin about the model's
    // forward (Z) axis with throttle.
    void RigProp(Transform model)
    {
        var nodes = new List<Transform>();
        foreach (var n in propNodes) { var t = FindDeep(model, n); if (t != null) nodes.Add(t); }
        if (nodes.Count == 0) { Debug.LogWarning("RealCockpit: prop nodes not found."); return; }

        bool has = false; Bounds b = default;
        foreach (var t in nodes) { var r = t.GetComponent<Renderer>(); if (r == null) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        Vector3 c = has ? b.center : nodes[0].position;

        propPivot = new GameObject("PropPivot").transform;
        propPivot.SetParent(model.parent, true);
        propPivot.position = c;
        propPivot.rotation = model.rotation;
        foreach (var t in nodes) t.SetParent(propPivot, true);
    }

    // Slide a group of nodes by ONE common offset so the geometry that is ACTUALLY drawn
    // (the vertices referenced by the current triangles — after any KeepHalf cut) is centred on
    // the cockpit centreline (model-x = 0). We must look at referenced vertices, not mesh.bounds:
    // KeepHalf only edits triangles, so the vertex array (and thus bounds) still spans both seats.
    void CentreGroup(Transform model, string[] names)
    {
        var ts = new List<Transform>();
        bool has = false; float minX = 0f, maxX = 0f;
        foreach (var n in names)
        {
            var t = FindDeep(model, n);
            if (t == null) continue;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            ts.Add(t);
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i++)
            {
                float x = model.InverseTransformPoint(t.TransformPoint(verts[tris[i]])).x;
                if (!has) { minX = maxX = x; has = true; }
                else { if (x < minX) minX = x; if (x > maxX) maxX = x; }
            }
        }
        if (!has) { Debug.LogWarning("RealCockpit: centre nodes not found."); return; }
        float cxModel = (minX + maxX) * 0.5f;
        Vector3 shift = model.TransformVector(new Vector3(-cxModel, 0f, 0f));
        foreach (var t in ts) t.position += shift;
        Debug.Log($"RealCockpit: centred {string.Join("+", names)}, referenced-x [{minX:F3},{maxX:F3}] -> shift {-cxModel:F3}");
    }

    // Translate a node so its bounds centre lands on the cockpit centreline (model-x = 0).
    static void CentreX(Transform model, Transform node)
    {
        var r = node.GetComponent<Renderer>();
        if (r == null) return;
        float cxModel = model.InverseTransformPoint(r.bounds.center).x;
        node.position += model.TransformVector(new Vector3(-cxModel, 0f, 0f));
    }

    // Mesh surgery: keep only the triangles on the model's LEFT (holder-local x &lt; 0),
    // turning a twin (yoke / seat) mesh into a single pilot-side one.
    static void KeepHalf(Transform holder, Transform node)
    {
        var mf = node.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var mesh = mf.mesh;                                 // instance (safe to edit)
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var lx = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            lx[i] = holder.InverseTransformPoint(node.TransformPoint(verts[i])).x;
        var keep = new List<int>(tris.Length);
        for (int i = 0; i < tris.Length; i += 3)
        {
            float cx = (lx[tris[i]] + lx[tris[i + 1]] + lx[tris[i + 2]]) / 3f;
            if (cx < 0f) { keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]); }
        }
        mesh.triangles = keep.ToArray();
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    void LateUpdate()
    {
        if (phys == null) return;

        if (yokeRoot != null)
        {
            // Driven by the SAME inputs as the aircraft, so the wheel always agrees with what
            // the aeroplane is doing. Both motions are eased, and both return to neutral by
            // themselves because the inputs fall back to 0 when the pilot lets go.
            //
            //   ROLL  -> twist about the column (yokeRoot's local +Z, pointing at the panel).
            //            Measured from an isolated render of the wheel: a POSITIVE angle about
            //            that axis reads ANTI-clockwise from the seat, so rollSign = -1 is what
            //            makes roll right (+1) turn the wheel clockwise, as a real yoke does.
            //   PITCH -> slide along the column. pitchInput is negative for nose UP (the DOWN
            //            key), and a negative offset on +Z moves the wheel back toward the
            //            pilot — so DOWN/nose-up PULLS the wheel in and UP/nose-down pushes it
            //            toward the panel. pitchSign flips it.
            float k = 1f - Mathf.Exp(-yokeSmooth * Time.deltaTime);   // frame-rate independent easing
            float tRoll   = rollSign  * Mathf.Clamp(phys.rollInput,  -1f, 1f) * yokeMaxRoll;
            float tTravel = pitchSign * Mathf.Clamp(phys.pitchInput, -1f, 1f) * yokeMaxTravel;
            curRoll   = Mathf.Lerp(curRoll,   tRoll,   k);
            curTravel = Mathf.Lerp(curTravel, tTravel, k);

            if (yokeVisual != null)
                yokeVisual.localRotation = Quaternion.AngleAxis(curRoll, Vector3.forward);
            yokeRoot.localPosition = yokeRootBasePos + Vector3.forward * curTravel;
            if (yokeShaft != null)   // shaft grows aft out of the panel as the wheel comes back
                yokeShaft.localScale = new Vector3(1f, 1f, Mathf.Clamp((shaftLen - curTravel) / shaftLen, 0.05f, 5f));
        }

        if (propPivot != null)   // spin the front fan with throttle
            propPivot.Rotate(0f, 0f, propSpinMax * phys.Throttle01 * Time.deltaTime, Space.Self);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }
}
