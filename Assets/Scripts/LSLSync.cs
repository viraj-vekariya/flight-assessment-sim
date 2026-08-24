/// <summary>
/// LSLSync — LabStreamingLayer marker outlet, and the sim's side of the EEG clock.
///
/// This is the ONLY place the simulator touches EEG infrastructure. It pushes
/// MARKERS and reports the LSL clock; it never reads, records, or invents EEG data.
/// If the LSL4Unity package is not installed every method is an inert no-op and the
/// simulator behaves identically, just without live marker output.
///
/// HOW TO ENABLE
///   1. Window -> Package Manager -> + -> Add package from git URL:
///      https://github.com/labstreaminglayer/LSL4Unity.git
///   2. Project Settings -> Player -> Other Settings -> Scripting Define Symbols:
///      add  LSL4UNITY
///   3. Recompile. Every ExperimentLogger.Mark() now also pushes an LSL marker.
///
/// RECORDING WORKFLOW
///   * Start the EEG amplifier's LSL stream.
///   * Start LabRecorder, tick BOTH the EEG stream and "FlightSimMarkers", press Record.
///   * Press Play in the simulator and run the session.
///   * The resulting .xdf holds EEG and markers on one clock. Drop it into the
///     trial's eeg/ folder (see the README.txt written there).
///
/// WHY THE MARKER STRING MATCHES THE CSV EXACTLY
///   The tag pushed here is byte-identical to the `marker` column of events.csv
///   (with "|detail" appended when there is a detail field). Offline alignment is
///   therefore a string match plus one clock, with no mapping table to maintain.
/// </summary>
public static class LSLSync
{
#if LSL4UNITY
    static LSL.StreamOutlet outlet;

    /// <summary>True when a live LSL outlet exists.</summary>
    public static bool Available => outlet != null;

    /// <summary>liblsl's local clock, the time base the EEG stream is stamped on.</summary>
    public static double Clock() => LSL.LSL.local_clock();

    public static void Init()
    {
        if (outlet != null) return;
        var info = new LSL.StreamInfo(
            name:           "FlightSimMarkers",
            type:           "Markers",
            channel_count:  1,
            nominal_srate:  0,                          // irregular (event-driven)
            channel_format: LSL.channel_format_t.cf_string,
            source_id:      "flightsim_" + ParticipantManager.ID);
        // Describe the stream so the .xdf is self-documenting.
        var desc = info.desc();
        desc.append_child_value("experiment", "pilot_cognitive_workload");
        desc.append_child_value("participant", ParticipantManager.FilePrefix);
        desc.append_child_value("session", ParticipantManager.Session.ToString());
        desc.append_child_value("marker_vocabulary", string.Join(" ", EventMarkers.All));
        outlet = new LSL.StreamOutlet(info);
    }

    public static void Marker(string tag)
    {
        if (outlet != null && !string.IsNullOrEmpty(tag))
            outlet.push_sample(new string[] { tag });
    }

    public static void Shutdown() { outlet?.Close(); outlet = null; }
#else
    // ---- No LSL4Unity package: inert. -----------------------------------------
    // Available stays false, so ExperimentLogger writes t_lsl = -1 and sync.json
    // records lsl_available=false. Nothing pretends an EEG link exists.
    public static bool Available => false;
    public static double Clock() => -1.0;
    public static void Init() { }
    public static void Marker(string tag) { }
    public static void Shutdown() { }
#endif
}
