// UiDesignShots — photographs every menu screen so the interface can be judged from
// pictures. Dev tool, runs ONLY with -uishots, never ships.
//
//   Unity -batchmode -projectPath <p> -executeMethod PlayCapture.RunUiShots \
//         -uishots -logFile ui.log
//
// It walks the real state machine: the entry screen, the hub on each tab, the
// briefing, the lab-session confirmation, help, then flies one mission at 8x and
// photographs the results page, then the hub again with that mission marked. The
// private tab/overlay fields of MenuUI are set by reflection so the screens are the
// production code, not a copy of it.

using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

public class UiDesignShots : MonoBehaviour
{
    public static bool Finished { get; private set; }
    string dir;
    int n;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "-uishots") { new GameObject("UiDesignShots").AddComponent<UiDesignShots>(); return; }
    }

    IEnumerator Start()
    {
        SimDriver.Claim("UiDesignShots");
        dir = Path.Combine(Application.persistentDataPath, "UiDesign");
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) { try { File.Delete(f); } catch { } }
        Debug.Log("[UISHOTS] screen " + Screen.width + "x" + Screen.height + " -> " + dir);

        float t0 = Time.realtimeSinceStartup;
        while (GameManager.Instance == null || GameManager.Instance.Aircraft == null)
        {
            if (Time.realtimeSinceStartup - t0 > 60f) { Debug.LogError("[UISHOTS] no GameManager"); Done(); yield break; }
            yield return null;
        }
        var gm = GameManager.Instance;
        yield return new WaitForSecondsRealtime(3f);

        // 1. entry screen, with a code typed so the preview line shows
        var pui = gm.GetComponent<ParticipantUI>();
        SetField(pui, "input", "P007");
        yield return Shot("participant_entry");

        // 2. the hub
        ParticipantManager.SetID("UISHOT");
        gm.SetParticipantReady();
        yield return null; yield return null;
        var menu = gm.GetComponent<MenuUI>();
        yield return Shot("hub_grid");
        SetEnum(menu, "tab", 1); yield return Shot("hub_crosswind");
        SetEnum(menu, "tab", 2); yield return Shot("hub_order");
        SetEnum(menu, "tab", 3); yield return Shot("hub_attempts");
        SetEnum(menu, "tab", 0);

        string next = ParticipantProgress.NextMissionId();
        SetField(menu, "briefId", next); SetEnum(menu, "overlay", 1); yield return Shot("briefing");
        SetField(menu, "briefNotes", true); yield return Shot("briefing_notes");
        SetField(menu, "briefNotes", false);
        SetEnum(menu, "overlay", 2); yield return Shot("lab_confirm");
        SetEnum(menu, "overlay", 3); yield return Shot("help");
        SetEnum(menu, "overlay", 0);

        // 3. one mission at 8x, hands off, then the results page
        Time.timeScale = 8f;
        gm.StartSingleMission("L3");
        t0 = Time.realtimeSinceStartup;
        while (gm.State != GameState.Results)
        {
            if (Time.realtimeSinceStartup - t0 > 180f) { Debug.LogError("[UISHOTS] mission did not end"); break; }
            yield return null;
        }
        Time.timeScale = 1f;
        yield return null; yield return null;
        yield return Shot("results_mission");

        // 4. back in the hub with L3 marked
        gm.Confirm();
        yield return null; yield return null;
        yield return Shot("hub_after_mission");

        // 5. free flight ended
        gm.StartFlight(FlightMode.FreeFlight);
        yield return new WaitForSecondsRealtime(1f);
        gm.Aircraft.throttle = 0f;
        yield return null;
        // end it the way Esc would not (Esc goes to the hub); crash state is the other
        // free-flight ending, so photograph the plain "flight ended" via the level path
        gm.BackToMenu();
        yield return null;
        yield return Shot("hub_final");

        Debug.Log("[UISHOTS] " + n + " screenshots in " + dir);
        Done();
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        RenderTexture.active = null;   // the back buffer, which is where OnGUI draws
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();
        string file = Path.Combine(dir, (++n).ToString("00") + "_" + name + ".png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log("[UISHOTS]   wrote " + file);
        yield return null;
    }

    static void SetField(object o, string field, object value)
    {
        if (o == null) { Debug.LogWarning("[UISHOTS] no object for " + field); return; }
        var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning("[UISHOTS] no field " + field); return; }
        f.SetValue(o, value);
    }

    static void SetEnum(object o, string field, int value)
    {
        if (o == null) return;
        var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) { Debug.LogWarning("[UISHOTS] no field " + field); return; }
        f.SetValue(o, System.Enum.ToObject(f.FieldType, value));
    }

    void Done()
    {
        Finished = true;
        SimDriver.Release("UiDesignShots");
        // In a player there is no PlayCapture to stop the run: quit so the shell gets the
        // exit, and the window does not sit on someone's screen.
        if (!Application.isEditor) Application.Quit();
    }
}
