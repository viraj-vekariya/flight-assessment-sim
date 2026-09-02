using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Dev-only: renders the yoke both IN CONTEXT (pilot view) and ISOLATED (everything else hidden)
/// so the design can actually be seen and checked. Runs only with -yokeshot; never ships.
/// </summary>
public class YokeShot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-yokeshot") { new GameObject("YokeShot").AddComponent<YokeShot>(); return; }
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(6f);
        var gm = GameManager.Instance;
        var phys = gm != null ? gm.Aircraft : null;
        var ctl = phys != null ? phys.GetComponent<AircraftController>() : null;
        if (ctl != null) ctl.enabled = false;
        var cockCam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name == "CockpitCamera");
        var yokeVisual = GameObject.Find("YokeVisual");
        var holder = GameObject.Find("RealCockpitModel");
        if (phys == null || yokeVisual == null) { Debug.Log("[YOKESHOT] missing phys/yoke yokeFound=" + (yokeVisual != null)); yield break; }
        phys.transform.rotation = Quaternion.identity;

        var dc = new GameObject("YSCam").AddComponent<Camera>();
        // Not the display-only layers: their symbology lives 500 m above the aeroplane
        // and would otherwise appear in this dev shot as glyphs hanging in the sky.
        dc.cullingMask = ~CockpitBuilder.DisplayOnlyMask; dc.nearClipPlane = 0.003f; dc.fieldOfView = 45;

        // ---- 1) IN-CONTEXT: pilot's eye looking down at the yoke ----
        if (cockCam != null)
        {
            dc.transform.SetParent(cockCam.transform, false);
            dc.transform.localPosition = Vector3.zero;
            dc.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);
            yield return Shot(dc, phys, 0f, "yoke_context.png");
        }
        dc.transform.SetParent(null, true);

        // ---- 2) ISOLATED: hide EVERYTHING except the yoke, so its design is clearly visible ----
        var restore = new List<Renderer>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            bool isYoke = r.transform.IsChildOf(yokeVisual.transform);
            if (!isYoke && r.enabled) { r.enabled = false; restore.Add(r); }
        }
        dc.clearFlags = CameraClearFlags.SolidColor; dc.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
        Vector3 c = yokeVisual.transform.position;
        Vector3 fwd = holder != null ? holder.transform.forward : Vector3.forward;
        Vector3 up = holder != null ? holder.transform.up : Vector3.up;
        // straight-on from the pilot side (behind the wheel), a little above
        dc.transform.position = c - fwd * 0.28f + up * 0.05f;
        dc.transform.LookAt(c, up);
        dc.fieldOfView = 40;
        yield return Shot(dc, phys, 0f, "yoke_iso_neutral.png");
        yield return Shot(dc, phys, 1f, "yoke_iso_rollR.png");
        yield return Shot(dc, phys, -1f, "yoke_iso_rollL.png");
        // 3/4 view
        dc.transform.position = c - fwd * 0.24f + up * 0.12f + holder.transform.right * 0.14f;
        dc.transform.LookAt(c, up);
        yield return Shot(dc, phys, 0f, "yoke_iso_34.png");

        foreach (var r in restore) if (r != null) r.enabled = true;
        Debug.Log("[YOKESHOT] done");
    }

    IEnumerator Shot(Camera cam, CessnaPhysics phys, float roll, string file)
    {
        for (int f = 0; f < 16; f++) { phys.rollInput = roll; phys.pitchInput = 0f; yield return null; }
        int W = 1280, H = 800;
        var rt = new RenderTexture(W, H, 24);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        cam.targetTexture = prev; RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Application.persistentDataPath, file), tex.EncodeToPNG());
        Destroy(tex); Destroy(rt);
    }
}
