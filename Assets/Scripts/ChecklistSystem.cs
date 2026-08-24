// ChecklistSystem — data-driven abnormal/emergency drills.
//
// WHY IT EXISTS FOR THE EXPERIMENT
//   Procedural load is one of the demand dimensions the workload model scores, and
//   it is the dimension that most cleanly separates "dangerous" from "cognitively
//   demanding". A checklist turns an abnormality into a sequence of DISCRETE, TIMED
//   pilot actions, which gives the analysis:
//     * CHECKLIST_ITEM markers at known instants (clean EEG epoch boundaries),
//     * per-item completion latency (a behavioural workload measure that does not
//       depend on the participant self-reporting anything),
//     * an objective error count (items done out of order / omitted / timed out).
//
// TWO ITEM KINDS
//   DO   — completes when the aircraft/systems state actually satisfies Condition.
//          The pilot must perform the action; pressing a key does nothing.
//   CHECK— completes when the pilot acknowledges (SPACE). Used for verification
//          items that have no simulated state ("fuel shutoff — CHECK ON").
//   Mixing them is deliberate: real drills mix do-items and check-items, and the
//   two produce different behavioural signatures.
//
// NOT MODELLED: reading a paper checklist, crew callouts, or challenge-and-response
// with a pilot monitoring. Single-pilot, memory-item style only. Documented in
// MISSION_IMPLEMENTATION.md.

using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChecklistItemKind { Do, Check }

public class ChecklistItem
{
    public string Label;
    public ChecklistItemKind Kind;
    /// <summary>For Do items: returns true once the state satisfies the item.</summary>
    public Func<AircraftSystems, CessnaPhysics, bool> Condition;
    /// <summary>Seconds after the item is presented before it is logged as a timeout
    /// (the drill still advances, so one stuck item cannot deadlock a trial).</summary>
    public float TimeoutS = 30f;

    public ChecklistItem(string label, ChecklistItemKind kind,
                         Func<AircraftSystems, CessnaPhysics, bool> cond = null, float timeout = 30f)
    { Label = label; Kind = kind; Condition = cond; TimeoutS = timeout; }
}

public class ChecklistDef
{
    public string Id, Title;
    public List<ChecklistItem> Items = new List<ChecklistItem>();
    public ChecklistDef(string id, string title) { Id = id; Title = title; }
    public ChecklistDef Do(string label, Func<AircraftSystems, CessnaPhysics, bool> cond, float t = 30f)
    { Items.Add(new ChecklistItem(label, ChecklistItemKind.Do, cond, t)); return this; }
    public ChecklistDef Check(string label, float t = 20f)
    { Items.Add(new ChecklistItem(label, ChecklistItemKind.Check, null, t)); return this; }
}

/// <summary>The drills the twelve missions use. Content follows the standard
/// light-single abnormal/emergency flow in FAA-H-8083-3C ch.18 and the generic
/// C172-class POH section 3 ordering; it is a TEACHING-ACCURATE abstraction, not a
/// transcription of any one manufacturer's certified checklist.</summary>
public static class ChecklistLibrary
{
    public const string EngineRough   = "ENG_ROUGH";
    public const string EngineFailure = "ENG_FAIL";
    public const string Electrical    = "ELEC_ALT";
    public const string StaticBlock   = "PITOT_STATIC";
    public const string FlapFailure   = "FLAP_FAIL";
    public const string BeforeLanding = "BEFORE_LANDING";

    static Dictionary<string, ChecklistDef> map;

    public static ChecklistDef Get(string id)
    {
        if (map == null) Build();
        return map.TryGetValue(id, out var d) ? d : null;
    }

    public static IEnumerable<ChecklistDef> All()
    {
        if (map == null) Build();
        return map.Values;
    }

    static void Build()
    {
        map = new Dictionary<string, ChecklistDef>();

        // Rough running / partial power loss. The first three items are the classic
        // induction-icing and fuel-system sweep; carb heat is the item that actually
        // fixes the modelled failure.
        Add(new ChecklistDef(EngineRough, "ENGINE ROUGHNESS / PARTIAL POWER LOSS")
            .Do("CARBURETTOR HEAT — ON            [H]", (s, a) => s.CarbHeatOn, 25f)
            .Do("FUEL SELECTOR — SWITCH TANK      [J]", (s, a) => s.Selector != FuelSelector.Both, 25f)
            .Check("MIXTURE — RICH")
            .Check("ENGINE GAUGES — CHECK")
            .Check("LAND AS SOON AS PRACTICABLE"));

        // Complete power loss. Best-glide first — "fly the aeroplane" before anything
        // else (AFH ch.18: control, then field, then restart attempt).
        Add(new ChecklistDef(EngineFailure, "ENGINE FAILURE IN FLIGHT")
            .Do("AIRSPEED — BEST GLIDE (~120 km/h)", (s, a) => a.AirspeedKmh > 95f && a.AirspeedKmh < 145f, 30f)
            .Check("LANDING SITE — SELECT")
            .Do("CARBURETTOR HEAT — ON            [H]", (s, a) => s.CarbHeatOn, 20f)
            .Do("FUEL SELECTOR — SWITCH TANK      [J]", (s, a) => s.Selector != FuelSelector.Both, 20f)
            .Check("MIXTURE — RICH, IGNITION — BOTH")
            .Check("IF NO RESTART — SECURE & LAND"));

        // Alternator failure. AFH ch.18: shed non-essential loads IMMEDIATELY, then
        // plan to land at the nearest suitable airport.
        Add(new ChecklistDef(Electrical, "ALTERNATOR FAILURE / LOW VOLTS")
            .Check("AMMETER / VOLTS — CONFIRM")
            .Do("NON-ESSENTIAL LOADS — SHED       [K]", (s, a) => s.LoadShed, 25f)
            .Check("ALTERNATOR — RESET ATTEMPT")
            .Check("LAND AT NEAREST SUITABLE AIRPORT")
            .Check("PLAN FOR NO ELECTRIC FLAPS"));

        // Suspected static blockage. The handbook diagnostic is the alternate static
        // source, opened while climbing or descending so the needles move.
        Add(new ChecklistDef(StaticBlock, "SUSPECTED PITOT-STATIC BLOCKAGE")
            .Check("CROSS-CHECK — ATTITUDE vs ASI vs ALT")
            .Do("ALTERNATE STATIC SOURCE — OPEN   [L]", (s, a) => s.AlternateStaticOpen, 30f)
            .Check("PITOT HEAT — ON")
            .Check("FLY ATTITUDE + POWER, NOT THE NEEDLES"));

        // Flap failure -> no-flap approach. The number that matters: up to 50% more
        // landing distance (AFH ch.18, Total Flap Failure).
        Add(new ChecklistDef(FlapFailure, "FLAP FAILURE — NO-FLAP APPROACH")
            .Check("FLAP POSITION — CONFIRM")
            .Check("APPROACH SPEED — NORMAL, FLATTER PATH")
            .Check("LANDING DISTANCE — UP TO 50% GREATER")
            .Check("GO-AROUND — BRIEFED"));

        Add(new ChecklistDef(BeforeLanding, "BEFORE LANDING")
            .Check("SEATBELTS / HARNESS — SECURE")
            .Do("CARBURETTOR HEAT — ON            [H]", (s, a) => s.CarbHeatOn, 25f)
            .Check("MIXTURE — RICH")
            .Do("FLAPS — AS REQUIRED              [F]", (s, a) => a.Flaps01 > 0.2f, 30f));
    }

    static void Add(ChecklistDef d) => map[d.Id] = d;
}

/// <summary>Runtime state of the one checklist currently being run.</summary>
public class ChecklistRun
{
    public ChecklistDef Def;
    public int Index;
    public float StartedAt, ItemShownAt;
    public int Timeouts;
    public bool Complete => Def == null || Index >= Def.Items.Count;
    public ChecklistItem Current => Complete ? null : Def.Items[Index];
}
