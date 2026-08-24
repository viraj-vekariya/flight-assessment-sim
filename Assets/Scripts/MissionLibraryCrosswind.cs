// MissionLibraryCrosswind — the PSYCHOMOTOR-INTEGRATED axis.
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHY THESE ARE NOT ON THE LOW / MEDIUM / HIGH SCALE
// ═══════════════════════════════════════════════════════════════════════════════
// The main experiment's Low/Medium/High axis works because manual and psychomotor
// demand are MATCHED WITHIN EACH PHASE ROW. That matching is the entire licence for
// reading a difference between classes as cognitive rather than muscular: if HIGH also
// meant "more hand movement", an EEG difference could be motor activity and movement
// artifact, and no analysis afterwards could separate them.
//
// A crosswind raises manual demand by construction. That is what a crosswind IS. So
// putting a crosswind mission on the cognitive scale would break the one property that
// makes the scale interpretable — and it would break it invisibly, because the mission
// would still look like a perfectly reasonable "hard" condition.
//
// These six missions therefore sit on a SECOND AXIS with its own analysis, and the
// conclusion drawn from them is stated as "increased integrated psychomotor/cognitive
// demand", never as "crosswind increased cognitive workload".
//
// ═══════════════════════════════════════════════════════════════════════════════
// WHAT MAKES THE AXIS DEFENSIBLE ANYWAY
// ═══════════════════════════════════════════════════════════════════════════════
// 1. CONTROL ACTIVITY IS MEASURED, NOT ASSUMED. Control-input rate, cumulative control
//    activity and control-input variability are logged for every trial (see
//    ControlActivity.cs), so "more EEG activity" can be regressed against "more hand
//    movement" instead of being confused with it.
//
// 2. THE COGNITIVE COMPONENT IS ISOLATED AS A DISCRETE EVENT. None of these missions is
//    scored as "fly a crosswind landing". Each carries a STATED crosswind limit and a
//    forced continue-or-abandon decision against it, which has an onset, a response and
//    a reaction time and can be epoched on its own — cleanly separated from the
//    continuous tracking segment around it.
//
// 3. THE THREE LEVELS VARY ONE PHYSICAL QUANTITY. Light, moderate and near-limit
//    crosswind, from the same direction, with everything else identical. Where the
//    cognitive axis deliberately uses a different mechanism per variant, this axis
//    deliberately uses the SAME mechanism at three magnitudes — which is what makes it
//    a dose-response series rather than a set of scenarios.
//
// 4. THE NOVICE CAVEAT IS DECLARED. For a non-pilot, crosswind handling is substantially
//    SKILL ACQUISITION rather than workload, and skill acquisition changes across a
//    session in a way workload does not. That is why these are analysed separately and
//    why trial order within the block is counterbalanced: a learning trend that is
//    confounded with crosswind level would be indistinguishable from a dose response.
//
// ═══════════════════════════════════════════════════════════════════════════════
// THE NUMBERS
// ═══════════════════════════════════════════════════════════════════════════════
// The Cessna 172's demonstrated crosswind component is 15 kt (7.7 m/s). It is a
// DEMONSTRATED value, not a certified limit — but it is the number pilots are taught to
// treat as one, so it is the number the briefings quote and the decisions are made
// against. The three levels are set at roughly 25%, 60% and 95% of it, so that the
// top level is genuinely at the edge of what the aeroplane and the pilot can do and
// the decision to abandon is a live one rather than a formality.

using System.Collections.Generic;
using UnityEngine;

public static class MissionLibraryCrosswind
{
    /// <summary>The C172's demonstrated crosswind component, m/s (15 kt).</summary>
    public const float DemonstratedCrosswindMs = 7.7f;

    /// <summary>Crosswind components for the three levels, m/s: ~25%, ~60%, ~95% of the
    /// demonstrated value. Positive = from the RIGHT, held constant across the set so
    /// that a participant is never asked to reverse a correction they have just learned
    /// — direction is a nuisance variable here, not a manipulation.</summary>
    public static readonly float[] Levels = { 2.0f, 4.6f, 7.3f };
    static readonly string[] LevelNames = { "light", "moderate", "near-limit" };
    /// <summary>Gust added at the top level only: a gusty crosswind cannot be corrected
    /// once and left, which is the qualitative change that makes the near-limit case
    /// different in kind and not only in size.</summary>
    static readonly float[] Gusts = { 0f, 0f, 2.4f };

    public static List<MissionDefinition> All()
    {
        var o = new List<MissionDefinition>();
        for (int i = 0; i < Levels.Length; i++) { o.Add(Takeoff(i)); o.Add(Landing(i)); }
        return o;
    }

    static string Kt(float ms) => Mathf.RoundToInt(ms * 1.94384f).ToString();

    static MissionDefinition Common(MissionDefinition m, int level)
    {
        m.Axis = LoadAxis.PsychomotorIntegrated;
        m.Variant = level + 1;
        m.Mechanism = "psychomotor-integrated demand at a graded crosswind, with an isolated limit decision";
        // Headwind held at a constant 3 m/s so that only the CROSS component varies:
        // otherwise the total wind would change with the level and the take-off roll
        // and approach angle would change with it, adding a second manipulation.
        m.SetWind(Levels[level], 3f, Gusts[level], Aerodrome.RunwayHeadingDeg);
        m.AmbientTurbulence = 0.04f + 0.03f * level;
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // CROSSWIND TAKE-OFF
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition Takeoff(int level)
    {
        var m = MissionLibrary.TaxiBase("XT" + (level + 1),
                    "Crosswind take-off — " + LevelNames[level], WorkloadClass.Medium, level + 1);
        Common(m, level);
        string wind = m.WindReport;
        float xw = m.CrosswindMs;

        m.Objective = "Taxi to runway 01, assess the crosswind against the aircraft's demonstrated " +
                      "limit, decide whether to depart, and if so fly an accurate crosswind take-off.";
        m.Brief = "You are on stand 1, engine running. Surface wind is " + wind + " — a crosswind of " +
                  "about " + Kt(xw) + " knots on runway 01. This aircraft's DEMONSTRATED CROSSWIND is " +
                  "15 knots; above that the manufacturer makes no promises and you should not depart. " +
                  "Taxi via taxiway A, hold short, and you will be asked for your decision. If you " +
                  "depart: aileron into the wind on the roll, keep straight with rudder, and climb " +
                  "runway heading to 600 m.";
        m.Events.Add(MissionDefinition.Msg(1f, "TAXI VIA ALPHA, HOLD SHORT RUNWAY 01. WIND " + wind + "."));
        m.Events.Add(MissionDefinition.Msg(66f, "WIND CHECK: " + wind + ". CROSSWIND COMPONENT " + Kt(xw) + " KNOTS."));
        // THE ISOLATED COGNITIVE EVENT. Everything around it is tracking; this is the
        // one part of the mission that is a judgement, and it is the part that is
        // epoched as a cognitive component.
        m.Events.Add(MissionDefinition.Decide(84f, "CROSSWIND " + Kt(xw) + " KT AGAINST A 15 KT LIMIT — " +
                     "DEPART OR HOLD? — decide", 10f, 6f));
        m.Events.Add(MissionDefinition.TakeoffClearance(112f, "wind " + wind + ", climb runway heading to 600 m.", 6f));
        m.Events.Add(MissionDefinition.Probe(212f, "REPORT PASSING 300 M — respond", 5f, 12f));
        m.TargetAltitudeM = 600f;

        int lvl = level;
        m.Profile = new WorkloadProfile {
            MentalDemand = 2 + lvl, TemporalDemand = 2 + (lvl > 1 ? 1 : 0), DecisionComplexity = 2 + (lvl > 0 ? 1 : 0),
            WorkingMemory = 2, AttentionSwitching = 2 + lvl, SituationAwareness = 2 + lvl,
            Perception = 2 + lvl, ManualControl = 2 + lvl, ProceduralLoad = 2,
            Uncertainty = 1 + lvl, Communication = 2, ErrorConsequence = 2 + lvl };
        m.Expected = new ExpectedTlx {
            Mental = 45 + 14 * lvl, Physical = 40 + 18 * lvl, Temporal = 38 + 12 * lvl,
            Performance = 42 + 13 * lvl, Effort = 50 + 16 * lvl, Frustration = 32 + 16 * lvl };
        m.LoadRationale =
            "Level " + (lvl + 1) + " of three on the PSYCHOMOTOR-INTEGRATED axis: a " + Kt(xw) + " kt " +
            "crosswind, about " + Mathf.RoundToInt(100f * xw / DemonstratedCrosswindMs) + "% of the " +
            "demonstrated component. ManualControl rises with the level BY DESIGN — that is what is " +
            "being manipulated — which is exactly why this mission is not on the cognitive axis, where " +
            "manual demand is held constant. What must not be claimed from this series is that " +
            "crosswind raised cognitive workload; what can be claimed is that integrated " +
            "psychomotor/cognitive demand rose, and the logged control-activity covariates are what " +
            "let those be told apart.";
        m.EegRelevance =
            "Two separable segments on one clock. The DECISION_PROMPT epoch at ~84 s happens with the " +
            "aeroplane STATIONARY at the holding point — no control activity, no movement artifact — " +
            "so it is a clean event-related cognitive measure taken under the crosswind condition. " +
            "The take-off roll and climb are the continuous psychomotor segment, analysed against the " +
            "control-activity covariates rather than as cognitive load. Comparing the decision epoch " +
            "across the three levels is the one contrast on this axis that is NOT motor-contaminated.";
        m.ExpectedErrors = "Departing above the demonstrated crosswind (the scored error at level 3); " +
                           "no aileron into wind on the roll; drifting downwind of the centreline; " +
                           "rotating early and being blown off the extended centreline.";
        m.SuccessCriteria = "An explicit decision made in window; if departing, the centreline held " +
                            "within the runway width during the roll and the climb flown on runway " +
                            "heading to 600 m.";
        m.FailureConditions = "Runway excursion; crash; no decision made.";
        m.AviationBasis = "FAA-H-8083-3C ch.5 (crosswind take-off: aileron into the wind, directional " +
                          "control with rudder) and the C172 POH's 15 kt demonstrated crosswind, which " +
                          "is a demonstrated value rather than a certified limitation — a distinction " +
                          "the brief states honestly rather than presenting it as a hard limit.";
        m.Approximations = "The wind is a horizontal field with a power-law surface profile and " +
                           "band-limited gusts; there is no terrain-induced rotor, no mechanical " +
                           "turbulence from buildings, and no wind gradient below 3 m. Weathervaning " +
                           "is modelled as a fin yawing moment plus tyre grip, not as a full " +
                           "landing-gear side-force model.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.DecisionPrompt,
                                    EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // CROSSWIND LANDING
    // ════════════════════════════════════════════════════════════════════════════

    static MissionDefinition Landing(int level)
    {
        var m = MissionLibrary.ApproachBase("XL" + (level + 1),
                    "Crosswind landing — " + LevelNames[level], WorkloadClass.Medium, level + 1);
        Common(m, level);
        string wind = m.WindReport;
        float xw = m.CrosswindMs;

        m.Objective = "Fly a crosswind approach, assess the crosswind against the demonstrated limit " +
                      "on short final, decide to land or go around, and carry out the decision.";
        m.Brief = "You are 12 km on final for runway 01 at 500 m. Surface wind is " + wind + " — a " +
                  "crosswind of about " + Kt(xw) + " knots. This aircraft's DEMONSTRATED CROSSWIND is " +
                  "15 knots. Fly the approach: crab into the wind to hold the centreline, and " +
                  "straighten the aircraft with rudder before touchdown. You will be asked on short " +
                  "final whether you are continuing.";
        m.Events.Add(MissionDefinition.Msg(1f, "CLEARED TO LAND RUNWAY 01. WIND " + wind + "."));
        m.Events.Add(MissionDefinition.Cfg(MissionLibrary.BaselineEndT + 18f, "FLAPS 10 — configure for the approach"));
        m.Events.Add(MissionDefinition.List(MissionLibrary.BaselineEndT + 38f, ChecklistLibrary.BeforeLanding, "BEFORE LANDING"));
        m.Events.Add(MissionDefinition.Msg(MissionLibrary.BaselineEndT + 66f,
                     "WIND CHECK: " + wind + ". CROSSWIND COMPONENT " + Kt(xw) + " KNOTS."));
        // THE ISOLATED COGNITIVE EVENT, on short final and against a stated number.
        m.Events.Add(MissionDefinition.Decide(MissionLibrary.BaselineEndT + 82f,
                     "CROSSWIND " + Kt(xw) + " KT AGAINST A 15 KT LIMIT — CONTINUE OR GO AROUND? — decide", 9f, 6f));
        m.Events.Add(MissionDefinition.Probe(MissionLibrary.BaselineEndT + 128f, "REPORT SHORT FINAL — respond", 5f, 10f));

        int lvl = level;
        m.Profile = new WorkloadProfile {
            MentalDemand = 2 + lvl, TemporalDemand = 3 + (lvl > 1 ? 1 : 0), DecisionComplexity = 2 + (lvl > 0 ? 1 : 0),
            WorkingMemory = 2, AttentionSwitching = 2 + lvl, SituationAwareness = 3 + (lvl > 1 ? 1 : 0),
            Perception = 2 + lvl, ManualControl = 3 + lvl, ProceduralLoad = 2,
            Uncertainty = 1 + lvl, Communication = 2, ErrorConsequence = 3 + (lvl > 1 ? 1 : 0) };
        m.Expected = new ExpectedTlx {
            Mental = 50 + 14 * lvl, Physical = 46 + 18 * lvl, Temporal = 46 + 14 * lvl,
            Performance = 48 + 14 * lvl, Effort = 56 + 15 * lvl, Frustration = 36 + 18 * lvl };
        m.LoadRationale =
            "Level " + (lvl + 1) + " of three on the psychomotor-integrated axis, and the harder half " +
            "of the pair: a crosswind take-off is over in seconds whereas a crosswind approach is " +
            "three minutes of continuous correction ending in a decrab that has to be timed. " +
            "ManualControl reaches " + (3 + lvl) + " at this level, the highest value anywhere in the " +
            "bank — declared, not concealed, and the reason this mission is on its own axis. Note " +
            "that the wind the aircraft is in DECREASES on the way down through the surface layer, so " +
            "the crab that was correct at 500 m is too much at 50 m and the correction has to be " +
            "continuously revised rather than set once.";
        m.EegRelevance =
            "The continuous segment is the axis's psychomotor measure and must be read together with " +
            "the control-activity covariates. The DECISION_PROMPT epoch on short final is the isolated " +
            "cognitive component — but unlike the take-off pair it occurs while the pilot IS flying, " +
            "so it carries motor activity that the take-off decision does not. That difference is " +
            "deliberate and useful: the take-off decision and the landing decision are the same " +
            "judgement made with and without concurrent manual control, which is a within-axis " +
            "control for exactly the contamination this axis exists to handle.";
        m.ExpectedErrors = "Landing crabbed (side-loading the gear); drifting downwind of the " +
                           "centreline; over-controlling in the gusts at level 3; continuing above " +
                           "the demonstrated crosswind; deciding by default.";
        m.SuccessCriteria = "An explicit decision made in window; if continuing, touchdown on the " +
                            "runway, aligned with the centreline, wings level or into wind, sink rate " +
                            "inside the acceptable band.";
        m.FailureConditions = "Crash; runway excursion; no decision made.";
        m.AviationBasis = "FAA-H-8083-3C ch.8 (crosswind approach and landing: the crab and the " +
                          "sideslip methods, and the decrab before touchdown) and the C172 POH's " +
                          "15 kt demonstrated crosswind component.";
        m.Approximations = "As for the crosswind take-off. In addition, touchdown scoring judges sink " +
                           "rate, bank and alignment but does not model gear side-load, so landing " +
                           "crabbed is penalised through alignment and excursion rather than through " +
                           "a modelled undercarriage failure.";
        m.RequiredMarkers = new[] { EventMarkers.MissionStart, EventMarkers.DecisionPrompt,
                                    EventMarkers.ProbeOnset, EventMarkers.MissionEnd };
        return m;
    }
}
