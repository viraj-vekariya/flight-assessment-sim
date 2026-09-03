// GizmoIcons — turn off Unity's built-in gizmo ICONS in the Scene and Game views.
//
// WHAT THIS IS FOR
//   Unity draws a billboarded icon over every GameObject carrying certain components:
//   a loudspeaker for an AudioSource, a loudspeaker with radiating lines for an
//   AudioListener (which reads as a sun), a cloud for a WindZone, a movie camera for a
//   Camera, a bulb for a Light. They are drawn by the EDITOR, on top of everything, with
//   no depth test — which is why they appear to be stuck to the aeroplane and floating in
//   the cockpit.
//
//   They are not objects in the scene. Nothing in the simulation creates them, deleting
//   scene objects will not remove them, and THEY DO NOT EXIST IN A BUILD: the shipped
//   Windows player has never shown them and never will. They appear only in the Editor's
//   Game view, and only while its "Gizmos" toggle is on.
//
// WHY DO IT IN CODE RATHER THAN JUST SAYING "CLICK THE GIZMOS BUTTON"
//   Because the setting is per-user and easy to lose, and because a participant-facing
//   screenshot taken from the Editor should look like the simulator, not like a scene
//   view. This disables the icons themselves, so they stay gone whether or not the Gizmos
//   toggle happens to be on.
//
// HOW
//   Through UnityEditor.AnnotationUtility, which is internal, so it is reached by
//   reflection. Every call is guarded: if a future Unity version renames any of it, this
//   file logs once and does nothing. It is in an Editor folder, so it is never compiled
//   into a player and cannot affect the build.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GizmoIcons
{
    const string Menu = "Tools/Experiment/";

    static GizmoIcons()
    {
        // Applied on every editor load rather than once, because the annotation state is
        // per-project-per-user and a fresh checkout or a cleared Library brings the icons
        // back. Re-enable them from the menu below if they are ever wanted.
        EditorApplication.delayCall += () => SetAll(false, quiet: true);
    }

    [MenuItem(Menu + "Hide gizmo icons in Scene and Game view")]
    public static void Hide() => SetAll(false, quiet: false);

    [MenuItem(Menu + "Show gizmo icons again")]
    public static void Show() => SetAll(true, quiet: false);

    static void SetAll(bool enabled, bool quiet)
    {
        try
        {
            Type util = typeof(Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
            if (util == null) { Warn("UnityEditor.AnnotationUtility not found"); return; }

            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            MethodInfo getAnnotations = util.GetMethod("GetAnnotations", Flags);
            MethodInfo setIconEnabled = util.GetMethod("SetIconEnabled", Flags);
            if (getAnnotations == null || setIconEnabled == null)
            { Warn("GetAnnotations / SetIconEnabled not found"); return; }

            var annotations = getAnnotations.Invoke(null, null) as Array;
            if (annotations == null) { Warn("GetAnnotations returned nothing"); return; }

            int n = 0;
            foreach (object a in annotations)
            {
                Type at = a.GetType();
                FieldInfo fClass = at.GetField("classID");
                FieldInfo fScript = at.GetField("scriptClass");
                if (fClass == null || fScript == null) continue;
                int classId = (int)fClass.GetValue(a);
                string scriptClass = (string)fScript.GetValue(a);
                // 1 = show icon, 0 = hide it.
                setIconEnabled.Invoke(null, new object[] { classId, scriptClass, enabled ? 1 : 0 });
                n++;
            }

            if (!quiet)
                Debug.Log("[GizmoIcons] " + (enabled ? "enabled" : "disabled") + " " + n
                        + " gizmo icons. These are Editor-only overlays; a build never shows them.");
        }
        catch (Exception e)
        {
            Warn(e.Message);
        }
    }

    static bool warned;
    static void Warn(string why)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning("[GizmoIcons] could not change gizmo icons (" + why + "). "
                       + "Turn them off by hand with the Gizmos button in the Game view toolbar. "
                       + "This affects the Editor only — a build never draws them.");
    }
}
