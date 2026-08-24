// MissionLibraryV2 — VARIANT 2 of every (phase, class) cell.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHAT A "VARIANT" IS, AND WHAT IT IS NOT
// ═══════════════════════════════════════════════════════════════════════════════
// A variant is a SECOND INTERCHANGEABLE REALISATION of the same experimental cell,
// not a second difficulty setting and not a re-skin. Variant 2 of TAKE-OFF/HIGH must
// impose the same CLASS of demand as variant 1 — high cognitive load during taxi and
// departure, with manual demand matched to its own row — while getting there by a
// DIFFERENT COGNITIVE MECHANISM.
//
// The different-mechanism rule is what makes the bank worth having. If variant 2 were
// variant 1 with the numbers nudged, a participant on variant 2 would simply be doing
// variant 1, and the bank would buy nothing but the appearance of breadth. Because the
// mechanisms differ, a class effect that survives across variants is a class effect and
// not an artefact of one particular scenario — which is a stronger claim than the
// twelve-mission design could make at all.
//
// Every mission declares its `Mechanism`, and the mission battery FAILS a cell whose
// three variants repeat a mechanism string. The rule is enforced, not just asserted.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHAT IS HELD CONSTANT WITH VARIANT 1
// ═══════════════════════════════════════════════════════════════════════════════
//   * 300 s duration and a 60 s un-manipulated in-task baseline — identical.
//   * Start geometry per phase — identical, because approach and departure geometry
//     are controlled constants, not nuisance variables.
//   * Manual/psychomotor demand matched within the phase row (spread <= 1 on 0-4).
//   * Every load-inducing event carries jitter drawn from the trial seed.
//
// Reviewers should be able to diff a variant against variant 1 and find the mechanism
// changed and nothing else structural.

using System.Collections.Generic;
using UnityEngine;

public static class MissionLibraryV2
{
    public static List<MissionDefinition> All() =>
        new List<MissionDefinition> { L1(), L2(), L3(), L4(), M1(), M2(), M3(), M4(), H1(), H2(), H3(), H4() };

    const float T0 = MissionLibrary.BaselineEndT;

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 1 — TAXI AND TAKE-OFF
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L1()
    {
        var m = MissionLibrary.TaxiBase("L1V2", "Normal departure with a routine readback", WorkloadClass.Low, 2);
        m.Objective = "Taxi to runway 01, hold short, read back the departure clearance, take off and " +
                      "climb to 600 m on runway heading.";
        m.Brief = "You are on stand 1, engine running. Taxi via taxiway A to the holding point for " +
                  "runway 01 and hold short. Read back your departure clearance when it is given. " +
                  "Then line up, take off, and climb straight ahead to 600 m. Clear day, no other traffic.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01."));
        m.Events.Add(MissionDefinition.Readback(70f, "CLEARANCE: runway heading, climb 600 m — ACKNOWLEDGE", 8f, 4f));
        m.Events.Add(MissionDefinition.TakeoffClearance(92f, "climb runway heading to 600 m.", 6f));
        m.Mechanism = "reference - single-threaded procedure with one acknowledgement";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 1, DecisionComplexity = 1, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 1, Perception = 2, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 0, Communication = 1, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 32, Physical = 25, Temporal = 22, Performance = 25, Effort = 32, Frustration = 16 };
        m.LoadRationale =
            "The take-off row's reference condition, realised a second way. The single acknowledgement " +
            "is deliberately trivial — one item, no readback content to hold, ample window — and exists " +
            "so that the LOW cell contains the same KIND of activity as its MEDIUM and HIGH neighbours " +
            "(a radio call happens) without any of the demand that makes them what they are. Without " +
            "it, a participant could distinguish LOW from the rest purely by the radio being silent, " +
            "and 'the radio was quiet' is a cue about the condition rather than a property of it.";
        m.EegRelevance =
            "Phase-matched baseline for M1V2 and H1V2. Its first 60 s is taxiing, the same baseline " +
            "activity as the other two missions in the row, so the within-row class contrast is clean.";
        m.ExpectedErrors = "Taxiing past the hold-short line; taking off without the clearance; " +
                           "drifting off runway heading in the climb.";
        m.SuccessCriteria = "Hold short respected, clearance acknowledged, airborne and established " +
                            "at 600 m +/- 90 m on runway heading.";
        m.FailureConditions = "Runway incursion; crash; failure to get airborne inside the trial.";
        m.AviationBasis = "FAA-H-8083-3C ch.2 (airport operations) and the standard departure " +
                          "clearance readback required by ICAO Annex 10 vol. II.";
        m.Approximations = "Read-back is a single keypress acknowledgement, not speech: the simulator " +
                           "cannot check that the pilot read the clearance back CORRECTLY, only that " +
                           "they responded and how quickly.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M1()
    {
        var m = MissionLibrary.TaxiBase("M1V2", "Runway change before line-up", WorkloadClass.Medium, 2);
        m.Objective = "Taxi for runway 01, absorb a late runway and departure change at the holding " +
                      "point, and fly the amended departure.";
        m.Brief = "You are on stand 1, engine running. Taxi via taxiway A for runway 01 and hold short. " +
                  "Expect a straight-ahead departure to 600 m. Clear day.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. EXPECT RUNWAY 01, RUNWAY HEADING."));
        // The change arrives while still taxiing: the pilot must REPLACE a briefed plan
        // rather than build one, which is the working-memory cost this cell is about.
        m.Events.Add(MissionDefinition.Readback(T0 + 8f, "CHANGE OF DEPARTURE: after airborne turn LEFT " +
                     "heading 340, climb 750 m. READ BACK", 9f, 6f));
        m.Events.Add(MissionDefinition.TakeoffClearance(104f, "left heading 340, climb 750 m.", 7f));
        m.Events.Add(MissionDefinition.Atc(146f, ScenarioEventType.AltitudeChange, 750f, "climb and maintain 750 m"));
        m.Events.Add(MissionDefinition.Atc(186f, ScenarioEventType.HeadingChange, 340f, "left heading 340"));
        m.Events.Add(MissionDefinition.Probe(238f, "CONFIRM ASSIGNED HEADING — respond", 6f, 10f));
        // TargetHeadingDeg is the target from t = 0, NOT the amended one: the amendment
        // arrives via the HeadingChange event at 158 s and must not be pre-applied.
        // Setting it to 330 here made the climb-out target 330 from brake release, so the
        // aeroplane turned left off the flattened aerodrome corridor while still below
        // 150 m and flew into rising ground — the battery caught it as
        // "Destroyed (terrain impact)" at 131 m, x = -677 m.
        m.TargetAltitudeM = 750f;
        // And the amended heading itself is now 340 rather than 330, and the climb is
        // commanded BEFORE the turn, so the nominal track stays inside the departure
        // corridor until the aeroplane is high enough to cross the hills. Verified by
        // the terrain-clearance design check, not by eye.
        m.TargetHeadingDeg = 0f;
        m.Mechanism = "working memory - replacing a briefed plan";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 4,
            AttentionSwitching = 2, SituationAwareness = 2, Perception = 2, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 1, Communication = 2, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 60, Physical = 30, Temporal = 45, Performance = 45, Effort = 58, Frustration = 38 };
        m.LoadRationale =
            "MEDIUM by working memory, and specifically by INTERFERENCE rather than by volume. The " +
            "pilot is not given two items to remember; they are given one item that REPLACES an item " +
            "they have already committed to, at a moment when the original plan is about to be " +
            "executed. Replacing an active plan is measurably more costly than forming one, and the " +
            "characteristic error is not forgetting the new clearance but reverting to the old one — " +
            "which is exactly what the confirm-heading probe at 232 s is there to catch. Manual demand " +
            "stays at 2, matched to L1V2 and H1V2: a left turn after take-off is not harder to fly " +
            "than a straight climb.";
        m.EegRelevance =
            "A clean event-related contrast at the clearance change against the mission's own quiet " +
            "taxi baseline, plus a sustained maintenance period between the change and its execution " +
            "during which the amended clearance must be held.";
        m.ExpectedErrors = "Flying the ORIGINAL runway heading after take-off; climbing to 600 m " +
                           "instead of 750 m; answering the probe with the briefed rather than the " +
                           "amended heading.";
        m.SuccessCriteria = "Amended clearance acknowledged, hold short respected, established on 340 " +
                            "at 750 m.";
        m.FailureConditions = "Runway incursion; crash; established on the original clearance at the " +
                              "end of the trial.";
        m.AviationBasis = "Amended departure clearances at the holding point are routine and are a " +
                          "recognised source of read-back/hear-back error (ICAO Doc 9683 human-factors " +
                          "training manual, communication chapter).";
        m.Approximations = "No speech recognition: the read-back is acknowledged, not verified, so a " +
                           "pilot who mis-read-back but flew correctly is scored as correct.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage,
                                    EventMarkers.TargetChange, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H1()
    {
        var m = MissionLibrary.TaxiBase("H1V2", "Intersection departure with a performance decision", WorkloadClass.High, 2);
        m.Objective = "Taxi for runway 01, evaluate an offered intersection departure against the " +
                      "runway actually available, decide, and fly the departure.";
        m.Brief = "You are on stand 1, engine running. Taxi via taxiway A for runway 01. The full " +
                  "runway is 600 m. ATC may offer you an intersection departure to save time — an " +
                  "intersection departure means less runway ahead of you. Departure is runway heading, " +
                  "climb 600 m.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA FOR RUNWAY 01. TRAFFIC IS 4 MILES FINAL."));
        // The offer is a genuine trade with two different failure modes, and a clock.
        m.Events.Add(MissionDefinition.Msg(T0 + 4f, "ARE YOU ABLE INTERSECTION BRAVO? 380 M AVAILABLE. " +
                     "TRAFFIC 3 MILES FINAL — IF UNABLE, EXPECT A 4 MINUTE DELAY."));
        m.Events.Add(MissionDefinition.Decide(T0 + 10f, "ACCEPT INTERSECTION BRAVO OR REQUEST FULL LENGTH? — decide", 10f, 6f));
        m.Events.Add(MissionDefinition.Traffic(T0 + 26f, TrafficBehaviour.ConvergingApproach,
                     "TRAFFIC 3 MILES FINAL RUNWAY 01", false, 6f, 5f));
        m.Events.Add(MissionDefinition.Readback(T0 + 44f, "IF ACCEPTING: LINE UP BRAVO, NO DELAY. " +
                     "IF NOT: HOLD SHORT. ACKNOWLEDGE", 8f, 6f));
        m.Events.Add(MissionDefinition.TakeoffClearance(126f, "no delay, traffic 2 miles final.", 8f));
        m.Events.Add(MissionDefinition.Probe(190f, "REPORT PASSING 300 M — respond", 5f, 14f));
        m.Events.Add(MissionDefinition.Atc(214f, ScenarioEventType.HeadingChange, 25f, "right heading 025 for traffic"));
        m.TargetAltitudeM = 600f;
        m.Mechanism = "forward reasoning about a physical margin under a clock";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 4, DecisionComplexity = 4, WorkingMemory = 3,
            AttentionSwitching = 3, SituationAwareness = 4, Perception = 3, ManualControl = 2,
            ProceduralLoad = 3, Uncertainty = 3, Communication = 3, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 82, Physical = 35, Temporal = 80, Performance = 62, Effort = 80, Frustration = 60 };
        m.LoadRationale =
            "HIGH by a mechanism variant 1 does not use. H1 is CONCURRENCY — several things at once. " +
            "This is a single question that has to be ANSWERED CORRECTLY, under a clock, where both " +
            "answers cost something and the wrong one is unrecoverable: accept and you commit to a " +
            "take-off from 380 m of runway; decline and you take a delay with traffic on final. The " +
            "pilot has to reason forward from a number they were given in the brief to a physical " +
            "margin, while a converging aeroplane makes the clock visible rather than merely stated. " +
            "The subsequent departure is deliberately ordinary — the load is front-loaded into the " +
            "decision so the EEG contrast has a clean onset. Manual demand 2, matched to the row.";
        m.EegRelevance =
            "The strongest single DECISION_PROMPT -> DECISION_MADE epoch in the take-off row, with a " +
            "decision that is genuinely effortful rather than a button press. Reaction time here is a " +
            "primary behavioural measure and should correlate with the EEG index if the manipulation " +
            "works. The visible converging traffic gives an independent, non-verbal time cue whose " +
            "onset is separately marked.";
        m.ExpectedErrors = "Accepting the intersection without evaluating it (compliance bias); " +
                           "deciding by default (letting the window expire); accepting and then " +
                           "rotating late; forgetting the traffic-avoidance turn at 214 s.";
        m.SuccessCriteria = "An explicit decision made inside the window, hold short respected until " +
                            "cleared, airborne and established at 600 m on the assigned heading.";
        m.FailureConditions = "Runway incursion; crash; no decision made; departing without clearance.";
        m.AviationBasis = "Intersection departures and the associated 'runway remaining' judgement are " +
                          "treated in FAA-H-8083-3C ch.2, and accepting an intersection departure " +
                          "without computing the remaining distance is a documented contributor to " +
                          "runway-overrun events (FAA InFO 07004).";
        m.Approximations = "The aeroplane's take-off performance is the flight model's, not a " +
                           "certified performance chart: 380 m is genuinely marginal in this model but " +
                           "the pilot cannot consult a real TODA/TORA table, only the figure in the " +
                           "brief. The intersection is represented by the existing link taxiway.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.DecisionPrompt,
                                    EventMarkers.TrafficOnset, EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 2 — CLIMB / DEPARTURE
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L2()
    {
        var m = MissionLibrary.ClimbBase("L2V2", "Assigned climb with one amendment", WorkloadClass.Low, 2);
        m.Objective = "Fly the departure climb and accept one routine altitude amendment.";
        m.Brief = "Departure leg, 400 m, runway heading. ATC will clear you to climb. Fly the " +
                  "departure accurately. Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Atc(T0 + 4f, ScenarioEventType.AltitudeChange, 700f, "climb and maintain 700 m"));
        m.Events.Add(MissionDefinition.Atc(196f, ScenarioEventType.AltitudeChange, 800f, "climb and maintain 800 m"));
        m.TargetAltitudeM = 400f;
        m.Mechanism = "reference - steady-state tracking with one target change";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 0, DecisionComplexity = 0, WorkingMemory = 1,
            AttentionSwitching = 0, SituationAwareness = 1, Perception = 1, ManualControl = 2,
            ProceduralLoad = 0, Uncertainty = 0, Communication = 0, ErrorConsequence = 0 };
        m.Expected = new ExpectedTlx { Mental = 28, Physical = 26, Temporal = 20, Performance = 24, Effort = 30, Frustration = 14 };
        m.LoadRationale =
            "The climb row's reference. Two target changes across 300 s is well inside what a single " +
            "channel absorbs without competition: each arrives alone, with no other task running, and " +
            "the pilot has minutes to settle on it. It is included rather than a pure level-off so " +
            "that ALTITUDE CAPTURE — the manual activity that MEDIUM and HIGH in this row also " +
            "perform — is present in the reference, keeping manual demand matched at 2 across the row.";
        m.EegRelevance =
            "Phase-matched baseline for M2V2 and H2V2, and one of the two cleanest low-artifact " +
            "airborne segments in the bank.";
        m.ExpectedErrors = "Overshooting the new altitude; drifting off heading during the level-off.";
        m.SuccessCriteria = "Both assigned altitudes captured and held within +/- 80 m, heading within +/- 14°.";
        m.FailureConditions = "Crash; loss of control.";
        m.AviationBasis = "Routine departure vectoring; FAA-H-8083-3C ch.3 (basic flight manoeuvres).";
        m.Approximations = "No SID; ATC is a scripted sequence with no dialogue.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TargetChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M2()
    {
        var m = MissionLibrary.ClimbBase("M2V2", "Carburettor ice on the climb", WorkloadClass.Medium, 2);
        m.Objective = "Fly the assigned climb, notice a gradual power loss, diagnose it and cure it.";
        m.Brief = "Departure leg, 400 m, runway heading. ATC will clear you to climb. Fly the " +
                  "departure. Cool, damp, hazy air.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Atc(T0 + 4f, ScenarioEventType.AltitudeChange, 800f, "climb and maintain 800 m"));
        // GRADUAL. TRIGGER_ARMED and CUE_ONSET are far apart here on purpose — this is
        // the bank's clearest demonstration that they are different instants.
        m.Events.Add(MissionDefinition.Fail(104f, FailureKind.CarbIce, 0.75f, "GRADUAL POWER LOSS", 14f));
        m.Events.Add(MissionDefinition.List(196f, ChecklistLibrary.EngineRough, "ROUGH RUNNING / PARTIAL POWER"));
        m.Events.Add(MissionDefinition.Probe(254f, "REPORT LEVEL — respond", 5f, 10f));
        m.Visibility01 = 0.25f;
        m.AmbientTurbulence = 0.05f;
        m.TargetAltitudeM = 400f;
        m.Mechanism = "diagnosis of a gradual, ambiguous cue";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 2, WorkingMemory = 1,
            AttentionSwitching = 3, SituationAwareness = 2, Perception = 2, ManualControl = 2,
            ProceduralLoad = 2, Uncertainty = 4, Communication = 1, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 62, Physical = 32, Temporal = 40, Performance = 52, Effort = 62, Frustration = 48 };
        m.LoadRationale =
            "MEDIUM by UNCERTAINTY, which is the opposite pole from variant 1's mechanism. M2 is a " +
            "cabin door: instantaneous, unmistakable, and startling, with almost nothing to work out. " +
            "Carburettor ice is the reverse — nothing happens suddenly, there is no annunciation, and " +
            "the only evidence is a slow decay in manifold pressure and climb rate that could equally " +
            "be a mis-set throttle, a heavier aeroplane, or nothing at all. The demand is in NOTICING " +
            "an absence and then attributing it, which is why Uncertainty is the only 4 in the profile " +
            "while Temporal Demand stays at 2. Manual demand 2, matched to the row.";
        m.EegRelevance =
            "The bank's clearest case for epoching on CUE_ONSET rather than TRIGGER_ARMED. The " +
            "abnormality is armed at ~104 s but is not perceptible for tens of seconds, so a naive " +
            "analysis time-locked to the injection would average across a window in which nothing had " +
            "yet happened to the pilot. The interval between the two markers is itself a measure of " +
            "how long detection took.";
        m.ExpectedErrors = "Not noticing the decay at all; attributing it to the throttle and pushing " +
                           "power up instead of applying carburettor heat; applying carb heat and " +
                           "removing it too early; losing the altitude assignment while diagnosing.";
        m.SuccessCriteria = "Power loss detected, carburettor heat applied, climb re-established, " +
                            "assigned altitude held.";
        m.FailureConditions = "Crash; complete power loss through untreated icing.";
        m.AviationBasis = "FAA-H-8083-3C ch.7 and AC 20-113: carburettor icing is most likely in " +
                          "humid air between about -7 and 21 °C, develops gradually, and presents as " +
                          "an unexplained loss of power with rough running.";
        m.Approximations = "Icing severity is a scripted ramp rather than a function of modelled " +
                           "humidity and carburettor temperature, and carburettor heat clears it at a " +
                           "fixed rate. There is no carburettor air-temperature gauge.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H2()
    {
        var m = MissionLibrary.ClimbBase("H2V2", "Partial power loss after take-off — the turn-back decision", WorkloadClass.High, 2);
        m.Objective = "Fly the departure, handle a partial power loss at low altitude, and decide " +
                      "between continuing ahead and turning back.";
        m.Brief = "Departure leg, 400 m, runway heading. ATC will clear you to climb. The aerodrome is " +
                  "behind you. Terrain ahead is flat and open. Fly the departure.";
        m.Events.Add(MissionDefinition.Msg(1f, "DEPARTURE — maintain 400 m, heading 000°."));
        m.Events.Add(MissionDefinition.Atc(T0 + 4f, ScenarioEventType.AltitudeChange, 900f, "climb and maintain 900 m"));
        // Partial, not total: total power loss removes the decision, and the decision is
        // the whole mission. Severity 0.55 leaves the aeroplane flying but not climbing.
        m.Events.Add(MissionDefinition.Fail(98f, FailureKind.EngineRoughness, 0.55f, "PARTIAL POWER LOSS", 12f));
        m.Events.Add(MissionDefinition.List(126f, ChecklistLibrary.EngineRough, "PARTIAL POWER LOSS DRILL"));
        m.Events.Add(MissionDefinition.Decide(158f, "CONTINUE AHEAD OR TURN BACK TO THE FIELD? — decide", 10f, 10f));
        m.Events.Add(MissionDefinition.Msg(176f, "NO OTHER TRAFFIC. FIELD IS 4 KM BEHIND YOU. SURFACE WIND CALM."));
        m.Events.Add(MissionDefinition.Probe(244f, "STATE YOUR INTENTIONS — respond", 6f, 12f));
        m.TargetAltitudeM = 400f;
        m.Mechanism = "irreversible commitment against a shrinking margin";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 4, DecisionComplexity = 4, WorkingMemory = 2,
            AttentionSwitching = 3, SituationAwareness = 4, Perception = 3, ManualControl = 3,
            ProceduralLoad = 3, Uncertainty = 3, Communication = 2, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 84, Physical = 45, Temporal = 82, Performance = 66, Effort = 84, Frustration = 66 };
        m.LoadRationale =
            "HIGH by a mechanism the row's variant 1 does not use. H2 is resource budgeting over " +
            "minutes; this is a commitment made in seconds whose margin is SHRINKING while it is being " +
            "made. Partial rather than total power is the point: with the engine dead there is no " +
            "decision worth measuring, whereas with partial power the aeroplane will fly but not " +
            "climb, both options remain nominally available, and the option set degrades continuously. " +
            "This is the classic turn-back trade, and the correct answer for a low-time pilot at this " +
            "height is usually to continue ahead — so a participant who turns back has made a " +
            "recognisable, analysable error rather than merely a different choice. Manual demand is 3, " +
            "one above the row's other two; that is the maximum spread the design permits and it is " +
            "declared rather than hidden, because a degraded aeroplane cannot be flown with the same " +
            "hands as a healthy one. Control-activity covariates are logged for exactly this reason.";
        m.EegRelevance =
            "A sharp CUE_ONSET at the power loss followed by a sustained high-load segment through the " +
            "drill and the decision. The DECISION_PROMPT epoch is the primary contrast; the interval " +
            "from CUE_ONSET to PILOT_FIRST_RESPONSE indexes startle recovery.";
        m.ExpectedErrors = "Turning back (the documented fatal error); leaving the throttle where it " +
                           "is; stalling the turn; running the drill while allowing the speed to decay.";
        m.SuccessCriteria = "Power loss handled, an explicit decision made in window, aircraft under " +
                            "control at the end of the trial.";
        m.FailureConditions = "Crash; stall/spin; no decision made.";
        m.AviationBasis = "FAA-H-8083-3C ch.18 and the AOPA Air Safety Institute's work on the " +
                          "'impossible turn': the turn-back after a power loss on departure is among " +
                          "the most consistently fatal manoeuvres in light aviation, and the accident " +
                          "record turns on altitude available rather than pilot skill.";
        m.Approximations = "Partial power is a fixed multiplier on available thrust, not a modelled " +
                           "cylinder or induction fault, and it neither worsens nor recovers during " +
                           "the trial. The aerodrome behind the aircraft is reachable in the model but " +
                           "the trial does not require a landing.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 3 — CRUISE / EN-ROUTE
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L3()
    {
        var m = MissionLibrary.CruiseBase("L3V2", "Level cruise with one heading change", WorkloadClass.Low, 2);
        m.Objective = "Hold 700 m and fly one assigned heading change.";
        m.Brief = "Straight and level at 700 m, heading 000°. Hold altitude and heading accurately. " +
                  "ATC may give you a turn. Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "MAINTAIN 700 M, HEADING 000°."));
        m.Events.Add(MissionDefinition.Atc(T0 + 20f, ScenarioEventType.HeadingChange, 20f, "right heading 020"));
        m.TargetHeadingDeg = 0f;
        m.Mechanism = "reference - steady-state tracking with one turn";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 0, DecisionComplexity = 0, WorkingMemory = 1,
            AttentionSwitching = 0, SituationAwareness = 1, Perception = 1, ManualControl = 2,
            ProceduralLoad = 0, Uncertainty = 0, Communication = 0, ErrorConsequence = 0 };
        m.Expected = new ExpectedTlx { Mental = 26, Physical = 24, Temporal = 18, Performance = 24, Effort = 28, Frustration = 13 };
        m.LoadRationale =
            "The cruise row's reference, with a single 20° turn so that the reference contains the " +
            "same manual activity — a coordinated turn and a roll-out onto a target — that MEDIUM and " +
            "HIGH in this row require. A pure wings-level hold would have left manual demand lower in " +
            "LOW than in its own row's other cells, which is precisely the confound the row matching " +
            "exists to prevent.";
        m.EegRelevance =
            "Phase-matched baseline for M3V2 and H3V2. Cruise is the lowest-artifact condition in the " +
            "bank and the natural reference for the sustained-load analyses.";
        m.ExpectedErrors = "Losing altitude in the turn; overshooting the assigned heading.";
        m.SuccessCriteria = "Altitude within +/- 70 m and heading within +/- 12° for the majority of the trial.";
        m.FailureConditions = "Crash; loss of control.";
        m.AviationBasis = "Straight-and-level and medium turns, FAA-H-8083-3C ch.3.";
        m.Approximations = "No other traffic and no weather; ATC is a scripted sequence.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TargetChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M3()
    {
        var m = MissionLibrary.CruiseBase("M3V2", "Diversion to an alternate", WorkloadClass.Medium, 2);
        m.Objective = "Cruise en route, then re-plan to an alternate when the destination closes.";
        m.Brief = "En route at 700 m, heading 000° for your destination. Hold altitude and heading. " +
                  "Clear day.";
        m.Events.Add(MissionDefinition.Msg(1f, "MAINTAIN 700 M, HEADING 000° — 18 MILES TO RUN."));
        m.Events.Add(MissionDefinition.Msg(T0 + 6f, "DESTINATION IS NOW CLOSED — RUNWAY OBSTRUCTED. " +
                     "ALTERNATE IS 40 MILES NORTH-WEST, OR 25 MILES EAST WITH LOWER CLOUD."));
        m.Events.Add(MissionDefinition.Decide(T0 + 14f, "SELECT AN ALTERNATE — decide", 12f, 8f));
        m.Events.Add(MissionDefinition.Readback(T0 + 40f, "CLEARED TO THE ALTERNATE: heading 315, " +
                     "climb 900 m. READ BACK", 9f, 6f));
        m.Events.Add(MissionDefinition.Atc(T0 + 52f, ScenarioEventType.HeadingChange, 315f, "left heading 315"));
        m.Events.Add(MissionDefinition.Atc(T0 + 58f, ScenarioEventType.AltitudeChange, 900f, "climb and maintain 900 m"));
        m.Events.Add(MissionDefinition.Probe(238f, "CONFIRM FUEL ENDURANCE — respond", 6f, 12f));
        m.TargetHeadingDeg = 0f;
        m.Mechanism = "re-planning against competing constraints";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 2, DecisionComplexity = 4, WorkingMemory = 3,
            AttentionSwitching = 2, SituationAwareness = 2, Perception = 1, ManualControl = 2,
            ProceduralLoad = 1, Uncertainty = 1, Communication = 3, ErrorConsequence = 1 };
        m.Expected = new ExpectedTlx { Mental = 66, Physical = 28, Temporal = 44, Performance = 50, Effort = 64, Frustration = 44 };
        m.LoadRationale =
            "MEDIUM by RE-PLANNING rather than by retention. Variant 1 of this cell loads working " +
            "memory with a turnover of clearances; here there is only one clearance, but before it " +
            "arrives the pilot has to construct and compare two options that trade against each other " +
            "on different dimensions — distance against weather — with no dominant answer. Decision " +
            "complexity is therefore 4 while working memory stays at 3 and temporal demand at 2: it is " +
            "a hard question asked calmly, which is a different load profile from an easy question " +
            "asked repeatedly. Manual demand 2, matched to the row.";
        m.EegRelevance =
            "A long DECISION_PROMPT window (12 s) makes this one of the few epochs in the bank where " +
            "a slow, deliberative decision process can be observed rather than a reflexive response. " +
            "Contrast the reaction-time distribution here against H1V2's, which is the same marker " +
            "under a clock.";
        m.ExpectedErrors = "Choosing by proximity without considering the weather; not deciding inside " +
                           "the window; continuing toward the closed destination; losing altitude " +
                           "during the re-plan.";
        m.SuccessCriteria = "An explicit alternate chosen in window, clearance acknowledged, " +
                            "established on 315 at 900 m.";
        m.FailureConditions = "Crash; no decision made.";
        m.AviationBasis = "In-flight diversion decision-making, FAA-H-8083-25 ch.17 (aeronautical " +
                          "decision-making); plan-continuation bias is the documented failure mode.";
        m.Approximations = "The two alternates are described in the radio call only — neither exists " +
                           "in the world, and the trial ends before either could be reached. What is " +
                           "measured is the decision and the subsequent tracking, not an arrival.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.DecisionPrompt,
                                    EventMarkers.TargetChange, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H3()
    {
        var m = MissionLibrary.CruiseBase("H3V2", "Fuel imbalance and tank management", WorkloadClass.High, 2);
        m.Objective = "Cruise en route while managing an asymmetric fuel state against a deadline.";
        m.Brief = "En route at 700 m, heading 000°. Fuel is in two tanks, left and right, selected by " +
                  "the fuel selector on the console. Hold altitude and heading. Clear day.";
        m.Events.Add(MissionDefinition.Msg(1f, "MAINTAIN 700 M, HEADING 000°."));
        // Starvation on the SELECTED side: curable, but only by acting, and the cure has
        // its own consequence because the other tank then drains.
        m.Events.Add(MissionDefinition.Fail(92f, FailureKind.FuelStarvation, 1f, "FUEL PRESSURE", 12f));
        m.Events.Add(MissionDefinition.List(132f, ChecklistLibrary.EngineRough, "ENGINE ROUGHNESS / FUEL"));
        m.Events.Add(MissionDefinition.Readback(168f, "CLEARANCE: cross the boundary AT OR ABOVE 850 M " +
                     "WITHIN 6 MINUTES. READ BACK", 9f, 6f));
        m.Events.Add(MissionDefinition.Atc(178f, ScenarioEventType.AltitudeChange, 850f, "climb and maintain 850 m"));
        m.Events.Add(MissionDefinition.Decide(226f, "CONTINUE OR DIVERT ON REMAINING FUEL? — decide", 9f, 10f));
        m.Events.Add(MissionDefinition.Probe(268f, "REPORT FUEL REMAINING — respond", 5f, 10f));
        m.StartFuelL = 92f;
        m.Mechanism = "resource management with a self-inflicted cure";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 3, DecisionComplexity = 4, WorkingMemory = 4,
            AttentionSwitching = 4, SituationAwareness = 3, Perception = 2, ManualControl = 2,
            ProceduralLoad = 3, Uncertainty = 3, Communication = 2, ErrorConsequence = 3 };
        m.Expected = new ExpectedTlx { Mental = 82, Physical = 32, Temporal = 66, Performance = 62, Effort = 80, Frustration = 62 };
        m.LoadRationale =
            "HIGH by a mechanism distinct from variant 1's. H3 gives the pilot instruments that lie " +
            "consistently; this gives them instruments that tell the truth about a state they must " +
            "actively manage. The cure — switching tanks — restores power immediately and then starts " +
            "draining the only remaining supply, so the action that solves the emergency creates the " +
            "next constraint, and the pilot must hold that forward consequence while also holding a " +
            "crossing restriction with a time limit. Working memory and attention switching are both " +
            "4 because the fuel state, the restriction and the clock are three independent things that " +
            "must all stay live. Manual demand 2, matched to the row.";
        m.EegRelevance =
            "Like H2, a sustained elevation rather than a single transient, but with a discrete " +
            "recovery point (the tank change) that splits the trial into a pre-cure and post-cure " +
            "segment on the same clock. Those two segments are matched for visuals and manual demand " +
            "and differ in what the pilot has to keep in mind, which makes them a useful within-trial " +
            "contrast independent of the between-mission one.";
        m.ExpectedErrors = "Not identifying the selector as the cure; switching to the empty tank; " +
                           "switching and then forgetting the remaining tank is finite; dropping the " +
                           "crossing restriction while troubleshooting.";
        m.SuccessCriteria = "Power restored by tank selection, restriction honoured, an explicit " +
                            "continue/divert decision made in window.";
        m.FailureConditions = "Crash; fuel exhaustion; no decision made.";
        m.AviationBasis = "FAA-H-8083-3C ch.18 and the NTSB's long-standing finding that fuel " +
                          "starvation — fuel aboard but not reaching the engine — remains a leading " +
                          "cause of light-aircraft power loss, and is usually cured by the selector.";
        m.Approximations = "Two lumped tanks with a linear burn split and no cross-feed, unporting or " +
                           "attitude dependence. Fuel quantity is shown as a gauge value, not a " +
                           "totaliser, and burn is scaled so the constraint is live inside 300 s.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // ROW 4 — APPROACH AND LANDING
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition L4()
    {
        var m = MissionLibrary.ApproachBase("L4V2", "Normal landing with a configuration call", WorkloadClass.Low, 2);
        m.Objective = "Fly a straight-in approach and land on runway 01.";
        m.Brief = "You are 12 km on final for runway 01 at 500 m. Descend, configure and land. " +
                  "Clear day, light air, no other traffic.";
        m.Events.Add(MissionDefinition.Msg(1f, "CLEARED TO LAND RUNWAY 01. WIND CALM."));
        m.Events.Add(MissionDefinition.Cfg(T0 + 30f, "FLAPS 10 — configure for the approach"));
        m.Events.Add(MissionDefinition.List(T0 + 52f, ChecklistLibrary.BeforeLanding, "BEFORE LANDING"));
        m.Mechanism = "reference - rehearsed approach with a routine configuration change";
        m.Profile = new WorkloadProfile {
            MentalDemand = 1, TemporalDemand = 1, DecisionComplexity = 1, WorkingMemory = 1,
            AttentionSwitching = 1, SituationAwareness = 2, Perception = 2, ManualControl = 3,
            ProceduralLoad = 2, Uncertainty = 0, Communication = 1, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 34, Physical = 40, Temporal = 26, Performance = 34, Effort = 40, Frustration = 20 };
        m.LoadRationale =
            "The approach row's reference. Manual demand is 3 rather than 2 because an approach IS " +
            "harder to fly than cruise — and that is exactly why the row is matched INTERNALLY at 3 " +
            "and never compared across rows without the phase term. The configuration call and the " +
            "checklist are present so that the reference contains the same procedural furniture as " +
            "M4V2 and H4V2, differing only in what the pilot has to think about.";
        m.EegRelevance =
            "Phase-matched baseline for M4V2 and H4V2 — the reference against which the approach row's " +
            "class contrasts are read. Note that the approach row has the highest movement artifact in " +
            "the bank; the row-internal comparison is what controls for it.";
        m.ExpectedErrors = "High or fast on final; late configuration; long or firm touchdown.";
        m.SuccessCriteria = "Touchdown on the runway, wings level, sink rate inside the acceptable band.";
        m.FailureConditions = "Crash; runway excursion; not landed inside the trial.";
        m.AviationBasis = "Normal approach and landing, FAA-H-8083-3C ch.8.";
        m.Approximations = "No glideslope guidance and no PAPI: the approach is flown visually.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.ChecklistStart, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition M4()
    {
        var m = MissionLibrary.ApproachBase("M4V2", "Late runway change on final", WorkloadClass.Medium, 2);
        m.Objective = "Fly the approach, absorb a late runway change, and land.";
        m.Brief = "You are 12 km on final for runway 01 at 500 m. Descend, configure and land. " +
                  "Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "CLEARED TO LAND RUNWAY 01. WIND CALM."));
        m.Events.Add(MissionDefinition.Cfg(T0 + 20f, "FLAPS 10 — configure for the approach"));
        // Late enough that the plan is committed, early enough that it is still flyable.
        m.Events.Add(MissionDefinition.Readback(T0 + 62f, "CHANGE OF RUNWAY: SIDESTEP AND LAND RUNWAY 01 " +
                     "RIGHT, DISPLACED THRESHOLD 200 M. READ BACK", 9f, 7f));
        m.Events.Add(MissionDefinition.Msg(T0 + 76f, "TOUCH DOWN BEYOND THE DISPLACED THRESHOLD."));
        m.Events.Add(MissionDefinition.List(T0 + 96f, ChecklistLibrary.BeforeLanding, "BEFORE LANDING"));
        m.Events.Add(MissionDefinition.Probe(T0 + 132f, "CONFIRM LANDING RUNWAY — respond", 6f, 10f));
        m.Mechanism = "re-planning under time pressure late in a committed approach";
        m.Profile = new WorkloadProfile {
            MentalDemand = 2, TemporalDemand = 4, DecisionComplexity = 2, WorkingMemory = 3,
            AttentionSwitching = 2, SituationAwareness = 3, Perception = 2, ManualControl = 3,
            ProceduralLoad = 2, Uncertainty = 1, Communication = 2, ErrorConsequence = 2 };
        m.Expected = new ExpectedTlx { Mental = 66, Physical = 46, Temporal = 74, Performance = 56, Effort = 68, Frustration = 52 };
        m.LoadRationale =
            "MEDIUM by TIME PRESSURE on a committed plan, where variant 1 of this cell is degraded " +
            "perception. Nothing here is hard to see and nothing is ambiguous — the pilot is told " +
            "clearly what to do. The cost is entirely that the instruction arrives after the approach " +
            "is established and must be executed inside a window that is closing at the aircraft's own " +
            "descent rate, with a displaced threshold that changes the aiming point as well as the " +
            "runway. Temporal demand is the only 4. Manual demand 3, matched to the row.";
        m.EegRelevance =
            "An event-related response at a moment of high existing task load, which is the condition " +
            "under which spare-capacity measures are most diagnostic. Compare the probe response time " +
            "here against L4V2's baseline: the same probe under a heavier load is the cleanest " +
            "secondary-task contrast the approach row offers.";
        m.ExpectedErrors = "Landing on the original runway; touching down short of the displaced " +
                           "threshold; destabilising the approach during the sidestep; going around " +
                           "unnecessarily.";
        m.SuccessCriteria = "Change acknowledged, touchdown beyond the displaced threshold on the " +
                            "nominated runway, wings level.";
        m.FailureConditions = "Crash; runway excursion; not landed inside the trial.";
        m.AviationBasis = "Late landing-clearance and runway changes are a recognised destabilising " +
                          "factor on approach; FAA-H-8083-3C ch.8 and the Flight Safety Foundation's " +
                          "stabilised-approach criteria.";
        m.Approximations = "There is one physical runway. The 'right' runway and its displaced " +
                           "threshold are represented by a lateral offset and a shifted aiming point " +
                           "on the same strip, so the geometry of the sidestep is real but the second " +
                           "runway is not separately modelled.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.AtcMessage,
                                    EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    static MissionDefinition H4()
    {
        var m = MissionLibrary.ApproachBase("H4V2", "Flap failure on final — the no-flap approach", WorkloadClass.High, 2);
        m.Objective = "Fly the approach, discover the flaps will not extend, re-compute the approach " +
                      "and land without them.";
        m.Brief = "You are 12 km on final for runway 01 at 500 m. Descend, configure and land. " +
                  "Runway 01 is 600 m long. Clear day, light air.";
        m.Events.Add(MissionDefinition.Msg(1f, "CLEARED TO LAND RUNWAY 01. WIND CALM. RUNWAY 600 M."));
        m.Events.Add(MissionDefinition.Cfg(T0 + 16f, "FLAPS 10 — configure for the approach"));
        // The motor dies where it stands, so the flaps neither extend NOR retract. The
        // pilot discovers it by selecting and getting nothing.
        m.Events.Add(MissionDefinition.Fail(T0 + 22f, FailureKind.FlapMotorFailure, 1f, "FLAPS INOPERATIVE", 8f));
        m.Events.Add(MissionDefinition.List(T0 + 48f, ChecklistLibrary.FlapFailure, "FLAP FAILURE — NO-FLAP APPROACH"));
        m.Events.Add(MissionDefinition.Msg(T0 + 66f, "NO-FLAP APPROACH SPEED IS 15 KM/H HIGHER. " +
                     "LANDING DISTANCE INCREASES BY ABOUT HALF."));
        m.Events.Add(MissionDefinition.Decide(T0 + 88f, "LAND ON 600 M WITHOUT FLAPS, OR GO AROUND? — decide", 10f, 8f));
        m.Events.Add(MissionDefinition.Probe(T0 + 142f, "CONFIRM APPROACH SPEED — respond", 6f, 10f));
        m.Mechanism = "re-computing a procedure whose parameters have changed";
        m.Profile = new WorkloadProfile {
            MentalDemand = 4, TemporalDemand = 3, DecisionComplexity = 3, WorkingMemory = 3,
            AttentionSwitching = 3, SituationAwareness = 3, Perception = 2, ManualControl = 4,
            ProceduralLoad = 4, Uncertainty = 2, Communication = 2, ErrorConsequence = 4 };
        m.Expected = new ExpectedTlx { Mental = 78, Physical = 58, Temporal = 68, Performance = 68, Effort = 82, Frustration = 64 };
        m.LoadRationale =
            "HIGH by RE-COMPUTATION, where variant 1 of this cell is irreversible commitment under a " +
            "clock. The engine failure in H4 removes options; this one keeps every option and changes " +
            "all their numbers. A rehearsed procedure the pilot already knows — the approach — must be " +
            "executed with two of its parameters altered at once (a higher speed and a longer roll) " +
            "onto a runway short enough that the alteration matters. Procedural load is 4 because the " +
            "drill is real and the recomputed approach must then actually be flown. Manual demand is " +
            "4, one above the row's other two, which is the permitted maximum spread: a flapless " +
            "approach is genuinely harder to fly and that is declared, not concealed. The " +
            "control-activity covariates exist to let an analysis account for it.";
        m.EegRelevance =
            "A discovery event (selecting flaps and getting nothing) that is neither loud nor sudden, " +
            "so CUE_ONSET is driven by the pilot's own action rather than by an annunciation — the " +
            "only mission in the bank where the pilot effectively creates their own cue onset. " +
            "Reaction time from selection to first response is therefore a purer detection measure " +
            "than in the annunciated failures.";
        m.ExpectedErrors = "Flying the normal approach speed and floating; landing long and running " +
                           "off the end; continuing to cycle the flap selector; not deciding.";
        m.SuccessCriteria = "Failure recognised, drill run, an explicit decision made in window, " +
                            "touchdown on the runway with the aircraft stopped on it.";
        m.FailureConditions = "Crash; runway excursion; no decision made.";
        m.AviationBasis = "FAA-H-8083-3C ch.18 (flap malfunction) and ch.8: a no-flap approach is " +
                          "flown faster and flatter and lengthens the landing roll substantially.";
        m.Approximations = "The flap motor fails to a fixed position and cannot be recovered; there is " +
                           "no manual extension and no split-flap asymmetry in this mission. The " +
                           "quoted speed and distance penalties are given to the pilot in a radio call " +
                           "rather than being looked up in a POH.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.TriggerArmed, EventMarkers.CueOnset,
                                    EventMarkers.ChecklistStart, EventMarkers.DecisionPrompt, EventMarkers.MissionEnd };
        return m;
    }
}
