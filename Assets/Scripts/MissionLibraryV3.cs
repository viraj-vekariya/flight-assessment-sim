// MissionLibraryV3 — VARIANT 3 of every (phase, class) cell.
//
// See MissionLibraryV2.cs for what a variant is and what it is not. The rule that
// matters: each of a cell's three variants must reach its workload class by a
// DIFFERENT cognitive mechanism, and the mission battery fails any cell whose variants
// repeat a `Mechanism` string.
//
// Variant 3 completes the mechanism coverage. Reading the HIGH row across variants:
//
//   TAKE-OFF   V1 concurrency        V2 forward reasoning     V3 sustained monitoring
//                                        about a margin           + prospective memory
//   CLIMB      V1 depleting resource V2 irreversible          V3 prioritisation among
//                                        commitment                simultaneous demands
//   CRUISE     V1 wrong information  V2 resource management   V3 concurrency
//   APPROACH   V1 irreversible       V2 re-computation        V3 startle + re-planning
//                 commitment                                       at high stakes
//
// No mechanism appears twice inside a cell, and the set spans the demand types the
// workload model actually weights: information processing, decision complexity,
// working memory, attention switching, uncertainty and temporal demand.

using System.Collections.Generic;
using UnityEngine;

public static class MissionLibraryV3
{
    public static List<MissionDefinition> All() =>
        new List<MissionDefinition> { L1(), L2(), L3(), L4(), M1(), M2(), M3(), M4(), H1(), H2(), H3(), H4() };

    const float T0 = MissionLibrary.BaselineEndT;

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 1 — TAXI AND TAKE-OFF
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L1()
    {
        var m = MissionLibrary.TaxiBase("L1V3", "Normal departure to a higher level-off", WorkloadClass.Low, 3);
        m.Objective = "Taxi to runway 01, hold short, take off and climb to 800 m on runway heading.";
        m.Brief = "You are on stand 1, engine running. Taxi via taxiway A to the holding point for " +
                  "runway 01 and hold short. Wait for your take-off clearance, then line up, take off " +
                  "and climb straight ahead to 800 m. Clear day, no other traffic.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01."));
        m.Events.Add(MissionDefinition.TakeoffClearance(86f, "climb runway heading to 800 m.", 6f));
        m.TargetAltitudeM = 800f;
        m.Mechanism = "reference - single-threaded procedure to a higher level-off";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 1, DecisionComplexity = 1, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 1, Perception = 2, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 0, Communication = 1, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 30, Physical = 26, Temporal = 20, Performance = 26, Effort = 31, Frustration = 15 };
        m.LoadRationale =
            "The take-off row's reference, realised a third way. The only difference from variant 1 is " +
            "the level-off altitude, which is deliberate: a LOW cell's job is to establish what the " +
            "phase costs before anything is added, and inventing demand to make the three LOW variants " +
            "look different would defeat that. What the variants buy in the LOW cells is protection " +
            "against a participant flying the identical trial twice, not mechanism diversity — the " +
            "mechanism diversity that matters is in MEDIUM and HIGH.";
        m.EegRelevance = "Phase-matched baseline for M1V3 and H1V3; taxi baseline identical to the row.";
        m.ExpectedErrors = "Taxiing past the hold-short line; departing without clearance; levelling " +
                           "at 600 m out of habit if the participant has flown another variant.";
        m.SuccessCriteria = "Hold short respected, airborne and established at 800 m +/- 90 m on runway heading.";
        m.FailureConditions = "Runway incursion; crash; failure to get airborne inside the trial.";
        m.AviationBasis = "FAA-H-8083-3C ch.2 and ch.5 (normal take-off and climb).";
        m.Approximations = "Same as L1: a small-field taxi layout, and ATC is a scripted sequence.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M1()
    {
        var m = MissionLibrary.TaxiBase("M1V3", "Conditional line-up clearance", WorkloadClass.Medium, 3);
        m.Objective = "Taxi for runway 01, hold a conditional clearance until its condition is met, " +
                      "then line up and depart.";
        m.Brief = "You are on stand 1, engine running. Taxi via taxiway A for runway 01 and hold short. " +
                  "There is arriving traffic. Departure is runway heading, climb 600 m. Clear day.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. ONE ARRIVAL."));
        // A CONDITIONAL clearance: an instruction that must be held, unexecuted, until an
        // external event occurs. The pilot must monitor for the condition, not for a call.
        m.Events.Add(MissionDefinition.Readback(T0 + 6f, "BEHIND THE LANDING AIRCRAFT, LINE UP RUNWAY 01 " +
                     "BEHIND. READ BACK", 9f, 5f));
        m.Events.Add(MissionDefinition.Traffic(T0 + 20f, TrafficBehaviour.ConvergingApproach,
                     "TRAFFIC ON SHORT FINAL RUNWAY 01", false, 6f, 6f));
        m.Events.Add(MissionDefinition.Msg(T0 + 48f, "CONTACT TOWER 118.7 AFTER THE ARRIVAL HAS PASSED."));
        m.Events.Add(MissionDefinition.TakeoffClearance(122f, "the arrival is clear, no delay.", 8f));
        m.Events.Add(MissionDefinition.Probe(206f, "CONFIRM AIRBORNE AND CLIMBING — respond", 6f, 12f));
        m.TargetAltitudeM = 600f;
        m.Mechanism = "prospective memory - holding an instruction until its condition occurs";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 3,
            AttentionSwitching = 3, SituationAwareness = 2, Perception = 3, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 1, Communication = 2, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 60, Physical = 30, Temporal = 44, Performance = 48, Effort = 60, Frustration = 42 };
        m.LoadRationale =
            "MEDIUM by PROSPECTIVE MEMORY — remembering to do something later, on a cue that is not a " +
            "reminder. This is the mechanism neither of the other two variants of this cell uses: M1 " +
            "is visual search against a present scene, M1V2 is replacing a plan already held, and this " +
            "is holding an intention across a delay while attending to something else. It is also the " +
            "one mechanism here with a documented aviation failure mode of its own: conditional " +
            "clearances are a recognised runway-incursion risk precisely because the pilot must both " +
            "identify the right aircraft and inhibit acting until it has passed. Manual demand 2, " +
            "matched to the row.";
        m.EegRelevance =
            "The interval between the conditional clearance and its execution is a maintenance period " +
            "of a different kind from M1V2's: nothing must be recalled on demand, but an intention " +
            "must be kept live against a competing task. The frequency-change call at T0+48 is a " +
            "deliberate secondary intention layered on the first.";
        m.ExpectedErrors = "Lining up before the arrival has passed (the incursion); forgetting the " +
                           "clearance entirely and waiting for a fresh one; missing the frequency change.";
        m.SuccessCriteria = "Conditional clearance acknowledged and NOT acted on early, hold short " +
                            "respected, airborne and established at 600 m.";
        m.FailureConditions = "Runway incursion; crash; failure to get airborne inside the trial.";
        m.AviationBasis = "ICAO Doc 4444 conditional clearances, and the EUROCONTROL/FAA runway-safety " +
                          "literature identifying mis-executed conditional line-up clearances as a " +
                          "recurring incursion cause.";
        m.Approximations = "The arriving aircraft is a scripted visual object on a fixed path; it " +
                           "cannot be talked to, and the hold-short gate is released by the scripted " +
                           "clearance rather than by the pilot's judgement that the runway is clear.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage,
                                    EventMarkers.TrafficOnset, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H1()
    {
        var m = MissionLibrary.TaxiBase("H1V3", "Low-visibility taxi past a hot spot", WorkloadClass.High, 3);
        m.Objective = "Taxi to runway 01 in poor visibility, navigate a known hot spot by signs and " +
                      "markings alone, hold a conditional clearance, and depart.";
        m.Brief = "You are on stand 1, engine running. Visibility is poor. Taxi via taxiway A for " +
                  "runway 01. There is a hot spot where taxiway A meets the link — the geometry there " +
                  "is confusing and aircraft have entered the runway from it by mistake. Hold short of " +
                  "runway 01. Departure is runway heading, climb 600 m.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. RVR 550 METRES. " +
                     "CAUTION HOT SPOT AT THE LINK."));
        m.Events.Add(MissionDefinition.Traffic(46f, TrafficBehaviour.TaxiingParallel, "TRAFFIC AHEAD ON ALPHA", false, 6f, 6f));
        m.Events.Add(MissionDefinition.Readback(T0 + 10f, "BEHIND THE DEPARTING AIRCRAFT, LINE UP " +
                     "RUNWAY 01 BEHIND. READ BACK", 9f, 6f));
        m.Events.Add(MissionDefinition.Traffic(T0 + 24f, TrafficBehaviour.HoldingOnRunway, "TRAFFIC DEPARTING RUNWAY 01", false, 6f, 6f));
        m.Events.Add(MissionDefinition.Probe(T0 + 40f, "CONFIRM POSITION ON ALPHA — respond", 6f, 8f));
        m.Events.Add(MissionDefinition.Msg(T0 + 58f, "REPORT WHEN YOU ARE HOLDING SHORT. TRAFFIC 5 MILES FINAL."));
        m.Events.Add(MissionDefinition.TakeoffClearance(140f, "no delay, traffic 3 miles final.", 8f));
        m.Events.Add(MissionDefinition.Wx(T0 + 90f, 90f, 0.5f, "VISIBILITY FURTHER REDUCED", 8f));
        m.Events.Add(MissionDefinition.Probe(232f, "REPORT PASSING 300 M — respond", 5f, 12f));
        m.Visibility01 = 0.62f;
        m.TargetAltitudeM = 600f;
        m.Mechanism = "sustained monitoring under degraded perception + prospective memory";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 3, DecisionComplexity = 3, WorkingMemory = 4,
            AttentionSwitching = 4, SituationAwareness = 4, Perception = 4, ManualControl = 2,
            ProceduralLoad = 3, Uncertainty = 4, Communication = 3, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 84, Physical = 36, Temporal = 70, Performance = 66, Effort = 84, Frustration = 68 };
        m.LoadRationale =
            "HIGH by SUSTAINED MONITORING under degraded input, combined with an intention held across " +
            "it — a third distinct mechanism for this cell. H1 is concurrency (many things at once) " +
            "and H1V2 is a single hard decision under a clock; this is neither. Nothing here is " +
            "individually difficult and there is no moment of crisis. The load is that the pilot can " +
            "never stop checking where they are, because the usual cue — being able to see the layout " +
            "— has been removed, and they must simultaneously keep a conditional clearance alive. " +
            "Perception and Uncertainty are both 4, which no other take-off mission has. Manual demand " +
            "stays at 2: taxiing slowly in poor visibility is not physically harder, which is exactly " +
            "why this mission is a clean high-load condition for an EEG contrast.";
        m.EegRelevance =
            "The bank's best candidate for a SUSTAINED rather than event-locked effect in the take-off " +
            "row, and therefore the natural partner to H2 and H3V2 in a windowed-classifier analysis. " +
            "The two position-confirmation probes are spare-capacity measures taken under continuous " +
            "monitoring load rather than after a discrete event.";
        m.ExpectedErrors = "Taking a wrong turn at the hot spot; crossing the hold-short line while " +
                           "looking for signs; lining up before the departing aircraft has gone; " +
                           "missing a probe while navigating.";
        m.SuccessCriteria = "Correct route flown, hold short respected, conditional clearance not " +
                            "acted on early, airborne and established at 600 m.";
        m.FailureConditions = "Runway incursion; crash; failure to get airborne inside the trial.";
        m.AviationBasis = "Low-visibility ground operations and published hot spots; FAA-H-8083-25 " +
                          "ch.14 and the FAA/ICAO runway-safety programmes, which identify hot spots " +
                          "precisely because layout confusion under reduced visibility is a leading " +
                          "incursion cause.";
        m.Approximations = "Reduced visibility is fog density and sky severity, not a modelled RVR: " +
                           "the quoted 550 m is a briefing figure, not a measured one. The hot spot is " +
                           "the existing link-taxiway junction, briefed as confusing rather than " +
                           "physically redesigned to be so.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage, EventMarkers.TrafficOnset,
                                    EventMarkers.WeatherOnset, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 2 — CLIMB / DEPARTURE
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L2()
    {
        var m = MissionLibrary.ClimbBase("L2V3", "Departure heading and level-off", WorkloadClass.Low, 3);
        m.Objective = "Fly an assigned departure heading and level off at the assigned altitude.";
        m.Brief = "Departure leg, 400 m. ATC will give you a heading and a climb. Fly them accurately. " +
                  "Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Atc(T0 + 6f, ScenarioEventType.HeadingChange, 340f, "left heading 340"));
        m.Events.Add(MissionDefinition.Atc(T0 + 14f, ScenarioEventType.AltitudeChange, 700f, "climb and maintain 700 m"));
        m.TargetAltitudeM = 400f;
        m.Mechanism = "reference - steady-state tracking with a turn and a level-off";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 0, DecisionComplexity = 0, WorkingMemory = 1,
            AttentionSwitching = 0, SituationAwareness = 1, Perception = 1, ManualControl = 2,
            ProceduralLoad = 0, Uncertainty = 0, Communication = 0, ErrorConsequence = 0 };
        m.Expected = new ExpectedTlx { Mental = 29, Physical = 26, Temporal = 21, Performance = 25, Effort = 30, Frustration = 15 };
        m.LoadRationale =
            "The climb row's reference. Two instructions arrive close together and then nothing else " +
            "happens for four minutes — enough activity to match the row's manual demand, far too " +
            "little to compete for any cognitive resource.";
        m.EegRelevance = "Phase-matched baseline for M2V3 and H2V3.";
        m.ExpectedErrors = "Overshooting the heading; climbing through the assigned altitude.";
        m.SuccessCriteria = "Heading and altitude captured and held within tolerance.";
        m.FailureConditions = "Crash; loss of control.";
        m.AviationBasis = "Routine departure vectoring; FAA-H-8083-3C ch.3.";
        m.Approximations = "No SID; ATC is a scripted sequence with no dialogue.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TargetChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M2()
    {
        var m = MissionLibrary.ClimbBase("M2V3", "Re-clearances and a frequency change", WorkloadClass.Medium, 3);
        m.Objective = "Fly the departure while absorbing a sequence of amended clearances and an " +
                      "administrative change.";
        m.Brief = "Departure leg, 400 m, heading 000°. ATC is busy this morning. Fly the departure " +
                  "and comply with what you are given. Clear day.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Readback(T0 + 4f, "CLEARANCE: climb 700 m, heading 020. READ BACK", 8f, 4f));
        m.Events.Add(MissionDefinition.Atc(T0 + 14f, ScenarioEventType.AltitudeChange, 700f, "climb and maintain 700 m"));
        m.Events.Add(MissionDefinition.Atc(T0 + 20f, ScenarioEventType.HeadingChange, 20f, "right heading 020"));
        m.Events.Add(MissionDefinition.Msg(T0 + 46f, "SQUAWK 4271. CONTACT DEPARTURE 124.35."));
        m.Events.Add(MissionDefinition.Readback(T0 + 70f, "AMENDED: climb 850 m, heading 350. READ BACK", 8f, 6f));
        m.Events.Add(MissionDefinition.Atc(T0 + 80f, ScenarioEventType.AltitudeChange, 850f, "climb and maintain 850 m"));
        m.Events.Add(MissionDefinition.Atc(T0 + 86f, ScenarioEventType.HeadingChange, 350f, "left heading 350"));
        m.Events.Add(MissionDefinition.Probe(T0 + 132f, "CONFIRM ASSIGNED SQUAWK — respond", 6f, 10f));
        m.Events.Add(MissionDefinition.Probe(T0 + 176f, "CONFIRM ASSIGNED ALTITUDE — respond", 6f, 10f));
        m.TargetAltitudeM = 400f;
        m.Mechanism = "working memory turnover across mixed verbal and numeric items";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 1, WorkingMemory = 4,
            AttentionSwitching = 3, SituationAwareness = 2, Perception = 2, ManualControl = 2,
            ProceduralLoad = 1, Uncertainty = 1, Communication = 3, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 64, Physical = 30, Temporal = 56, Performance = 50, Effort = 62, Frustration = 46 };
        m.LoadRationale =
            "MEDIUM by VOLUME of items rather than by their difficulty, which distinguishes it from " +
            "both siblings: M2 is one startling event with nothing to remember, M2V2 is one ambiguous " +
            "event with nothing to remember, and this has nothing surprising at all and six things to " +
            "hold. The items are deliberately of mixed type — two altitudes, two headings, a squawk " +
            "and a frequency — because same-type items interfere with each other in a way mixed items " +
            "do not, and the design wants turnover, not a memory-span test. Communication is 4, the " +
            "highest in the bank. Manual demand 2, matched to the row.";
        m.EegRelevance =
            "The most regular event structure in the climb row: paired clearance/execution events at " +
            "predictable spacing, which supports averaging across repetitions within a single trial " +
            "rather than relying on the between-mission contrast alone.";
        m.ExpectedErrors = "Flying the first clearance after the second has been issued; confusing the " +
                           "squawk with the frequency; answering a probe with a superseded value.";
        m.SuccessCriteria = "Final assigned altitude and heading held, both probes answered correctly " +
                            "in window.";
        m.FailureConditions = "Crash; loss of control.";
        m.AviationBasis = "Clearance read-back/hear-back load; ICAO Doc 9683 human-factors training " +
                          "manual, communication chapter.";
        m.Approximations = "Squawk and frequency are spoken and acknowledged but there is no " +
                           "transponder or radio panel to set them on, so the items are held in memory " +
                           "rather than actioned — which is the load being manipulated, but it is not " +
                           "the full task a real pilot would perform.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage,
                                    EventMarkers.TargetChange, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H2()
    {
        var m = MissionLibrary.ClimbBase("H2V3", "Traffic and terrain while being re-cleared", WorkloadClass.High, 3);
        m.Objective = "Fly the departure and prioritise correctly when traffic, terrain and a new " +
                      "clearance all demand attention at once.";
        m.Brief = "Departure leg, 400 m, heading 000°. Rising ground to the north-east. ATC will work " +
                  "you. Fly the departure. Hazy.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°. TERRAIN NORTH-EAST."));
        m.Events.Add(MissionDefinition.Atc(T0 + 4f, ScenarioEventType.AltitudeChange, 800f, "climb and maintain 800 m"));
        // Three demands inside ~20 s, deliberately CONFLICTING in what they ask for:
        // the clearance turns toward the terrain, the traffic is on that side.
        m.Events.Add(MissionDefinition.Traffic(T0 + 44f, TrafficBehaviour.CrossingDeparture,
                     "TRAFFIC 2 O'CLOCK, CROSSING, SAME LEVEL", true, 7f, 6f));
        m.Events.Add(MissionDefinition.Readback(T0 + 52f, "AMENDED: right heading 050 for spacing. READ BACK", 8f, 5f));
        m.Events.Add(MissionDefinition.Atc(T0 + 60f, ScenarioEventType.HeadingChange, 50f, "right heading 050"));
        m.Events.Add(MissionDefinition.Atc(T0 + 66f, ScenarioEventType.Alarm, 0f, "TERRAIN AHEAD — CHECK ALTITUDE") );
        m.Events.Add(MissionDefinition.Decide(T0 + 78f, "ACCEPT THE TURN, OR REFUSE IT FOR TERRAIN? — decide", 9f, 8f));
        m.Events.Add(MissionDefinition.Probe(T0 + 150f, "REPORT LEVEL — respond", 5f, 10f));
        m.Visibility01 = 0.28f;
        m.AmbientTurbulence = 0.06f;
        m.TargetAltitudeM = 400f;
        m.Mechanism = "prioritisation among simultaneous conflicting demands";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 4, DecisionComplexity = 3, WorkingMemory = 3,
            AttentionSwitching = 4, SituationAwareness = 4, Perception = 3, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 3, Communication = 3, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 82, Physical = 34, Temporal = 80, Performance = 64, Effort = 82, Frustration = 66 };
        m.LoadRationale =
            "HIGH by PRIORITISATION, which is not the same as concurrency and is the third distinct " +
            "mechanism in this cell. H2 is one slow problem; H2V2 is one fast commitment; this is " +
            "three demands arriving together that CANNOT all be satisfied — the clearance turns the " +
            "aeroplane toward the terrain and toward the traffic, so complying immediately is wrong " +
            "and refusing outright is also wrong. Attention switching and situation awareness are both " +
            "4. The correct behaviour is the standard aviate-navigate-communicate ordering, which " +
            "gives a defensible right answer to score against rather than a matter of taste. Manual " +
            "demand 2, matched to the row.";
        m.EegRelevance =
            "Three separately-marked onsets inside about twenty seconds — TRAFFIC_ONSET, a clearance, " +
            "and a WARNING_APPEARS — which makes this the bank's best test of whether an EEG workload " +
            "index tracks demand at a resolution finer than the mission. If the index only separates " +
            "missions and not the segments inside this one, that is an informative negative result.";
        m.ExpectedErrors = "Turning into the terrain because ATC said so (compliance bias); fixating " +
                           "on the traffic and losing the altitude; refusing the turn without saying " +
                           "so; letting the decision window expire.";
        m.SuccessCriteria = "Terrain avoided, traffic acknowledged, an explicit decision made in " +
                            "window, altitude held.";
        m.FailureConditions = "Crash; terrain impact; loss of control; no decision made.";
        m.AviationBasis = "Aviate-navigate-communicate prioritisation and the handling of conflicting " +
                          "ATC instructions; FAA-H-8083-25 ch.2 and ch.17. Accepting a clearance that " +
                          "the pilot can see is unsafe is a documented crew-resource-management failure.";
        m.Approximations = "The terrain warning is a scripted call, not a modelled TAWS with a real " +
                           "terrain database lookup — though the rising ground it refers to genuinely " +
                           "exists in the world and can genuinely be hit.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TrafficOnset,
                                    EventMarkers.WarningAppears, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 3 — CRUISE / EN-ROUTE
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L3()
    {
        var m = MissionLibrary.CruiseBase("L3V3", "Single-waypoint navigation", WorkloadClass.Low, 3);
        m.Objective = "Track to a single waypoint at 700 m.";
        m.Brief = "Straight and level at 700 m. A waypoint is displayed on the navigation display. " +
                  "Track to it, holding altitude. Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "MAINTAIN 700 M. PROCEED DIRECT TO WAYPOINT ALPHA."));
        m.Waypoints.Add(new Waypoint(new Vector3(900f, 700f, 3200f), "ALPHA", 220f));
        m.Goal = ScenarioGoal.Navigate;
        m.Mechanism = "reference - steady-state tracking to a displayed target";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 0, DecisionComplexity = 0, WorkingMemory = 1,
            AttentionSwitching = 0, SituationAwareness = 2, Perception = 1, ManualControl = 2,
            ProceduralLoad = 0, Uncertainty = 0, Communication = 0, ErrorConsequence = 0 };
        m.Expected = new ExpectedTlx { Mental = 30, Physical = 25, Temporal = 20, Performance = 26, Effort = 30, Frustration = 15 };
        m.LoadRationale =
            "The cruise row's reference, realised as navigation rather than as a heading hold. " +
            "Situation awareness is 2 rather than 1 because the pilot must relate the display to the " +
            "aeroplane, which is the same relating that M3V3 and H3V3 require — the reference has to " +
            "contain the activity, only without the competition. One waypoint, displayed " +
            "continuously, no time limit.";
        m.EegRelevance = "Phase-matched baseline for M3V3 and H3V3, with the navigation display in " +
                         "active use so display scanning is present in the reference too.";
        m.ExpectedErrors = "Wandering off track; losing altitude while looking at the display.";
        m.SuccessCriteria = "Waypoint reached, altitude held within +/- 70 m.";
        m.FailureConditions = "Crash; loss of control.";
        m.AviationBasis = "Pilotage and basic navigation display use; FAA-H-8083-25 ch.16.";
        m.Approximations = "The waypoint is a marker in the world and a symbol on the MFD; there is no " +
                           "VOR, GPS receiver or flight plan to program.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M3()
    {
        var m = MissionLibrary.CruiseBase("M3V3", "Traffic search in reduced visibility", WorkloadClass.Medium, 3);
        m.Objective = "Hold altitude and heading while searching for and acquiring called traffic in " +
                      "poor visibility.";
        m.Brief = "Straight and level at 700 m, heading 000°. Visibility is reduced in haze. ATC will " +
                  "call traffic to you. Hold altitude and heading.";
        m.Events.Add(MissionDefinition.Msg(1f, "MAINTAIN 700 M, HEADING 000°. HAZE, VISIBILITY 4 KM."));
        m.Events.Add(MissionDefinition.Trfc(T0 + 12f, "TRAFFIC 11 O'CLOCK, 3 MILES, OPPOSITE DIRECTION — REPORT IN SIGHT", 9f, 6f));
        m.Events.Add(MissionDefinition.Traffic(T0 + 40f, TrafficBehaviour.CrossingDeparture,
                     "TRAFFIC 1 O'CLOCK, CROSSING LEFT TO RIGHT", true, 9f, 7f));
        m.Events.Add(MissionDefinition.Wx(T0 + 70f, 100f, 0.45f, "VISIBILITY FURTHER REDUCED", 8f));
        m.Events.Add(MissionDefinition.Trfc(T0 + 108f, "SECOND TRAFFIC 2 O'CLOCK, 2 MILES — REPORT IN SIGHT", 9f, 8f));
        m.Events.Add(MissionDefinition.Probe(T0 + 152f, "CONFIRM LEVEL — respond", 5f, 10f));
        m.Visibility01 = 0.4f;
        m.AmbientTurbulence = 0.08f;
        m.Mechanism = "visual search and sustained monitoring under degraded input";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 1, WorkingMemory = 2,
            AttentionSwitching = 4, SituationAwareness = 2, Perception = 4, ManualControl = 2,
            ProceduralLoad = 1, Uncertainty = 2, Communication = 2, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 60, Physical = 30, Temporal = 42, Performance = 52, Effort = 62, Frustration = 50 };
        m.LoadRationale =
            "MEDIUM by PERCEPTUAL load, the third distinct mechanism in this cell alongside M3's " +
            "working memory and M3V2's re-planning. The pilot is asked to do something easy to " +
            "describe and hard to do — find a small object against a low-contrast background while " +
            "holding altitude on instruments — so Perception and Attention Switching are 4 while " +
            "Decision Complexity and Working Memory stay at 2. The searches are deliberately not all " +
            "successful: one target is genuinely difficult to acquire, so a participant who reports " +
            "everything in sight immediately is displaying a response bias worth measuring. Manual " +
            "demand 2, matched to the row.";
        m.EegRelevance =
            "Alpha-band effects of visual search and the switching between an outside scan and an " +
            "instrument scan are among the better-established EEG workload signatures, which makes " +
            "this the mission most likely to produce a positive result if the recording chain is " +
            "sound — and therefore a useful pipeline check as well as a condition.";
        m.ExpectedErrors = "Losing altitude while looking outside; reporting traffic in sight that has " +
                           "not been acquired; missing the second call while still searching for the first.";
        m.SuccessCriteria = "Both traffic calls responded to in window, altitude within +/- 70 m and " +
                            "heading within +/- 12° for the majority of the trial.";
        m.FailureConditions = "Crash; loss of control; mid-air collision.";
        m.AviationBasis = "See-and-avoid limitations and the empty-field problem; FAA-H-8083-25 ch.2 " +
                          "and AC 90-48. The see-and-avoid literature is explicit that acquisition " +
                          "rates in haze are poor even when the traffic is called.";
        m.Approximations = "Traffic is a small number of scripted aircraft on fixed paths; 'report in " +
                           "sight' is a keypress and the simulator cannot verify the pilot actually " +
                           "saw the aeroplane, only that they responded and when.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TrafficOnset,
                                    EventMarkers.WeatherOnset, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H3()
    {
        var m = MissionLibrary.CruiseBase("H3V3", "Everything at once in the cruise", WorkloadClass.High, 3);
        m.Objective = "Hold the cruise while a clearance, traffic and a systems annunciation compete " +
                      "for attention simultaneously.";
        m.Brief = "Straight and level at 700 m, heading 000°. Busy sector. Hold altitude and heading " +
                  "and comply with what you are given. Hazy.";
        m.Events.Add(MissionDefinition.Msg(1f, "MAINTAIN 700 M, HEADING 000°."));
        m.Events.Add(MissionDefinition.Readback(T0 + 8f, "CLEARANCE: descend 500 m, heading 030, cross " +
                     "the boundary WITHIN 4 MINUTES. READ BACK", 9f, 5f));
        m.Events.Add(MissionDefinition.Atc(T0 + 18f, ScenarioEventType.AltitudeChange, 500f, "descend and maintain 500 m"));
        m.Events.Add(MissionDefinition.Atc(T0 + 24f, ScenarioEventType.HeadingChange, 30f, "right heading 030"));
        m.Events.Add(MissionDefinition.Traffic(T0 + 34f, TrafficBehaviour.CrossingDeparture,
                     "TRAFFIC 12 O'CLOCK, 2 MILES, CONVERGING", true, 7f, 5f));
        m.Events.Add(MissionDefinition.Fail(T0 + 44f, FailureKind.EngineRoughness, 0.3f, "ROUGH RUNNING", 7f));
        m.Events.Add(MissionDefinition.List(T0 + 76f, ChecklistLibrary.EngineRough, "ROUGH RUNNING DRILL"));
        m.Events.Add(MissionDefinition.Readback(T0 + 118f, "AMENDED: maintain 600 m, heading 010. READ BACK", 8f, 6f));
        m.Events.Add(MissionDefinition.Atc(T0 + 128f, ScenarioEventType.AltitudeChange, 600f, "climb and maintain 600 m"));
        m.Events.Add(MissionDefinition.Atc(T0 + 134f, ScenarioEventType.HeadingChange, 10f, "left heading 010"));
        m.Events.Add(MissionDefinition.Probe(T0 + 176f, "CONFIRM ASSIGNED ALTITUDE — respond", 6f, 10f));
        m.Visibility01 = 0.3f;
        m.AmbientTurbulence = 0.1f;
        m.Mechanism = "concurrency - competing demands with no gaps between them";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 4, DecisionComplexity = 3, WorkingMemory = 4,
            AttentionSwitching = 4, SituationAwareness = 4, Perception = 3, ManualControl = 2,
            ProceduralLoad = 4, Uncertainty = 2, Communication = 4, ErrorConsequence = 3 };
        m.Expected = new ExpectedTlx { Mental = 86, Physical = 36, Temporal = 84, Performance = 68, Effort = 86, Frustration = 70 };
        m.LoadRationale =
            "HIGH by CONCURRENCY — the highest predicted load in the cruise row and the mission with " +
            "the least idle time in the bank. Each individual demand here is one a MEDIUM mission " +
            "would carry alone; the manipulation is that they overlap, so that no task can be finished " +
            "before the next begins and the pilot is forced to interleave rather than queue. That is " +
            "the distinction Wickens' multiple-resource account is about, and it is a different claim " +
            "from H3's (information that is wrong) or H3V2's (a resource that is running out). " +
            "Communication, working memory and attention switching are all 4. Manual demand stays at " +
            "2 — nothing about the aeroplane is degraded — which is what makes this a clean high-load " +
            "condition despite being the busiest.";
        m.EegRelevance =
            "The saturation case. If an EEG workload index does not separate this from L3V3, the index " +
            "is not measuring workload in this paradigm at all, so it functions as the positive " +
            "control for the whole cruise row. The overlapping events also mean epochs here will " +
            "contaminate each other, and the analysis should use the sustained window rather than " +
            "event-related averaging.";
        m.ExpectedErrors = "Dropping the crossing time; flying a superseded clearance; abandoning the " +
                           "drill part-way; missing the traffic; answering the probe with the old altitude.";
        m.SuccessCriteria = "Final assigned altitude and heading held, drill run, traffic acknowledged, " +
                            "probe answered in window.";
        m.FailureConditions = "Crash; loss of control.";
        m.AviationBasis = "Task saturation and interleaving in single-pilot operations; " +
                          "FAA-H-8083-25 ch.2 (workload management) and Wickens' multiple-resource " +
                          "account of concurrent-task interference.";
        m.Approximations = "Roughness here is a mild 0.3 severity that does not threaten the flight — " +
                           "it is present as a demand on attention rather than as an emergency, which " +
                           "is a deliberate difference from H2V2 where the same failure kind is severe.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TargetChange, EventMarkers.TrafficOnset,
                                    EventMarkers.TriggerArmed, EventMarkers.CueOnset, EventMarkers.ChecklistStart,
                                    EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 4 — APPROACH AND LANDING
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L4()
    {
        var m = MissionLibrary.ApproachBase("L4V3", "Normal landing from a closer final", WorkloadClass.Low, 3);
        m.Objective = "Fly a straight-in approach and land on runway 01.";
        m.Brief = "You are 9 km on final for runway 01 at 400 m. Descend, configure and land. " +
                  "Clear day, light air, no other traffic.";
        m.StartPos = new Vector3(0f, 400f, -9000f);
        m.StartAltitudeM = 400f; m.TargetAltitudeM = 400f;
        m.Events.Add(MissionDefinition.Msg(1f, "CLEARED TO LAND RUNWAY 01. WIND CALM."));
        m.Events.Add(MissionDefinition.List(T0 + 40f, ChecklistLibrary.BeforeLanding, "BEFORE LANDING"));
        m.Mechanism = "reference - rehearsed approach from a shorter final";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 2, DecisionComplexity = 1, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 2, Perception = 2, ManualControl = 3,
            ProceduralLoad = 2, Uncertainty = 0, Communication = 1, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 35, Physical = 42, Temporal = 30, Performance = 35, Effort = 42, Frustration = 22 };
        m.LoadRationale =
            "The approach row's reference, from a closer starting point so that a participant who has " +
            "flown another variant does not simply repeat a memorised profile. Temporal demand is 2 " +
            "rather than 1 because there is less track distance to lose the height in; it remains an " +
            "unhurried, single-threaded approach with nothing to decide.";
        m.EegRelevance = "Phase-matched baseline for M4V3 and H4V3.";
        m.ExpectedErrors = "High on final because the descent was started late; long touchdown.";
        m.SuccessCriteria = "Touchdown on the runway, wings level, sink rate inside the acceptable band.";
        m.FailureConditions = "Crash; runway excursion; not landed inside the trial.";
        m.AviationBasis = "Normal approach and landing, FAA-H-8083-3C ch.8.";
        m.Approximations = "No glideslope guidance and no PAPI; the approach is flown visually.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.ChecklistStart, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M4()
    {
        var m = MissionLibrary.ApproachBase("M4V3", "Traffic on the runway — the go-around", WorkloadClass.Medium, 3);
        m.Objective = "Fly the approach, recognise that the runway is occupied, go around, and " +
                      "re-position for a second approach.";
        m.Brief = "You are 12 km on final for runway 01 at 500 m. Descend, configure and land. There " +
                  "is departing traffic ahead of you. Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "CONTINUE APPROACH RUNWAY 01. ONE DEPARTURE AHEAD."));
        m.Events.Add(MissionDefinition.Cfg(T0 + 24f, "FLAPS 10 — configure for the approach"));
        m.Events.Add(MissionDefinition.List(T0 + 44f, ChecklistLibrary.BeforeLanding, "BEFORE LANDING"));
        m.Events.Add(MissionDefinition.Traffic(T0 + 76f, TrafficBehaviour.HoldingOnRunway,
                     "TRAFFIC STILL ON THE RUNWAY", false, 6f, 6f));
        m.Events.Add(MissionDefinition.Around(T0 + 92f, "traffic on the runway — climb runway heading 500 m", 6f));
        m.Events.Add(MissionDefinition.Msg(T0 + 118f, "CLIMB 500 M RUNWAY HEADING. EXPECT A LEFT CIRCUIT " +
                     "FOR A SECOND APPROACH."));
        m.Events.Add(MissionDefinition.Probe(T0 + 160f, "REPORT DOWNWIND — respond", 6f, 10f));
        m.Mechanism = "abandoning a committed procedure and executing a rehearsed alternative";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 2,
            AttentionSwitching = 2, SituationAwareness = 3, Perception = 3, ManualControl = 3,
            ProceduralLoad = 4, Uncertainty = 2, Communication = 2, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 62, Physical = 50, Temporal = 66, Performance = 56, Effort = 68, Frustration = 50 };
        m.LoadRationale =
            "MEDIUM by PROCEDURAL EXECUTION under the reluctance to abandon a plan. The go-around is a " +
            "rehearsed drill, so decision complexity is only 2 — the pilot is told to go around and " +
            "there is nothing to work out. What makes it demanding is that it must be executed " +
            "promptly, in the correct order (power, attitude, configuration), at the exact moment the " +
            "pilot is most committed to landing. Procedural load is 4, the highest in this row's " +
            "MEDIUM cells. That is a different mechanism from M4's degraded perception and M4V2's " +
            "time-pressured re-plan, and it is the one with the best-documented failure mode: " +
            "plan-continuation bias. Manual demand 3, matched to the row.";
        m.EegRelevance =
            "GO_AROUND_COMMANDED to GO_AROUND_INITIATED is a directly measured response latency at a " +
            "moment of maximum commitment, and is the cleanest behavioural index of " +
            "plan-continuation bias the bank produces. It pairs with the EEG epoch at the same instant.";
        m.ExpectedErrors = "Continuing to land anyway; going around but retracting flap before " +
                           "establishing a climb; losing runway heading during the go-around; " +
                           "missing the downwind report while re-configuring.";
        m.SuccessCriteria = "Go-around initiated promptly, climb established on runway heading at " +
                            "500 m, aircraft under control at the end of the trial.";
        m.FailureConditions = "Crash; landing on an occupied runway; loss of control.";
        m.AviationBasis = "Go-around technique and the decision to go around; FAA-H-8083-3C ch.8, and " +
                          "the Flight Safety Foundation's finding that the commonest go-around error " +
                          "is not executing one.";
        m.Approximations = "The trial ends in the climb-out rather than at a second touchdown — 300 s " +
                           "does not contain an approach, a go-around and a full circuit — so the " +
                           "second approach is briefed but not flown.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TrafficOnset,
                                    EventMarkers.GoAroundCommanded, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H4()
    {
        var m = MissionLibrary.ApproachBase("H4V3", "Late go-around and re-sequence", WorkloadClass.High, 3);
        m.Objective = "Fly the approach, execute a go-around ordered at low altitude, and immediately " +
                      "absorb a re-sequencing clearance while re-configuring.";
        m.Brief = "You are 12 km on final for runway 01 at 500 m. Descend, configure and land. " +
                  "Moderate traffic. Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "CONTINUE APPROACH RUNWAY 01."));
        m.Events.Add(MissionDefinition.Cfg(T0 + 20f, "FLAPS 10 — configure for the approach"));
        m.Events.Add(MissionDefinition.List(T0 + 40f, ChecklistLibrary.BeforeLanding, "BEFORE LANDING"));
        m.Events.Add(MissionDefinition.Traffic(T0 + 84f, TrafficBehaviour.CrossingRunway,
                     "VEHICLE CROSSING THE RUNWAY", false, 6f, 5f));
        // Ordered LATE and LOW: the startle is the point, and the re-clearance lands on
        // top of it before the go-around is even stabilised.
        m.Events.Add(MissionDefinition.Around(T0 + 104f, "GO AROUND, GO AROUND — vehicle on the runway", 7f));
        m.Events.Add(MissionDefinition.Readback(T0 + 116f, "CLIMB 600 M, LEFT HEADING 300, NUMBER THREE " +
                     "IN SEQUENCE. READ BACK", 9f, 5f));
        m.Events.Add(MissionDefinition.Atc(T0 + 126f, ScenarioEventType.AltitudeChange, 600f, "climb and maintain 600 m"));
        m.Events.Add(MissionDefinition.Atc(T0 + 132f, ScenarioEventType.HeadingChange, 300f, "left heading 300"));
        m.Events.Add(MissionDefinition.Traffic(T0 + 146f, TrafficBehaviour.ConvergingApproach,
                     "TRAFFIC 10 O'CLOCK, SAME LEVEL, JOINING", true, 7f, 6f));
        m.Events.Add(MissionDefinition.Probe(T0 + 190f, "CONFIRM YOUR SEQUENCE NUMBER — respond", 6f, 10f));
        m.Mechanism = "startle followed immediately by re-planning at high stakes";
        m.Profile = new WorkloadProfile {
            MentalDemand = 3, TemporalDemand = 4, DecisionComplexity = 3, WorkingMemory = 3,
            AttentionSwitching = 3, SituationAwareness = 4, Perception = 3, ManualControl = 3,
            ProceduralLoad = 4, Uncertainty = 2, Communication = 2, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 86, Physical = 54, Temporal = 86, Performance = 70, Effort = 88, Frustration = 72 };
        m.LoadRationale =
            "HIGH by STARTLE FOLLOWED BY LOAD, which is the sequence the startle literature identifies " +
            "as the dangerous one and which neither sibling reproduces. H4 is a committed emergency " +
            "with a clock; H4V2 is a recomputation with time to do it. Here the pilot is startled at " +
            "the worst moment — low, slow, configured to land — and then, before the go-around is " +
            "stabilised, is given three new items and a joining aircraft. The manipulation is the " +
            "OVERLAP between the startle-recovery period and the arrival of new information, which is " +
            "why working memory and attention switching are 4 despite the go-around itself being a " +
            "rehearsed drill. Manual demand 3, matched to the row.";
        m.EegRelevance =
            "The bank's cleanest startle epoch, with an unambiguous onset (GO_AROUND_COMMANDED) at a " +
            "known aircraft state, followed by a measurable recovery interval before the clearance " +
            "arrives. The contrast of interest is not just load versus baseline but whether the " +
            "response to the clearance differs from the same clearance delivered in a calm state, " +
            "which M2V3 provides.";
        m.ExpectedErrors = "Landing anyway; going around but mishandling the configuration; flying the " +
                           "runway heading instead of 300; losing the sequence number; missing the " +
                           "joining traffic while re-configuring.";
        m.SuccessCriteria = "Go-around initiated promptly, climb established, assigned altitude and " +
                            "heading captured, traffic acknowledged, probe answered in window.";
        m.FailureConditions = "Crash; landing on an occupied runway; loss of control.";
        m.AviationBasis = "Startle and surprise in flight operations (EASA startle-effect research; " +
                          "Landman et al. on surprise and the 'freeze' response), combined with " +
                          "go-around technique from FAA-H-8083-3C ch.8.";
        m.Approximations = "The crossing vehicle is a scripted traffic object rather than a ground " +
                           "vehicle model. The trial ends on the re-sequencing leg; there is no second " +
                           "approach inside 300 s.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TrafficOnset,
                                    EventMarkers.GoAroundCommanded, EventMarkers.TargetChange,
                                    EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }
}
