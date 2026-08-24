// SimDriver — exactly one thing may fly the aeroplane at a time.
//
// WHY THIS EXISTS
//   The project has six headless harnesses that write control inputs: the mission
//   battery, the control battery, the wind battery, the flight verifier, the mission
//   screenshotter and the cockpit audit. If two of them run at once they fight every
//   frame, and the symptom is NOT a crash or an exception — it is a battery that
//   completes, reports numbers, and is wrong. FINAL_TEST_REPORT records this exact
//   failure already happening once: "let two harnesses drive the aeroplane at once —
//   this exact bug silently invalidated a whole battery run".
//
//   The defence in place was an OPT-OUT LIST inside FlightTest: it spawned on any
//   -batchmode run unless the command line contained one of seven named flags. That is
//   a defence that decays. Every new harness must remember to add its own flag to
//   someone else's file, and the failure mode for forgetting is silent corruption of
//   the new harness's results. It had already decayed: -windtest was not on the list,
//   so the wind battery ran with FlightTest flying the aeroplane, AircraftController
//   disabled behind its back, and every commanded control input discarded.
//
// THE RULE
//   A driver CLAIMS control before it writes anything. The first claim wins; any
//   later claim is refused, loudly, and the loser must stand down. No list to
//   maintain, and a conflict announces itself in the log instead of quietly changing
//   the numbers.

using UnityEngine;

public static class SimDriver
{
    /// <summary>Who currently owns the aircraft's control inputs, or null.</summary>
    public static string Owner { get; private set; }

    /// <summary>Try to take control. Returns false if someone else already has it —
    /// the caller must then disable itself and write nothing.</summary>
    public static bool Claim(string who)
    {
        if (Owner != null && Owner != who)
        {
            Debug.LogError($"[SimDriver] '{who}' tried to drive the aircraft but '{Owner}' already owns it. " +
                           $"'{who}' will stand down. Two drivers at once silently corrupts every " +
                           $"measurement in the run — do not run these harnesses together.");
            return false;
        }
        if (Owner == null) Debug.Log("[SimDriver] control claimed by " + who);
        Owner = who;
        return true;
    }

    public static void Release(string who) { if (Owner == who) Owner = null; }

    /// <summary>True when nothing has claimed control, i.e. the human is flying.</summary>
    public static bool Free => Owner == null;
}
