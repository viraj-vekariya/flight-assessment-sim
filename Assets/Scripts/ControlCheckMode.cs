// ControlCheckMode — a control bench. Every cockpit control, its input, its target
// variable and its visual state, on one screen, without running a mission.
//
// WHY IT EXISTS
//   Debugging a control by flying H4 is slow and confounded: if the aeroplane misbehaves
//   you cannot tell whether the trim wheel is wired backwards, the flap detent is
//   snapping to the wrong index, or the mission is doing what it is supposed to. This
//   mode answers "does this control do what it says" in isolation, in seconds.
//
//   It is also the pre-session check an experimenter should run with the participant in
//   the headset: reach for each control once, confirm it moves and the number changes,
//   then start the session. That catches a dead controller or a mis-seated headset
//   before an hour of EEG is wasted rather than after.
//
// ENTRY
//   Main menu -> CONTROL CHECK, or launch with -controlcheck.
//   It parks the aircraft on the runway, engine running, and never records anything.
//
// SAFETY
//   ControlCheckMode.Active is checked by both interactors so controls work here even
//   though the game state is not Flying. It writes NO experiment data — no trial folder,
//   no markers, no telemetry — so it can never contaminate a participant's dataset.

using UnityEngine;

public class ControlCheckMode : MonoBehaviour
{
    public static bool Active { get; private set; }
    static ControlCheckMode instance;

    GUIStyle title, head, row, val, small, btn;
    CessnaPhysics ac;
    AircraftController ctl;
    AircraftSystems sys;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-controlcheck") { Enter(); return; }
    }

    public static void Enter()
    {
        if (instance != null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        instance = gm.gameObject.AddComponent<ControlCheckMode>();
        Active = true;
        gm.EnterControlCheck();
    }

    public static void Exit()
    {
        if (instance == null) return;
        Active = false;
        var gm = GameManager.Instance;
        Destroy(instance);
        instance = null;
        if (gm != null) gm.ExitControlCheck();
    }

    void Awake()
    {
        var gm = GameManager.Instance;
        ac = gm != null ? gm.Aircraft : null;
        if (ac != null) { ctl = ac.GetComponent<AircraftController>(); sys = ac.GetComponent<AircraftSystems>(); }
    }

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        head  = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .85f, .35f) } };
        row   = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(.88f, .88f, .9f) } };
        val   = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.45f, .95f, .6f) } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(.65f, .68f, .74f) } };
        btn   = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Exit();
    }

    void OnGUI()
    {
        if (!Active || ac == null) return;
        Styles();

        float w = 430f;
        GUI.Box(new Rect(12f, 12f, w, 620f), "");
        GUILayout.BeginArea(new Rect(24f, 24f, w - 24f, 600f));

        GUILayout.Label("CONTROL CHECK", title);
        GUILayout.Label(VRRuntime.StatusLine, small);
        GUILayout.Label("Modality: " + VRRuntime.Modality +
                        "   ·   " + (VRRuntime.Active ? "grip = grab, trigger = press, A/X = acknowledge"
                                                      : "LEFT-drag a control; click switches"), small);
        GUILayout.Space(8);

        GUILayout.Label("PRIMARY FLIGHT", head);
        Line("Pitch input", ac.pitchInput, ctl != null && ctl.PitchOverridden);
        Line("Roll input", ac.rollInput, ctl != null && ctl.RollOverridden);
        Line("Yaw input", ac.yawInput, ctl != null && ctl.YawOverridden);
        Line("Elevator (stick+trim)", ac.ElevatorCmd, false);
        GUILayout.Space(6);

        GUILayout.Label("ENGINE / CONFIGURATION", head);
        Line("Throttle", ac.Throttle01, ctl != null && ctl.ThrottleOverridden);
        Line("Trim  (+up / -down)", ac.trim, ctl != null && ctl.TrimOverridden);
        Row("Flaps selected", ctl != null ? AircraftController.FlapLabels[ctl.FlapDetentIndex] : "?");
        Line("Flaps actual", ac.Flaps01, false);
        Line("Flap authority", ac.flapAuthority01, false);
        Line("Brake pressure", ac.brakeInput01, ctl != null && ctl.BrakeOverridden);
        Row("Brakes active", ac.braking ? "YES" : "no");
        Row("On ground", ac.Grounded ? "YES" : "no");
        GUILayout.Space(6);

        GUILayout.Label("SYSTEMS", head);
        if (sys != null)
        {
            Row("Carb heat", sys.CarbHeatOn ? "ON" : "off");
            Row("Fuel selector", sys.Selector.ToString().ToUpper());
            Row("Load shed", sys.LoadShed ? "ON" : "off");
            Row("Alternate static", sys.AlternateStaticOpen ? "OPEN" : "closed");
            Row("Bus volts", sys.BusVolts.ToString("F1") + " V");
        }
        GUILayout.Space(6);

        GUILayout.Label("PHYSICAL CONTROLS", head);
        var rig = CockpitControlRig.Instance;
        if (rig == null) GUILayout.Label("cockpit rig not built (GLB not loaded?)", small);
        else
            foreach (var c in rig.Controls)
            {
                if (c == null) continue;
                string state = c.spec.kind == ControlKind.Toggle ? (c.ToggleState ? "ON" : "off")
                             : (c.spec.detents != null && c.spec.detents.Length > 0)
                                 ? (c.spec.detentLabels != null && c.DetentIndex < c.spec.detentLabels.Length
                                        ? c.spec.detentLabels[c.DetentIndex] : c.DetentIndex.ToString())
                             : c.Value.ToString("F2");
                GUILayout.BeginHorizontal();
                GUILayout.Label((c.Grabbed ? "▶ " : c.Hovered ? "· " : "  ") + c.spec.label, row, GUILayout.Width(170));
                GUILayout.Label(state, val, GUILayout.Width(70));
                GUILayout.Label(c.spec.kind.ToString(), small);
                GUILayout.EndHorizontal();
            }

        GUILayout.Space(10);
        if (GUILayout.Button("RESET ALL CONTROLS", btn, GUILayout.Height(26)))
        {
            ctl?.ResetConfiguration(0f);
            CockpitControlRig.Instance?.ResetAll(0f);
            sys?.ResetAll();
        }
        if (GUILayout.Button("BACK TO MENU  [Esc]", btn, GUILayout.Height(26))) Exit();
        GUILayout.EndArea();
    }

    void Line(string name, float v, bool overridden)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, row, GUILayout.Width(170));
        GUILayout.Label(v.ToString("F3"), val, GUILayout.Width(70));
        // A filled bar reads faster than a number when you are checking a control sweeps
        // its whole range.
        int n = Mathf.RoundToInt(Mathf.Abs(v) * 12f);
        GUILayout.Label(new string('|', Mathf.Clamp(n, 0, 12)) + (overridden ? "   [cockpit]" : ""), small);
        GUILayout.EndHorizontal();
    }

    void Row(string name, string v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, row, GUILayout.Width(170));
        GUILayout.Label(v, val);
        GUILayout.EndHorizontal();
    }
}
