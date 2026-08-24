using System.Collections;
using System.IO;
using UnityEngine;

/// Dev-only verification harness for the panel screens and the hidden cockpit clutter.
/// Runs only with -screens; never ships. Logs which model nodes are hidden/visible, then
/// grabs the pilot's view plus the raw PFD / MFD render textures at a lively attitude.
public class ScreenProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-screens") { new GameObject("ScreenProbe").AddComponent<ScreenProbe>(); return; }
    }

    CessnaPhysics phys;
    bool hold;
    Quaternion holdRot = Quaternion.identity;

    void LateUpdate()
    {
        if (phys == null || !hold) return;
        phys.transform.rotation = holdRot;
        var rb = phys.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.angularVelocity = Vector3.zero;
        // hold a realistic cruise so the readouts show the digit counts a pilot actually sees
        rb.linearVelocity = phys.transform.forward * 62f;   // ~223 km/h
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(7f);
        var holder = GameObject.Find("RealCockpitModel");
        var gm = GameManager.Instance;
        phys = gm != null ? gm.Aircraft : null;
        if (holder == null || phys == null) { Debug.Log("[SCREENS] missing holder/phys"); yield break; }
        var ctl = phys.GetComponent<AircraftController>();
        if (ctl != null) ctl.enabled = false;
        foreach (var ft in FindObjectsByType<FlightTest>(FindObjectsSortMode.None)) Destroy(ft.gameObject);

        var H = holder.transform;
        var sb = new System.Text.StringBuilder("\n[SCREENS] node visibility\n");
        string[] shouldHide = { "Object_15", "Object_17", "Object_60", "Object_64" };
        string[] shouldShow = { "Object_90", "Object_94", "Object_32", "Object_35", "Object_120",
                                "Object_96", "Object_98", "Object_81", "Object_83" };
        foreach (var nm in shouldHide) sb.AppendLine("  HIDE " + nm + " -> " + Report(H, nm));
        foreach (var nm in shouldShow) sb.AppendLine("  SHOW " + nm + " -> " + Report(H, nm));
        sb.AppendLine("  PFD quad = " + (GameObject.Find("LivePFDQuad") != null) +
                      ", MFD quad = " + (GameObject.Find("LiveMFDQuad") != null));
        Debug.Log(sb.ToString());

        // a lively attitude at cruise height: 18 deg bank, 8 deg nose up, heading 040,
        // ~1200 m and ~223 km/h so ALT reads four digits and SPD three
        hold = true;
        holdRot = Quaternion.Euler(-8f, 40f, 18f);
        phys.ResetTo(phys.transform.position + Vector3.up * 1200f, holdRot, true, 62f);
        yield return new WaitForSeconds(2f);
        Debug.Log($"[SCREENS] attitude: roll={phys.RollDeg:F1} pitch={phys.PitchDeg:F1} hdg={phys.HeadingDeg:F0} vs={phys.VerticalSpeedMs:F1} spd={phys.AirspeedKmh:F0}");

        // ---- the two glass screens, straight off their own render textures ----
        Grab("PFDCam", "scr_pfd.png");
        Grab("MFDCam", "scr_mfd.png");

        // ---- the MFD compass ring at two headings, to prove the 8 labels orbit ----
        holdRot = Quaternion.Euler(0f, 0f, 0f);
        yield return new WaitForSeconds(1f);
        Grab("MFDCam", "scr_mfd_hdg000.png");
        holdRot = Quaternion.Euler(0f, 90f, 0f);
        yield return new WaitForSeconds(1f);
        Grab("MFDCam", "scr_mfd_hdg090.png");
        holdRot = Quaternion.Euler(-8f, 40f, 18f);
        yield return new WaitForSeconds(1f);

        // ---- screen quads + the live tuner ----
        var tuner = FindFirstObjectByType<ScreenTuner>();
        if (tuner == null) Debug.Log("[SCREENS] NO ScreenTuner component");
        else
        {
            Debug.Log($"[SCREENS] ScreenTuner: pfdQuad={(tuner.pfdQuad != null ? tuner.pfdQuad.name : "NULL")} " +
                      $"mfdQuad={(tuner.mfdQuad != null ? tuner.mfdQuad.name : "NULL")}");
            foreach (var q in new[] { tuner.pfdQuad, tuner.mfdQuad })
                if (q != null)
                    Debug.Log($"[SCREENS]   {q.name} pos={q.localPosition:F4} euler={q.localEulerAngles:F1} scale={q.localScale:F4}");
            // the P key path, exercised without a keyboard
            var mi = typeof(ScreenTuner).GetMethod("PrintBoth",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi != null) mi.Invoke(tuner, null);
            else Debug.Log("[SCREENS] PrintBoth not found");
        }

        // ---- the pilot's view, and a look down at the floor ----
        var cockCam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name == "CockpitCamera");
        if (cockCam != null)
        {
            var dc = new GameObject("ScrCam").AddComponent<Camera>();
            dc.cullingMask = cockCam.cullingMask; dc.nearClipPlane = 0.003f; dc.fieldOfView = cockCam.fieldOfView;
            dc.transform.SetParent(cockCam.transform, false);
            dc.transform.localPosition = Vector3.zero;
            dc.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);
            yield return Shot(dc, "scr_pilot.png");
            dc.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);   // down at the floor
            yield return Shot(dc, "scr_floor.png");

            // ---- ID pass: flat-colour every nearby renderer so each pixel names its object ----
            Vector3 eye = cockCam.transform.position;
            var near = new System.Collections.Generic.List<Renderer>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && Vector3.Distance(r.bounds.ClosestPoint(eye), eye) < 2.0f) near.Add(r);
            var saved = new Material[near.Count][];
            var sb2 = new System.Text.StringBuilder("\n[SCREENS] ID pass colour -> object\n");
            for (int i = 0; i < near.Count; i++)
            {
                saved[i] = near[i].sharedMaterials;
                var c = new Color(((i % 16) * 17) / 255f, ((i / 16) * 17) / 255f, 0.5f);
                var m = new Material(Shader.Find("Unlit/Color")) { color = c };
                var arr = new Material[near[i].sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = m;
                near[i].sharedMaterials = arr;
                string path = near[i].name; var pt = near[i].transform.parent;
                for (int g = 0; g < 3 && pt != null; g++) { path = pt.name + "/" + path; pt = pt.parent; }
                sb2.AppendLine($"  rgb({(i % 16) * 17},{(i / 16) * 17},128) = {path}");
            }
            Debug.Log(sb2.ToString());
            yield return Shot(dc, "scr_floor_ids.png");
            for (int i = 0; i < near.Count; i++) near[i].sharedMaterials = saved[i];
        }
        Debug.Log("[SCREENS] done -> " + Application.persistentDataPath);
    }

    static string Report(Transform holder, string nm)
    {
        var t = FindDeep(holder, nm);
        if (t == null) return "NOT FOUND";
        int on = 0, off = 0;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { if (r.enabled) on++; else off++; }
        return $"renderers on={on} off={off}";
    }

    void Grab(string camName, string file)
    {
        var cam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name == camName);
        if (cam == null || cam.targetTexture == null) { Debug.Log("[SCREENS] no " + camName); return; }
        var rt = cam.targetTexture;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(Application.persistentDataPath, file), tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[SCREENS] {file} = {rt.width}x{rt.height} aa={rt.antiAliasing}");
    }

    IEnumerator Shot(Camera cam, string file)
    {
        yield return null; yield return null;
        int W = 1100, H = 750;
        var rt = new RenderTexture(W, H, 24);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        cam.targetTexture = prev; RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Application.persistentDataPath, file), tex.EncodeToPNG());
        Destroy(tex); Destroy(rt);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }
}
