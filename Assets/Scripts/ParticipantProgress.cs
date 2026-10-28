using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// What the current participant has already flown, read from the data tree.
///
/// The folders are the record (see ParticipantManager.SetID for why PlayerPrefs is
/// not). A mission counts as DONE when at least one of its trial folders has a
/// trial.json with complete=true and continuous=true — the same rule as
/// tools/build_dataset.py's `usable`, so the hub and the exporter agree on who still
/// owes which missions. A folder with telemetry but no usable trial.json is PARTIAL:
/// the participant started it and quit, crashed, or closed the game.
///
/// This also defines the ORDER the 42 missions are offered in. A visit's twelve
/// (ExperimentSession.Begin) are block 0 of this order, so a participant who does the
/// lab protocol first and the rest later sees no change in sequence. Blocks 1 and 2
/// rotate the variant index so all 36 cognitive-axis missions are covered exactly
/// once; the six crosswind missions come last because they are a separate axis.
/// </summary>
public static class ParticipantProgress
{
    public enum Status { None, Partial, Done }

    public class Trial
    {
        public string Visit = "", Tag = "", MissionId = "";
        public int Rows, Expected;
        public bool HasJson, Complete, Continuous;
        public System.DateTime When;
        public bool Usable => HasJson && Complete && Continuous;
    }

    public class Person
    {
        public string Id = "";
        public int Visits, Done, Partial;
        public System.DateTime Last;
    }

    /// <summary>IDs the batteries and design tools write under; never a person.</summary>
    public static readonly string[] SyntheticIds = { "TEST01", "SHOTS", "DESIGN", "LEVEL", "CTEST", "UNKN", "UISHOT" };

    static string scannedPid;
    static readonly Dictionary<string, Status> status = new Dictionary<string, Status>();
    static readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
    static readonly List<Trial> trials = new List<Trial>();
    static List<string> order;
    static string orderPid;

    /// <summary>Forget the cached scan; the next query re-reads the disk.</summary>
    public static void Refresh() { scannedPid = null; }

    public static int Total => MissionLibrary.All().Count;

    public static Status Of(string missionId)
    {
        Ensure();
        Status s;
        return status.TryGetValue(missionId, out s) ? s : Status.None;
    }

    public static int Attempts(string missionId)
    {
        Ensure();
        int n;
        return attempts.TryGetValue(missionId, out n) ? n : 0;
    }

    public static int DoneCount    { get { Ensure(); int n = 0; foreach (var kv in status) if (kv.Value == Status.Done) n++; return n; } }
    public static int PartialCount { get { Ensure(); int n = 0; foreach (var kv in status) if (kv.Value == Status.Partial) n++; return n; } }

    /// <summary>Every trial folder of this participant, newest visit first.</summary>
    public static List<Trial> Trials { get { Ensure(); return trials; } }

    public static int DoneIn(WorkloadClass c, LoadAxis axis = LoadAxis.Cognitive)
    {
        int n = 0;
        foreach (var m in MissionLibrary.ForClass(c, axis)) if (Of(m.Id) == Status.Done) n++;
        return n;
    }

    // ── order ───────────────────────────────────────────────────────────────────

    /// <summary>All 42 missions in the order this participant is offered them.</summary>
    public static List<string> FullOrder()
    {
        string pid = ParticipantManager.ID;
        if (order != null && orderPid == pid) return order;
        int pnum = ExperimentSession.StableNumber(pid);
        var ids = new List<string>();
        var seen = new HashSet<string>();
        for (int k = 0; k < MissionLibrary.VariantCount; k++)
        {
            var block = new List<string>();
            foreach (var m in MissionLibrary.SessionMissions(pnum + k)) block.Add(m.Id);
            block = ExperimentSession.BalancedLatinSquareRow(block, (pnum + k) % 12);
            block = ExperimentSession.DeClumpClasses(block);
            foreach (var id in block) if (seen.Add(id)) ids.Add(id);
        }
        foreach (var m in MissionLibrary.PsychomotorAxis()) if (seen.Add(m.Id)) ids.Add(m.Id);
        foreach (var m in MissionLibrary.All()) if (seen.Add(m.Id)) ids.Add(m.Id);   // never lose one
        order = ids; orderPid = pid;
        return order;
    }

    /// <summary>1-based position of a mission in the participant's order.</summary>
    public static int Position(string missionId) => FullOrder().IndexOf(missionId) + 1;

    /// <summary>The first mission in the order that is not yet DONE, or null when
    /// all of them are.</summary>
    public static string NextMissionId()
    {
        foreach (var id in FullOrder()) if (Of(id) != Status.Done) return id;
        return null;
    }

    // ── scanning ────────────────────────────────────────────────────────────────

    static void Ensure()
    {
        if (scannedPid == ParticipantManager.ID) return;
        scannedPid = ParticipantManager.ID;
        status.Clear(); attempts.Clear(); trials.Clear();
        if (!ParticipantManager.IsSet) return;
        try { ScanInto(Path.Combine(ExperimentLogger.ExperimentRoot, ParticipantManager.FilePrefix), status, attempts, trials); }
        catch (System.Exception e) { Debug.LogWarning("[Progress] scan failed: " + e.Message); }
    }

    static readonly Regex RxMission = new Regex("\"mission_id\"\\s*:\\s*\"([^\"]*)\"");
    static readonly Regex RxComplete = new Regex("\"complete\"\\s*:\\s*(true|false)");
    static readonly Regex RxCont = new Regex("\"continuous\"\\s*:\\s*(true|false)");
    static readonly Regex RxRows = new Regex("\"telemetry_rows\"\\s*:\\s*(\\d+)");
    static readonly Regex RxExp = new Regex("\"telemetry_rows_expected\"\\s*:\\s*(\\d+)");

    static void ScanInto(string pidDir, Dictionary<string, Status> st, Dictionary<string, int> att, List<Trial> list)
    {
        if (!Directory.Exists(pidDir)) return;
        var visits = Directory.GetDirectories(pidDir);
        System.Array.Sort(visits);
        System.Array.Reverse(visits);
        foreach (var vdir in visits)
        {
            string visit = Path.GetFileName(vdir);
            if (!visit.StartsWith("S")) continue;
            foreach (var tdir in Directory.GetDirectories(vdir))
            {
                string tag = Path.GetFileName(tdir);
                if (!tag.StartsWith("T")) continue;
                int us = tag.IndexOf('_');
                if (us < 0) continue;
                var t = new Trial { Visit = visit, Tag = tag, MissionId = tag.Substring(us + 1), When = Directory.GetLastWriteTime(tdir) };
                if (t.MissionId.StartsWith("BASELINE")) continue;
                if (!File.Exists(Path.Combine(tdir, "telemetry.csv")) && !File.Exists(Path.Combine(tdir, "trial.json"))) continue;
                string jp = Path.Combine(tdir, "trial.json");
                if (File.Exists(jp))
                {
                    string js = File.ReadAllText(jp);
                    t.HasJson = true;
                    var m = RxMission.Match(js); if (m.Success && m.Groups[1].Value != "") t.MissionId = m.Groups[1].Value;
                    m = RxComplete.Match(js); t.Complete = m.Success && m.Groups[1].Value == "true";
                    m = RxCont.Match(js);     t.Continuous = !m.Success || m.Groups[1].Value == "true";
                    m = RxRows.Match(js);     if (m.Success) int.TryParse(m.Groups[1].Value, out t.Rows);
                    m = RxExp.Match(js);      if (m.Success) int.TryParse(m.Groups[1].Value, out t.Expected);
                }
                list.Add(t);
                int n; att.TryGetValue(t.MissionId, out n); att[t.MissionId] = n + 1;
                var s = t.Usable ? Status.Done : Status.Partial;
                Status cur;
                if (!st.TryGetValue(t.MissionId, out cur) || s > cur) st[t.MissionId] = s;
            }
        }
    }

    /// <summary>Everyone with a folder under experiment/, battery IDs excluded, most
    /// recent first. Used by the entry screen so a returning participant is picked
    /// from a list instead of retyped.</summary>
    public static List<Person> PeopleOnDisk()
    {
        var people = new List<Person>();
        try
        {
            string root = ExperimentLogger.ExperimentRoot;
            if (!Directory.Exists(root)) return people;
            foreach (var d in Directory.GetDirectories(root))
            {
                string id = Path.GetFileName(d);
                if (System.Array.IndexOf(SyntheticIds, id) >= 0) continue;
                var p = new Person { Id = id, Last = Directory.GetLastWriteTime(d) };
                var st = new Dictionary<string, Status>(); var att = new Dictionary<string, int>(); var tl = new List<Trial>();
                ScanInto(d, st, att, tl);
                foreach (var s in Directory.GetDirectories(d)) if (Path.GetFileName(s).StartsWith("S")) p.Visits++;
                foreach (var kv in st) { if (kv.Value == Status.Done) p.Done++; else p.Partial++; }
                foreach (var t in tl) if (t.When > p.Last) p.Last = t.When;
                people.Add(p);
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[Progress] people scan failed: " + e.Message); }
        people.Sort((a, b) => b.Last.CompareTo(a.Last));
        return people;
    }
}
