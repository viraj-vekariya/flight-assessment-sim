using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes one timestamped CSV per measured-level run (L0/L1/L2), metric units,
/// with a session-start marker. 10 Hz samples of the flight state + deviation
/// from the level's target, plus a summary block with the per-level outcome.
/// Local only — analysis-ready for later phases.
/// </summary>
public class FlightDataLogger : MonoBehaviour
{
    StreamWriter writer;
    bool open;
    public string FilePath { get; private set; }

    const int Columns = 13;

    public void Begin(FlightMode mode)
    {
        string pid = ParticipantManager.FilePrefix;
        string dir = Path.Combine(Application.persistentDataPath, "FlightSimData", pid);
        Directory.CreateDirectory(dir);
        string stamp = ParticipantManager.ISONow();
        FilePath = Path.Combine(dir, $"{pid}_{ParticipantManager.SessionTag}_{mode}_{stamp}.csv");

        writer = new StreamWriter(FilePath, false, Encoding.UTF8);
        writer.WriteLine("# session_start," + System.DateTime.Now.ToString("o"));
        writer.WriteLine("# participant," + pid);
        writer.WriteLine("# session," + ParticipantManager.Session);
        writer.WriteLine("# mode," + mode);
        writer.WriteLine("time_s,airspeed_kmh,altitude_m,heading_deg,vspeed_ms," +
                         "target_alt_m,target_hdg_deg,alt_err_m,hdg_err_deg," +
                         "throttle,flaps,grounded,stalled");
        open = true;
    }

    public void Sample(CessnaPhysics ac, LevelManager lvl, float t)
    {
        if (!open) return;
        var ci = CultureInfo.InvariantCulture;
        string[] c =
        {
            t.ToString("F2", ci),
            ac.AirspeedKmh.ToString("F1", ci),
            ac.AltitudeM.ToString("F1", ci),
            ac.HeadingDeg.ToString("F1", ci),
            ac.VerticalSpeedMs.ToString("F2", ci),
            lvl.TargetAltitude.ToString("F0", ci),
            lvl.TargetHeading.ToString("F0", ci),
            lvl.AltError.ToString("F1", ci),
            lvl.HeadingError.ToString("F1", ci),
            ac.Throttle01.ToString("F2", ci),
            ac.Flaps01.ToString("F2", ci),
            ac.Grounded ? "1" : "0",
            ac.Stalled ? "1" : "0"
        };
        writer.WriteLine(string.Join(",", c));
    }

    public void LogEvent(float t, string tag)
    {
        if (!open) return;
        string[] c = new string[Columns];
        for (int i = 0; i < Columns; i++) c[i] = "";
        c[0] = t.ToString("F2", CultureInfo.InvariantCulture);
        c[Columns - 1] = "\"" + tag.Replace("\"", "'") + "\"";
        writer.WriteLine(string.Join(",", c));
    }

    public void WriteSummary(LevelResult r)
    {
        if (!open) return;
        writer.WriteLine();
        writer.WriteLine("# SUMMARY");
        writer.WriteLine("# outcome," + (r.Passed ? "PASS" : "FAIL"));
        writer.WriteLine("# score," + r.Score.ToString("F1"));
        foreach (var kv in r.Metrics)
            writer.WriteLine("# " + kv.Key + "," + kv.Value.ToString("F2"));
    }

    public void Close()
    {
        if (!open) return;
        writer.Flush();
        writer.Close();
        open = false;
    }

    void OnDestroy() => Close();
}
