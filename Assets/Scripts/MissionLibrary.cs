// MissionLibrary — the twelve experimental missions.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE DESIGN: FLIGHT PHASE FULLY CROSSED WITH WORKLOAD CLASS
// ═══════════════════════════════════════════════════════════════════════════════
//
//                  LOW                  MEDIUM                 HIGH
//   TAXI/TAKE-OFF  L1 normal departure  M1 traffic + clearance H1 amended clearance,
//                                                                blocked runway, conflict
//   CLIMB/DEPART   L2 assigned climb    M2 cabin door opens    H2 alternator failure
//   CRUISE/ENROUTE L3 level hold        M3 ATC re-clearances   H3 unreliable instruments
//   APPROACH/LAND  L4 normal landing    M4 weather + sidestep  H4 engine failure
//
// WHY CROSSED, AND WHY THIS REPLACED THE PREVIOUS SET
//   The earlier twelve were 6 cruise, 1 climb, 4 approach and 1 circuit, and the
//   classes were not evenly spread across them: HIGH was three approaches plus a
//   circuit while LOW was three cruises plus one landing. **Flight phase was therefore
//   CONFOUNDED with workload class.** Any EEG or NASA-TLX difference between LOW and
//   HIGH could have been the difference between cruising and landing — a difference in
//   psychomotor demand, visual flow, and intrinsic phase workload — rather than a
//   difference in cognitive load. No amount of analysis could have separated them
//   afterwards. That was the single biggest threat to the study's internal validity
//   and it is now designed out: every class appears once in every phase.
//
//   The crossed grid also gives something the old set could not: **every mission has a
//   phase-matched LOW baseline**. H4 is compared against L4, both approaches; H1
//   against L1, both taxi-and-take-off. Previously only one such matched pair existed.
//
//   And it satisfies the project's requirements document, which specifies three task
//   stages — take-off, flight, landing — with the initial focus on TAKE-OFF at three
//   levels. The previous set contained no take-off mission at all.
//
// CONTROLS HELD CONSTANT
//   * 300 s nominal duration for every mission (see FINAL_EXPERIMENT_PROTOCOL.md for
//     the analysis of this against the requirements' 10-15 minute figure).
//   * 60 s un-manipulated in-task baseline at the head of every mission. Within a
//     phase row all three missions have the SAME baseline activity, which is what the
//     LOW-MEDIUM-HIGH contrast needs; the resting baselines are the cross-phase reference.
//   * Manual/psychomotor demand is matched within each phase row (spread <= 1 point on
//     a 0-4 scale), so a class difference inside a row cannot be muscle activity.
//   * Every load-inducing event carries jitter, so onset time is unpredictable.
//
// THE PRINCIPLE THE REQUIREMENTS INSIST ON, AND THIS SET HONOURS
//   "The three versions should not simply differ by weather." Weather appears in only
//   two of the eight non-LOW missions and is never the sole distinguishing factor.
//   Each class is separated by a DIFFERENT cognitive mechanism:
//     MEDIUM — visual search + procedure (M1), startle without danger (M2),
//              working memory (M3), degraded perception + re-planning (M4)
//     HIGH   — concurrency under time pressure (H1), forward reasoning about a
//              depleting resource (H2), self-consistent WRONG information (H3),
//              irreversible commitment under a clock (H4)
//
// HIGH IS NOT "DANGEROUS"
//   H2 is an alternator failure — undramatic, nothing catches fire — and it scores
//   ABOVE H4, an engine failure, because reasoning forward about a resource you are
//   spending is harder than executing a rehearsed drill. M2 is the most startling
//   event in the set and is only MEDIUM, because FAA-H-8083-3C is explicit that an
//   open cabin door "seldom if ever compromises the airplane's ability to fly" and
//   the hazard is the pilot's reaction. Both are falsifiable predictions.

using System.Collections.Generic;
using UnityEngine;

public static class MissionLibrary
{
    /// <summary>Nominal length of EVERY mission (s). Held constant on purpose.</summary>
    public const float StandardDurationS = 300f;
    /// <summary>Quiet, un-manipulated segment at the head of every mission (s).</summary>
    public const float StandardBaselineS = 60f;

    // ── SHARED GEOMETRY ─────────────────────────────────────────────────────────
    // Every mission of a given phase starts from the SAME place, so approach and
    // departure geometry are controlled constants rather than nuisance variables.
    public const float CruiseAltM = 700f;
    public const float ClimbStartAltM = 400f;
    public const float ApproachAltM = 500f;
    public static readonly Vector3 CruiseStart = new Vector3(0f, CruiseAltM, -2000f);
    public static readonly Vector3 ClimbStart = new Vector3(0f, ClimbStartAltM, 1500f);
    public static readonly Vector3 ApproachStart = new Vector3(0f, ApproachAltM, -12000f);
    /// <summary>When the phase's main task is released — exactly at the end of the
    /// in-task baseline, so the baseline is never contaminated.</summary>
    public const float BaselineEndT = 62f;

    static List<MissionDefinition> all;

    public static List<MissionDefinition> All()
    {
        if (all == null)
            all = new List<MissionDefinition> { L1(), L2(), L3(), L4(), M1(), M2(), M3(), M4(), H1(), H2(), H3(), H4() };
        return all;
    }

    public static List<MissionDefinition> ForClass(WorkloadClass c)
    {
        var o = new List<MissionDefinition>();
        foreach (var m in All()) if (m.Class == c) o.Add(m);
        return o;
    }

    public static List<MissionDefinition> ForPhase(FlightPhase p)
    {
        var o = new List<MissionDefinition>();
        foreach (var m in All()) if (m.Phase == p) o.Add(m);
        return o;
    }

    public static MissionDefinition Get(string id)
    {
        foreach (var m in All()) if (m.Id == id) return m;
        return null;
    }

    static MissionDefinition Base(string id, string name, WorkloadClass cls, FlightPhase phase) =>
        new MissionDefinition
        {
            Id = id, Name = name, Class = cls, Phase = phase,
            DurationS = StandardDurationS, InTaskBaselineS = StandardBaselineS,
            StartFuelL = 180f, StartAirspeedKmh = 180f,
        };

    // Common set-up for the taxi/take-off row: parked on stand 1, taxi clearance up
    // front, then the phase-specific manipulation.
    static MissionDefinition TaxiBase(string id, string name, WorkloadClass cls)
    {
        var m = Base(id, name, cls, FlightPhase.Takeoff);
        m.Start = ScenarioStart.Runway;          // ground start; the engine puts it on the stand
        m.Goal = ScenarioGoal.TaxiTakeoff;
        m.StartAltitudeM = 0f; m.StartAirspeedKmh = 0f;
        m.StartHeadingDeg = 0f; m.TargetHeadingDeg = 0f;
        m.TargetAltitudeM = 600f;
        m.AltToleranceM = 90f; m.HdgToleranceDeg = 15f;
        foreach (var w in Aerodrome.TaxiRoute()) m.Waypoints.Add(w);
        return m;
    }

    static MissionDefinition ClimbBase(string id, string name, WorkloadClass cls)
    {
        var m = Base(id, name, cls, FlightPhase.Climb);
        m.Start = ScenarioStart.Airborne; m.Goal = ScenarioGoal.HoldTargets;
        m.StartPos = ClimbStart;
        m.StartAltitudeM = ClimbStartAltM; m.TargetAltitudeM = ClimbStartAltM;
        m.StartHeadingDeg = 0f; m.TargetHeadingDeg = 0f;
        m.AltToleranceM = 80f; m.HdgToleranceDeg = 14f;
        return m;
    }

    static MissionDefinition CruiseBase(string id, string name, WorkloadClass cls)
    {
        var m = Base(id, name, cls, FlightPhase.Cruise);
        m.Start = ScenarioStart.Airborne; m.Goal = ScenarioGoal.HoldTargets;
        m.StartPos = CruiseStart;
        m.StartAltitudeM = CruiseAltM; m.TargetAltitudeM = CruiseAltM;
        m.StartHeadingDeg = 0f; m.TargetHeadingDeg = 0f;
        m.AltToleranceM = 70f; m.HdgToleranceDeg = 12f;
        return m;
    }

    static MissionDefinition ApproachBase(string id, string name, WorkloadClass cls)
    {
        var m = Base(id, name, cls, FlightPhase.Approach);
        m.Start = ScenarioStart.Airborne; m.Goal = ScenarioGoal.Land;
        m.StartPos = ApproachStart;
        m.StartAltitudeM = ApproachAltM; m.TargetAltitudeM = ApproachAltM;
        m.StartHeadingDeg = 0f; m.TargetHeadingDeg = 0f;
        m.AltToleranceM = 80f; m.HdgToleranceDeg = 12f;
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 1 — TAXI AND TAKE-OFF
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L1()
    {
        var m = TaxiBase("L1", "Normal departure from the stand", WorkloadClass.Low);
        m.Objective = "Taxi to runway 01, hold short, take off and climb to 600 m on runway heading.";
        m.Brief = "You are on stand 1, engine running. Taxi via taxiway A to the holding point " +
                  "for runway 01, hold short, and wait for your take-off clearance. Then line up, " +
                  "take off, and climb straight ahead to 600 m. Clear day, no other traffic.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01."));
        m.Events.Add(MissionDefinition.TakeoffClearance(88f, "climb runway heading to 600 m.", 6f));
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 1, DecisionComplexity = 1, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 1, Perception = 2, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 0, Communication = 1, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 30, Physical = 25, Temporal = 20, Performance = 25, Effort = 30, Frustration = 15 };
        m.LoadRationale =
            "The reference condition for the take-off phase, and the mission that establishes what " +
            "a take-off costs before anything is added. There is real work — follow a taxi route by " +
            "sign and marking, respect a hold-short line, wait for a clearance, then fly an accurate " +
            "climb — but it is all single-threaded, unhurried and fully specified in advance. " +
            "Procedural load is 2 because the sequence genuinely has to be executed in order; " +
            "everything the model weights heavily is at 0 or 1.";
        m.EegRelevance =
            "The phase-matched baseline for M1 and H1. Its first 60 s is taxiing, which is the same " +
            "baseline activity as the other two missions in this row, so the class contrast within " +
            "the row is clean. Taxi is also the lowest-motion segment of any non-cruise mission, " +
            "which makes it a useful low-artifact reference.";
        m.ExpectedErrors = "Taxiing past the hold-short line; taking off without waiting for the " +
                           "clearance; drifting off the runway heading in the climb.";
        m.SuccessCriteria = "Hold short respected, take-off clearance received before entering the " +
                            "runway, airborne, and the assigned climb held.";
        m.FailureConditions = "Runway incursion (entering the runway uncleared); crash; not airborne.";
        m.AviationBasis = "Normal taxi and departure. The hold-short line is the standard mandatory " +
                          "runway-holding position, and crossing it uncleared is a runway incursion — " +
                          "the surface-movement error category the marking exists to prevent.";
        m.Approximations = "Taxi is about 330 m and roughly a minute, not the 10-15 minutes of an " +
                           "airline gate-to-runway sequence. The stand is placed close to the runway " +
                           "(a small-field layout) rather than the aeroplane being sped up, so taxi " +
                           "speed, steering and braking stay realistic. See the duration analysis in " +
                           "FINAL_EXPERIMENT_PROTOCOL.md.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.PhaseChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M1()
    {
        var m = TaxiBase("M1", "Departure with traffic and a two-part clearance", WorkloadClass.Medium);
        m.Visibility01 = 0.35f;
        m.Objective = "Taxi with other traffic on the move, absorb a two-part departure clearance, " +
                      "and depart on the assigned heading and level.";
        m.Brief = "Stand 1, engine running, light rain and reducing visibility. Taxi via alpha to " +
                  "the holding point for runway 01. There is other traffic moving on the airfield. " +
                  "ATC will pass a departure clearance you must read back before you are cleared to go.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. TRAFFIC IS MOVING."));
        m.Events.Add(MissionDefinition.Wx(6f, 250f, 0.30f, "LIGHT RAIN, REDUCING VISIBILITY", 0f));
        // A visible aeroplane crosses the runway ahead — something to actually look for.
        m.Events.Add(MissionDefinition.Traffic(40f, TrafficBehaviour.CrossingRunway,
                     "TRAFFIC CROSSING RUNWAY 01 AHEAD", false, 6f, 10f));
        // Two-item departure clearance, read back, THEN cleared.
        m.Events.Add(MissionDefinition.Readback(78f, "DEPARTURE CLEARANCE: right heading 040°, climb 700 m — ACKNOWLEDGE", 7f, 8f));
        m.Events.Add(MissionDefinition.TakeoffClearance(96f, "no delay, traffic 4 miles final.", 8f));
        m.Events.Add(MissionDefinition.Atc(150f, ScenarioEventType.HeadingChange, 40f, "right heading 040°"));
        m.Events.Add(MissionDefinition.Atc(158f, ScenarioEventType.AltitudeChange, 700f, "climb and maintain 700 m"));
        m.Events.Add(MissionDefinition.Probe(230f, "OPS CHECK — respond", 5f, 12f));
        m.TargetAltitudeM = 700f;
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 2,
            AttentionSwitching = 3, SituationAwareness = 2, Perception = 3, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 1, Communication = 2, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 55, Physical = 30, Temporal = 50, Performance = 45, Effort = 55, Frustration = 35 };
        m.LoadRationale =
            "Adds two things to L1 and nothing else: something to LOOK FOR outside, and a clearance " +
            "with two items to hold. The traffic is a real aeroplane on a scripted path, so the " +
            "pilot's scan must leave the instruments and the taxi route to find it, and reducing " +
            "visibility makes that search cost more. The departure clearance must be read back " +
            "before the take-off clearance is issued, and its two items are not flown until after " +
            "rotation — so they sit in working memory across the busiest part of the mission. " +
            "Manual demand is identical to L1, so the step up is attentional and mnemonic.";
        m.EegRelevance =
            "Two well-separated discrete onsets (the traffic sighting, the clearance) against a " +
            "phase-matched baseline shared with L1 and H1. The interval between the read-back and " +
            "flying the two items is a working-memory maintenance window with no other event in it.";
        m.ExpectedErrors = "Missing the crossing traffic; entering the runway before the read-back; " +
                           "flying only the heading and forgetting the level, or vice versa.";
        m.SuccessCriteria = "Hold short respected, clearance acknowledged, airborne, and both " +
                            "clearance items flown.";
        m.FailureConditions = "Runway incursion; crash; clearance never acknowledged.";
        m.AviationBasis = "Departure with surface traffic and a read-back-required clearance. " +
                          "Read-back/hear-back is a recognised error source, and channel load rises " +
                          "with the number of elements per transmission.";
        m.Approximations = "The crossing aircraft follows a fixed path and cannot collide — conflict " +
                           "is scripted geometry, not emergent, so every participant meets it at the " +
                           "same (jittered) instant and the EEG epoch is comparable. 'Read-back' is a " +
                           "single acknowledgement keypress, so it measures acceptance LATENCY, not " +
                           "read-back accuracy; whether the items were retained is inferred from " +
                           "whether the aircraft was actually flown to them.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TrafficOnset,
                                    EventMarkers.AtcMessage, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H1()
    {
        var m = TaxiBase("H1", "Amended clearance, blocked runway, departure conflict", WorkloadClass.High);
        m.Visibility01 = 0.55f;
        m.Objective = "Absorb an amended departure clearance while taxiing, wait out a blocked " +
                      "runway, depart without delay, and resolve a conflict after rotation.";
        m.Brief = "Stand 1, engine running, heavy rain and poor visibility. Taxi via alpha to the " +
                  "holding point for runway 01. The airfield is busy. Expect changes.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. EXPECT DELAYS."));
        m.Events.Add(MissionDefinition.Wx(4f, 280f, 0.55f, "HEAVY RAIN, POOR VISIBILITY", 0f));
        // Traffic taxiing toward the same holding point — seen through the weather.
        m.Events.Add(MissionDefinition.Traffic(30f, TrafficBehaviour.TaxiingParallel,
                     "TRAFFIC AHEAD ON ALPHA", false, 6f, 8f));
        // The clearance is AMENDED mid-taxi: three items, one of them a restriction that
        // is not flown for another two minutes.
        m.Events.Add(MissionDefinition.Readback(48f, "AMENDED CLEARANCE: left heading 320°, climb 800 m, " +
                     "CROSS 5 MILES AT OR ABOVE 500 M — ACKNOWLEDGE", 6f, 6f));
        // ... and the runway is occupied, so the clearance the pilot is waiting for does not come.
        m.Events.Add(MissionDefinition.Traffic(72f, TrafficBehaviour.HoldingOnRunway,
                     "TRAFFIC HOLDING ON RUNWAY 01", false, 6f, 8f));
        m.Events.Add(MissionDefinition.Msg(84f, "HOLD POSITION — TRAFFIC ON THE RUNWAY."));
        // Then it clears, late, with a no-delay instruction.
        m.Events.Add(MissionDefinition.TakeoffClearance(120f, "NO DELAY, traffic 3 miles final.", 10f));
        m.Events.Add(MissionDefinition.Atc(190f, ScenarioEventType.HeadingChange, 320f, "left heading 320°"));
        m.Events.Add(MissionDefinition.Atc(196f, ScenarioEventType.AltitudeChange, 800f, "climb and maintain 800 m"));
        // A crossing aircraft appears in the departure path while the pilot is still
        // reconfiguring and still holding the crossing restriction.
        m.Events.Add(MissionDefinition.Traffic(222f, TrafficBehaviour.CrossingDeparture,
                     "TRAFFIC 1 O'CLOCK CROSSING, SAME LEVEL — respond", true, 6f, 14f));
        m.Events.Add(MissionDefinition.Decide(248f, "TURN OR CLIMB? — decide", 7f, 10f));
        m.TargetAltitudeM = 800f;
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 4, DecisionComplexity = 3, WorkingMemory = 4,
            AttentionSwitching = 4, SituationAwareness = 3, Perception = 3, ManualControl = 2,
            ProceduralLoad = 3, Uncertainty = 3, Communication = 3, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 85, Physical = 40, Temporal = 82, Performance = 70, Effort = 85, Frustration = 70 };
        m.LoadRationale =
            "The highest working-memory load in the set, and the clearest case of load emerging from " +
            "INTERACTION rather than accumulation. The pilot is given a three-item amended clearance " +
            "while still taxiing in poor visibility with traffic ahead — so the clearance has to be " +
            "held while a surface-navigation task is running. One item (the crossing restriction) is " +
            "not actionable for another two minutes, so it must survive the take-off. Then the " +
            "expected clearance does NOT come, because the runway is blocked, which is a specific and " +
            "under-appreciated load: waiting while primed to act. When it finally comes it comes with " +
            "'no delay', converting a patience task into a time-pressure task in one transmission. " +
            "Note manual demand is 2 — identical to L1 and M1. Nothing about this mission is " +
            "physically harder than a normal departure.";
        m.EegRelevance =
            "Expected to show the largest sustained frontal-midline theta rise of the take-off row, " +
            "with a distinct step at the amended clearance. The blocked-runway wait is a rare clean " +
            "example of high cognitive load with almost NO motor activity — the aeroplane is " +
            "stationary — which makes it the strongest available test of whether the workload " +
            "measures are tracking cognition rather than movement.";
        m.ExpectedErrors = "Dropping the crossing restriction; entering the runway while it is " +
                           "occupied; rushing the line-up after the 'no delay'; missing the crossing " +
                           "traffic while reconfiguring.";
        m.SuccessCriteria = "Hold short respected while the runway is occupied, amended clearance " +
                            "acknowledged and all three items honoured, conflict acknowledged, " +
                            "a deliberate decision made.";
        m.FailureConditions = "Runway incursion; crash; no response to the conflict.";
        m.AviationBasis = "Amended departure clearances, blocked-runway holds and departure conflicts " +
                          "are routine at a busy field. The 'no delay' instruction after an extended " +
                          "hold is a recognised rush-inducing pattern.";
        m.Approximations = "Traffic is scripted and cannot collide. Weather is fog density plus " +
                           "turbulence, not a meteorological model, and its intensity is in simulator " +
                           "units. The crossing restriction is not independently checked by the sim — " +
                           "compliance is read from the telemetry afterwards.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TrafficOnset,
                                    EventMarkers.AtcMessage, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 2 — CLIMB / DEPARTURE
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L2()
    {
        var m = ClimbBase("L2", "Steady assigned climb", WorkloadClass.Low);
        m.Objective = "Hold the departure leg, then fly one assigned climb accurately.";
        m.Brief = "Departure leg, 400 m, runway heading. Hold height and heading until ATC clears " +
                  "you to climb, then climb and level off accurately. Clear day.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Atc(BaselineEndT, ScenarioEventType.AltitudeChange, 900f, "climb and maintain 900 m"));
        m.TargetAltitudeM = 400f;
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 0, DecisionComplexity = 0, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 1, Perception = 1, ManualControl = 2,
            ProceduralLoad = 1, Uncertainty = 0, Communication = 1, ErrorConsequence = 0 };
        m.Expected = new ExpectedTlx { Mental = 25, Physical = 25, Temporal = 15, Performance = 25, Effort = 30, Frustration = 12 };
        m.LoadRationale =
            "A single continuous tracking task with one unhurried, single-item instruction. The " +
            "phase-matched reference for M2 and H2. Kept non-trivial by a tight altitude tolerance " +
            "(±80 m) and a real level-off, so it measures low workload rather than disengagement.";
        m.EegRelevance =
            "The quietest airborne mission after L3. One isolated, time-stamped target change gives " +
            "a clean within-mission before/after contrast at the lowest possible background load.";
        m.ExpectedErrors = "Overshooting the level-off; heading drift during the climb.";
        m.SuccessCriteria = "The new level acquired and held; heading maintained; no crash.";
        m.FailureConditions = "Crash; failure to start the climb within 60 s of the instruction.";
        m.AviationBasis = "Routine departure climb under ATC.";
        m.Approximations = "No autopilot exists in this simulator, so the climb is hand-flown. That " +
                           "raises manual demand relative to a real departure and is why ManualControl " +
                           "is scored 2 rather than 1.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TargetChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M2()
    {
        var m = ClimbBase("M2", "Cabin door opens on the climb-out", WorkloadClass.Medium);
        m.Objective = "Fly the assigned climb accurately, and keep flying it when startled.";
        m.Brief = "Departure leg, 400 m, runway heading. ATC will clear you to climb. Whatever " +
                  "happens, fly the aeroplane first.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Atc(BaselineEndT, ScenarioEventType.AltitudeChange, 900f, "climb and maintain 900 m"));
        // Loud, sudden, and per FAA-H-8083-3C ch.18 almost harmless.
        m.Events.Add(MissionDefinition.Fail(105f, FailureKind.DoorOpen, 1f, "CABIN DOOR OPEN", 22f));
        m.Events.Add(MissionDefinition.Probe(165f, "OPS CHECK — respond", 5f, 10f));
        m.Events.Add(MissionDefinition.Atc(205f, ScenarioEventType.HeadingChange, 330f, "left heading 330°", 12f));
        m.Events.Add(MissionDefinition.Probe(250f, "OPS CHECK — respond", 5f, 10f));
        m.TargetAltitudeM = 400f;
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 1,
            AttentionSwitching = 3, SituationAwareness = 2, Perception = 2, ManualControl = 3,
            ProceduralLoad = 1, Uncertainty = 3, Communication = 1, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 50, Physical = 40, Temporal = 45, Performance = 40, Effort = 55, Frustration = 50 };
        m.LoadRationale =
            "The set's deliberate dissociation of STARTLE from DANGER, and the strongest single " +
            "argument that this experiment measures workload rather than fear. A door popping open " +
            "in the climb is sudden, loud and physically distracting, yet FAA-H-8083-3C is explicit " +
            "that it 'seldom if ever compromises the airplane's ability to fly' and that the hazard " +
            "is the PILOT'S REACTION. So Uncertainty and AttentionSwitching are high, ErrorConsequence " +
            "is 1, and the correct response is very nearly to do nothing. If participants' EEG and " +
            "TLX put M2 up with the HIGH missions, we are measuring arousal; if they put it in the " +
            "middle, we are measuring demand. Either result is worth having.";
        m.EegRelevance =
            "The cleanest startle probe available: one abrupt onset with a large unpredictable jitter " +
            "(±22 s) against an ordinary climb. The 30-60 s after CUE_ONSET is the startle refractory " +
            "window the literature says to measure, and the two probes fall inside and outside it, " +
            "giving a within-mission spare-capacity contrast.";
        m.ExpectedErrors = "Reaching for the door and losing the climb; a large heading excursion at " +
                           "the bang; abandoning the climb to return immediately.";
        m.SuccessCriteria = "Assigned climb continued and held; heading held within 15°; probes answered.";
        m.FailureConditions = "Crash; loss of control; abandoning the assigned climb after the event.";
        m.AviationBasis = "FAA-H-8083-3C ch.18 'Door Opening In-Flight' — concentrate on flying, do " +
                          "not rush to land, do not release the harness to reach the door.";
        m.Approximations = "The door is a small drag and yaw increment (about 0.7°/s of uncorrected " +
                           "yaw) plus a loud cue and a banner. There is no dedicated airflow-noise " +
                           "asset, so the ACOUSTIC component of the startle is under-delivered — the " +
                           "main known weakness of this manipulation.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H2()
    {
        var m = ClimbBase("H2", "Alternator failure on the departure climb", WorkloadClass.High);
        m.Objective = "Manage a draining battery while flying an assigned climb and a level restriction, " +
                      "and decide whether to continue or return.";
        m.Brief = "Departure leg, 400 m, runway heading. ATC will clear you to climb and will give " +
                  "you a restriction. Fly the departure.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Readback(BaselineEndT, "CLEARANCE: climb 900 m, cross 10 miles " +
                     "AT OR ABOVE 700 M — ACKNOWLEDGE", 7f, 5f));
        m.Events.Add(MissionDefinition.Atc(BaselineEndT + 6f, ScenarioEventType.AltitudeChange, 900f, "climb and maintain 900 m"));
        // Alternator quits early, so the drain has time to matter. No bang, no fire.
        m.Events.Add(MissionDefinition.Fail(96f, FailureKind.AlternatorFailure, 1f, "LOW VOLTS", 15f));
        m.Events.Add(MissionDefinition.List(150f, ChecklistLibrary.Electrical, "ALTERNATOR FAILURE DRILL"));
        m.Events.Add(MissionDefinition.Decide(212f, "CONTINUE THE DEPARTURE OR RETURN? — decide", 8f, 12f));
        m.Events.Add(MissionDefinition.Probe(258f, "REPORT LEVEL — respond", 5f, 10f));
        m.TargetAltitudeM = 400f;
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 3, DecisionComplexity = 4, WorkingMemory = 3,
            AttentionSwitching = 3, SituationAwareness = 3, Perception = 2, ManualControl = 2,
            ProceduralLoad = 4, Uncertainty = 2, Communication = 2, ErrorConsequence = 3 };
        m.Expected = new ExpectedTlx { Mental = 80, Physical = 35, Temporal = 62, Performance = 60, Effort = 78, Frustration = 58 };
        m.LoadRationale =
            "The RESOURCE-BUDGETING mission and the highest decision complexity in the set. Nothing " +
            "here is startling and nothing is instantly dangerous — the load is entirely in having to " +
            "reason FORWARD about a depleting resource whose consumption the pilot controls, while " +
            "flying a climb, honouring a crossing restriction, and working a drill. The decision has " +
            "real branches with different failure modes: shed load early and the battery lasts; " +
            "continue outbound on a draining bus and the return leg gets worse; turn back early and " +
            "the clearance is abandoned. That forward reasoning under a moving constraint is the kind " +
            "of element interactivity that raises intrinsic cognitive load, and it is why H2 outranks " +
            "H4 on the model despite H4 being far more dramatic. Manual demand is 2, the same as L2.";
        m.EegRelevance =
            "A SUSTAINED elevation rather than a transient spike — valuable because most of the set is " +
            "event-locked. Between LOW VOLTS and the decision there should be a persistent theta/alpha " +
            "shift with no single dominant onset, which is the pattern a windowed classifier has to " +
            "detect and an ERP analysis cannot.";
        m.ExpectedErrors = "Missing the low-volts indication; never shedding load; dropping the " +
                           "crossing restriction while working the drill; deciding by default " +
                           "(continuing because no decision was made).";
        m.SuccessCriteria = "Low volts recognised, load shed, restriction honoured, an explicit " +
                            "continue/return decision made in window.";
        m.FailureConditions = "Crash; no decision made; loss of control.";
        m.AviationBasis = "FAA-H-8083-3C ch.18 'Electrical System': shed non-essential loads " +
                          "immediately, land at the nearest suitable airport, and note that a 40 A " +
                          "load can flatten the battery in 10-15 minutes.";
        m.Approximations = "The battery is a single lumped 24 Ah store with a linear " +
                           "state-of-charge-to-voltage curve and two load levels (shed / not shed). " +
                           "Individual breakers and per-equipment draw are not modelled, and the drain " +
                           "is tuned so the decision is live inside a 300 s trial — faster than a real " +
                           "battery would go flat.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 3 — CRUISE / EN-ROUTE  (the requirements' "Flight" stage)
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L3()
    {
        var m = CruiseBase("L3", "Straight-and-level cruise hold", WorkloadClass.Low);
        m.Objective = "Hold 700 m and heading 000° in calm air for the whole run.";
        m.Brief = "Cruise, calm air, no traffic, no radio. Fly it accurately — altitude within 70 m " +
                  "and heading within 12° — and nothing else will be asked of you.";
        m.Events.Add(MissionDefinition.Msg(1f, "CRUISE — maintain 700 m, heading 000°. Calm air."));
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 0, DecisionComplexity = 0, WorkingMemory = 1,
            AttentionSwitching = 0, SituationAwareness = 1, Perception = 1, ManualControl = 2,
            ProceduralLoad = 0, Uncertainty = 0, Communication = 0, ErrorConsequence = 0 };
        m.Expected = new ExpectedTlx { Mental = 20, Physical = 20, Temporal = 10, Performance = 25, Effort = 25, Frustration = 10 };
        m.LoadRationale =
            "The floor of the whole design: one continuous two-axis tracking task with a fixed target, " +
            "no secondary task, no communication and no decisions. Demand is almost entirely in the " +
            "manual/perceptual loop, which is precisely the demand that must NOT differ between " +
            "classes. Tolerances are the tightest in the set on purpose — that keeps the pilot engaged " +
            "and guards against this drifting into underload, where workload indices invert and the " +
            "class label would be meaningless.";
        m.EegRelevance =
            "The cleanest within-subject reference epoch: 300 s of stationary demand with no discrete " +
            "events at all. Frontal-midline theta should be lowest and parietal alpha highest of the " +
            "twelve. Also the correct normalisation reference for the cruise row.";
        m.ExpectedErrors = "Slow altitude drift; heading wander after inattention; over-controlling.";
        m.SuccessCriteria = "Runs 300 s without a crash. Performance is continuous (time in tolerance, " +
                            "RMS altitude and heading error), not pass/fail.";
        m.FailureConditions = "Crash, or more than 30 s beyond ±300 m of the assigned altitude.";
        m.AviationBasis = "Normal cruise. Cruise is the documented workload trough of a normal flight.";
        m.Approximations = "Hand-flown throughout (no autopilot), which raises manual demand relative " +
                           "to a real cruise leg.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M3()
    {
        var m = CruiseBase("M3", "Multi-part ATC re-clearances in the cruise", WorkloadClass.Medium);
        m.Objective = "Absorb, acknowledge and fly a stream of multi-item clearances.";
        m.Brief = "Busy sector, calm air. ATC will pass multi-part clearances — heading, altitude and " +
                  "a report — in single transmissions. Acknowledge each in the window and fly it.";
        m.Events.Add(MissionDefinition.Msg(1f, "CRUISE — maintain 700 m, heading 000°. Expect re-routing."));
        m.Events.Add(MissionDefinition.Readback(72f, "CLEARANCE: right heading 050°, climb 850 m — ACKNOWLEDGE", 6f, 8f));
        m.Events.Add(MissionDefinition.Atc(76f, ScenarioEventType.HeadingChange, 50f, "right heading 050°"));
        m.Events.Add(MissionDefinition.Atc(80f, ScenarioEventType.AltitudeChange, 850f, "climb and maintain 850 m"));
        m.Events.Add(MissionDefinition.Readback(150f, "CLEARANCE: left heading 340°, descend 600 m — ACKNOWLEDGE", 5f, 8f));
        m.Events.Add(MissionDefinition.Atc(154f, ScenarioEventType.HeadingChange, 340f, "left heading 340°"));
        m.Events.Add(MissionDefinition.Atc(158f, ScenarioEventType.AltitudeChange, 600f, "descend and maintain 600 m"));
        m.Events.Add(MissionDefinition.Readback(228f, "CLEARANCE: heading 020°, climb 780 m, report level — ACKNOWLEDGE", 4.5f, 8f));
        m.Events.Add(MissionDefinition.Atc(232f, ScenarioEventType.HeadingChange, 20f, "heading 020°"));
        m.Events.Add(MissionDefinition.Atc(236f, ScenarioEventType.AltitudeChange, 780f, "climb and maintain 780 m"));
        m.Events.Add(MissionDefinition.Probe(262f, "REPORT LEVEL — respond", 5f, 6f));
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 1, WorkingMemory = 3,
            AttentionSwitching = 3, SituationAwareness = 2, Perception = 2, ManualControl = 2,
            ProceduralLoad = 1, Uncertainty = 1, Communication = 3, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 55, Physical = 25, Temporal = 50, Performance = 45, Effort = 55, Frustration = 35 };
        m.LoadRationale =
            "The pure working-memory and channel-switching manipulation. Same aircraft, same calm air " +
            "and the same tracking task as L3 — the ONLY change is that instructions now arrive in " +
            "bundles of two to three items, must be acknowledged inside a shrinking window, and one " +
            "is a deferred report held across 30 s of other flying. That is textbook multiple-resource " +
            "competition: verbal working memory and visual-manual tracking loaded together. Manual " +
            "demand is identical to L3, so the step up is unambiguously cognitive.";
        m.EegRelevance =
            "Should give the clearest frontal-midline theta increase of the MEDIUM set, since fm-theta " +
            "tracks working-memory load specifically. Each clearance is an isolated, jittered, " +
            "time-stamped onset suitable for event-locked averaging, and the deferred report creates a " +
            "sustained maintenance interval.";
        m.ExpectedErrors = "Acknowledging but flying only the first item; forgetting the deferred " +
                           "report; reversing left/right on the 340° clearance.";
        m.SuccessCriteria = "All three clearances acknowledged in window, both items of each flown, " +
                            "deferred report made.";
        m.FailureConditions = "Crash; more than one clearance missed entirely.";
        m.AviationBasis = "Multi-element re-clearance in congested airspace; channel load rises with " +
                          "elements per transmission.";
        m.Approximations = "'Read-back' is a single acknowledgement keypress — acceptance latency, not " +
                           "read-back accuracy. Retention is inferred from whether the aircraft was " +
                           "flown to the new targets.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage,
                                    EventMarkers.TargetChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H3()
    {
        var m = CruiseBase("H3", "Unreliable instruments in the cruise", WorkloadClass.High);
        m.Objective = "Recognise that the instruments are lying, keep flying accurately, and decide " +
                      "what to do about it.";
        m.Brief = "Cruise, 700 m, heading 000°, weather deteriorating ahead. Fly the assigned level " +
                  "and heading. If anything changes, handle it as you would in the aircraft.";
        m.Events.Add(MissionDefinition.Msg(1f, "CRUISE — maintain 700 m, heading 000°."));
        m.Events.Add(MissionDefinition.Wx(66f, 210f, 0.45f, "IN CLOUD — REDUCED VISIBILITY", 6f));
        // Partial static blockage: altimeter, ASI and VSI all agree with each other and
        // are all wrong together. There is no warning and no annunciator, by design.
        m.Events.Add(MissionDefinition.Fail(92f, FailureKind.StaticBlocked, 1f, "INSTRUMENT DISAGREEMENT", 18f));
        m.Events.Add(MissionDefinition.List(150f, ChecklistLibrary.StaticBlock, "PITOT-STATIC DRILL"));
        // ... and ATC re-clears while the picture is still unresolved.
        m.Events.Add(MissionDefinition.Readback(196f, "CLEARANCE: descend 500 m, right heading 060° — ACKNOWLEDGE", 5f, 10f));
        m.Events.Add(MissionDefinition.Atc(202f, ScenarioEventType.AltitudeChange, 500f, "descend and maintain 500 m"));
        m.Events.Add(MissionDefinition.Atc(206f, ScenarioEventType.HeadingChange, 60f, "right heading 060°"));
        m.Events.Add(MissionDefinition.Decide(240f, "CONTINUE OR DIVERT? — decide", 8f, 10f));
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 3, DecisionComplexity = 3, WorkingMemory = 3,
            AttentionSwitching = 4, SituationAwareness = 4, Perception = 4, ManualControl = 3,
            ProceduralLoad = 3, Uncertainty = 4, Communication = 2, ErrorConsequence = 3 };
        m.Expected = new ExpectedTlx { Mental = 85, Physical = 40, Temporal = 68, Performance = 70, Effort = 85, Frustration = 75 };
        m.LoadRationale =
            "The highest-uncertainty mission in the set. A partial static blockage is singled out by " +
            "FAA-H-8083-3C as 'insidious' precisely because it is SELF-CONSISTENT: the altimeter, the " +
            "airspeed indicator and the VSI all corroborate a picture that is wrong, so the normal " +
            "cross-check — the thing a pilot falls back on — actively confirms the error. Resolving it " +
            "means distrusting the primary instruments and flying attitude and power instead, which is " +
            "expensive in working memory and attention. Being in cloud removes the outside horizon that " +
            "would otherwise settle the argument, and an ATC re-clearance arrives while the picture is " +
            "still unresolved, forcing a channel switch at the worst moment. The load is emergent, not " +
            "additive: none of these three things alone is a HIGH mission.";
        m.EegRelevance =
            "Expected to show the largest sustained frontal theta rise and the poorest secondary-task " +
            "performance of the cruise row. Because the phase and the manual task are matched to L3 " +
            "and M3, the L3 -> M3 -> H3 progression is the cleanest three-level dose-response contrast " +
            "the design offers, with visual scene, aircraft and tracking task all held constant.";
        m.ExpectedErrors = "Chasing the false altimeter; never opening the alternate static source; " +
                           "flying the re-clearance using the lying instruments; deciding nothing.";
        m.SuccessCriteria = "Alternate static opened OR the aircraft flown on attitude and power; " +
                            "clearance acknowledged; a deliberate decision made.";
        m.FailureConditions = "Crash; loss of control; no decision made.";
        m.AviationBasis = "FAA-H-8083-3C ch.18 'Pitot-Static System': with a restricted static source " +
                          "the altimeter, ASI and VSI mislead together, and the confirmation is to open " +
                          "the alternate static source while climbing or descending.";
        m.Approximations = "Pitot-static errors are a first-order linear approximation of the handbook's " +
                           "error signature, not a pressure-system model. 'In cloud' is fog density; " +
                           "there is no cloud layer, precipitation or airframe icing.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 4 — APPROACH / LANDING
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L4()
    {
        var m = ApproachBase("L4", "Routine approach and landing", WorkloadClass.Low);
        m.Objective = "Hold the inbound leg, then fly a stable approach and land on the centreline.";
        m.Brief = "You are 12 km out, lined up with the runway at 500 m. Hold height and heading " +
                  "until cleared to descend, then fly a normal approach and landing. Calm wind, " +
                  "unlimited visibility, no traffic.";
        m.Events.Add(MissionDefinition.Msg(1f, "INBOUND — maintain 500 m, heading 000°. Expect a visual approach."));
        m.Events.Add(MissionDefinition.Msg(BaselineEndT, "CLEARED TO LAND RUNWAY 01 — descend at your discretion, wind calm."));
        m.Events.Add(MissionDefinition.Cfg(150f, "APPROACH FLAPS — as required   [F]"));
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 1, DecisionComplexity = 1, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 2, Perception = 2, ManualControl = 3,
            ProceduralLoad = 1, Uncertainty = 0, Communication = 0, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 30, Physical = 32, Temporal = 25, Performance = 32, Effort = 38, Frustration = 15 };
        m.LoadRationale =
            "The honest treatment of a landing at the LOW class, and the control that makes the whole " +
            "design testable. A landing is an intrinsic workload peak — physiologically the " +
            "highest-demand phase of a normal flight — so labelling it LOW would be wrong if the " +
            "demand were cognitive. It is not: a clean visual approach in nil wind is a continuous, " +
            "highly practised PERCEPTUAL-MOTOR task with no diagnosis, no ambiguity, no concurrent " +
            "task, and one decision that never becomes live. L4 therefore has the highest manual " +
            "demand in the LOW class and the lowest scores on everything the model weights heavily.";
        m.EegRelevance =
            "The critical control for the movement/effort confound, and the phase-matched baseline for " +
            "M4 and H4. If the workload indices separate L4 from H4 despite closely matched " +
            "psychomotor demand (3 vs 4), the separation is cognitive rather than muscular.";
        m.ExpectedErrors = "High or low on profile; late flare; drifting off centreline; floating.";
        m.SuccessCriteria = "Touchdown on the runway within ±16 m of the centreline, sink < 3.5 m/s.";
        m.FailureConditions = "Crash; landing off the runway; not landed within 300 s.";
        m.AviationBasis = "Normal visual approach and landing.";
        m.Approximations = "No runway lighting or PAPI, no ATC sequencing; the approach is visual and " +
                           "unaided. Note `alt_err_m` is only meaningful during the level segment of " +
                           "this mission — see TELEMETRY_SCHEMA.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.Touchdown, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M4()
    {
        var m = ApproachBase("M4", "Deteriorating weather and a late runway change", WorkloadClass.Medium);
        m.Visibility01 = 0.45f;
        m.Objective = "Fly an approach in worsening weather and re-plan it when the runway changes late.";
        m.Brief = "Inbound, 500 m, 12 km out, weather deteriorating. Hold height and heading until " +
                  "cleared, then fly the approach. Expect changes on final.";
        m.Events.Add(MissionDefinition.Msg(1f, "INBOUND — maintain 500 m, heading 000°. Weather deteriorating."));
        m.Events.Add(MissionDefinition.Wx(30f, 240f, 0.45f, "TURBULENCE AND REDUCING VISIBILITY", 6f));
        m.Events.Add(MissionDefinition.Msg(BaselineEndT, "CLEARED TO LAND RUNWAY 01 — descend at your discretion."));
        m.Events.Add(MissionDefinition.Probe(120f, "OPS CHECK — respond", 5f, 10f));
        // A late side-step: the whole approach picture has to be rebuilt on final.
        m.Events.Add(MissionDefinition.Readback(168f, "SIDE-STEP: displaced threshold, aim 200 m LONG, " +
                     "wind now 070° gusting — ACKNOWLEDGE", 6f, 12f));
        m.Events.Add(MissionDefinition.Wx(176f, 90f, 0.55f, "GUSTY CROSSWIND ON FINAL", 0f));
        m.Events.Add(MissionDefinition.Probe(226f, "OPS CHECK — respond", 5f, 10f));
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 3,
            AttentionSwitching = 2, SituationAwareness = 3, Perception = 3, ManualControl = 3,
            ProceduralLoad = 2, Uncertainty = 2, Communication = 2, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 58, Physical = 48, Temporal = 55, Performance = 55, Effort = 65, Frustration = 45 };
        m.LoadRationale =
            "Raises load two ways that are deliberately different from each other: it degrades the " +
            "INPUT (visibility falls, so the external horizon and the runway picture get harder to " +
            "read, and turbulence makes the tracking task noisier) and then it invalidates the PLAN " +
            "late, when the pilot is already committed. The side-step arrives on final and has to be " +
            "held and applied while flying — the aiming point moves and the wind changes at the same " +
            "time. It is MEDIUM rather than HIGH because nothing is ambiguous and nothing is " +
            "irreversible: a go-around is always available and the aeroplane is serviceable.";
        m.EegRelevance =
            "The mission most exposed to the motor-artifact confound in the whole set, because " +
            "turbulence provokes corrective inputs. It is instrumented for exactly that check: " +
            "`ctrl_jerk` is logged at 50 Hz so band power can be regressed on motor activity, and " +
            "probe reaction time is the motor-light workload measure to fall back on.";
        m.ExpectedErrors = "Chasing the altimeter in turbulence; flying the original aiming point " +
                           "after the side-step; missing a probe while fighting a gust; landing long.";
        m.SuccessCriteria = "Side-step acknowledged, approach flown to a landing or a deliberate " +
                            "go-around, probes answered.";
        m.FailureConditions = "Crash; landing off the runway; loss of control not recovered in 10 s.";
        m.AviationBasis = "Deteriorating VMC, crosswind, and a late runway/threshold change. " +
                          "Turbulence and crosswind raise measured workload and degrade tracking; " +
                          "late changes on final are a recognised destabilising factor.";
        m.Approximations = "Turbulence is band-limited Perlin gusting, not a spectral (Dryden/von " +
                           "Karman) model; visibility is fog density. Intensities are SIMULATOR UNITS, " +
                           "not meteorological turbulence categories, and must be reported as such. " +
                           "The 'displaced threshold' is an instruction, not new runway geometry.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.WeatherOnset,
                                    EventMarkers.AtcMessage, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H4()
    {
        var m = ApproachBase("H4", "Engine failure on the approach", WorkloadClass.High);
        // Offset from the extended centreline so the runway is MARGINAL when the engine
        // quits: roughly a 7-8:1 glide is needed against the ~9:1 available, before the
        // cost of the turn. The tempting option and the safe option genuinely differ.
        m.StartPos = new Vector3(-900f, ApproachAltM, -9000f);
        m.SurvivalIsSuccess = true;
        m.Objective = "Lose the engine with the runway not quite made, and put the aeroplane down " +
                      "under control.";
        m.Brief = "Inbound, 500 m, positioning for runway 01. Hold height and heading until cleared. " +
                  "Fly the aeroplane.";
        m.Events.Add(MissionDefinition.Msg(1f, "INBOUND — maintain 500 m, heading 000°."));
        m.Events.Add(MissionDefinition.Msg(BaselineEndT, "CLEARED TO LAND RUNWAY 01 — descend at your discretion."));
        m.Events.Add(MissionDefinition.Fail(96f, FailureKind.EngineFailure, 1f, "ENGINE FAILURE", 14f));
        m.Events.Add(MissionDefinition.List(106f, ChecklistLibrary.EngineFailure, "ENGINE FAILURE DRILL"));
        m.Events.Add(MissionDefinition.Decide(128f, "RUNWAY OR FIELD AHEAD? — decide", 6f, 6f));
        m.Profile = new WorkloadProfile {
            MentalDemand = 3, TemporalDemand = 4, DecisionComplexity = 3, WorkingMemory = 2,
            AttentionSwitching = 3, SituationAwareness = 4, Perception = 3, ManualControl = 4,
            ProceduralLoad = 3, Uncertainty = 2, Communication = 1, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 75, Physical = 60, Temporal = 88, Performance = 70, Effort = 85, Frustration = 70 };
        m.LoadRationale =
            "The time-pressure pole of the HIGH class. Note the profile is deliberately a DIFFERENT " +
            "SHAPE from H2's: temporal demand and error consequence are at 4 while uncertainty is only " +
            "2, because there is nothing to diagnose — the engine has stopped and the pilot knows it " +
            "instantly. The demand is energy management under an irreversible clock: glide speed, site " +
            "selection and a committed turn inside about 90 seconds, with the memory drill competing " +
            "for the same attention. It is scored slightly BELOW H2 and H3 precisely because a trained " +
            "pilot has a rehearsed schema for it — the expertise effect this design is meant to expose " +
            "rather than assume away. That prediction is the most fragile in the study and depends on " +
            "the participant actually having the schema, which is why flight hours are a covariate.";
        m.EegRelevance =
            "The strongest startle onset in the set and the closest thing to a step change in demand: " +
            "silence, then nothing but glide. Expect the largest transient theta burst at CUE_ONSET, " +
            "and degraded checklist and decision performance in the 30-60 s startle window after it. " +
            "Also the mission most at risk of motor-artifact contamination, which is why it is paired " +
            "with L4 (same phase, matched manual demand, minimal cognitive demand).";
        m.ExpectedErrors = "Holding the nose up and decaying toward the stall; stretching the glide " +
                           "to a runway that cannot be reached; starting the drill before establishing " +
                           "the glide; freezing for several seconds (cognitive lock-up).";
        m.SuccessCriteria = "Best-glide speed established, a landing site committed to, and a " +
                            "survivable arrival — on the runway or under control on the ground.";
        m.FailureConditions = "Stall/spin; uncontrolled ground impact; no decision before 150 m.";
        m.AviationBasis = "FAA-H-8083-3C ch.18, engine failure and emergency approach. The handbook's " +
                          "worked example (300 ft AGL, a 4-second reaction time, ~1,000 fpm power-off " +
                          "descent) is why the turn back usually is not made, and it names the " +
                          "psychological hazards directly — reluctance to accept the emergency, and " +
                          "the desire to save the aeroplane leading to a stretched glide.";
        m.Approximations = "No propeller windmilling-drag model and no restart logic beyond the " +
                           "checklist items. The surrounding terrain has no prepared off-field landing " +
                           "sites, so 'field ahead' is judged only by whether the arrival is survivable " +
                           "under the existing crash model. Best-glide speed is asserted in the drill " +
                           "rather than derived from a published polar.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }
}
