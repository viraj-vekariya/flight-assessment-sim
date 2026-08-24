// The 9 Phase-2 scenarios, defined purely as DATA. Each builder takes a difficulty
// 0..1 that scales tolerances, event frequency/intensity and response windows, so
// "easy -> hard" is one knob. ScenarioEngine interprets these; adding a new level
// means adding a builder here, not touching the engine.

using System.Collections.Generic;
using UnityEngine;

public static class ScenarioLibrary
{
    // landmark positions (match WorldBuilder), used for navigation waypoints
    static readonly Vector3 Downtown = new Vector3(250f, 500f, 1950f);
    static readonly Vector3 SuburbA = new Vector3(-950f, 500f, 1500f);
    static readonly Vector3 SuburbB = new Vector3(1250f, 500f, 1050f);
    static readonly Vector3 Farm = new Vector3(-1250f, 500f, -300f);

    static float Lerp(float easy, float hard, float d) => Mathf.Lerp(easy, hard, Mathf.Clamp01(d));

    /// <summary>The nine scenarios at a chosen difficulty, in curriculum order.</summary>
    public static List<Scenario> All(float d) => new List<Scenario>
    {
        Turns(d), Monitor(d), Alarms(d), Weather(d), Navigate(d),
        Decision(d), Multitask(d), Approach(d), FullMission(d)
    };

    /// <summary>Baseline (easy hold) then a graded ramp across the scenarios.</summary>
    public static List<Scenario> GradedSession()
    {
        var list = new List<Scenario>();
        var baseline = Hold("SC0_Baseline", "Baseline — calm hold", 0f);
        baseline.Desc = "Warm-up: hold altitude and heading in calm air.";
        list.Add(baseline);
        list.Add(Turns(0.3f));
        list.Add(Monitor(0.45f));
        list.Add(Alarms(0.6f));
        list.Add(Weather(0.7f));
        list.Add(Multitask(0.85f));
        return list;
    }

    // ---- shared template: a level-hold base any scenario can build on ----
    static Scenario Hold(string id, string title, float d)
    {
        return new Scenario
        {
            Id = id, Title = title, Difficulty = d,
            Start = ScenarioStart.Airborne, Goal = ScenarioGoal.HoldTargets,
            Duration = Lerp(60f, 100f, d),
            TargetAltitude = 500f, TargetHeading = 0f,
            AltTolerance = Lerp(120f, 45f, d),
            HdgTolerance = Lerp(14f, 5f, d),
        };
    }

    // ---- basic campaign levels (public) ----
    public static Scenario StraightLevel(float d)
    {
        var s = Hold("C_SL", "First flight — straight & level", d);
        s.Desc = "Hold altitude and heading in calm air. Use gentle inputs.";
        s.Duration = Lerp(45f, 70f, d);
        s.AltTolerance = Lerp(150f, 80f, d); s.HdgTolerance = Lerp(16f, 9f, d);
        return s;
    }

    public static Scenario ClimbDescend(float d)
    {
        var s = Hold("C_CD", "Climb & descend", d);
        s.Desc = "Follow the altitude calls — climb, then descend — holding heading.";
        s.AltTolerance = Lerp(120f, 70f, d);
        s.Events.Add(new ScenarioEvent { Time = 12f, Type = ScenarioEventType.AltitudeChange, Value = 700f, Label = "climb to 700 m" });
        s.Events.Add(new ScenarioEvent { Time = Lerp(42f, 36f, d), Type = ScenarioEventType.AltitudeChange, Value = 400f, Label = "descend to 400 m" });
        return s;
    }

    public static Scenario Takeoff(float d)
    {
        return new Scenario
        {
            Id = "C_TO", Title = "Takeoff", Difficulty = d,
            Start = ScenarioStart.Runway, Goal = ScenarioGoal.TakeoffClimb,
            Duration = 120f, TargetAltitude = 300f, TargetHeading = 0f,
            AltTolerance = 60f, HdgTolerance = 15f,
            Desc = "Hold Left-Shift for full power, keep straight (Q/E), rotate ~100 km/h, climb to 300 m."
        };
    }

    public static Scenario LevelHold(float d)
    {
        var s = Hold("C_LH", "Precision hold", d);
        s.Desc = "Hold altitude and heading to a tight tolerance.";
        s.AltTolerance = Lerp(80f, 40f, d); s.HdgTolerance = Lerp(10f, 5f, d);
        return s;
    }

    // 1. Turns / coordination — a series of ATC heading changes to track.
    public static Scenario Turns(float d)
    {
        var s = Hold("SC1_Turns", "Turns & coordination", d);
        s.Desc = "Follow ATC heading changes, holding altitude through each turn.";
        int turns = Mathf.RoundToInt(Lerp(3f, 6f, d));
        float[] hdgs = { 90f, 150f, 60f, 200f, 300f, 20f, 120f };
        float gap = s.Duration / (turns + 1);
        for (int i = 0; i < turns; i++)
            s.Events.Add(new ScenarioEvent
            {
                Time = gap * (i + 1), Type = ScenarioEventType.HeadingChange,
                Value = hdgs[(i + 1) % hdgs.Length],
                Label = "turn to " + hdgs[(i + 1) % hdgs.Length].ToString("F0") + "°"
            });
        return s;
    }

    // 2. Instrument monitoring — gauges fail; notice and acknowledge.
    public static Scenario Monitor(float d)
    {
        var s = Hold("SC2_Monitor", "Instrument monitoring", d);
        s.Desc = "Watch the panel: when an instrument fails, press SPACE to acknowledge.";
        int faults = Mathf.RoundToInt(Lerp(2f, 5f, d));
        float win = Lerp(4f, 2f, d), dur = Lerp(7f, 5f, d);
        float gap = s.Duration / (faults + 1);
        for (int i = 0; i < faults; i++)
            s.Events.Add(new ScenarioEvent
            {
                Time = gap * (i + 1) + Random.Range(-2f, 2f),
                Type = ScenarioEventType.InstrumentFault, Duration = dur,
                Gauge = -1, RequiresResponse = true, ResponseWindow = win,
                Label = "INSTRUMENT FAULT"
            });
        return s;
    }

    // 3. Sudden alarms / warnings — acknowledge quickly while holding.
    public static Scenario Alarms(float d)
    {
        var s = Hold("SC3_Alarms", "Sudden alarms", d);
        s.Desc = "Acknowledge each warning (SPACE) as fast as you can while holding altitude/heading.";
        int n = Mathf.RoundToInt(Lerp(3f, 7f, d));
        float win = Lerp(3.5f, 1.6f, d);
        string[] warn = { "ENGINE TEMP", "LOW OIL PRESS", "FUEL LOW", "GEAR WARN", "TRAFFIC", "STALL WARN", "ELECTRICAL" };
        float gap = s.Duration / (n + 1);
        for (int i = 0; i < n; i++)
            s.Events.Add(new ScenarioEvent
            {
                Time = gap * (i + 1) + Random.Range(-2f, 2f), Type = ScenarioEventType.Alarm,
                RequiresResponse = true, ResponseWindow = win, Label = warn[i % warn.Length]
            });
        return s;
    }

    // 4. Low-visibility / weather — turbulence + fog for the run.
    public static Scenario Weather(float d)
    {
        var s = Hold("SC4_Weather", "Low-visibility & weather", d);
        s.Desc = "Hold altitude/heading through turbulence and reduced visibility.";
        s.AltTolerance = Lerp(140f, 70f, d);
        s.Events.Add(new ScenarioEvent
        {
            Time = 4f, Type = ScenarioEventType.Weather, Duration = s.Duration - 6f,
            Value = Lerp(0.4f, 1f, d), Label = "WEATHER"
        });
        return s;
    }

    // 5. Navigation — fly through a sequence of waypoints over the landmarks.
    public static Scenario Navigate(float d)
    {
        var s = new Scenario
        {
            Id = "SC5_Nav", Title = "Navigation", Difficulty = d,
            Start = ScenarioStart.Airborne, Goal = ScenarioGoal.Navigate,
            Duration = Lerp(180f, 150f, d),
            TargetAltitude = 500f, TargetHeading = 0f,
            AltTolerance = 200f, HdgTolerance = 180f,
            Desc = "Fly through each waypoint marker in order. The HUD shows bearing + distance."
        };
        float rad = Lerp(220f, 120f, d);
        s.Waypoints.Add(new Waypoint(SuburbB, "ALPHA", rad));
        s.Waypoints.Add(new Waypoint(Downtown, "BRAVO", rad));
        s.Waypoints.Add(new Waypoint(SuburbA, "CHARLIE", rad));
        if (d > 0.5f) s.Waypoints.Add(new Waypoint(Farm, "DELTA", rad));
        return s;
    }

    // 6. Time-constrained decision — respond to prompts within a shrinking window.
    public static Scenario Decision(float d)
    {
        var s = Hold("SC6_Decision", "Time-constrained decision", d);
        s.Desc = "When a decision prompt appears, respond (SPACE) before the window expires.";
        int n = Mathf.RoundToInt(Lerp(3f, 5f, d));
        float win = Lerp(2.5f, 1.2f, d);
        string[] q = { "DIVERT? SPACE", "GO-AROUND? SPACE", "ABORT? SPACE", "CONFIRM? SPACE" };
        float gap = s.Duration / (n + 1);
        for (int i = 0; i < n; i++)
            s.Events.Add(new ScenarioEvent
            {
                Time = gap * (i + 1), Type = ScenarioEventType.Decision,
                RequiresResponse = true, ResponseWindow = win, Label = q[i % q.Length]
            });
        return s;
    }

    // 7. Multi-tasking mission — heading changes + faults + a secondary task at once.
    public static Scenario Multitask(float d)
    {
        var s = Hold("SC7_Multitask", "Multi-tasking mission", d);
        s.Desc = "Hold the target, follow heading changes, AND respond to alarms/faults — all at once.";
        s.Events.Add(new ScenarioEvent { Time = 12f, Type = ScenarioEventType.HeadingChange, Value = 140f, Label = "turn to 140°" });
        s.Events.Add(new ScenarioEvent { Time = 40f, Type = ScenarioEventType.HeadingChange, Value = 60f, Label = "turn to 060°" });
        s.Events.Add(new ScenarioEvent { Time = 65f, Type = ScenarioEventType.AltitudeChange, Value = 700f, Label = "climb to 700 m" });
        int n = Mathf.RoundToInt(Lerp(3f, 6f, d));
        float win = Lerp(3f, 1.6f, d);
        for (int i = 0; i < n; i++)
            s.Events.Add(new ScenarioEvent
            {
                Time = 8f + i * (s.Duration - 12f) / n, Type = (i % 2 == 0) ? ScenarioEventType.Alarm : ScenarioEventType.InstrumentFault,
                Duration = 6f, RequiresResponse = true, ResponseWindow = win, Gauge = -1,
                Label = (i % 2 == 0) ? "WARNING" : "INSTRUMENT FAULT"
            });
        if (d > 0.5f)
            s.Events.Add(new ScenarioEvent { Time = 6f, Type = ScenarioEventType.Weather, Duration = s.Duration - 10f, Value = Lerp(0.3f, 0.7f, d), Label = "WEATHER" });
        return s;
    }

    // 8. Landing / approach — fly the final and touch down on the runway.
    public static Scenario Approach(float d)
    {
        var s = new Scenario
        {
            Id = "SC8_Approach", Title = "Landing / approach", Difficulty = d,
            Start = ScenarioStart.Airborne, Goal = ScenarioGoal.Land,
            Duration = 180f,
            TargetAltitude = 0f, TargetHeading = 0f,
            AltTolerance = 100f, HdgTolerance = 10f,
            Desc = "You start on a 3 km final lined up with the runway. Descend and land on the centreline."
        };
        if (d > 0.4f)
            s.Events.Add(new ScenarioEvent { Time = 2f, Type = ScenarioEventType.Weather, Duration = 120f, Value = Lerp(0.3f, 0.8f, d), Label = "CROSSWIND" });
        return s;
    }

    // 9. Full mission — takeoff, navigate the waypoints, then land.
    public static Scenario FullMission(float d)
    {
        var s = new Scenario
        {
            Id = "SC9_Mission", Title = "Full mission", Difficulty = d,
            Start = ScenarioStart.Runway, Goal = ScenarioGoal.Mission,
            Duration = 360f,
            TargetAltitude = 400f, TargetHeading = 0f,
            AltTolerance = 150f, HdgTolerance = 180f,
            Desc = "Take off, climb, fly through the waypoints, then return and land on the runway."
        };
        float rad = Lerp(250f, 150f, d);
        s.Waypoints.Add(new Waypoint(new Vector3(0f, 400f, 1200f), "DEPART", rad));
        s.Waypoints.Add(new Waypoint(Downtown, "CITY", rad));
        s.Waypoints.Add(new Waypoint(SuburbA, "TURN", rad));
        if (d > 0.5f)
            s.Events.Add(new ScenarioEvent { Time = 60f, Type = ScenarioEventType.Alarm, RequiresResponse = true, ResponseWindow = 3f, Label = "TRAFFIC" });
        return s;
    }

    // ---- "Weather Transfer": a complete 7-minute mission from the SECOND strip,
    // through weather + vigilance + navigation, to a landing on the MAIN runway.
    // Self-contained data — uses only existing event types / goal / scoring.
    // Fixed medium difficulty (the d param is accepted for Campaign compatibility).
    public static Scenario WeatherTransfer(float d)
    {
        var s = new Scenario
        {
            Id = "SC_TRANSFER", Title = "Weather Transfer", Difficulty = 0.5f,
            Start = ScenarioStart.Runway, Goal = ScenarioGoal.Mission,
            Duration = 420f,
            TargetAltitude = 450f, TargetHeading = 0f,
            AltTolerance = 150f, HdgTolerance = 180f,
            Desc = "Depart the runway, climb through weather to the waypoints (HOTEL, INDIA, JULIET), then land back on the runway."
        };

        // PHASE 1 — pre-departure (0–15 s)
        s.Events.Add(E(0f, ScenarioEventType.Message,
            "CLEARANCE RECEIVED — Runway heading, climb to 450 m. First waypoint: HOTEL. Winds calm."));

        // PHASE 2 — climb & departure (15–80 s)
        s.Events.Add(E(20f, ScenarioEventType.Message, "DEPARTURE — climb to 450 m, maintain runway heading."));
        s.Events.Add(AV(45f, ScenarioEventType.AltitudeChange, 450f, "climb to 450 m"));
        s.Events.Add(E(70f, ScenarioEventType.Message, "DEPARTURE COMPLETE — contact approach on reaching 450 m."));

        // PHASE 3 — weather & vigilance (80–220 s): the demanding section
        s.Events.Add(new ScenarioEvent { Time = 80f, Type = ScenarioEventType.Weather, Duration = 130f, Value = 0.55f, Label = "WEATHER ALERT" });
        s.Events.Add(new ScenarioEvent { Time = 105f, Type = ScenarioEventType.InstrumentFault, Duration = 10f, Gauge = -1, RequiresResponse = true, ResponseWindow = 4.5f, Label = "INSTRUMENT FAULT" });
        s.Events.Add(AV(130f, ScenarioEventType.HeadingChange, 45f, "turn RIGHT, fly heading 045°"));
        s.Events.Add(new ScenarioEvent { Time = 155f, Type = ScenarioEventType.Alarm, RequiresResponse = true, ResponseWindow = 3.5f, Label = "TRAFFIC ALERT" });
        s.Events.Add(AV(175f, ScenarioEventType.AltitudeChange, 350f, "descend to 350 m"));
        s.Events.Add(new ScenarioEvent { Time = 200f, Type = ScenarioEventType.InstrumentFault, Duration = 8f, Gauge = -1, RequiresResponse = true, ResponseWindow = 4f, Label = "INSTRUMENT FAULT" });

        // PHASE 4 — approach setup (220–300 s): weather has cleared (lasted 130 s)
        s.Events.Add(E(225f, ScenarioEventType.Message, "Weather clearing — begin descent. Fly toward JULIET, main runway ahead."));
        s.Events.Add(AV(235f, ScenarioEventType.HeadingChange, 0f, "turn to heading 000°"));
        s.Events.Add(AV(240f, ScenarioEventType.AltitudeChange, 250f, "descend to 250 m"));

        // PHASE 5 — landing (300+ s): touch down on the main runway
        s.Events.Add(E(290f, ScenarioEventType.Message, "Cleared to land — main runway. Maintain the centreline."));
        s.Events.Add(AV(305f, ScenarioEventType.AltitudeChange, 0f, "descend and land"));

        // Waypoints: HOTEL (off the SE strip), INDIA (over SuburbB), JULIET (on the
        // extended main-runway centreline, south of the threshold). Then land.
        float rad = 200f;
        s.Waypoints.Add(new Waypoint(new Vector3(1820f, 450f, -1320f), "HOTEL", rad));
        s.Waypoints.Add(new Waypoint(new Vector3(1250f, 350f, 1050f), "INDIA", rad));
        s.Waypoints.Add(new Waypoint(new Vector3(0f, 250f, -1300f), "JULIET", rad));
        return s;
    }

    // small builders to keep the event list above readable
    static ScenarioEvent E(float t, ScenarioEventType type, string label)
        => new ScenarioEvent { Time = t, Type = type, Label = label };
    static ScenarioEvent AV(float t, ScenarioEventType type, float value, string label)
        => new ScenarioEvent { Time = t, Type = type, Value = value, Label = label };
}
