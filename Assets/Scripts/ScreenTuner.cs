using UnityEngine;

/// <summary>
/// Live keyboard tuner for the two glass-panel screens. Fly, look at where the PFD / MFD quads
/// actually land on the model's panel, nudge them until they sit flush, then press P and paste
/// the printed numbers straight back into the two Build() calls in RealCockpit.cs.
///
///   F9              tuning mode ON / OFF   (OFF by default — see the note below)
///   TAB             switch between the PFD and the MFD
///   Left / Right    move X by ±0.002        Up / Down   move Y by ±0.002
///   PageUp / PageDn move Z by ±0.002        + / -       scale both X and Y by ±0.002
///   [ / ]           tilt about X by ∓1°     P           print BOTH screens, copy-paste ready
///
/// WHY THE F9 GATE: Unity's default "Vertical"/"Horizontal" axes bind the ARROW KEYS as well as
/// WASD, so the arrows fly the aeroplane. While tuning mode is ON this zeroes the pitch/roll
/// commands (through AircraftController's existing override API, the same one the mouse-grab
/// interaction uses), so an arrow press moves the screen WITHOUT also pitching the nose. Turn
/// tuning off and the arrows fly again exactly as before. Harmless to ship: it only logs, moves
/// two transforms, and does nothing at all until F9 is pressed.
/// </summary>
[DefaultExecutionOrder(-60)]   // ahead of AircraftController, like CockpitInteraction
public class ScreenTuner : MonoBehaviour
{
    public Transform pfdQuad;
    public Transform mfdQuad;

    public KeyCode toggleKey = KeyCode.F9;
    public float moveStep  = 0.002f;
    public float scaleStep = 0.002f;
    public float rotStep   = 1f;

    bool active;
    bool tuningMfd;          // false = PFD selected, true = MFD selected

    Transform Selected => tuningMfd ? mfdQuad : pfdQuad;
    string SelectedName => tuningMfd ? "MFD (right screen)" : "PFD (left screen)";

    void Update()
    {
        // only while actually flying, so it can never interfere with the menus
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Flying) return;

        if (Input.GetKeyDown(toggleKey))
        {
            active = !active;
            Debug.Log(active
                ? $"[SCREENTUNER] ON — tuning {SelectedName}. TAB=switch  arrows=XY  PgUp/PgDn=Z  +/-=scale  [ ]=tilt  P=print"
                : "[SCREENTUNER] OFF — arrow keys fly the aeroplane again.");
            if (!active) ReleaseControls();       // MUST clear, or pitch/roll stay overridden
        }
        if (!active) return;

        // Arrow keys double as the flight pitch/roll axes — hold them off while tuning.
        // AircraftController latches hasPitch/hasRoll until someone calls ClearOverrides(), and
        // the only caller (CockpitInteraction) is switched off once the GLB cockpit loads — so
        // the release below is what stops this from disabling pitch and roll for good.
        var ctl = Controller();
        if (ctl != null && ctl.enabled) { ctl.SetPitch(0f); ctl.SetRoll(0f); }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            tuningMfd = !tuningMfd;
            Debug.Log("[SCREENTUNER] now tuning " + SelectedName);
        }

        var t = Selected;
        if (t == null) return;

        float dx = (Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
        float dy = (Input.GetKeyDown(KeyCode.UpArrow)    ? 1 : 0) - (Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0);
        float dz = (Input.GetKeyDown(KeyCode.PageUp)     ? 1 : 0) - (Input.GetKeyDown(KeyCode.PageDown)  ? 1 : 0);
        if (dx != 0f || dy != 0f || dz != 0f)
        {
            t.localPosition += new Vector3(dx, dy, dz) * moveStep;
            Log(t);
        }

        bool up   = Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)  || Input.GetKeyDown(KeyCode.KeypadPlus);
        bool down = Input.GetKeyDown(KeyCode.Minus)  || Input.GetKeyDown(KeyCode.KeypadMinus);
        if (up || down)
        {
            float d = (up ? 1f : -1f) * scaleStep;
            var s = t.localScale;
            t.localScale = new Vector3(Mathf.Max(0.001f, s.x + d), Mathf.Max(0.001f, s.y + d), s.z);
            Log(t);
        }

        float dr = (Input.GetKeyDown(KeyCode.RightBracket) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftBracket) ? 1 : 0);
        if (dr != 0f)
        {
            var e = t.localEulerAngles;
            t.localEulerAngles = new Vector3(e.x + dr * rotStep, e.y, e.z);
            Log(t);
        }

        if (Input.GetKeyDown(KeyCode.P)) PrintBoth();
    }

    AircraftController Controller()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.Aircraft != null ? gm.Aircraft.GetComponent<AircraftController>() : null;
    }

    // Hand the pitch/roll axes back to the keyboard.
    void ReleaseControls()
    {
        var ctl = Controller();
        if (ctl != null) ctl.ClearOverrides();
    }

    void OnDisable()
    {
        if (active) { active = false; ReleaseControls(); }
    }

    void Log(Transform t)
    {
        Debug.Log($"[SCREENTUNER] {SelectedName}  pos={t.localPosition.ToString("F4")}  " +
                  $"euler={t.localEulerAngles.ToString("F1")}  size=({t.localScale.x:F4}, {t.localScale.y:F4})");
    }

    // Prints both screens exactly in the form the two Build() calls in RealCockpit.cs expect.
    void PrintBoth()
    {
        var sb = new System.Text.StringBuilder("\n[SCREENTUNER] paste these into RealCockpit.cs:\n\n");
        sb.AppendLine(Block("pfd.Build(phys, holder.transform,", pfdQuad));
        sb.AppendLine(Block("mfd.Build(phys.transform, holder.transform,", mfdQuad));
        Debug.Log(sb.ToString());
    }

    static string Block(string header, Transform t)
    {
        if (t == null) return header + "\n    // (quad missing)\n";
        Vector3 p = t.localPosition, e = t.localEulerAngles, s = t.localScale;
        // keep the euler angles in -180..180 so they read the way they were typed
        float ex = e.x > 180f ? e.x - 360f : e.x;
        float ey = e.y > 180f ? e.y - 360f : e.y;
        float ez = e.z > 180f ? e.z - 360f : e.z;
        return header + "\n"
             + $"    new Vector3({p.x:F4}f, {p.y:F4}f, {p.z:F4}f),\n"
             + $"    Quaternion.Euler({ex:F1}f, {ey:F1}f, {ez:F1}f),\n"
             + $"    new Vector2({s.x:F4}f, {s.y:F4}f),\n"
             + "    CockpitBuilder.CockpitLayer);";
    }
}
