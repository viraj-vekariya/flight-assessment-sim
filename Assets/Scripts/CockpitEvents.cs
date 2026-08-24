// CockpitEvents — the one channel by which physical cockpit interaction reaches the
// experiment's marker stream.
//
// WHY A HUB RATHER THAN DIRECT CALLS
//   The controls (yoke, throttle, flap lever, trim wheel, switches) live on the
//   aircraft and the cockpit; the logger lives on the scenario engine. Wiring each
//   control directly to the engine would either couple the cockpit to the experiment
//   (so the cockpit stops working outside a mission) or duplicate the logging. A static
//   event hub keeps the cockpit ignorant of the experiment: it announces what the pilot
//   did, and whoever is listening decides whether that is data.
//
//   When no mission is running, nothing is subscribed and the announcements evaporate.
//
// MARKER DISCIPLINE — READ THIS BEFORE ADDING AN EVENT
//   These become EEG markers. A marker stream is only useful if a marker means
//   something. Raising one per frame while a lever moves would bury the meaningful
//   events (a failure cue, a decision) in thousands of motor ticks and make
//   event-locked averaging useless.
//
//   So: announce STATE TRANSITIONS, not motion.
//     * a detent was selected            -> yes, one event
//     * a switch changed state           -> yes, one event
//     * a control was grabbed / released -> yes, one event each
//     * the trim wheel moved 0.4 degrees -> NO. The subscriber debounces trim and
//                                           throttle; continuous position is already in
//                                           the 50 Hz telemetry, which is where
//                                           continuous data belongs.

using UnityEngine;

public static class CockpitEvents
{
    /// <summary>A physical control was grabbed. Argument is a stable control id
    /// ("yoke", "throttle", "flaps", "trim", "carb_heat", ...).</summary>
    public static System.Action<string> OnGrab;
    /// <summary>A physical control was released.</summary>
    public static System.Action<string> OnRelease;

    /// <summary>A flap detent was selected. (index, label)</summary>
    public static System.Action<int, string> OnFlapSelected;
    /// <summary>Trim position changed. Continuous — the subscriber MUST debounce.</summary>
    public static System.Action<float> OnTrimChanged;
    /// <summary>Throttle moved by a physical lever. Continuous — subscriber debounces.</summary>
    public static System.Action<float> OnThrottleMoved;
    /// <summary>Wheel brakes crossed the applied/released threshold. (applied, pressure)</summary>
    public static System.Action<bool, float> OnBrakeStateChanged;

    public static void RaiseGrab(string id)            => OnGrab?.Invoke(id);
    public static void RaiseRelease(string id)         => OnRelease?.Invoke(id);
    public static void RaiseFlapSelected(int i, string l) => OnFlapSelected?.Invoke(i, l);
    public static void RaiseTrimChanged(float v)       => OnTrimChanged?.Invoke(v);
    public static void RaiseThrottleMoved(float v)     => OnThrottleMoved?.Invoke(v);
    public static void RaiseBrakeState(bool on, float p) => OnBrakeStateChanged?.Invoke(on, p);

    /// <summary>Drop every subscriber. Called when a trial ends so a stale engine can
    /// never keep logging into a closed file.</summary>
    public static void ClearAll()
    {
        OnGrab = null; OnRelease = null; OnFlapSelected = null;
        OnTrimChanged = null; OnThrottleMoved = null; OnBrakeStateChanged = null;
    }
}
