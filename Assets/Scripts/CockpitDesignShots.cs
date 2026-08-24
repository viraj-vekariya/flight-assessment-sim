// CockpitDesignShots — renders the cockpit from the PILOT'S EYE for design review.
//
// Dev/documentation tool. Runs ONLY with -designshots; never ships and never touches
// experiment data.
//
// Why this exists: the control battery proves the levers are WIRED correctly. It says
// nothing about whether they look like aircraft controls, whether a pilot can tell them
// apart, or whether anything reads as a debug object. That needs eyes on a picture taken
// from where the participant's head actually is.
//
// HOW STATE IS SET
//   By driving the AEROPLANE, never the control. Every cockpit control follows the
//   aircraft when it is not in a hand (PhysicalControl.FollowAircraft), so setting the
//   control directly is both a lie and, for the spring-loaded brake, a no-op that made
//   the "brake full" picture identical to the "brake off" one. Driving the aeroplane and
//   photographing what the cockpit does is the only version of this that can catch a
//   control that fails to follow.
//
//   Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunDesignShots \
//         -designshots -logFile design.log
//
// NOTE: no -nographics (the PFD/MFD render to off-screen cameras) and no -quit.

using System.Collections;
using System.IO;
using UnityEngine;

public class CockpitDesignShots : MonoBehaviour
{
    public static bool Finished { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-designshots") { new GameObject("CockpitDesignShots").AddComponent<CockpitDesignShots>(); return; }
    }

    const int W = 1600, H = 1000;
    string dir;
    Camera shotCam;
    float trueFov = 78f;   // the real cockpit camera's fov, read in BuildCam
    Transform eye;
    CockpitControlRig rig;
    CessnaPhysics ac;
    AircraftController ctl;

    // Held aircraft state. Re-asserted every frame, because the override model expires
    // after GraceFrames and the keyboard path would otherwise take the axis back.
    float holdThrottle, holdBrake, holdPitch, holdRoll;
    bool holding;

    IEnumerator Start()
    {
        dir = Path.Combine(Application.persistentDataPath, "CockpitDesign");
        Directory.CreateDirectory(dir);

        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Debug.LogError("[DESIGN] no GameManager"); Done(); yield break; }
            yield return null;
        }
        var gm = GameManager.Instance;
        if (!ParticipantManager.IsSet) ParticipantManager.SetID("DESIGN");
        gm.SetParticipantReady();

        yield return new WaitForSecondsRealtime(14f);       // GLB cockpit load + rig build
        rig = CockpitControlRig.Instance;
        if (rig == null) { Debug.LogError("[DESIGN] no CockpitControlRig — GLB did not load"); Done(); yield break; }

        ac  = gm.Aircraft;
        ctl = ac.GetComponent<AircraftController>();

        ControlCheckMode.Enter();
        yield return new WaitForSecondsRealtime(0.5f);
        ac.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        yield return new WaitForSecondsRealtime(0.5f);

        BuildCam();
        if (shotCam == null) { Debug.LogError("[DESIGN] no CockpitCamera"); Done(); yield break; }

        holding = true;
        StartCoroutine(HoldLoop());

        // ── 00 the view the participant actually gets ────────────────────────────
        yield return Shot(Vector3.zero, Vector3.zero, -1f, "00_pilot_view.png");

        // UPPER and LOWER, asked for explicitly: what is above the glareshield and what is
        // in the footwell are both parts of the cockpit a seated participant can see by
        // looking, and neither had ever been rendered.
        yield return Shot(Vector3.zero, new Vector3(-30f, 0f, 0f), -1f, "00_view_up.png");
        yield return Shot(Vector3.zero, new Vector3( 40f, 0f, 0f), -1f, "00_view_down.png");
        yield return Shot(Vector3.zero, new Vector3( 12f, -42f, 0f), -1f, "00_view_left.png");
        yield return Shot(Vector3.zero, new Vector3( 12f,  42f, 0f), -1f, "00_view_right.png");

        // The panel on its own, square-on, which is how a panel layout is judged.
        yield return Shot(Vector3.zero, new Vector3(14f, 0f, 0f), 46f, "00_panel.png");

        // Looking down-right at the quadrant — where a pilot's eye goes for power.
        yield return Shot(Vector3.zero, new Vector3(33f, 27f, 0f), 46f, "03_reach.png");

        // ── 01 THROTTLE, both ends, driving the AEROPLANE ───────────────────────
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f);
        yield return CloseUp("throttle", 0.17f, 0.02f, -0.05f, 42f, "01_throttle_idle.png");
        yield return SetState(throttle: 1f, flapDetent: 0, brake: 0f);
        yield return CloseUp("throttle", 0.22f, 16f, -16f, 40f, "01_throttle_full.png");

        // ── 01 FLAPS, every detent the aeroplane has ────────────────────────────
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f);
        yield return CloseUp("flaps", 0.16f, 0.02f, -0.06f, 42f, "01_flaps_up.png");
        yield return SetState(throttle: 0f, flapDetent: 1, brake: 0f, settle: 3.5f);
        yield return CloseUp("flaps", 0.20f, 16f, -16f, 40f, "01_flaps_10.png");
        yield return SetState(throttle: 0f, flapDetent: 2, brake: 0f, settle: 4.5f);
        yield return CloseUp("flaps", 0.20f, 16f, -16f, 40f, "01_flaps_full.png");
        // The whole quadrant with flaps down and power up, so the two are seen disagreeing
        // — which is the only way to tell they are separate controls.
        yield return SetState(throttle: 1f, flapDetent: 2, brake: 0f, settle: 1.5f);
        yield return CloseUp("throttle", 0.26f, 0.03f, -0.05f, 52f, "01_quadrant_split.png");
        yield return CloseUp("throttle", 0.20f, 0.00f, -0.10f, 44f, "01_quadrant_side.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f, settle: 4.5f);

        // ── 02 BRAKE and the pedals it drives ───────────────────────────────────
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f);
        yield return CabinShot(new Vector3(0f, 0.50f, 0.50f), new Vector3(0f, 0.288f, 0.778f), 48f, "02_pedals_off.png");
        yield return CloseUp("brake", 0.17f, 0.02f, 0.05f, 42f, "02_brake_off.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 1f);
        yield return CabinShot(new Vector3(0f, 0.50f, 0.50f), new Vector3(0f, 0.288f, 0.778f), 48f, "02_pedals_full.png");
        yield return CloseUp("brake", 0.22f, 18f, 18f, 40f, "02_brake_full.png");
        // Wide, from behind the seat, so a pedal that leaves the footwell is unmissable.
        yield return CabinShot(new Vector3(-0.30f, 0.52f, 0.33f), new Vector3(0f, 0.288f, 0.778f), 55f, "02_pedals_full_wide.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f);
        yield return CabinShot(new Vector3(-0.30f, 0.52f, 0.33f), new Vector3(0f, 0.288f, 0.778f), 55f, "02_pedals_off_wide.png");

        // ── 05 YOKE at the ends of its travel ───────────────────────────────────
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f, pitch: 0f, roll: 0f);
        yield return Shot(Vector3.zero, new Vector3(30f, 0f, 0f), 50f, "05_yoke_neutral.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f, pitch: 1f, roll: 0f);
        yield return Shot(Vector3.zero, new Vector3(30f, 0f, 0f), 50f, "05_yoke_full_down.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f, pitch: -1f, roll: 0f);
        yield return Shot(Vector3.zero, new Vector3(30f, 0f, 0f), 50f, "05_yoke_full_up.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f, pitch: 0f, roll: -1f);
        yield return Shot(Vector3.zero, new Vector3(30f, 0f, 0f), 50f, "05_yoke_full_left.png");
        yield return SetState(throttle: 0f, flapDetent: 0, brake: 0f, pitch: 0f, roll: 0f);

        // ── 04 the displays, framed so the legends can be read ──────────────────
        yield return Shot(Vector3.zero, new Vector3(16f, 11f, 0f), 22f, "04_mfd.png");
        yield return Shot(Vector3.zero, new Vector3(16f, -8f, 0f), 22f, "04_pfd.png");
        // The systems bay on the left, which no picture had ever shown on its own.
        yield return CloseUp("fuel_selector", 0.17f, 0.05f, 0.00f, 44f, "04_pedestal.png");
        yield return CloseUp("trim", 0.15f, 0.03f, -0.02f, 44f, "04_trim.png");
        yield return CloseUp("load_shed", 0.17f, 0.02f, 0.06f, 44f, "04_switch_bank.png");

        holding = false;
        IdentifyRenderers();
        yield return WriteGeometryReport();
        Debug.Log("[DESIGN] wrote shots to " + dir);
        ControlCheckMode.Exit();
        Done();
    }

    // ── driving the aeroplane ───────────────────────────────────────────────────

    /// <summary>Re-asserts the held state every frame. The override model expires after a
    /// grace of one frame by design, so a state set once and photographed later would have
    /// quietly reverted to whatever the keyboard path decided.</summary>
    IEnumerator HoldLoop()
    {
        while (holding)
        {
            if (ctl != null)
            {
                ctl.SetThrottle(holdThrottle);
                ctl.SetBrake(holdBrake);
                ctl.SetPitch(holdPitch);
                ctl.SetRoll(holdRoll);
            }
            yield return null;
        }
    }

    IEnumerator SetState(float throttle, int flapDetent, float brake,
                         float pitch = 0f, float roll = 0f, float settle = 1.2f)
    {
        holdThrottle = throttle; holdBrake = brake; holdPitch = pitch; holdRoll = roll;
        if (ctl != null) ctl.SetFlapDetent(flapDetent);
        yield return new WaitForSecondsRealtime(settle);
    }

    /// <summary>Names every cockpit renderer that occupies a meaningful part of the
    /// pilot's view, with its screen-space box. This is how an unexplained object gets
    /// identified — "there is a black bar across the windscreen" becomes a mesh name.</summary>
    void IdentifyRenderers()
    {
        // BACK TO THE PILOT'S EYE FIRST. CloseUp() detaches the camera to aim it at a
        // control, so without this the "pilot-view occupancy" table was computed from the
        // WORLD ORIGIN — several hundred metres away, with the whole cockpit behind the
        // camera. It reported one visible renderer and looked like a working tool.
        shotCam.transform.SetParent(eye, false);
        shotCam.transform.localPosition = Vector3.zero;
        shotCam.transform.localRotation = Quaternion.identity;
        shotCam.fieldOfView = trueFov;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("PILOT-VIEW OCCUPANCY  (normalised screen box, 0..1; REAL cockpit camera fov)");
        sb.AppendLine("Visible means 0..1 in BOTH axes.");
        sb.AppendLine("elev/azim are the angles from the pilot's eye to the object centre, in degrees.");
        sb.AppendLine("name                                     xmin  xmax  ymin  ymax   elev  azim");
        var rows = new System.Collections.Generic.List<string>();

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            var b = r.bounds;
            float xmin = 9e9f, xmax = -9e9f, ymin = 9e9f, ymax = -9e9f;
            bool anyFront = false;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var sp = shotCam.WorldToViewportPoint(corner);
                if (sp.z <= 0f) continue;
                anyFront = true;
                xmin = Mathf.Min(xmin, sp.x); xmax = Mathf.Max(xmax, sp.x);
                ymin = Mathf.Min(ymin, sp.y); ymax = Mathf.Max(ymax, sp.y);
            }
            if (!anyFront) continue;
            string path = NodePath(r.transform);
            bool isControl = path.Contains("Ctl_") || path.Contains("Object_52")
                          || path.Contains("ControlArea") || path.Contains("YokeVisual");
            float w = xmax - xmin, h = ymax - ymin;
            if (!isControl)
            {
                if (w < 0.02f || h < 0.02f) continue;                 // too small to matter
                if (xmax < 0f || xmin > 1f || ymax < 0f || ymin > 1f) continue;   // off screen
            }
            Vector3 toC = shotCam.transform.InverseTransformPoint(b.center);
            float elev = Mathf.Atan2(toC.y, toC.z) * Mathf.Rad2Deg;
            float azim = Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg;
            rows.Add(string.Format("{0,-40} {1,5:0.00} {2,5:0.00} {3,5:0.00} {4,5:0.00} {5,6:0.0} {6,5:0.0}",
                                   path, xmin, xmax, ymin, ymax, elev, azim));
        }
        rows.Sort();
        foreach (var r in rows) sb.AppendLine(r);
        File.WriteAllText(System.IO.Path.Combine(dir, "pilot_view_occupancy.txt"), sb.ToString());
        Debug.Log("[DESIGN]   wrote pilot_view_occupancy.txt (" + rows.Count + " renderers)");
    }

    /// <summary>Dimensions, in metres, of the things a layout decision depends on: where
    /// the panel face is, how big the controls are, and where the pedals sit. Guessing any
    /// of these is how a control ends up inside the panel or a hinge ends up a metre from
    /// the part it hinges.</summary>
    IEnumerator WriteGeometryReport()
    {
        var model = rig.transform;
        var mdl = GameObject.Find("RealCockpitModel");
        Transform root = mdl != null ? mdl.transform : model;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("COCKPIT GEOMETRY, model-local metres (+Z toward the panel, +Y up, +X right)");
        sb.AppendLine();
        sb.AppendLine("CONTROLS");
        sb.AppendLine("id              localPos                    visual rest             capture");
        foreach (var c in rig.Controls)
        {
            if (c == null) continue;
            Vector3 lp = root.InverseTransformPoint(c.transform.position);
            string vis = c.spec.visual != null
                ? c.spec.visual.localPosition.ToString("0.0000") : "(none)";
            sb.AppendLine(string.Format("{0,-14} {1,-26} {2,-22} {3:0.000}",
                                        c.spec.id, lp.ToString("0.0000"), vis, c.spec.captureRadius));
        }

        sb.AppendLine();
        sb.AppendLine("KEY NODES (world-axis-aligned bounds, then centre in model-local)");
        foreach (string n in new[] { "Object_52", "YokeVisual", "YokeRoot", "PedalPivot", "ControlArea" })
        {
            var t = FindDeepStatic(root, n);
            if (t == null) { sb.AppendLine(string.Format("{0,-14} MISSING", n)); continue; }
            var rends = t.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                sb.AppendLine(string.Format("{0,-14} no renderer, localPos {1}", n, t.localPosition.ToString("0.0000")));
                continue;
            }
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            sb.AppendLine(string.Format("{0,-14} size {1}  centre(model) {2}",
                                        n, b.size.ToString("0.000"),
                                        root.InverseTransformPoint(b.center).ToString("0.0000")));
        }

        // EVERY GLB node, in the cockpit's own axes. Placing generated furniture against
        // a model you have not measured is guesswork, and guesswork is how a control ends
        // up overhanging the edge of the panel it is supposed to be mounted on.
        sb.AppendLine();
        sb.AppendLine("GLB NODES with geometry, model-local metres, sorted by volume");
        sb.AppendLine("name                       xmin   xmax   ymin   ymax   zmin   zmax");
        var nodes = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.name.StartsWith("Ctl_")) continue;
            Bounds db;
            if (!DrawnBox(root, r, out db)) continue;
            Vector3 lo = db.min, hi = db.max;
            Vector3 sz = hi - lo;
            float vol = sz.x * sz.y * sz.z;
            nodes.Add(new System.Collections.Generic.KeyValuePair<float, string>(-vol,
                string.Format("{0,-24} {1,6:0.000} {2,6:0.000} {3,6:0.000} {4,6:0.000} {5,6:0.000} {6,6:0.000}",
                              r.transform.name, lo.x, hi.x, lo.y, hi.y, lo.z, hi.z)));
        }
        nodes.Sort((a2, b2) => a2.Key.CompareTo(b2.Key));
        int shown = 0;
        foreach (var kv in nodes) { sb.AppendLine(kv.Value); if (++shown >= 70) break; }

        // ── PANEL DEPTH MAP ───────────────────────────────────────────────────────
        // Where the panel actually IS, and which mesh it is. Node bounds say what a mesh
        // spans; they do not say which mesh a pilot's hand would meet first at a given spot
        // on the panel, and that is the question a control's position depends on.
        //
        // Computed from the model-space boxes rather than by raycasting. Raycasting was the
        // first approach and it returned "nothing there" for all 288 samples even with
        // temporary colliders and a synced physics scene — and a measurement tool whose
        // failure mode is a confident blank grid is worse than no tool. Boxes cannot lie in
        // that direction: if a box contains the sample point, geometry is there.
        var boxes = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Bounds>>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds bb;
            if (!DrawnBox(root, r, out bb)) continue;
            boxes.Add(new System.Collections.Generic.KeyValuePair<string, Bounds>(r.transform.name, bb));
        }

        // ── WHAT IS IN THE YOKE VOLUME ────────────────────────────────────────────
        // Anything drawn in the box the control wheel occupies. "There are two grey pegs
        // hanging under the yoke and I do not know what they are" is a question a tool
        // should answer in one line, not a thing to be inferred from screenshots.
        sb.AppendLine();
        sb.AppendLine("DRAWN GEOMETRY INSIDE THE YOKE VOLUME  (x +-0.09, y 0.33..0.50, z 0.60..0.78)");
        var yokeBox = new Bounds(new Vector3(0f, 0.415f, 0.690f), new Vector3(0.180f, 0.170f, 0.180f));
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds yb;
            if (!DrawnBox(root, r, out yb)) continue;
            if (!yb.Intersects(yokeBox)) continue;
            sb.AppendLine(string.Format("   {0,-22} {1}  size {2}",
                r.transform.name, yb.center.ToString("0.000"), yb.size.ToString("0.000")));
        }

        sb.AppendLine();
        sb.AppendLine("PANEL DEPTH MAP — front-most geometry at each point of the panel, model-local");
        sb.AppendLine("Upper grid: the z of the nearest surface (smaller z = closer to the pilot).");
        sb.AppendLine("Lower grid: which mesh that is, keyed below. '.' = nothing at all there.");
        var keyOf = new System.Collections.Generic.Dictionary<string, char>();
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var zGrid = new System.Text.StringBuilder();
        var nGrid = new System.Text.StringBuilder();

        zGrid.Append("   y \\ x ");
        nGrid.Append("   y \\ x ");
        for (float x = -0.170f; x <= 0.171f; x += 0.020f)
        { zGrid.Append(string.Format("{0,7:0.00}", x)); nGrid.Append(string.Format("{0,3:0}", x * 100f)); }
        zGrid.AppendLine(); nGrid.AppendLine();

        for (float y = 0.560f; y >= 0.249f; y -= 0.020f)
        {
            zGrid.Append(string.Format("{0,8:0.000}", y));
            nGrid.Append(string.Format("{0,8:0.000}", y));
            for (float x = -0.170f; x <= 0.171f; x += 0.020f)
            {
                string best = null; float bestZ = 9e9f;
                foreach (var kv in boxes)
                {
                    var bb = kv.Value;
                    if (x < bb.min.x || x > bb.max.x || y < bb.min.y || y > bb.max.y) continue;
                    if (bb.min.z < 0.60f) continue;          // behind the pilot, not panel
                    if (bb.min.z < bestZ) { bestZ = bb.min.z; best = kv.Key; }
                }
                if (best == null) { zGrid.Append("      ."); nGrid.Append("  ."); continue; }
                zGrid.Append(string.Format("{0,7:0.000}", bestZ));
                char ch;
                if (!keyOf.TryGetValue(best, out ch))
                { ch = alphabet[Mathf.Min(keyOf.Count, alphabet.Length - 1)]; keyOf[best] = ch; }
                nGrid.Append("  " + ch);
            }
            zGrid.AppendLine(); nGrid.AppendLine();
        }
        sb.Append(zGrid.ToString());
        sb.AppendLine();
        sb.Append(nGrid.ToString());
        sb.AppendLine();
        sb.AppendLine("KEY");
        foreach (var kv in keyOf) sb.AppendLine("   " + kv.Value + " = " + kv.Key);

        sb.AppendLine();
        sb.AppendLine("DISPLAY QUADS");
        foreach (var rr in root.GetComponentsInChildren<Renderer>(true))
        {
            string nm = rr.transform.name;
            if (nm.IndexOf("PFD", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                nm.IndexOf("MFD", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                nm.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            Vector3 c2 = root.InverseTransformPoint(rr.bounds.center);
            sb.AppendLine(string.Format("{0,-18} centre(model) {1}  worldSize {2}",
                nm, c2.ToString("0.0000"), rr.bounds.size.ToString("0.0000")));
        }

        sb.AppendLine();
        sb.AppendLine("HOLDER lossyScale: " + root.lossyScale.ToString("0.0000"));
        sb.AppendLine("EYE POINT (model-local): " + root.InverseTransformPoint(eye.position).ToString("0.0000"));
        File.WriteAllText(Path.Combine(dir, "cockpit_geometry.txt"), sb.ToString());
        Debug.Log("[DESIGN]   wrote cockpit_geometry.txt");
        yield break;
    }


    /// <summary>Model-local box of the geometry a renderer ACTUALLY DRAWS.
    ///
    /// Not the same thing as the mesh's bounds. RealCockpit turns this twin-seat model into
    /// a single-pilot cockpit by deleting triangles — the co-pilot's yoke, seat and controls
    /// — but the vertices stay in the buffer, so `sharedMesh.bounds` still spans both halves
    /// of the cabin. Reading those bounds is what produced the confident and wrong conclusion
    /// that the yoke sits 63 mm right of the pilot and covers the right-hand panel. It does
    /// not: the drawn wheel is 94 mm across, centred on the eye, exactly as RealCockpit's own
    /// log says.
    ///
    /// Returns false when the renderer draws nothing at all.</summary>
    static bool DrawnBox(Transform root, Renderer r, out Bounds box)
    {
        box = new Bounds();
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        var mesh = mf.sharedMesh;
        Vector3 lo = new Vector3(9e9f, 9e9f, 9e9f), hi = -lo;
        bool any = false;

        if (!mesh.isReadable)
        {
            // Cannot look at the triangles; fall back to the whole-mesh box rather than
            // silently dropping the object.
            Bounds mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x,
                                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 lp0 = root.InverseTransformPoint(r.transform.TransformPoint(c));
                lo = Vector3.Min(lo, lp0); hi = Vector3.Max(hi, lp0);
            }
            box.SetMinMax(lo, hi);
            return true;
        }

        var verts = mesh.vertices;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            var tris = mesh.GetTriangles(sub);
            for (int i = 0; i < tris.Length; i++)
            {
                Vector3 lp = root.InverseTransformPoint(r.transform.TransformPoint(verts[tris[i]]));
                if (!any) { lo = hi = lp; any = true; }
                else { lo = Vector3.Min(lo, lp); hi = Vector3.Max(hi, lp); }
            }
        }
        if (!any) return false;
        box.SetMinMax(lo, hi);
        return true;
    }

    static Transform FindDeepStatic(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        { var f = FindDeepStatic(root.GetChild(i), name); if (f != null) return f; }
        return null;
    }

    static string NodePath(Transform t)
    {
        string s = t.name;
        for (var p = t.parent; p != null && s.Length < 60; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    void BuildCam()
    {
        var cockCam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None),
                                        c => c.name == "CockpitCamera");
        if (cockCam == null) return;
        eye = cockCam.transform;
        var g = new GameObject("DesignShotCam");
        shotCam = g.AddComponent<Camera>();
        shotCam.CopyFrom(cockCam);          // same culling as the pilot sees — never ~0
        trueFov = cockCam.fieldOfView;
        shotCam.enabled = false;
        g.transform.SetParent(eye, false);
    }

    /// <summary>Offset and look-angle are relative to the PILOT'S EYE, so every picture
    /// is one a participant could actually have by leaning and looking.</summary>
    IEnumerator Shot(Vector3 localOffset, Vector3 localEuler, float fov, string file)
    {
        shotCam.transform.SetParent(eye, false);
        shotCam.transform.localPosition = localOffset;
        shotCam.transform.localRotation = Quaternion.Euler(localEuler);
        // fov <= 0 means "use the cockpit camera's OWN field of view", which is 78 deg
        // vertical (CockpitBuilder). A verification tool that does not reproduce the thing
        // being verified is worse than no tool.
        shotCam.fieldOfView = fov > 0f ? fov : trueFov;
        yield return Capture(file);
    }

    Transform Root
    {
        get
        {
            var mdl = GameObject.Find("RealCockpitModel");
            return mdl != null ? mdl.transform : rig.transform;
        }
    }

    /// <summary>A shot from a point in the CABIN, aimed at a point in the cabin. Both are
    /// model-local, so the framing is stated in the same numbers the cockpit is built in
    /// and cannot drift. Used for views the pilot's own head cannot give — chiefly the
    /// footwell, where a mis-hinged pedal leaves the cabin and no forward view would
    /// ever show it.</summary>
    IEnumerator CabinShot(Vector3 fromLocal, Vector3 atLocal, float fov, string file)
    {
        Transform root = Root;
        shotCam.transform.SetParent(root, false);
        shotCam.transform.localPosition = fromLocal;
        shotCam.transform.LookAt(root.TransformPoint(atLocal), root.up);
        shotCam.fieldOfView = fov;
        yield return Capture(file);
    }

    /// <summary>Frame one named control from inside the cabin.
    ///
    /// The camera is placed by walking BACK FROM THE CONTROL TOWARD THE PILOT'S EYE and
    /// then nudging sideways and up for a three-quarter view — after which it is clamped
    /// into the cabin. Both parts matter. An earlier version took a distance and a pair of
    /// Euler angles, and for a control on the right of the panel the sign of the azimuth
    /// put the camera at x = 0.203, which is outside a cabin 0.18 half-wide: the "close-up
    /// of the flap lever" was a photograph of the outside of the fuselage with the placards
    /// showing through it. A framing rule that can leave the aeroplane is not a framing
    /// rule.</summary>
    IEnumerator CloseUp(string controlId, float distance, float upOffset, float sideOffset,
                        float fov, string file)
    {
        Transform root = Root;
        Vector3 target = Vector3.zero;
        bool found = false;
        foreach (var c in rig.Controls)
            if (c != null && c.spec.id == controlId) { target = c.transform.position; found = true; break; }
        if (!found)
        {
            Debug.LogWarning("[DESIGN] no control '" + controlId + "' — skipping " + file);
            yield break;
        }

        Vector3 targetL = root.InverseTransformPoint(target);
        Vector3 eyeL = root.InverseTransformPoint(eye.position);
        Vector3 toEye = (eyeL - targetL).normalized;
        Vector3 camL = targetL + toEye * distance
                     + new Vector3(sideOffset, upOffset, 0f);

        // INSIDE THE CABIN, always. Measured extents: the cabin is x +-0.18, the seat is
        // around z 0.33 and the panel z 0.76, and the eye is at y 0.58.
        camL.x = Mathf.Clamp(camL.x, -0.125f, 0.125f);
        camL.y = Mathf.Clamp(camL.y, 0.300f, 0.620f);
        camL.z = Mathf.Clamp(camL.z, 0.400f, 0.720f);

        shotCam.transform.SetParent(root, false);
        shotCam.transform.localPosition = camL;
        shotCam.transform.LookAt(target, root.up);
        shotCam.fieldOfView = fov;
        yield return Capture(file);
    }

    IEnumerator Capture(string file)
    {
        var rt = new RenderTexture(W, H, 24);
        shotCam.targetTexture = rt;
        shotCam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        shotCam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Destroy(tex); Destroy(rt);
        Debug.Log("[DESIGN]   wrote " + file);
        yield return null;
    }

    void Done() { Finished = true; }
}
