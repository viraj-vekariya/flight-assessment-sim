// CockpitAudit — dev-only visual calibration probe for the cockpit redesign.
//
// Renders the pilot's view at a fixed resolution and reports, for every control and
// screen, its SCREEN-SPACE bounding box in NORMALISED coordinates (0..1 of the render
// target). That makes before/after comparison objective instead of impressionistic:
// "the CARB HEAT label is 0.31 of the screen wide" is checkable, "it looks big" is not.
//
// Runs only with -cockpitaudit. Never ships, never touches experiment data.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class CockpitAudit : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-cockpitaudit") { new GameObject("CockpitAudit").AddComponent<CockpitAudit>(); return; }
    }

    const int W = 1600, H = 1000;
    const float PanelZRef = 0.750f;
    public static string Tag = "before";

    IEnumerator Start()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("tag=")) Tag = a.Substring(4);

        yield return new WaitForSeconds(8f);

        var gm = GameManager.Instance;
        var phys = gm != null ? gm.Aircraft : null;
        if (phys == null) { Debug.Log("[AUDIT] no aircraft"); yield break; }
        foreach (var ft in FindObjectsByType<FlightTest>(FindObjectsSortMode.None)) Destroy(ft.gameObject);
        var ctl = phys.GetComponent<AircraftController>();
        if (ctl != null) ctl.enabled = false;

        var cockCam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None),
                                        c => c.name == "CockpitCamera");
        if (cockCam == null) { Debug.Log("[AUDIT] no CockpitCamera"); yield break; }
        var holderGo = GameObject.Find("RealCockpitModel");
        var holder = holderGo != null ? holderGo.transform : cockCam.transform;

        yield return new WaitForSeconds(1f);

        // ---- runtime-visible object inventory: anything that should not face a participant
        var sb = new StringBuilder("\n[AUDIT] ===== RUNTIME-VISIBLE OBJECT SCAN =====\n");
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            string n = r.name.ToLowerInvariant();
            string path = Path(r.transform);
            bool suspicious = n.Contains("icon") || n.Contains("gizmo") || n.Contains("probe")
                           || n.Contains("debug") || n.Contains("marker") || n.Contains("speaker")
                           || n.Contains("camera") || n.Contains("audio");
            if (suspicious) sb.AppendLine($"  SUSPECT renderer: {path}");
        }
        foreach (var au in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            sb.AppendLine($"  AudioSource: {Path(au.transform)}  (gizmo only unless it has a Renderer)");
        foreach (var cm in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            sb.AppendLine($"  Camera: {Path(cm.transform)} enabled={cm.enabled} target={(cm.targetTexture!=null?"RT":"screen")} depth={cm.depth} mask=0x{cm.cullingMask:X}");
        Debug.Log(sb.ToString());

        // ---- screen-space bounding boxes, normalised
        var rows = new List<string>();
        void Box(string id, Transform t, bool includeChildren)
        {
            if (t == null) { rows.Add($"  {id,-22} MISSING"); return; }
            var rs = includeChildren ? t.GetComponentsInChildren<Renderer>(true) : t.GetComponents<Renderer>();
            if (rs.Length == 0) { rows.Add($"  {id,-22} no renderer"); return; }
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f; bool any = false;
            foreach (var r in rs)
            {
                if (!r.enabled) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents,
                        new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1));
                    Vector3 sp = cockCam.WorldToScreenPoint(c);
                    if (sp.z <= 0f) continue;
                    float nx = sp.x / cockCam.pixelWidth, ny = sp.y / cockCam.pixelHeight;
                    x0 = Mathf.Min(x0, nx); x1 = Mathf.Max(x1, nx);
                    y0 = Mathf.Min(y0, ny); y1 = Mathf.Max(y1, ny);
                    any = true;
                }
            }
            if (!any) { rows.Add($"  {id,-22} off-screen / behind camera"); return; }
            rows.Add($"  {id,-22} x={x0:F3} y={y0:F3} w={(x1-x0):F3} h={(y1-y0):F3}"
                   + (x1 < 0f || x0 > 1f || y1 < 0f || y0 > 1f ? "   [OFF-SCREEN]" : ""));
        }

        Box("PFD", Find("LivePFDQuad"), false);
        Box("MFD", Find("LiveMFDQuad"), false);
        Box("YOKE", Find("YokeVisual"), true);

        var rig = FindFirstObjectByType<CockpitControlRig>();
        foreach (var pc in FindObjectsByType<PhysicalControl>(FindObjectsSortMode.None))
        {
            Box("CTRL " + pc.spec.id, pc.transform, true);
            // and the label alone, which is the thing under suspicion
            var lbl = pc.GetComponentInChildren<TextMesh>(true);
            if (lbl != null) Box("  lbl " + pc.spec.id, lbl.transform, true);
        }

        var sb2 = new StringBuilder($"\n[AUDIT] ===== SCREEN BOXES ({Tag}) normalised to the cockpit camera =====\n");
        sb2.AppendLine($"  render target {W}x{H}, cam pixel {cockCam.pixelWidth}x{cockCam.pixelHeight}, fov {cockCam.fieldOfView}\n");
        foreach (var r in rows) sb2.AppendLine(r);
        Debug.Log(sb2.ToString());

        // ---- the picture
        var rt = new RenderTexture(W, H, 24) { antiAliasing = 2 };
        var prev = cockCam.targetTexture;
        cockCam.targetTexture = rt; cockCam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        cockCam.targetTexture = prev; RenderTexture.active = null;
        File.WriteAllBytes(Path2($"cockpit_{Tag}.png"), tex.EncodeToPNG());

        // ---- throttle travel: idle / half / full, so "visual position == aircraft value"
        //      is checkable in pictures and not only in the control-test numbers ----
        PhysicalControl thr = null;
        foreach (var pc in FindObjectsByType<PhysicalControl>(FindObjectsSortMode.None))
            if (pc.spec.id == "throttle") thr = pc;
        var tcam = new GameObject("ThrCam").AddComponent<Camera>();
        tcam.cullingMask = cockCam.cullingMask; tcam.nearClipPlane = 0.003f;
        tcam.fieldOfView = 34f;
        tcam.transform.SetParent(cockCam.transform, false);
        tcam.transform.localRotation = Quaternion.Euler(46f, 0f, 0f);
        if (thr != null)
        {
            foreach (var (v, nm) in new[] { (0f, "idle"), (0.5f, "half"), (1f, "full") })
            {
                thr.SetSilently(v);
                yield return null; yield return null;
                var rtT = new RenderTexture(900, 640, 24) { antiAliasing = 2 };
                tcam.targetTexture = rtT; tcam.Render();
                RenderTexture.active = rtT;
                var tT = new Texture2D(900, 640, TextureFormat.RGB24, false);
                tT.ReadPixels(new Rect(0, 0, 900, 640), 0, 0); tT.Apply();
                tcam.targetTexture = null; RenderTexture.active = null;
                File.WriteAllBytes(Path2($"throttle_{Tag}_{nm}.png"), tT.EncodeToPNG());
                Debug.Log($"[AUDIT] throttle {nm}: phys.throttle={(phys != null ? phys.Throttle01 : -1f):F2}");
                Destroy(tT); Destroy(rtT);
            }
            thr.SetSilently(0f);
        }
        Destroy(tcam.gameObject);

        // Close-up of the LEFT SYSTEMS BLOCK at the angle the user photographed, which is
        // where the ZTest-Always label bug was visible and the wide shots were not.
        var scam = new GameObject("SysCam").AddComponent<Camera>();
        scam.cullingMask = cockCam.cullingMask; scam.nearClipPlane = 0.003f; scam.fieldOfView = 30f;
        scam.transform.SetParent(holder, false);
        scam.transform.localPosition = new Vector3(-0.035f, 0.470f, 0.610f);
        scam.transform.localRotation = Quaternion.LookRotation(
            new Vector3(-0.125f, 0.335f, PanelZRef) - scam.transform.localPosition, Vector3.up);
        var rtS = new RenderTexture(980, 860, 24) { antiAliasing = 2 };
        scam.targetTexture = rtS; scam.Render();
        RenderTexture.active = rtS;
        var tS = new Texture2D(980, 860, TextureFormat.RGB24, false);
        tS.ReadPixels(new Rect(0, 0, 980, 860), 0, 0); tS.Apply();
        scam.targetTexture = null; RenderTexture.active = null;
        File.WriteAllBytes(Path2($"systems_{Tag}.png"), tS.EncodeToPNG());
        Destroy(tS); Destroy(rtS); Destroy(scam.gameObject);

        // a look down-left and down-right, where the systems controls live
        var dc = new GameObject("AuditCam").AddComponent<Camera>();
        dc.cullingMask = cockCam.cullingMask; dc.nearClipPlane = 0.003f;
        dc.fieldOfView = cockCam.fieldOfView;
        dc.transform.SetParent(cockCam.transform, false);
        foreach (var (nm, eul) in new[] { ("downL", new Vector3(30f, -34f, 0f)),
                                          ("downR", new Vector3(30f,  34f, 0f)),
                                          ("down",  new Vector3(40f,   0f, 0f)) })
        {
            dc.transform.localRotation = Quaternion.Euler(eul);
            var rt2 = new RenderTexture(W, H, 24) { antiAliasing = 2 };
            dc.targetTexture = rt2; dc.Render();
            RenderTexture.active = rt2;
            var t2 = new Texture2D(W, H, TextureFormat.RGB24, false);
            t2.ReadPixels(new Rect(0, 0, W, H), 0, 0); t2.Apply();
            dc.targetTexture = null; RenderTexture.active = null;
            File.WriteAllBytes(Path2($"cockpit_{Tag}_{nm}.png"), t2.EncodeToPNG());
            Destroy(t2); Destroy(rt2);
        }
        Debug.Log("[AUDIT] done -> " + Application.persistentDataPath);
    }

    static string Path2(string f) => System.IO.Path.Combine(Application.persistentDataPath, f);
    static Transform Find(string n) { var g = GameObject.Find(n); return g != null ? g.transform : null; }
    static string Path(Transform t)
    {
        string p = t.name; var q = t.parent;
        for (int i = 0; i < 4 && q != null; i++) { p = q.name + "/" + p; q = q.parent; }
        return p;
    }
}
