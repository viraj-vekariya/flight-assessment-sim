using System.Collections;
using System.IO;
using UnityEngine;

/// Dev-only verification harness for the dynamic yoke. Runs only with -probe; never ships.
/// 1) confirms the physics sign convention (roll right / nose up) from the live attitude,
/// 2) renders the pilot's view at each control extreme so the wheel can be eyeballed.
public class YokeProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-probe") { new GameObject("YokeProbe").AddComponent<YokeProbe>(); return; }
    }

    CessnaPhysics phys;
    float holdRoll, holdPitch;
    bool freeze;
    Transform rigRoot, rigVisual, rigShaft;

    void LateUpdate()
    {
        if (phys == null) return;
        phys.rollInput = holdRoll; phys.pitchInput = holdPitch;
        if (freeze)
        {
            phys.transform.rotation = Quaternion.identity;
            var rb = phys.GetComponent<Rigidbody>();
            if (rb != null) rb.angularVelocity = Vector3.zero;
        }
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(7f);
        var holder = GameObject.Find("RealCockpitModel");
        var gm = GameManager.Instance;
        phys = gm != null ? gm.Aircraft : null;
        if (holder == null || phys == null) { Debug.Log("[PROBE] missing holder/phys"); yield break; }
        var ctl = phys.GetComponent<AircraftController>();
        if (ctl != null) ctl.enabled = false;
        // the batchmode flight verifier also drives the inputs — it would fight this probe
        foreach (var ft in FindObjectsByType<FlightTest>(FindObjectsSortMode.None)) Destroy(ft.gameObject);

        var H = holder.transform;
        var root = GameObject.Find("YokeRoot");
        var vis = GameObject.Find("YokeVisual");
        var shaft = GameObject.Find("YokeShaft");
        rigRoot = root != null ? root.transform : null;
        rigVisual = vis != null ? vis.transform : null;
        rigShaft = shaft != null ? shaft.transform : null;
        Debug.Log($"[PROBE] rig: YokeRoot={(root != null)} YokeVisual={(vis != null)} YokeShaft={(shaft != null)}");
        if (root != null) Debug.Log($"[PROBE] rootLocal={root.transform.localPosition:F4} parent={root.transform.parent.name}");
        foreach (var nm in new[] { "Object_90", "Object_94" })
        {
            var t = FindDeep(H, nm);
            if (t == null) { Debug.Log("[PROBE] missing " + nm); continue; }
            var r = t.GetComponent<Renderer>();
            Vector3 c = H.InverseTransformPoint(r.bounds.center);
            Debug.Log($"[PROBE] {nm} parent={t.parent.name} on={r.enabled} boundsC={c:F4} (whole vertex array)");
        }

        // ---------- 1) physics sign check ----------
        freeze = false;
        yield return SignCheck(+1f, 0f, "rollInput=+1");
        yield return SignCheck(-1f, 0f, "rollInput=-1");
        yield return SignCheck(0f, -1f, "pitchInput=-1 (DOWN key)");
        yield return SignCheck(0f, +1f, "pitchInput=+1 (UP key)");

        // ---------- 2) visual check ----------
        freeze = true; holdRoll = holdPitch = 0f;
        yield return new WaitForSeconds(1f);
        var cockCam = System.Array.Find(FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name == "CockpitCamera");
        if (cockCam == null) { Debug.Log("[PROBE] no CockpitCamera"); yield break; }

        var dc = new GameObject("ProbeCam").AddComponent<Camera>();
        dc.cullingMask = cockCam.cullingMask; dc.nearClipPlane = 0.003f; dc.fieldOfView = cockCam.fieldOfView;
        dc.transform.SetParent(cockCam.transform, false);
        dc.transform.localPosition = Vector3.zero;
        dc.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);
        yield return State(0f, 0f, dc, "yk_pilot_neutral.png");
        yield return State(+1f, 0f, dc, "yk_pilot_rollR.png");
        yield return State(-1f, 0f, dc, "yk_pilot_rollL.png");
        yield return State(0f, -1f, dc, "yk_pilot_pitchUp_DOWNkey.png");
        yield return State(0f, +1f, dc, "yk_pilot_pitchDn_UPkey.png");

        // ---------- 3) close-ups: camera parented to the cockpit so it tracks the aircraft,
        //              and everything except the yoke hidden so the motion is unmistakable ----------
        var keep = new System.Collections.Generic.List<Renderer>();
        var vt = vis != null ? vis.transform : null;
        var st = shaft != null ? shaft.transform : null;
        var hidden = new System.Collections.Generic.List<Renderer>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            bool isYoke = (vt != null && r.transform.IsChildOf(vt)) || (st != null && r.transform.IsChildOf(st));
            if (isYoke) keep.Add(r);
            else if (r.enabled) { r.enabled = false; hidden.Add(r); }
        }
        Debug.Log($"[PROBE] isolated yoke renderers = {keep.Count}");

        dc.transform.SetParent(H, false);
        dc.clearFlags = CameraClearFlags.SolidColor; dc.backgroundColor = new Color(0.62f, 0.66f, 0.70f);
        dc.fieldOfView = 34f;
        Vector3 aimL = new Vector3(0f, 0.445f, 0.718f);          // the yoke, in model space

        // straight on from the pilot's side (what the pilot sees)
        dc.transform.localPosition = new Vector3(0f, 0.452f, 0.560f);
        dc.transform.localRotation = Quaternion.LookRotation(aimL - dc.transform.localPosition, Vector3.up);
        yield return State(0f, 0f, dc, "yk_close_neutral.png");
        yield return State(+1f, 0f, dc, "yk_close_rollR.png");
        yield return State(-1f, 0f, dc, "yk_close_rollL.png");
        yield return State(0f, -1f, dc, "yk_close_pitchUp_DOWNkey.png");
        yield return State(0f, +1f, dc, "yk_close_pitchDn_UPkey.png");

        // from the left, across the column, so the pull/push slide is visible in profile
        dc.transform.localPosition = new Vector3(-0.20f, 0.452f, 0.700f);
        dc.transform.localRotation = Quaternion.LookRotation(aimL - dc.transform.localPosition, Vector3.up);
        yield return State(0f, 0f, dc, "yk_side_neutral.png");
        yield return State(0f, -1f, dc, "yk_side_pitchUp_DOWNkey.png");
        yield return State(0f, +1f, dc, "yk_side_pitchDn_UPkey.png");

        foreach (var r in hidden) if (r != null) r.enabled = true;

        Debug.Log("[PROBE] done -> " + Application.persistentDataPath);
    }

    IEnumerator SignCheck(float roll, float pitch, string label)
    {
        holdRoll = 0f; holdPitch = 0f;
        phys.ResetTo(phys.transform.position + Vector3.up * 60f, Quaternion.identity, true, 55f);
        yield return new WaitForSeconds(0.6f);
        float r0 = phys.RollDeg, p0 = phys.PitchDeg;
        holdRoll = roll; holdPitch = pitch;
        yield return new WaitForSeconds(2.0f);
        Debug.Log($"[PROBE] {label}: dRoll={phys.RollDeg - r0:F1} deg (+ = right bank), dPitch={phys.PitchDeg - p0:F1} deg (+ = nose up)");
        holdRoll = 0f; holdPitch = 0f;
    }

    IEnumerator State(float roll, float pitch, Camera cam, string file)
    {
        holdRoll = roll; holdPitch = pitch;
        yield return new WaitForSeconds(1.2f);            // let the smoothing settle
        Debug.Log($"[PROBE] {file}: in(roll={phys.rollInput:F2},pitch={phys.pitchInput:F2}) "
                + $"visEuler={(rigVisual != null ? rigVisual.localEulerAngles.ToString("F1") : "-")} "
                + $"rootLocal={(rigRoot != null ? rigRoot.localPosition.ToString("F4") : "-")} "
                + $"shaftScaleZ={(rigShaft != null ? rigShaft.localScale.z.ToString("F2") : "-")} "
                + $"crashed={phys.Crashed}");
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
