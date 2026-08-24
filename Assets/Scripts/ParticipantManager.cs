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

    /// <summary>
    /// Register the participant. Session number auto-increments per participant
    /// (stored in PlayerPrefs under a participant-scoped key so different IDs
    /// don't share session counts).
    /// </summary>
    public static void SetID(string id)
    {
        ID      = id.Trim().ToUpper();
        Session = PlayerPrefs.GetInt(SessCountKey(), 0) + 1;
    }

    /// <summary>
    /// Persist the completed session counter so the next launch of the same
    /// participant ID starts on session N+1.
    /// </summary>
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
