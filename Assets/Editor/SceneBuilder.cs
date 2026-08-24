#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// "FlightSim ▸ Build Scene" — creates and saves Assets/Scenes/Main.unity with a
/// FlightSim/GameManager object and adds it to Build Settings. On Play the
/// GameManager builds the whole world, Cessna and cockpit from code, so you can
/// just open the project and press Play (this menu simply gives you a saved
/// scene asset and sets it as the startup scene).
/// </summary>
public static class SceneBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("FlightSim/Build Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("FlightSim").AddComponent<GameManager>();

        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        Debug.Log("FlightSim: scene saved to " + ScenePath + ". Press Play to fly.");
        EditorUtility.DisplayDialog("FlightSim",
            "Saved " + ScenePath + ".\n\nPress Play (▶) — the world, Cessna and cockpit " +
            "build automatically.", "OK");
    }
}
#endif
