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
// It renders:
//   00_pilot_view        the whole cockpit as the participant first sees it
//   01_quadrant_*        the three-lever quadrant, at each end of its travel
//   02_brake_*           the pedal area, off and full
//   03_reach             a wider framing that shows yoke + quadrant together
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

        ControlCheckMode.Enter();
        yield return new WaitForSecondsRealtime(0.5f);
        gm.Aircraft.ResetTo(gm.Runway.Start, gm.Runway.Rot, false, 0f);
        yield return new WaitForSecondsRealtime(0.5f);

        BuildCam();
        if (shotCam == null) { Debug.LogError("[DESIGN] no CockpitCamera"); Done(); yield break; }

        // ── the view the participant actually gets ────────────────────────────────
        yield return Shot(Vector3.zero, Vector3.zero, -1f, "00_pilot_view.png");   // the REAL pilot view

        // Looking down-right at the quadrant, which is where a pilot's eye goes when
        // reaching for power.
        yield return Shot(Vector3.zero, new Vector3(33f, 27f, 0f), 46f, "03_reach.png");

        // ── quadrant, both ends of travel ────────────────────────────────────────
        SetAll(0f);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Shot(Vector3.zero, new Vector3(40f, 27f, 0f), 30f, "01_quadrant_idle.png");

        SetAll(1f);
        yield return new WaitForSecondsRealtime(0.9f);
        yield return Shot(Vector3.zero, new Vector3(40f, 27f, 0f), 30f, "01_quadrant_full.png");

        // straight-on, so proportions and spacing can be judged without perspective
        yield return Shot(new Vector3(0.09f, -0.03f, 0.02f), new Vector3(38f, 6f, 0f), 34f, "01_quadrant_side.png");

        SetAll(0f);
        yield return new WaitForSecondsRealtime(0.6f);

        // ── brake / pedal area ───────────────────────────────────────────────────
        yield return Shot(new Vector3(0f, -0.02f, 0.02f), new Vector3(45f, 0f, 0f), 40f, "02_brake_off.png");
        var brake = Find("brake");
        if (brake != null) { brake.SetSilently(1f); yield return new WaitForSecondsRealtime(0.5f); }
        yield return Shot(new Vector3(0f, -0.02f, 0.02f), new Vector3(45f, 0f, 0f), 40f, "02_brake_full.png");
        // Tight framing from a leaned-forward head, which is how a pilot actually looks at
        // the pedals: they live in the footwell under the panel, not in the seated view.
        yield return Shot(Vector3.zero, new Vector3(44.6f, 0f, 0f), 30f, "02_pedals_full.png");
        yield return Shot(Vector3.zero, new Vector3(38f, -20f, 0f), 30f, "02_brake_handle_full.png");
        if (brake != null) { brake.SetSilently(0f); yield return new WaitForSecondsRealtime(0.5f); }
        yield return Shot(Vector3.zero, new Vector3(44.6f, 0f, 0f), 30f, "02_pedals_off.png");
        yield return Shot(Vector3.zero, new Vector3(38f, -20f, 0f), 30f, "02_brake_handle_off.png");
        // The MFD, framed on its own so the compass labels can be read.
        yield return Shot(Vector3.zero, new Vector3(16f, 11f, 0f), 22f, "04_mfd.png");

        IdentifyRenderers();
        Debug.Log("[DESIGN] wrote shots to " + dir);
        ControlCheckMode.Exit();
        Done();
    }

    /// <summary>Names every cockpit renderer that occupies a meaningful part of the
    /// pilot's view, with its screen-space box. This is how an unexplained object gets
    /// identified — "there is a black bar across the windscreen" becomes a mesh name.</summary>
    void IdentifyRenderers()
    {
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
            // Cockpit CONTROLS are always reported, even when they fall outside the
            // forward view — where they sit relative to the eye is exactly the thing being
            // measured, and filtering them out is how "the levers are invisible" went
            // unnoticed in the first place.
            bool isControl = path.Contains("Ctl_") || path.Contains("Object_52")
                          || path.Contains("ControlQuadrant") || path.Contains("YokeVisual");
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

    static string NodePath(Transform t)
    {
        string s = t.name;
        for (var p = t.parent; p != null && s.Length < 60; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    void SetAll(float v)
    {
        var th = Find("throttle"); if (th != null) th.SetSilently(v);
        var fl = Find("flaps");    if (fl != null) fl.SetSilently(v, v > 0.5f ? 2 : 0);
        var sp = Find("spoiler");  if (sp != null) sp.SetSilently(v, v > 0.5f ? 2 : 0);
    }

    PhysicalControl Find(string id)
    {
        foreach (var c in rig.Controls) if (c != null && c.spec.id == id) return c;
        return null;
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
        shotCam.transform.localPosition = localOffset;
        shotCam.transform.localRotation = Quaternion.Euler(localEuler);
        // fov <= 0 means "use the cockpit camera's OWN field of view". This matters more
        // than it sounds: the real cockpit camera is 78 deg vertical (CockpitBuilder), and
        // this tool used to hard-code 55 deg for the pilot shot. That made the pilot view
        // look like a telephoto lens, put the whole control area off the bottom of the
        // frame, and led to the conclusion that the controls "cannot be visible from the
        // seat" — when in the actual game they are. A verification tool that does not
        // reproduce the thing being verified is worse than no tool.
        shotCam.fieldOfView = fov > 0f ? fov : trueFov;

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
