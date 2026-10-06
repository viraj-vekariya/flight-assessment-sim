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
        // SINGLE-PILOT FOOTWELL (20 Sep 2026). Object_58 is a solid box on the centreline —
        // x +-0.047, floor to y 0.445, running z 0.667..0.873 — i.e. the control-column
        // tunnel that separates the two original footwells. It belongs to a TWO-seat cabin.
        // This build re-centres the seat and the yoke onto the centreline and now does the
        // same with one pair of rudder pedals, so the tunnel sits exactly where the single
        // pilot's feet go: a solid slab between his own two pedals, dividing nothing.
        "Object_58",
    };
    // ---- the glass the pilot looks THROUGH ------------------------------------------
    // Object_37 is the windscreen and 34/123 the side windows. All three share the GLB's
    // one "Window" material, which ships as alphaMode BLEND with
    // baseColorFactor (0.14, 0.14, 0.14, 0.37): a 37%-opaque DARK GREY pane. Everything
    // outside is seen through it, and measured through the pilot's camera it multiplies
    // the runway, the grass and the sky by about 0.5 each — uniformly, which is how it
    // was found: the level-design renders (exterior camera, no glass) and the seated
    // view disagreed by the same factor on every surface.
    //
    // The old sky only looked acceptable because its blue push over-brightened it enough
    // to survive being halved. A real 172 windscreen is clear. This sets the pane to a
    // faint, mostly-clear glass so the world outside is seen at the brightness the
    // lighting was actually designed at.
    public string[] glassNodes = { "Object_37", "Object_34", "Object_123" };
    public Color glassColour = new Color(0.90f, 0.94f, 1.00f, 0.10f);   // faint cool, ~90% transmissive

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

    // ---- the model's OWN rudder pedals, made dynamic --------------------------------
    // Q/E had NO cockpit feedback at all: the aeroplane yawed and rolled and nothing in
    // the cabin moved. The model already has the pedals, so they are driven rather than
    // drawn — same rule as the yoke, one-way from the live input, so they cannot disagree
    // with what the aeroplane is doing.
    //
    // MEASURED GEOMETRY (drawn triangles, model-local): Object_52 is ONE mesh holding BOTH
    // pedals, x -0.127..0.127, y 0.270..0.306, z 0.755..0.801, and its vertices split
    // cleanly about x = 0 — 504 either side and NOTHING within 20 mm of the centreline.
    // So it cuts in two exactly like the yoke and seat twin meshes. Each half is one
    // ~75 mm pedal, centred near x -0.09 and +0.09; the pilot's eye is on the centreline
    // (0, 0.58, 0.52), so those are this cockpit's left and right rudder pedals.
    //
    // THEY TRANSLATE, THEY DO NOT ROTATE. That is what rudder pedals do — the rudder bar
    // slides them fore and aft — and it also sidesteps the trap that broke this before:
    // Object_52's node origin is at the MODEL DATUM (localPosition exactly zero, because
    // glTF bakes the geometry into the vertices), roughly 280 mm below and 780 mm behind
    // the pedal faces. Hinging on that origin swung a pedal through the cabin floor. A
    // translation has no pivot to place, so there is nothing to get wrong.
    public string pedalTwinNode = "Object_52";
    /// <summary>Fore/aft movement of each pedal at full rudder, metres. The GLB is about
    /// a third of life size, so 18 mm here is roughly 50 mm of real pedal throw.</summary>
    public float pedalTravel = 0.018f;
    public float pedalSmooth = 12f;
    /// <summary>+1 means right rudder pushes the RIGHT pedal forward, away from the pilot.
    /// Verified in the control battery rather than assumed.</summary>
    public float pedalSign = 1f;

    // ONE pair, on the centreline. Object_52 holds FOUR pads — the pilot's pair at
    // x -0.130..-0.055 and the co-pilot's at x +0.055..+0.130, with a 110 mm gap between
    // them. (Splitting only at x = 0 yields one PAIR per side, so both of the pilot's
    // pedals move together and neither is a rudder pedal. The render is what showed that —
    // two pads in each footwell, not one.)
    //
    // The co-pilot's pair is NOT rebuilt: this cabin has been converted to single-pilot,
    // with the seat and the yoke already cut to the pilot's half and slid to the centreline.
    // The pilot's two pads get the same treatment, so his pedals end up in front of him at
    // x = 0 rather than off to one side, and the tunnel that used to sit between the two
    // footwells (Object_58) is hidden because there is only one footwell now.
    Transform pedalPilotL, pedalPilotR;
    Vector3 basePilotL, basePilotR;
    float curPedal;

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
        ClearTheGlass(holder.transform);
        RetrimInterior(holder.transform);
        CloseCentreStrip(holder.transform);
        CentreGroup(holder.transform, seatBodyTwinNodes);

        // Yoke: the model's OWN wheel — kept exactly as Blender has it, just cut to one,
        // centred in front of the pilot, and driven by the live controls.
        RigYoke(holder.transform);
        RigPedals(holder.transform);

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
            new Vector3(0.0461f - MfdShift, 0.4921f, 0.7535f),   // right MFD centre, slid left (CloseCentreStrip)
            Quaternion.identity,
            new Vector2(0.085f, 0.056f),
            CockpitBuilder.CockpitLayer);

        // Standby airspeed / attitude / altimeter either side of the displays, as on every
        // G1000 172 — live, and purely visual.
        holder.AddComponent<StandbyInstruments>().Build(phys, holder.transform, CockpitBuilder.CockpitLayer);

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
    /// <summary>Make the cabin glass transmit light. See glassNodes.
    ///
    /// The material is SHARED by all three panes, so it is edited once through the first
    /// renderer that carries it. gltfast's shader is glTF/PbrMetallicRoughness and its
    /// albedo property is "baseColorFactor" — not _Color or _BaseColor — and setting the
    /// wrong one is a silent no-op, which this project has been caught by before. So the
    /// property is looked up, the one found is logged, and finding none is a WARNING
    /// rather than nothing.</summary>
    void ClearTheGlass(Transform model)
    {
        Material glass = null; string on = null;
        foreach (var n in glassNodes)
        {
            var t = FindDeep(model, n);
            var r = t != null ? t.GetComponent<Renderer>() : null;
            if (r != null && r.sharedMaterial != null) { glass = r.sharedMaterial; on = n; break; }
        }
        if (glass == null)
        {
            Debug.LogWarning("[RealCockpit] no cabin glass found among " + string.Join("/", glassNodes)
                           + " — the windscreen keeps the GLB's dark tint.");
            return;
        }

        string prop = glass.HasProperty("baseColorFactor") ? "baseColorFactor"
                    : glass.HasProperty("_BaseColor")      ? "_BaseColor"
                    : glass.HasProperty("_Color")          ? "_Color" : null;
        if (prop == null)
        {
            Debug.LogWarning("[RealCockpit] cabin glass material '" + glass.name + "' (" + glass.shader.name
                           + ") has no albedo property this code knows — windscreen tint NOT changed.");
            return;
        }
        Color was = glass.GetColor(prop);
        glass.SetColor(prop, glassColour);
        Debug.Log("[RealCockpit] cabin glass '" + glass.name + "' via " + on + ": " + prop + " "
                + was.ToString("F2") + " -> " + glassColour.ToString("F2") + " (shared by all panes).");
    }

    /// <summary>Cut the twin pedal mesh in two and drive each half from the rudder input.
    ///
    /// The original node's renderer is switched off rather than destroyed, so if the split
    /// ever fails the pedals are still drawn — the cockpit degrades to the static geometry
    /// it had before instead of losing its pedals entirely.</summary>
    void RigPedals(Transform model)
    {
        var node = FindDeep(model, pedalTwinNode);
        if (node == null)
        {
            Debug.LogWarning("[RealCockpit] " + pedalTwinNode + " (rudder pedals) not found — "
                           + "the rudder will have no cockpit feedback.");
            return;
        }

        // WHERE TO CUT, measured from the mesh rather than typed in. Each footwell's pair
        // is divided at the midpoint of that pair's own x extent, so a model revision that
        // moves the pedals still splits them in the right place instead of silently
        // slicing one pad in half.
        if (!PedalSplits(model, node, out float lCut, out float rCut))
        {
            Debug.LogWarning("[RealCockpit] could not measure the pedal groups in "
                           + pedalTwinNode + " — leaving the pedals static.");
            return;
        }

        // Only the PILOT's two pads are rebuilt. The co-pilot's pair is simply never
        // recreated, and disabling the original node's renderer takes all four out of the
        // scene — so "removing" the second set costs nothing and leaves no orphan geometry.
        pedalPilotL = SplitPedal(model, node, cx => cx <  lCut,            "PedalPilotLeft");
        pedalPilotR = SplitPedal(model, node, cx => cx >= lCut && cx < 0f, "PedalPilotRight");
        if (pedalPilotL == null || pedalPilotR == null)
        {
            Debug.LogWarning("[RealCockpit] could not split " + pedalTwinNode
                           + " into the pilot's two pedals — leaving them static.");
            return;
        }

        var r = node.GetComponent<Renderer>();
        if (r != null) r.enabled = false;      // both original pairs go with it

        // SLIDE THE PAIR ONTO THE CENTRELINE, the same move CentreGroup makes for the seat
        // and the yoke. lCut is the midpoint of the pilot pair's own drawn extent, so
        // shifting by -lCut puts the divider between his two pedals exactly on x = 0 and
        // leaves the pads straddling it — in front of the pilot's eye point, which is also
        // on the centreline, and in the space Object_58 used to occupy.
        var centre = new Vector3(-lCut, 0f, 0f);
        pedalPilotL.localPosition += centre;
        pedalPilotR.localPosition += centre;

        basePilotL = pedalPilotL.localPosition;
        basePilotR = pedalPilotR.localPosition;
        SetLayer(pedalPilotL, CockpitBuilder.CockpitLayer);
        SetLayer(pedalPilotR, CockpitBuilder.CockpitLayer);

        Debug.Log("[RealCockpit] rudder pedals rigged: the pilot's pair only, cut at x="
                + lCut.ToString("F4") + " (co-pilot pair at x=" + rCut.ToString("F4")
                + " dropped), slid " + (-lCut * 1000f).ToString("F1")
                + " mm onto the centreline; +-" + (pedalTravel * 1000f).ToString("F0") + " mm of travel.");
    }

    /// <summary>Find the x at which each footwell's pedal PAIR divides: the midpoint of
    /// that pair's own extent, measured from the drawn triangles.</summary>
    bool PedalSplits(Transform model, Transform node, out float leftCut, out float rightCut)
    {
        leftCut = rightCut = 0f;
        var mf = node.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        var verts = mf.sharedMesh.vertices;
        var tris  = mf.sharedMesh.triangles;
        if (tris.Length == 0) return false;

        float lMin = 9f, lMax = -9f, rMin = 9f, rMax = -9f;
        for (int i = 0; i < tris.Length; i += 3)
        {
            float cx = 0f;
            for (int k = 0; k < 3; k++)
                cx += model.InverseTransformPoint(node.TransformPoint(verts[tris[i + k]])).x;
            cx /= 3f;
            if (cx < 0f) { lMin = Mathf.Min(lMin, cx); lMax = Mathf.Max(lMax, cx); }
            else         { rMin = Mathf.Min(rMin, cx); rMax = Mathf.Max(rMax, cx); }
        }
        if (lMax < lMin || rMax < rMin) return false;
        leftCut  = (lMin + lMax) * 0.5f;
        rightCut = (rMin + rMax) * 0.5f;
        return true;
    }

    /// <summary>One pedal as its own object, keeping the triangles whose centroid satisfies
    /// <paramref name="keep"/>. The same cut KeepHalf makes, but it BUILDS a new object
    /// instead of editing in place, because all four pedals are needed and they have to
    /// move independently.</summary>
    Transform SplitPedal(Transform model, Transform node, System.Func<float, bool> keep, string name)
    {
        var srcMf = node.GetComponent<MeshFilter>();
        var srcMr = node.GetComponent<MeshRenderer>();
        if (srcMf == null || srcMf.sharedMesh == null) return null;

        var src = srcMf.sharedMesh;
        var verts = src.vertices;
        var tris  = src.triangles;
        var lx = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            lx[i] = model.InverseTransformPoint(node.TransformPoint(verts[i])).x;

        // COMPACT the halves — keep only the vertices the kept triangles actually use.
        //
        // Filtering triangles alone and leaving the vertex array whole is the obvious way
        // to do this and it is subtly wrong: Mesh.RecalculateBounds works from VERTICES,
        // not from triangles, so each half would report the bounds of the WHOLE twin mesh.
        // Every consumer of those bounds then lies — frustum culling, the reach and
        // clearance tests, anything asking where the pedal is. This project has already
        // been burnt once by reading bounds that spanned triangles that had been removed.
        var srcN = src.normals; var srcU = src.uv;
        bool hasN = srcN != null && srcN.Length == verts.Length;
        bool hasU = srcU != null && srcU.Length == verts.Length;

        var remap = new Dictionary<int, int>();
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>();
        var newTris = new List<int>(tris.Length);
        for (int i = 0; i < tris.Length; i += 3)
        {
            float cx = (lx[tris[i]] + lx[tris[i + 1]] + lx[tris[i + 2]]) / 3f;
            if (!keep(cx)) continue;
            for (int k = 0; k < 3; k++)
            {
                int vi = tris[i + k];
                if (!remap.TryGetValue(vi, out int mapped))
                {
                    mapped = nv.Count;
                    remap[vi] = mapped;
                    nv.Add(verts[vi]);
                    if (hasN) nn.Add(srcN[vi]);
                    if (hasU) nu.Add(srcU[vi]);
                }
                newTris.Add(mapped);
            }
        }
        if (newTris.Count == 0) return null;

        var mesh = new Mesh { name = name + "Mesh" };
        mesh.SetVertices(nv);
        if (hasN) mesh.SetNormals(nn);
        if (hasU) mesh.SetUVs(0, nu);
        mesh.SetTriangles(newTris, 0);
        if (!hasN) mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(node.parent, false);
        go.transform.localPosition = node.localPosition;
        go.transform.localRotation = node.localRotation;
        go.transform.localScale    = node.localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (srcMr != null) mr.sharedMaterials = srcMr.sharedMaterials;
        go.layer = node.gameObject.layer;
        return go.transform;
    }

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

    // ── CENTRE STRIP REMOVED (6 Oct 2026, on request) ─────────────────────────
    // The bezel mesh Object_83 is ONE mesh holding both displays' surrounds AND the
    // 27 mm strip between them (x -0.0255 .. -0.001, holder space, measured from the GLB:
    // PFD surround ends at -0.026, MFD surround starts at +0.0014). The strip is deleted
    // and everything of the right display — its surround (rest of Object_83), its glass
    // (the right quad of Object_88) and the LiveMFD quad — slides left by MfdShift so the
    // two displays sit side by side with a 3 mm seam. The freed space on the right becomes
    // the gap between the map and the standby dials (StandbyInstruments).
    // A backing plate in the panel's own material sits behind the display block, because the panel mesh
    // behind the bezel is not guaranteed to be closed where the strip and the old MFD
    // edge used to be.
    public const float StripX0 = -0.0255f, StripX1 = -0.001f, MfdShift = 0.024f;

    static void CloseCentreStrip(Transform model)
    {
        int dropped = 0, moved = 0;
        foreach (var name in new[] { "Object_83", "Object_88" })
        {
            var t = FindDeep(model, name);
            var mf = t != null ? t.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[RealCockpit] centre strip: " + name + " not found"); continue; }
            var mesh = Instantiate(mf.sharedMesh);
            var verts = mesh.vertices;
            var shiftSet = new bool[verts.Length];
            float ModelX(int i) => model.InverseTransformPoint(t.TransformPoint(verts[i])).x;
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var tris = mesh.GetTriangles(sm);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    float cx = (ModelX(tris[i]) + ModelX(tris[i + 1]) + ModelX(tris[i + 2])) / 3f;
                    if (cx > StripX0 && cx < StripX1) { dropped++; continue; }
                    if (cx >= StripX1) { shiftSet[tris[i]] = shiftSet[tris[i + 1]] = shiftSet[tris[i + 2]] = true; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                mesh.SetTriangles(keep, sm);
            }
            Vector3 d = t.InverseTransformVector(model.TransformVector(new Vector3(-MfdShift, 0f, 0f)));
            for (int i = 0; i < verts.Length; i++) if (shiftSet[i]) { verts[i] += d; moved++; }
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            mf.mesh = mesh;
        }

        // backing plate behind the display block, panel grey
        var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plate.name = "DisplayBacking";
        var col = plate.GetComponent<Collider>(); if (col) Destroy(col);
        plate.transform.SetParent(model, false);
        plate.transform.localPosition = new Vector3(-0.012f, 0.4921f, 0.7615f);
        plate.transform.localScale = new Vector3(0.215f, 0.066f, 1f);
        // wear the panel's own material, so wherever it shows it reads as more panel
        var panel = FindDeep(model, "Object_81");
        var pr = panel != null ? panel.GetComponent<Renderer>() : null;
        if (pr != null) plate.GetComponent<Renderer>().sharedMaterial = pr.sharedMaterial;
        else plate.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.25f, 0.25f, 0.26f) };
        plate.layer = CockpitBuilder.CockpitLayer;
        Debug.Log($"[RealCockpit] centre strip removed: {dropped} triangles dropped, {moved} vertices of the right display slid {MfdShift * 1000f:F0} mm left");
    }

    /// <summary>Re-colour the cabin trim. The model ships its side walls and door cards in
    /// saturated tan ("Interior", 0.75/0.51/0.31) and chocolate brown ("Interior_2"), which
    /// reads as cardboard in the pilot's peripheral view. A G1000 172's cabin is trimmed in
    /// neutral grey and stone. Only baseColorFactor changes — gltfast's albedo property;
    /// _Color would be a silent no-op (see ClearTheGlass).</summary>
    static void RetrimInterior(Transform model)
    {
        var map = new Dictionary<string, Color>
        {
            { "Interior",   new Color(0.58f, 0.56f, 0.52f, 1f) },   // stone side walls
            { "Interior_2", new Color(0.25f, 0.25f, 0.26f, 1f) },   // charcoal door cards / lower trim
        };
        var done = new HashSet<Material>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || done.Contains(m) || !map.TryGetValue(m.name, out Color c)) continue;
                if (!m.HasProperty("baseColorFactor")) continue;
                m.SetColor("baseColorFactor", c);
                done.Add(m);
                Debug.Log("[RealCockpit] interior trim '" + m.name + "' -> " + c);
            }
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

        // Which of the nodes are BLADES (long, thin) and which is the spinner: a blade's
        // extent across the disc is several times the spinner's (0.52 m vs 0.14 m here). Only blades get swapped
        // for the blur disc; the spinner keeps turning visibly, as it does in life.
        float radius = 0f;
        foreach (var t in nodes)
        {
            var r = t.GetComponent<Renderer>(); if (r == null) continue;
            float acrossWorld = Mathf.Max(r.bounds.size.x, r.bounds.size.y);
            if (acrossWorld < 0.3f) continue;   // blades measure ~0.5 m in the scaled model, the spinner 0.14 m
            propBlades.Add(r);
            // disc radius = the farthest blade vertex from the spin axis
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                foreach (var v in mf.sharedMesh.vertices)
                {
                    Vector3 lp = propPivot.InverseTransformPoint(t.TransformPoint(v));
                    radius = Mathf.Max(radius, new Vector2(lp.x, lp.y).magnitude * propPivot.lossyScale.x);
                }
            else radius = Mathf.Max(radius, acrossWorld * 0.5f);
        }
        if (propBlades.Count > 0) BuildPropDisc(radius);
        Debug.Log($"[RealCockpit] prop: {propBlades.Count} blade renderer(s) of {nodes.Count} nodes, disc radius {radius:F2} m");
    }

    // ── PROPELLER BLUR ────────────────────────────────────────────────────────
    // A turning propeller is never seen as a blade: at 700-2700 rpm the eye (and any
    // camera at 60-90 fps) sees a faint, tinted disc with a brighter ring where the tip
    // stripes are. Drawing the blade rotating at the true rate strobes into a slowly
    // wandering blade — the "static prop" the old cockpit showed in flight. So above a
    // few hundred rpm the blades are hidden and a translucent disc is shown instead;
    // below that (engine stopped or windmilling slowly) the real blade turns.
    readonly List<Renderer> propBlades = new List<Renderer>();
    Transform propDisc; Material propDiscMat;
    float propAngle; Quaternion propDiscBase;

    void BuildPropDisc(float radius)
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        var rnd = new System.Random(7);
        var streak = new float[64];
        for (int i = 0; i < 64; i++) streak[i] = (float)rnd.NextDouble();
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;
                float a = 0f; Color c = new Color(0.10f, 0.10f, 0.11f);
                if (r < 1f && r > 0.12f)
                {
                    // blade chord shadow, heavier inboard, fading at the tip
                    a = Mathf.Lerp(0.11f, 0.05f, r);
                    // two painted tip stripes -> two faint bright rings
                    if ((r > 0.86f && r < 0.905f) || (r > 0.93f && r < 0.965f)) { c = new Color(0.95f, 0.95f, 0.92f); a = 0.07f; }
                    // subtle angular shimmer
                    a *= 0.92f + 0.16f * streak[Mathf.FloorToInt(ang * 64f) % 64];
                    a *= Mathf.Clamp01((1f - r) * 30f);   // soft outer edge
                }
                c.a = a;
                px[y * N + x] = c;
            }
        tex.SetPixels(px); tex.Apply(true);

        var m = new Mesh { name = "PropDisc" };
        m.vertices = new[] { new Vector3(-radius, -radius, 0f), new Vector3(-radius, radius, 0f), new Vector3(radius, radius, 0f), new Vector3(radius, -radius, 0f) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };   // double-sided
        m.RecalculateBounds();
        var go = new GameObject("PropDisc");
        go.layer = CockpitBuilder.CockpitLayer;
        propDisc = go.transform;
        propDisc.SetParent(propPivot.parent, false);
        propDisc.position = propPivot.position;
        propDisc.rotation = propPivot.rotation;
        propDiscBase = propDisc.localRotation;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        propDiscMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = propDiscMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
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

        if (pedalPilotL != null && pedalPilotR != null)
        {
            // RUDDER -> the pedals slide fore and aft, differentially, exactly as a rudder
            // bar moves them: right rudder pushes the RIGHT pedal away from the pilot (+Z
            // is toward the panel) and brings the LEFT one back. Eased on the same
            // frame-rate-independent law as the yoke, and self-centring because yawInput
            // returns to zero when the key is released.
            float kp = 1f - Mathf.Exp(-pedalSmooth * Time.deltaTime);
            curPedal = Mathf.Lerp(curPedal, Mathf.Clamp(phys.yawInput, -1f, 1f), kp);
            float d = pedalSign * curPedal * pedalTravel;
            pedalPilotR.localPosition = basePilotR + Vector3.forward * d;
            pedalPilotL.localPosition = basePilotL - Vector3.forward * d;
        }

        if (propPivot != null)
        {
            // True engine speed where the systems model exists (idle 700 rpm with the
            // throttle closed, windmilling after a failure); throttle-scaled otherwise.
            var sys = AircraftSystems.Instance;
            float rpm = sys != null ? sys.RPM : phys.Throttle01 * propSpinMax / 6f;
            bool blur = propDisc != null && rpm > 300f;
            if (propDisc != null && propDisc.gameObject.activeSelf != blur)
            {
                propDisc.gameObject.SetActive(blur);
                foreach (var r in propBlades) if (r != null) r.enabled = !blur;
            }
            if (blur)
            {
                // the disc turns slowly so the shimmer lives; denser at high rpm
                propAngle += Time.deltaTime * 90f;
                propDisc.localRotation = propDiscBase * Quaternion.Euler(0f, 0f, propAngle);
                propDiscMat.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(300f, 2400f, rpm)));
                propPivot.Rotate(0f, 0f, 1500f * Time.deltaTime, Space.Self);   // spinner
            }
            else propPivot.Rotate(0f, 0f, rpm * 6f * Time.deltaTime, Space.Self);
        }
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
