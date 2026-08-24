// WorkloadModel — the a-priori cognitive-load scoring framework (PHASE 2).
//
// THE PROBLEM THIS SOLVES
//   "Which mission is harder?" must not be answered by how dangerous it looks. An
//   engine failure in the cruise, at 900 m over flat ground, with one memory drill
//   and no time pressure, can be COGNITIVELY EASIER for a trained pilot than a
//   busy visual circuit with three ATC re-clearances and a runway change — because
//   the emergency is a single well-rehearsed schema, while the circuit is
//   continuous multitasking with working-memory turnover.
//
// THE MODEL
//   Each mission is scored 0-4 on twelve demand dimensions drawn from the workload
//   literature, then a weighted sum gives a single Predicted Load Index (PLI, 0-100).
//   Dimensions and weights follow:
//     * Wickens' Multiple Resource Theory (2008) — demand is not one pool; conflicts
//       WITHIN a resource cost more than demand spread ACROSS resources, which is
//       why ATTENTION SWITCHING and CONCURRENCY are separate dimensions from raw
//       perceptual and manual demand.
//     * NASA-TLX's own six sources (Hart & Staveland 1988) — mental, physical and
//       temporal demand, effort, performance pressure, frustration — so the model's
//       predictions are directly comparable to the instrument used to validate them.
//     * Sweller-style intrinsic/extraneous load — element interactivity is captured
//       by DecisionComplexity and WorkingMemory rather than by "number of events".
//     * Startle/surprise literature (Landman 2017; EASA 2018) — Uncertainty and
//       Surprise are scored separately from Time Pressure, because a surprising
//       event is not automatically an urgent one (an open cabin door is the
//       canonical example: high surprise, near-zero urgency).
//
//   IMPORTANT: the PLI is a PREDICTION, an ordering hypothesis for the design. It is
//   NOT evidence. The experiment tests it against NASA-TLX, task performance and
//   EEG. If the data disagree with the PLI, the DATA WIN and the classification is
//   revised — see COGNITIVE_LOAD_MODEL.md §"Falsifying the model".

using System.Collections.Generic;

/// <summary>The three experimental conditions. This is the label the ML model
/// ultimately predicts, and the only thing that is supposed to differ between
/// missions by design.</summary>
public enum WorkloadClass { Low, Medium, High }

/// <summary>Twelve 0-4 demand ratings + the weighted index. Every mission carries
/// one of these, filled in by hand from the task analysis in MISSION_DESIGN.md.</summary>
public class WorkloadProfile
{
    // 0 = essentially absent, 1 = light, 2 = moderate, 3 = heavy, 4 = extreme
    public int MentalDemand;         // information processing / diagnosis
    public int TemporalDemand;       // urgency, how much time the pilot has
    public int DecisionComplexity;   // number & interactivity of alternatives
    public int WorkingMemory;        // items to hold (clearances, targets, state)
    public int AttentionSwitching;   // forced channel switching per unit time
    public int SituationAwareness;   // effort to build/maintain the picture
    public int Perception;           // scanning / detection demand
    public int ManualControl;        // psychomotor tracking difficulty
    public int ProceduralLoad;       // checklist / drill execution
    public int Uncertainty;          // ambiguity of the cue, surprise
    public int Communication;        // verbal / R-T load
    public int ErrorConsequence;     // stakes, i.e. performance pressure

    /// <summary>Weights sum to 1. Higher weight = the literature treats that source
    /// as a stronger driver of *mental* workload specifically (as opposed to
    /// physical effort). ManualControl is deliberately weighted LOW: it is the main
    /// confound in EEG studies (movement artifact, physical demand) and we want the
    /// contrast between classes to be cognitive, not muscular.</summary>
    public static readonly Dictionary<string, float> Weights = new Dictionary<string, float>
    {
        ["MentalDemand"]       = 0.16f,
        ["TemporalDemand"]     = 0.13f,
        ["DecisionComplexity"] = 0.12f,
        ["WorkingMemory"]      = 0.11f,
        ["AttentionSwitching"] = 0.11f,
        ["SituationAwareness"] = 0.08f,
        ["Perception"]         = 0.06f,
        ["ManualControl"]      = 0.05f,   // low on purpose — see summary
        ["ProceduralLoad"]     = 0.07f,
        ["Uncertainty"]        = 0.06f,
        ["Communication"]      = 0.03f,
        ["ErrorConsequence"]   = 0.02f,
    };

    /// <summary>Predicted Load Index, 0-100.</summary>
    public float PLI
    {
        get
        {
            float w = 0f;
            w += MentalDemand       * Weights["MentalDemand"];
            w += TemporalDemand     * Weights["TemporalDemand"];
            w += DecisionComplexity * Weights["DecisionComplexity"];
            w += WorkingMemory      * Weights["WorkingMemory"];
            w += AttentionSwitching * Weights["AttentionSwitching"];
            w += SituationAwareness * Weights["SituationAwareness"];
            w += Perception         * Weights["Perception"];
            w += ManualControl      * Weights["ManualControl"];
            w += ProceduralLoad     * Weights["ProceduralLoad"];
            w += Uncertainty        * Weights["Uncertainty"];
            w += Communication      * Weights["Communication"];
            w += ErrorConsequence   * Weights["ErrorConsequence"];
            return w / 4f * 100f;   // ratings are 0-4
        }
    }

    /// <summary>Sum of the manual/physical dimensions only — the confound check.
    /// Two missions in DIFFERENT workload classes should ideally have SIMILAR
    /// values here, otherwise an EEG difference could be muscle, not mind.</summary>
    public int PhysicalConfound => ManualControl;

    public Dictionary<string, int> AsDictionary() => new Dictionary<string, int>
    {
        ["mental_demand"] = MentalDemand,
        ["temporal_demand"] = TemporalDemand,
        ["decision_complexity"] = DecisionComplexity,
        ["working_memory"] = WorkingMemory,
        ["attention_switching"] = AttentionSwitching,
        ["situation_awareness"] = SituationAwareness,
        ["perception"] = Perception,
        ["manual_control"] = ManualControl,
        ["procedural_load"] = ProceduralLoad,
        ["uncertainty"] = Uncertainty,
        ["communication"] = Communication,
        ["error_consequence"] = ErrorConsequence,
    };
}

/// <summary>The expected NASA-TLX shape for a mission. Recorded BEFORE any data is
/// collected so the prediction is falsifiable — it is a pre-registration, not a
/// post-hoc description, and it is never written into the participant's TLX file.</summary>
public class ExpectedTlx
{
    public int Mental, Physical, Temporal, Performance, Effort, Frustration;   // 0-100
    public float RTLX => (Mental + Physical + Temporal + Performance + Effort + Frustration) / 6f;
}
