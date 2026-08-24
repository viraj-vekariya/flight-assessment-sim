// ExperimentSession — trial order, seeds, and the resting-baseline blocks.
//
// ═══════════════════════════════════════════════════════════════════════════════
// ORDER: WHY A BALANCED LATIN SQUARE AND NOT SHUFFLING
// ═══════════════════════════════════════════════════════════════════════════════
// Twelve within-subject trials in one session means learning, boredom and fatigue
// all vary with POSITION. If order were random per participant, position effects
// would be unbalanced across conditions in any small sample, and with N in the tens
// (a realistic BTP scale) that imbalance would not average out.
//
// So the order is a BALANCED LATIN SQUARE over the twelve missions, indexed by the
// participant number. Every mission then appears in every ordinal position equally
// often across a full cycle of participants, and — because the square is balanced
// (Williams design) — every mission follows every other mission equally often, which
// also counterbalances first-order carry-over.
//
// One deliberate override sits on top of the square: NO TWO CONSECUTIVE TRIALS MAY
// BE THE SAME WORKLOAD CLASS more than twice in a row. Long runs of HIGH trials
// produce cumulative fatigue that a class label cannot separate from workload, and
// long runs of LOW trials produce vigilance decrement. The de-clumping pass is
// deterministic and is recorded in session.json alongside the raw square index, so
// the realised order is fully reconstructible.
//
// ═══════════════════════════════════════════════════════════════════════════════
// BASELINE DESIGN (PHASE 10) — and why it is not one baseline
// ═══════════════════════════════════════════════════════════════════════════════
// Three different references are collected, because no single one is sufficient and
// the literature does not support treating any one of them as universally correct:
//
//   1. RESTING, EYES OPEN — 120 s, seated in the cockpit, aircraft parked and
//      frozen, fixating a marked point on the panel. This is the PRIMARY
//      normalisation reference. Eyes-open is chosen as primary because every task
//      state in the experiment is eyes-open, and the eyes-open/eyes-closed
//      difference in occipital alpha is far larger than any workload effect we
//      expect to measure — normalising a task state against an eyes-closed rest
//      would put a huge, condition-irrelevant term into every index. Comparable
//      flight-sim EEG work uses exactly this (a static screen, eyes open) as its
//      rest condition.
//
//   2. RESTING, EYES CLOSED — 120 s. NOT used for workload normalisation. It is
//      collected for QUALITY CONTROL: alpha should rise sharply on eye closure, so
//      an absent eyes-closed alpha increase is the fastest available check that the
//      posterior electrodes are actually working before an hour of data is lost.
//      It also supports per-participant alpha-peak-frequency estimation, which is
//      the defensible way to set individual band edges rather than fixed 8-13 Hz.
//
//   3. IN-TASK BASELINE — the first 60 s of EVERY mission, flying the same
//      aircraft with the same visual scene and the same manual demand as the
//      loaded segment, with no manipulation. This is the reference that actually
//      controls for the things a resting baseline cannot: visual flow, motor
//      activity, and being in the task set at all. For a WITHIN-mission contrast
//      this is the correct denominator; the resting baselines are for
//      BETWEEN-mission and between-session comparability.
//
// The eyes-open rest is repeated at the END of the session as well, so drift over
// the session (fatigue, electrode impedance change) is measurable rather than
// assumed away.
//
// RECOMMENDED NORMALISATION (documented, not enforced — the analysis is free to
// differ, but must then say so): per participant, per session, z-score each band-
// power feature against that session's pooled in-task baseline segments; report the
// eyes-open resting normalisation as a sensitivity analysis. WITHIN-SESSION is
// essential — cross-session EEG workload models degrade toward chance, so a model
// normalised on another day's baseline is not a fair test.

using System.Collections.Generic;
using UnityEngine;

public enum BaselineKind { RestEyesOpen, RestEyesClosed }

public static class ExperimentSession
{
    public const float RestBaselineS = 120f;

    /// <summary>The realised trial order for this session (mission ids).</summary>
    public static List<string> Order { get; private set; } = new List<string>();
    public static int SquareIndex { get; private set; }
    public static int Seed { get; private set; }

    /// <summary>Build the counterbalanced order for a participant and open the
    /// session data folder. `participantNumber` is derived from the participant ID
    /// so the same code always gets the same row of the square (reproducible).</summary>
    /// <summary>The variant index this participant flies in each phase row, in the order
    /// of MissionLibrary.Rows. Recorded in session.json so the realised assignment is
    /// reconstructible from the participant code alone.</summary>
    public static int[] RowVariants { get; private set; } = new int[0];

    public static void Begin(string participantId, int sessionNumber)
    {
        int pnum = StableNumber(participantId);
        SquareIndex = (pnum + sessionNumber - 1) % 12;
        Seed = unchecked(pnum * 2654435761u).GetHashCode() ^ (sessionNumber * 104729);

        // WHICH TWELVE. The bank holds 36 cognitive-axis missions (4 phases x 3 classes
        // x 3 interchangeable variants). A session is still TWELVE — one variant index
        // per phase row — because 36 x 300 s is three hours of flying inside one EEG
        // session and fatigue would dominate every contrast the study exists to measure.
        //
        // The variant index rotates per row, so that WITHIN a row all three classes share
        // one variant (the Low-Medium-High contrast is therefore always variant-matched,
        // and variant can never masquerade as class) while ACROSS rows the participant
        // meets different variants (so variant is not perfectly nested in participant).
        // See MISSION_BANK_DESIGN.md.
        int variantSource = pnum + (sessionNumber - 1);
        var session = MissionLibrary.SessionMissions(variantSource);
        RowVariants = new int[MissionLibrary.Rows.Length];
        for (int r = 0; r < MissionLibrary.Rows.Length; r++)
            RowVariants[r] = MissionLibrary.VariantForRow(variantSource, MissionLibrary.Rows[r]);

        var ids = new List<string>();
        foreach (var m in session) ids.Add(m.Id);

        Order = BalancedLatinSquareRow(ids, SquareIndex);
        Order = DeClumpClasses(Order);

        ExperimentLogger.BeginSession(Order, Seed);
    }

    /// <summary>Human-readable variant assignment, for session.json and the operator's
    /// screen: "Takeoff=v2 Climb=v3 Cruise=v1 Approach=v2".</summary>
    public static string VariantSummary()
    {
        if (RowVariants == null || RowVariants.Length == 0) return "(not assigned)";
        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < RowVariants.Length && r < MissionLibrary.Rows.Length; r++)
            sb.Append(MissionLibrary.Rows[r]).Append("=v").Append(RowVariants[r]).Append(' ');
        return sb.ToString().TrimEnd();
    }

    /// <summary>Row `k` of a balanced (Williams) Latin square on n items. For even n
    /// a single square is balanced for first-order carry-over: each item follows
    /// each other item exactly once across the n rows.</summary>
    public static List<string> BalancedLatinSquareRow(List<string> items, int k)
    {
        int n = items.Count;
        var row = new List<string>(n);
        // Standard Williams construction: positions 0, 1, n-1, 2, n-2, 3, n-3, ...
        for (int i = 0; i < n; i++)
        {
            int j = (i % 2 == 0) ? i / 2 : n - 1 - (i / 2);
            row.Add(items[(j + k) % n]);
        }
        return row;
    }

    /// <summary>Break runs of three or more consecutive trials of the same workload
    /// class by swapping the offending trial with the nearest later trial of a
    /// different class. Deterministic, order-preserving elsewhere.</summary>
    static List<string> DeClumpClasses(List<string> order)
    {
        var outp = new List<string>(order);
        for (int i = 2; i < outp.Count; i++)
        {
            if (ClassOf(outp[i]) != ClassOf(outp[i - 1]) || ClassOf(outp[i]) != ClassOf(outp[i - 2])) continue;
            for (int j = i + 1; j < outp.Count; j++)
            {
                if (ClassOf(outp[j]) == ClassOf(outp[i])) continue;
                (outp[i], outp[j]) = (outp[j], outp[i]);
                break;
            }
        }
        return outp;
    }

    static WorkloadClass ClassOf(string id)
    {
        var m = MissionLibrary.Get(id);
        return m != null ? m.Class : WorkloadClass.Low;
    }

    /// <summary>Deterministic small integer from a participant code, so the same
    /// code always maps to the same Latin-square row on any machine.</summary>
    public static int StableNumber(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        int h = 0;
        foreach (char c in id.ToUpperInvariant()) h = (h * 31 + c) & 0x7fffffff;
        return h % 12;
    }

    /// <summary>Human-readable summary of the realised order, for session.json and
    /// for the experimenter's screen.</summary>
    public static string OrderSummary()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Order.Count; i++)
        {
            var m = MissionLibrary.Get(Order[i]);
            sb.Append(i + 1).Append('.').Append(Order[i]).Append('(').Append(m != null ? m.ClassTag[0].ToString() : "?").Append(") ");
        }
        return sb.ToString();
    }
}

/// <summary>Runs one resting-baseline block: the aircraft is parked and frozen, the
/// participant is given a fixation instruction, and the block is bracketed by
/// BASELINE_START / BASELINE_END markers on the same clock as everything else.
///
/// The aeroplane is FROZEN rather than merely idling so that there is no engine
/// vibration cue, no drift, and nothing the participant must attend to. Frozen also
/// means no control inputs, which makes this the one segment in the session with
/// guaranteed-zero motor artifact — useful as the artifact floor.</summary>
public class BaselineRunner : MonoBehaviour
{
    public bool Running { get; private set; }
    public BaselineKind Kind { get; private set; }
    public float Elapsed { get; private set; }
    public float Duration { get; private set; }
    public string Instruction { get; private set; } = "";

    ExperimentLogger log = new ExperimentLogger();
    CessnaPhysics ac;
    AircraftSystems sys;
    System.Action onDone;
    float sampleAccum;

    public void Begin(BaselineKind kind, float duration, CessnaPhysics aircraft, System.Action done)
    {
        Kind = kind; Duration = duration; Elapsed = 0f; Running = true;
        ac = aircraft; onDone = done; sampleAccum = 0f;
        sys = ac != null ? ac.GetComponent<AircraftSystems>() : null;

        Instruction = kind == BaselineKind.RestEyesOpen
            ? "REST — EYES OPEN\n\nSit still. Look at the centre of the instrument panel and keep\n" +
              "your eyes open. Try not to move, talk, or clench your jaw.\n" +
              "Blink normally — do not try to suppress blinks."
            : "REST — EYES CLOSED\n\nSit still and close your eyes. Stay awake.\n" +
              "Try not to move, talk, or clench your jaw.\n" +
              "You will be told when to open them.";

        // Park and freeze.
        if (ac != null)
        {
            var gmn = GameManager.Instance;
            if (gmn != null) ac.ResetTo(gmn.Runway.Start, gmn.Runway.Rot, false, 0f);
            ac.throttle = 0f;
            ac.pitchInput = ac.rollInput = ac.yawInput = 0f;
            ac.GetComponent<AircraftController>()?.ResetConfiguration();
            if (ac.Body != null) { ac.Body.isKinematic = true; ac.Body.linearVelocity = Vector3.zero; }
            if (sys != null) sys.ResetAll();
        }

        var mock = new MissionDefinition
        {
            Id = "BASELINE_" + (kind == BaselineKind.RestEyesOpen ? "EO" : "EC"),
            Name = "Resting baseline (" + (kind == BaselineKind.RestEyesOpen ? "eyes open" : "eyes closed") + ")",
            Class = WorkloadClass.Low, Phase = FlightPhase.Preflight,
            DurationS = duration, InTaskBaselineS = 0f,
            Objective = "Rest.", Brief = Instruction,
            LoadRationale = "Resting reference, not an experimental condition.",
            EegRelevance = kind == BaselineKind.RestEyesOpen
                ? "Primary normalisation reference. Eyes open, matching the ocular state of every task condition."
                : "Quality-control reference only. Alpha should rise on eye closure; use it to verify posterior electrodes and to estimate individual alpha peak frequency. NOT for workload normalisation.",
            Approximations = "The aircraft is frozen (kinematic rigidbody), so there is no engine sound, vibration or drift during the block."
        };
        log.BeginTrial(mock, null);
        log.Mark(EventMarkers.BaselineStart, kind.ToString() + "|" + duration.ToString("F0") + "s", ac);
    }

    void Update()
    {
        if (!Running) return;
        float dt = Time.deltaTime;
        Elapsed += dt;
        log.SetMissionTime(Elapsed);

        sampleAccum += dt;
        if (sampleAccum >= 1f / ExperimentLogger.TelemetryHz)
        {
            log.Sample(ac, sys, null, null, 0f, "REST_" + (Kind == BaselineKind.RestEyesOpen ? "EO" : "EC"));
            sampleAccum = 0f;
        }

        // The experimenter can end a block early (participant discomfort etc.);
        // the actual duration is in the markers, so a short block is never silent.
        if (Elapsed >= Duration || Input.GetKeyDown(KeyCode.Escape)) Finish();
    }

    void Finish()
    {
        Running = false;
        log.Mark(EventMarkers.BaselineEnd, Kind + "|actual_s=" + Elapsed.ToString("F1"), ac);
        log.Close();
        if (ac != null && ac.Body != null) ac.Body.isKinematic = false;
        onDone?.Invoke();
    }
}
