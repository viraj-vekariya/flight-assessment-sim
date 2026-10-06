using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Generates a JSON session report after every graded session or campaign run.
/// Computes the Cognitive Load Index (CLI) — a single 0..100 number that
/// measures how much the pilot's performance degraded under cognitive load
/// vs. the calm baseline level.
///
/// CLI = mean of three normalised degradation scores:
///   • Precision drop    (baseline precision − loaded precision)
///   • Reaction drop     (baseline reaction  − loaded reaction score)
///   • Error-rate rise   (baseline errors    − loaded errors score)
/// Each component is clamped to [0, 100]; the mean is the CLI.
/// CLI = 0 → no degradation (performed identically under load vs. baseline).
/// CLI = 100 → maximum possible performance collapse.
///
/// The per-trial NASA-TLX/BEDFORD self-reports it used to fold in have been removed
/// from this project, so the report is now purely behavioural.
///
/// Output: FlightSimData/{PID}/Reports/{PID}_S{N}_{stamp}.json
/// </summary>
public static class SessionReporter
{
    public static float  LastCLI      { get; private set; }
    public static string LastFilePath { get; private set; } = "";

    // ---- public entry point -----------------------------------------------

    public static void Generate(List<ScenarioResult> results)
    {
        if (results == null || results.Count == 0) return;

        float cli = ComputeCLI(results);
        LastCLI = cli;

        string pid     = ParticipantManager.FilePrefix;
        string sesTag  = ParticipantManager.SessionTag;
        string stamp   = ParticipantManager.ISONow();

        string dir = Path.Combine(Application.persistentDataPath,
                                  "FlightSimData", pid, "Reports");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{pid}_{sesTag}_{stamp}.json");
        LastFilePath = path;

        string json = BuildJSON(results, cli, stamp);
        File.WriteAllText(path, json, Encoding.UTF8);
        Debug.Log($"[SessionReporter] Written: {path}  CLI={cli:F1}");
    }

    // ---- CLI computation --------------------------------------------------

    static float ComputeCLI(List<ScenarioResult> results)
    {
        if (results.Count < 2) return 0f;

        var baseline = results[0];
        float bPrec = Component(baseline, "Precision");
        float bReact = Component(baseline, "Reaction");
        float bErr  = Component(baseline, "Errors");

        float sumPrec = 0f, sumReact = 0f, sumErr = 0f;
        int n = results.Count - 1;
        for (int i = 1; i <= n; i++)
        {
            sumPrec  += Component(results[i], "Precision");
            sumReact += Component(results[i], "Reaction");
            sumErr   += Component(results[i], "Errors");
        }
        float precDrop  = Mathf.Max(0f, bPrec  - sumPrec  / n);
        float reactDrop = Mathf.Max(0f, bReact - sumReact / n);
        float errDrop   = Mathf.Max(0f, bErr   - sumErr   / n);

        return Mathf.Clamp((precDrop + reactDrop + errDrop) / 3f, 0f, 100f);
    }

    static float Component(ScenarioResult r, string name)
    {
        foreach (var c in r.Breakdown)
            if (string.Compare(c.Name, name, System.StringComparison.OrdinalIgnoreCase) == 0)
                return c.Raw;
        return 50f;   // default when component not tracked for this goal
    }

    // ---- JSON builder (manual — avoids JsonUtility Dictionary limitations) -

    static string BuildJSON(List<ScenarioResult> results, float cli, string stamp)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        string pid   = ParticipantManager.FilePrefix;
        int    sess  = ParticipantManager.Session;
        float  base_ = results.Count > 0 ? results[0].Score : 0f;
        float  load  = 0f;
        for (int i = 1; i < results.Count; i++) load += results[i].Score;
        if (results.Count > 1) load /= (results.Count - 1);

        sb.AppendLine("{");
        sb.AppendLine($"  \"participant_id\": {J(pid)},");
        sb.AppendLine($"  \"session_number\": {sess},");
        sb.AppendLine($"  \"iso_datetime\": {J(System.DateTime.Now.ToString("o"))},");
        sb.AppendLine($"  \"sim_version\": \"v36\",");
        sb.AppendLine($"  \"cognitive_load_index\": {cli.ToString("F1", ci)},");
        sb.AppendLine($"  \"baseline_score\": {base_.ToString("F1", ci)},");
        sb.AppendLine($"  \"loaded_score_mean\": {load.ToString("F1", ci)},");
        sb.AppendLine($"  \"score_delta\": {(load - base_).ToString("F1", ci)},");
        sb.AppendLine( "  \"levels\": [");

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            string cond = i == 0 ? "BASE" : "LOAD";
            sb.AppendLine("    {");
            sb.AppendLine($"      \"index\": {i},");
            sb.AppendLine($"      \"id\": {J(r.SessionLabel)},");
            sb.AppendLine($"      \"condition\": {J(cond)},");
            sb.AppendLine($"      \"score\": {r.Score.ToString("F1", ci)},");
            sb.AppendLine($"      \"passed\": {(r.Passed ? "true" : "false")},");
            sb.AppendLine($"      \"outcome\": {J(r.Outcome)},");
            sb.AppendLine($"      \"precision\": {Component(r, "Precision").ToString("F1", ci)},");
            sb.AppendLine($"      \"reaction\": {Component(r, "Reaction").ToString("F1", ci)},");
            sb.AppendLine($"      \"errors\": {Component(r, "Errors").ToString("F1", ci)},");
            sb.AppendLine($"      \"completion\": {Component(r, "Completion").ToString("F1", ci)},");
            sb.AppendLine($"      \"smoothness\": {Component(r, "Smoothness").ToString("F1", ci)},");
            sb.AppendLine($"      \"landing\": {Component(r, "Landing").ToString("F1", ci)}");
            sb.Append(i < results.Count - 1 ? "    }," : "    }");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static string J(string s)
    {
        if (s == null) return "null";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
