using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LIVE cockpit tuning tool. Lets you position / rotate / scale cockpit elements (yoke, glass
/// screens, ...) WHILE THE SIM IS RUNNING, seeing the result instantly — instead of guessing
/// numbers in code. Every change is saved to PlayerPrefs per element and re-applied on every
/// run, so you tune once and it sticks (also carries into a VR build).
///
///   F8            toggle tuning mode on/off
///   TAB           select the next element
///   ← →           move on X (left / right)
///   ↑ ↓           move on Y (up / down)
///   PageUp/Down    move on Z (forward / back)
///   hold SHIFT     the same keys ROTATE instead of move
///   hold CTRL      ↑ ↓ SCALE the element
///   [  ]          decrease / increase the step size
///   P             print the element's final numbers to the Console
///   Backspace      reset the selected element to its built-in default
/// </summary>
public class CockpitTuner : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F8;
    public float moveStep = 0.01f;
    public float rotStep = 1f;
    public float scaleStep = 0.03f;

    class Item
    {
        public string id;
        public Transform t;
        public Vector3 defPos, defScale;
        public Quaternion defRot;
    }

    readonly List<Item> items = new List<Item>();
    int sel;
    bool active;
    GUIStyle style;

    /// Register a tunable element. Any previously-saved offset is applied immediately.
    public void Register(string id, Transform t)
    {
        if (t == null) return;
        var it = new Item { id = id, t = t, defPos = t.localPosition, defRot = t.localRotation, defScale = t.localScale };
        items.Add(it);
        LoadAndApply(it);
    }

    static string K(string id, string k) => "tune." + id + "." + k;

    void LoadAndApply(Item it)
    {
        if (!PlayerPrefs.HasKey(K(it.id, "px"))) return;
        it.t.localPosition = new Vector3(
            PlayerPrefs.GetFloat(K(it.id, "px")), PlayerPrefs.GetFloat(K(it.id, "py")), PlayerPrefs.GetFloat(K(it.id, "pz")));
        it.t.localEulerAngles = new Vector3(
            PlayerPrefs.GetFloat(K(it.id, "rx")), PlayerPrefs.GetFloat(K(it.id, "ry")), PlayerPrefs.GetFloat(K(it.id, "rz")));
        float s = PlayerPrefs.GetFloat(K(it.id, "s"), 1f);
        it.t.localScale = it.defScale * s;
    }

    void Save(Item it)
    {
        var p = it.t.localPosition; var r = it.t.localEulerAngles;
        PlayerPrefs.SetFloat(K(it.id, "px"), p.x); PlayerPrefs.SetFloat(K(it.id, "py"), p.y); PlayerPrefs.SetFloat(K(it.id, "pz"), p.z);
        PlayerPrefs.SetFloat(K(it.id, "rx"), r.x); PlayerPrefs.SetFloat(K(it.id, "ry"), r.y); PlayerPrefs.SetFloat(K(it.id, "rz"), r.z);
        float baseMag = it.defScale == Vector3.zero ? 1f : it.defScale.x;
        PlayerPrefs.SetFloat(K(it.id, "s"), baseMag == 0f ? 1f : it.t.localScale.x / baseMag);
        PlayerPrefs.Save();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) active = !active;
        if (!active || items.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.Tab)) sel = (sel + 1) % items.Count;
        if (Input.GetKeyDown(KeyCode.LeftBracket)) { moveStep *= 0.5f; rotStep *= 0.5f; scaleStep *= 0.5f; }
        if (Input.GetKeyDown(KeyCode.RightBracket)) { moveStep *= 2f; rotStep *= 2f; scaleStep *= 2f; }

        var it = items[sel];
        if (it.t == null) return;
        bool rot = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool scale = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand);
        bool changed = false;

        if (Input.GetKeyDown(KeyCode.Backspace)) { it.t.localPosition = it.defPos; it.t.localRotation = it.defRot; it.t.localScale = it.defScale; changed = true; }

        int dx = (Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
        int dy = (Input.GetKeyDown(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0);
        int dz = (Input.GetKeyDown(KeyCode.PageUp) ? 1 : 0) - (Input.GetKeyDown(KeyCode.PageDown) ? 1 : 0);

        if (scale && dy != 0) { it.t.localScale *= (1f + dy * scaleStep); changed = true; }
        else if (rot && (dx != 0 || dy != 0 || dz != 0)) { it.t.localRotation *= Quaternion.Euler(dy * rotStep, dx * rotStep, dz * rotStep); changed = true; }
        else if (!scale && (dx != 0 || dy != 0 || dz != 0)) { it.t.localPosition += new Vector3(dx, dy, dz) * moveStep; changed = true; }

        if (changed) Save(it);
    }

    void OnGUI()
    {
        if (!active) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, normal = { textColor = Color.white } };

        var it = items.Count > 0 ? items[sel] : null;
        string body = "<b>COCKPIT TUNING</b>  (F8 to exit)\n" +
                      "TAB=next   ←→=X  ↑↓=Y  PgUp/Dn=Z   SHIFT=rotate   CTRL=scale   [ ]=step   P=print   Backspace=reset\n" +
                      "step move=" + moveStep.ToString("F3") + "m  rot=" + rotStep + "°  scale=" + (scaleStep * 100f).ToString("F0") + "%\n\n";
        for (int i = 0; i < items.Count; i++)
        {
            var x = items[i];
            string mark = i == sel ? "► " : "   ";
            body += mark + "<b>" + x.id + "</b>";
            if (i == sel && x.t != null)
                body += "   pos" + x.t.localPosition.ToString("F3") + "  rot" + x.t.localEulerAngles.ToString("F0") + "  scale x" + (x.defScale.x == 0 ? 1f : x.t.localScale.x / x.defScale.x).ToString("F2");
            body += "\n";
        }
        GUI.Box(new Rect(8, 8, 900, 40 + 20 * items.Count + 80), "");
        GUI.Label(new Rect(16, 12, 890, 400), body, style);

        if (it != null && Input.GetKeyDown(KeyCode.P))
            Debug.Log($"[TUNE] {it.id}  pos={it.t.localPosition:F4}  euler={it.t.localEulerAngles:F2}  scale={it.t.localScale:F4}");
    }
}
