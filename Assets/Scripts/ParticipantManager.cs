using UnityEngine;

/// <summary>
/// Singleton store for the current participant ID and session number.
/// All CSV filenames, PlayerPrefs campaign keys, and JSON reports are keyed
/// to the active participant so multi-participant lab runs never mix data.
/// </summary>
public static class ParticipantManager
{
    public static string ID      { get; private set; } = "";
    public static int    Session { get; private set; } = 1;

    /// <summary>True once the participant entry screen has been confirmed.</summary>
    public static bool IsSet => !string.IsNullOrEmpty(ID);

    /// <summary>Register the participant, and work out which VISIT this is.
    ///
    /// The visit number is counted from the DATA TREE — the number of session folders
    /// already under experiment/&lt;PID&gt;/ — not from PlayerPrefs.
    ///
    /// PlayerPrefs was the previous source and it demonstrably did not work: participant
    /// P002 has two session folders on disk and BOTH are named S01, because the counter is
    /// only persisted by RecordSessionComplete() and a participant who stops part-way
    /// through (which is the normal case when someone flies five missions today and five
    /// tomorrow) never reaches it. Two visits then collide on the session number and only
    /// the timestamp tells them apart.
    ///
    /// Counting folders cannot drift from the data, survives a cleared PlayerPrefs, and
    /// is correct even if the participant quits mid-trial every single time.</summary>
    public static void SetID(string id)
    {
        ID      = id.Trim().ToUpper();
        Session = CountVisitsOnDisk() + 1;
    }

    /// <summary>How many session folders this participant already has. Counted, never
    /// cached — the folders are the record.</summary>
    static int CountVisitsOnDisk()
    {
        try
        {
            string dir = System.IO.Path.Combine(ExperimentLogger.ExperimentRoot, FilePrefix);
            if (!System.IO.Directory.Exists(dir)) return 0;
            int n = 0;
            foreach (var d in System.IO.Directory.GetDirectories(dir))
                if (System.IO.Path.GetFileName(d).StartsWith("S")) n++;
            return n;
        }
        catch { return 0; }   // unreadable disk must never block a participant starting
    }

    /// <summary>Kept for callers; the visit number no longer depends on it being
    /// reached, so a participant who quits half-way is still counted correctly.</summary>
    public static void RecordSessionComplete()
    {
        if (!IsSet) return;
        PlayerPrefs.SetInt(SessCountKey(), Session);
        PlayerPrefs.Save();
    }

    // ---- Campaign PlayerPrefs keys (per-participant) -----------------------

    public static string BestKey(int stage) => $"p_{ID}_camp_best_{stage}";
    public static string PassKey(int stage) => $"p_{ID}_camp_pass_{stage}";

    // ---- File / folder helpers --------------------------------------------

    /// <summary>Safe filename prefix: "P001" when set, "UNKN" otherwise.</summary>
    public static string FilePrefix   => IsSet ? ID : "UNKN";

    /// <summary>Session tag used in filenames, e.g. "S2".</summary>
    public static string SessionTag   => "S" + Session;

    /// <summary>Compact ISO timestamp safe for filenames: "20260619T143022".</summary>
    public static string ISONow()     => System.DateTime.Now.ToString("yyyyMMdd'T'HHmmss");

    // ---- Private ----------------------------------------------------------

    static string SessCountKey() => $"p_{ID}_sessions";
}
