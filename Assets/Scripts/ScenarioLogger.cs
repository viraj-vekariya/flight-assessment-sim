// Per-scenario data file: timestamped CSV with control inputs, flight params +
// deviations, plus inline timestamped EVENT MARKERS. The header carries a START
// trigger (wall-clock ISO time + a monotonic clock) so a future EEG/LSL stream can
// be aligned to these markers later — the "sync rails". No hardware is connected.

using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class ScenarioLogger
{
    /// <summary>UTF-8 with no byte-order mark — a BOM breaks plain json/csv readers.</summary>
    static readonly System.Text.UTF8Encoding Utf8NoBom = new System.Text.UTF8Encoding(false);

    StreamWriter w;
    bool open;
    public string FilePath { get; private set; }

    public void Begin(Scenario s)
    {
        string pid = ParticipantManager.FilePrefix;
        string dir = Path.Combine(Application.persistentDataPath, "FlightSimData", pid);
        Directory.CreateDirectory(dir);
        string stamp = ParticipantManager.ISONow();
        FilePath = Path.Combine(dir, $"{pid}_{ParticipantManager.SessionTag}_{s.Id}_{stamp}.csv");

        w = new StreamWriter(FilePath, false, Utf8NoBom);
        // --- sync rails: align EEG/LSL streams to these two clocks ---
        w.WriteLine("# START_TRIGGER," + System.DateTime.Now.ToString("o") +
                    ",monotonic_s," + Time.realtimeSinceStartup.ToString("F4", CultureInfo.InvariantCulture));
        w.WriteLine("# participant," + pid);
        w.WriteLine("# session," + ParticipantManager.Session);
        w.WriteLine("# scenario," + s.Id + "," + s.Title);
        w.WriteLine("# difficulty," + s.DiffLabel + "," + s.Difficulty.ToString("F2", CultureInfo.InvariantCulture));
        w.WriteLine("# goal," + s.Goal);
        LSLSync.Marker("SCENARIO_START," + s.Id);
        w.WriteLine("time_s,pitch_in,roll_in,yaw_in,throttle,flaps,brake," +
                    "airspeed_kmh,altitude_m,heading_deg,vspeed_ms,roll_deg,pitch_deg," +
                    "target_alt_m,target_hdg_deg,alt_err_m,hdg_err_deg,grounded,stalled,weather,fault");
        open = true;
    }

    public void Sample(CessnaPhysics ac, ScenarioEngine e, float t)
    {
        if (!open) return;
        var ci = CultureInfo.InvariantCulture;
        string[] c =
        {
            t.ToString("F2", ci),
            ac.pitchInput.ToString("F2", ci), ac.rollInput.ToString("F2", ci), ac.yawInput.ToString("F2", ci),
            ac.Throttle01.ToString("F2", ci), ac.Flaps01.ToString("F2", ci), ac.braking ? "1" : "0",
            ac.AirspeedKmh.ToString("F1", ci), ac.AltitudeM.ToString("F1", ci), ac.HeadingDeg.ToString("F1", ci),
            ac.VerticalSpeedMs.ToString("F2", ci), ac.RollDeg.ToString("F1", ci), ac.PitchDeg.ToString("F1", ci),
            e.CurTargetAlt.ToString("F0", ci), e.CurTargetHdg.ToString("F0", ci),
            e.AltError.ToString("F1", ci), e.HdgError.ToString("F1", ci),
            ac.Grounded ? "1" : "0", ac.Stalled ? "1" : "0",
            e.WeatherActive ? "1" : "0", e.FaultActive ? "1" : "0"
        };
        w.WriteLine(string.Join(",", c));
    }

    /// <summary>An inline, timestamped event marker (relative sim time + wall clock)
    /// AND a live LSL push if the LSL4Unity package is installed.</summary>
    public void Marker(float t, string tag, string detail = "")
    {
        if (!open) return;
        var ci = CultureInfo.InvariantCulture;
        w.WriteLine("# MARK," + t.ToString("F2", ci) + "," +
                    System.DateTime.Now.ToString("o") + "," + tag +
                    (string.IsNullOrEmpty(detail) ? "" : "," + detail.Replace(",", ";")));
        // push to LSL outlet (no-op when LSL4Unity is not installed)
        string lslTag = string.IsNullOrEmpty(detail) ? tag : tag + "," + detail;
        LSLSync.Marker(lslTag);
    }

    public void WriteSummary(ScenarioResult r)
    {
        if (!open) return;
        w.WriteLine();
        w.WriteLine("# SUMMARY");
        w.WriteLine("# outcome," + r.Outcome);
        w.WriteLine("# passed," + (r.Passed ? "1" : "0"));
        w.WriteLine("# score," + r.Score.ToString("F1", CultureInfo.InvariantCulture));
        foreach (var kv in r.Metrics)
            w.WriteLine("# " + kv.Key + "," + kv.Value.ToString("F2", CultureInfo.InvariantCulture));
        // transparent score breakdown: component, raw 0-100, weight, contribution
        foreach (var c in r.Breakdown)
            w.WriteLine("# component_" + c.Name + "," + c.Raw.ToString("F1", CultureInfo.InvariantCulture) +
                        ",w=" + c.Weight.ToString("F2", CultureInfo.InvariantCulture) +
                        ",contrib=" + c.Contribution.ToString("F1", CultureInfo.InvariantCulture));
    }

    public void Close()
    {
        if (!open) return;
        w.Flush(); w.Close(); open = false;
    }
}
