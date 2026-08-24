// Phase 5 — subjective mental-workload self-report, collected after every scenario
// trial (per supervisor direction, Aug 2026: mental/cognitive workload is now the
// sole target construct). Two validated instruments:
//   • NASA-TLX (Raw/unweighted TLX): 6 subscales 0..100; RTLX = their mean. This is
//     the standard simplified alternative to full NASA-TLX pairwise-comparison
//     weighting and is widely used in applied/flight-sim studies.
//   • BEDFORD Workload Scale: a single 1..10 hierarchical rating (Roscoe & Ellis).
// Collected blind to the trial's score (asked before the score breakdown is shown)
// so the performance readout can't bias the self-report.

public class WorkloadRating
{
    public float MentalDemand, PhysicalDemand, TemporalDemand, Performance, Effort, Frustration; // each 0..100
    public int Bedford = 1;   // 1..10

    public float RTLX => (MentalDemand + PhysicalDemand + TemporalDemand + Performance + Effort + Frustration) / 6f;

    public static readonly string[] BedfordStatements =
    {
        "1 — Workload insignificant",
        "2 — Workload low",
        "3 — Enough spare capacity for all desirable additional tasks",
        "4 — Insufficient spare capacity for easy attention to additional tasks",
        "5 — Reduced spare capacity; additional tasks cannot be given the desired amount of attention",
        "6 — Little spare capacity; level of effort allows little attention to additional tasks",
        "7 — Very little spare capacity, but maintenance of effort in the primary task not in question",
        "8 — Very high workload with almost no spare capacity; difficulty maintaining level of effort",
        "9 — Extremely high workload; no spare capacity; serious doubts about ability to maintain effort",
        "10 — Task abandoned; pilot unable to apply sufficient effort",
    };
}
