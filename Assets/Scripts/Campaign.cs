using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The structured level progression: one ordered ladder of stages that ascends in
/// difficulty and varies the skill each level. Each stage is just a Scenario built
/// at its difficulty (reusing ScenarioLibrary), so the campaign is data. Best score
/// + pass state per stage persist in PlayerPrefs, so you can go level by level.
/// </summary>
public static class Campaign
{
    public class Stage
    {
        public int Num;
        public string Name;
        public string Skill;
        public float Diff;                  // 0..1
        public Func<float, Scenario> Build;
    }

    public static readonly List<Stage> Stages = new List<Stage>
    {
        St(1,  "First Flight",     "Straight & level",     0.15f, ScenarioLibrary.StraightLevel),
        St(2,  "Up & Down",        "Climb & descend",      0.25f, ScenarioLibrary.ClimbDescend),
        St(3,  "Takeoff",          "Takeoff & climb",      0.30f, ScenarioLibrary.Takeoff),
        St(4,  "Turning",          "Turns & coordination", 0.35f, ScenarioLibrary.Turns),
        St(5,  "Precision Hold",   "Tight-tolerance hold", 0.50f, ScenarioLibrary.LevelHold),
        St(6,  "Scan",             "Instrument monitoring",0.50f, ScenarioLibrary.Monitor),
        St(7,  "Cross-Country",    "Navigation",           0.55f, ScenarioLibrary.Navigate),
        St(8,  "Emergencies",      "Sudden alarms",        0.60f, ScenarioLibrary.Alarms),
        St(9,  "Snap Decisions",   "Time-constrained",     0.65f, ScenarioLibrary.Decision),
        St(10, "Into the Weather", "Low-vis & turbulence", 0.70f, ScenarioLibrary.Weather),
        St(11, "The Approach",     "Landing",              0.65f, ScenarioLibrary.Approach),
        St(12, "Heads Full",       "Multi-tasking",        0.85f, ScenarioLibrary.Multitask),
        St(13, "Check Ride",       "Full mission",         0.90f, ScenarioLibrary.FullMission),
        St(14, "Weather Transfer", "SE strip → weather → land", 0.50f, ScenarioLibrary.WeatherTransfer),
    };

    static Stage St(int n, string name, string skill, float diff, Func<float, Scenario> b)
        => new Stage { Num = n, Name = name, Skill = skill, Diff = diff, Build = b };

    public static Stage Get(int num) => Stages.Find(s => s.Num == num);

    public static Scenario BuildScenario(Stage s)
    {
        var sc = s.Build(s.Diff);
        sc.CampaignStage = s.Num;
        sc.Title = "Lv " + s.Num + " · " + s.Name;
        return sc;
    }

    // ---- progress (persisted, keyed per participant) ----------------------
    public static float Best(int num)   => PlayerPrefs.GetFloat(ParticipantManager.BestKey(num), -1f);
    public static bool  Passed(int num) => PlayerPrefs.GetInt(ParticipantManager.PassKey(num), 0) == 1;

    public static void Record(int num, float score, bool passed)
    {
        string bk = ParticipantManager.BestKey(num);
        if (score > PlayerPrefs.GetFloat(bk, -1f)) PlayerPrefs.SetFloat(bk, score);
        if (passed) PlayerPrefs.SetInt(ParticipantManager.PassKey(num), 1);
        PlayerPrefs.Save();
    }

    /// <summary>First not-yet-passed stage (the one to "Continue" with).</summary>
    public static int NextStage()
    {
        foreach (var s in Stages) if (!Passed(s.Num)) return s.Num;
        return Stages[Stages.Count - 1].Num;   // all passed -> the last
    }

    /// <summary>A stage is unlocked if it's the first, already passed, or the previous one is passed.</summary>
    public static bool Unlocked(int num)
        => num <= Stages[0].Num || Passed(num) || Passed(num - 1);
}
