using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Persists each per-trial WorkloadRating: one JSON file
/// (FlightSimData/{PID}/Workload/{PID}_{Session}_{scenarioId}_{stamp}.json) plus an
/// append-only per-participant CSV row for quick cross-trial analysis. Also pushes an
/// LSL marker so the rating timestamp can be aligned to the EEG stream like any other
/// scenario event.
/// </summary>
public static class WorkloadLog
{
    /// <summary>UTF-8 without a byte-order mark — see ExperimentLogger.</summary>
    static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    public static float  LastRTLX     { get; private set; }
    public static int    LastBedford  { get; private set; }
    public static string LastFilePath { get; private set; } = "";

    public static void Save(string scenarioId, string scenarioTitle, WorkloadRating r)
    {
        LastRTLX = r.RTLX;
        LastBedford = r.Bedford;
        var ci = CultureInfo.InvariantCulture;

        string pid = ParticipantManager.FilePrefix;
        string dir = Path.Combine(Application.persistentDataPath, "FlightSimData", pid, "Workload");
        Directory.CreateDirectory(dir);
        string stamp = ParticipantManager.ISONow();
        string jsonPath = Path.Combine(dir, $"{pid}_{ParticipantManager.SessionTag}_{scenarioId}_{stamp}.json");
        LastFilePath = jsonPath;

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"participant_id\": {J(pid)},");
        sb.AppendLine($"  \"session_number\": {ParticipantManager.Session},");
        sb.AppendLine($"  \"scenario_id\": {J(scenarioId)},");
        sb.AppendLine($"  \"scenario_title\": {J(scenarioTitle)},");
        sb.AppendLine($"  \"iso_datetime\": {J(System.DateTime.Now.ToString("o"))},");
        sb.AppendLine( "  \"nasa_tlx\": {");
        sb.AppendLine($"    \"mental_demand\": {r.MentalDemand.ToString("F1", ci)},");
        sb.AppendLine($"    \"physical_demand\": {r.PhysicalDemand.ToString("F1", ci)},");
        sb.AppendLine($"    \"temporal_demand\": {r.TemporalDemand.ToString("F1", ci)},");
        sb.AppendLine($"    \"performance\": {r.Performance.ToString("F1", ci)},");
        sb.AppendLine($"    \"effort\": {r.Effort.ToString("F1", ci)},");
        sb.AppendLine($"    \"frustration\": {r.Frustration.ToString("F1", ci)},");
        sb.AppendLine($"    \"rtlx\": {r.RTLX.ToString("F1", ci)}");
        sb.AppendLine( "  },");
        sb.AppendLine($"  \"bedford\": {r.Bedford}");
        sb.AppendLine("}");
        File.WriteAllText(jsonPath, sb.ToString(), Utf8NoBom);

        string csvPath = Path.Combine(dir, $"{pid}_workload_log.csv");
        bool isNew = !File.Exists(csvPath);
        using (var w = new StreamWriter(csvPath, true, Utf8NoBom))
        {
            if (isNew)
                w.WriteLine("session,scenario_id,scenario_title,mental_demand,physical_demand,temporal_demand," +
                            "performance,effort,frustration,rtlx,bedford,iso_datetime");
            w.WriteLine(string.Join(",", new[]
            {
                ParticipantManager.Session.ToString(ci), scenarioId, scenarioTitle.Replace(",", ";"),
                r.MentalDemand.ToString("F1", ci), r.PhysicalDemand.ToString("F1", ci), r.TemporalDemand.ToString("F1", ci),
                r.Performance.ToString("F1", ci), r.Effort.ToString("F1", ci), r.Frustration.ToString("F1", ci),
                r.RTLX.ToString("F1", ci), r.Bedford.ToString(ci), System.DateTime.Now.ToString("o")
            }));
        }

        LSLSync.Marker("WORKLOAD_RATING,rtlx=" + r.RTLX.ToString("F1", ci) + ",bedford=" + r.Bedford);
        Debug.Log($"[WorkloadLog] {scenarioId}: RTLX={r.RTLX:F1} BEDFORD={r.Bedford} -> {jsonPath}");
    }

    static string J(string s)
    {
        if (s == null) return "null";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
