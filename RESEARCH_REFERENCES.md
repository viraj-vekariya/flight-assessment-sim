# RESEARCH_REFERENCES

Sources the mission design, the workload model, the protocol and the EEG guidance
rest on. Grouped by what they are used for, with a note on **what each one is
actually being cited for**, so a reader can check whether the citation supports the
claim rather than just decorating it.

Verification status is marked:
**[V]** the specific claim was read in the source during this work ·
**[S]** standard, widely reported result taken on the strength of secondary sources ·
**[?]** flagged as needing a page-level check before submission.

---

## 1. Workload theory and measurement

- **[S] Hart, S. G., & Staveland, L. E. (1988).** *Development of NASA-TLX
  (Task Load Index): Results of empirical and theoretical research.* In Hancock &
  Meshkati (Eds.), *Human Mental Workload*, 139–183.
  → the six subscales; the source instrument.
  NASA-TLX manual: <https://ntrs.nasa.gov/api/citations/20000021488/downloads/20000021488.pdf>

- **[V] NASA TLX manual — 21-point scales.** The subscales have 21 tick marks,
  0–100 in steps of 5.
  → why the sliders snap to 5 (`NASA_TLX.md` §3.1).

- **[V] Hart, S. G. (2006).** *NASA-Task Load Index (NASA-TLX); 20 Years Later.*
  *Proc. HFES Annual Meeting*, 50, 904–908.
  <https://journals.sagepub.com/doi/10.1177/154193120605000909>
  → that dropping the weighting stage (Raw TLX / RTLX) is the most common
  modification, and that raw-vs-weighted sensitivity comparisons are **mixed** —
  some find raw more sensitive, some equal, some less. Used to justify RTLX and to
  state the trade-off honestly rather than claiming raw is simply better.

- **[V] Validation of the Raw NASA-TLX** (pooled analysis, patient-monitoring tasks),
  *JMIR* 22(9):e19472, 2020. <https://www.jmir.org/2020/9/e19472/>
  → raw TLX correlates highly with weighted TLX and is more time-efficient.

- **[S] Wickens, C. D. (2008).** *Multiple Resources and Mental Workload.*
  *Human Factors*, 50(3), 449–455.
  <https://journals.sagepub.com/doi/10.1518/001872008X288394>
  → why attention switching and working memory are scored separately from raw
  perceptual/manual demand in the load model.

- **[S] O'Donnell, R. D., & Eggemeier, F. T. (1986).** *Workload assessment
  methodology.* In *Handbook of Perception and Human Performance*, Vol. II.
  → the subjective / performance / physiological triangulation this study uses.

- **[S] Roscoe, A. H., & Ellis, G. A. (1990).** *A subjective rating scale for
  assessing pilot workload in flight.* RAE TR 90019. → the Bedford scale.

- **[S] EUROCONTROL / Flight Safety Foundation, OGHFA briefing note, *Workload*.**
  <https://skybrary.aero/articles/workload-oghfa-bn>
  → that both overload **and underload** degrade performance; why the LOW missions
  are not empty.

- **[S] Comstock & Arnegard (1992), NASA TM-104174; Santiago-Espada et al. (2011),
  MATB-II User's Guide, NASA/TM-2011-217164.**
  <https://ntrs.nasa.gov/api/citations/20110014456/downloads/20110014456.pdf>
  → the reference task battery this simulator is the aviation-realistic analogue of;
  the professor's requested comparison point.

---

## 2. Aviation — the abnormal and emergency scenarios

The mission set's aviation content comes primarily from one authoritative source,
read directly for this work:

- **[V] FAA (2021). *Airplane Flying Handbook*, FAA-H-8083-3C, Chapter 18 —
  Emergency Procedures.**
  <https://www.faa.gov/regulations_policies/handbooks_manuals/aviation/airplane_handbook>

  Specific claims taken from it, and where each is used:

  | Claim | Used in |
  |---|---|
  | Engine failure after take-off: worked example at **300 ft AGL**, **4-second reaction time**, ~**1,000 fpm** power-off descent, 65 kt glide — why the "impossible turn" usually is not made | H4 rationale, `MISSION_DESIGN.md` |
  | *Psychological hazards*: reluctance to accept the emergency; undue concern about injury; **desire to save the airplane** leading to stretching the glide | H4 expected errors; `SurvivalIsSuccess` |
  | **Total flap failure**: a no-flap landing can require **up to 50% more** landing distance; flatter, faster approach; nose-high attitude obscures the runway | H2, `FLAP_FAIL` checklist |
  | **Asymmetric (split) flap**: pronounced roll toward the wing with the **less** deflected flap; cross-controlled; do not land with crosswind from the side of the deployed flap | `SplitFlap` model in `AircraftSystems.cs` |
  | **Electrical system**: shed non-essential loads **immediately**; a 40 A load can flatten the battery in **10–15 minutes**; electrically-selected **flaps do not function properly on a partially-depleted battery**; land at the nearest suitable airport | H2 — the entire resource-budgeting decision |
  | **Pitot-static system**: a partially blocked static source is *"insidious"*; in a descent the altimeter reads **too high**, the ASI **too fast**, the VSI **too low**; confirm by opening the **alternate static source while climbing or descending** | H3; `StaticBlocked` model; `PITOT_STATIC` checklist |
  | **Door opening in flight**: *"seldom if ever compromises the airplane's ability to fly"*; the hazard is the **pilot's reaction**; do not rush to land; do not release the harness | M4 — the entire startle-without-danger dissociation |

- **[V] NTSB Safety Alert SA-029, *Engine Power Loss Due to Carburetor Icing*.**
  <https://www.ntsb.gov/Advocacy/safety-alerts/Documents/SA-029.pdf>
  **[S] FAA, *Pilot's Handbook of Aeronautical Knowledge* (FAA-H-8083-25), induction
  icing.**
  → that carburettor ice is a leading cause of GA power loss, that the first
  indication in a **fixed-pitch** aeroplane is an **RPM drop** followed by roughness,
  and that it is most insidious at reduced power in the descent. Used for M3 and H1.

- **[S] CAST/ICAO Common Taxonomy Team (CICTT), *Phase of Flight — Definitions and
  Usage Notes*.**
  <https://skybrary.aero/articles/casticao-common-taxonomy-team-cictt>
  → the phase vocabulary used in `FlightPhase` and the telemetry.

- **[S] Boeing (2025). *Statistical Summary of Commercial Jet Airplane Accidents,
  1959–2024*.** <https://www.boeing.com/content/dam/boeing/v2/safety/statsum.pdf>
  → landing ≈1% of flight time but ≈35% of fatal accidents; cruise ≈10%. Used only
  as *exposure* evidence for the twin-peaked workload profile, never as a workload
  measurement.

- **[S] Yuan et al. (2025). *Pilot mental workload in the A320 traffic pattern
  (HRV).* Front. Neuroergonomics 6:1672492.** <https://doi.org/10.3389/fnrgo.2025.1672492>
  → landing = highest heart rate / lowest HRV; cruise = the trough. The
  physiological half of the twin-peak claim.

- **[S] Blajev, T., & Curtis, W. (2017). *Go-Around Decision-Making and Execution
  Project.* Flight Safety Foundation.**
  <https://flightsafety.org/wp-content/uploads/2017/03/Go-around-study_final.pdf>
  → continuing an unstable approach as the dominant go-around-related risk factor;
  used in H1.

- **[S] Vivaldi, B. E. (2004). *Effect of Crosswind and Turbulence on Mental
  Workload and Tracking.* MS thesis, Embry-Riddle.**
  <https://commons.erau.edu/db-theses/207/>
  → turbulence and crosswind raise measured workload and degrade tracking; used in M2
  and H1.

- **[S] ICRAT (2018). *In Search of the Upper Limit to ATC Communication.***
  <https://bpb-us-e1.wpmucdn.com/blog.umd.edu/dist/9/604/files/2019/02/ICRAT-2018-PAPER-upper-limit-1joq1m6.pdf>
  → communication channel load rises with elements per transmission; used in M1.

---

## 3. Startle, surprise and acute stress

- **[S] Landman, A., et al. (2017). *Dealing With Unexpected Events on the Flight
  Deck: A Conceptual Model of Startle and Surprise.* Human Factors.**
  <https://journals.sagepub.com/doi/10.1177/0018720817723428>
- **[S] EASA (2018). *Startle Effect Management*, Final Report
  EASA_REP_RESEA_2015_3.**
  <https://www.easa.europa.eu/sites/default/files/dfu/EASA_Research_Startle_Effect_Managements_Final_Report.pdf>
- **[S] Martin, W. L., et al. (2016). *Impairment effects of startle on pilots during
  unexpected critical events.* Aviation Psychology and Applied Human Factors.**
  → the ~30–60 s post-startle window of impaired processing; why M4 and H4 place
  probes inside and outside that window, and why event jitter matters.

- **[?] NASA/TM-2015-218930, *Effects of Acute Stress on Aircrew Performance*.**
  → author list unverified. Check before citing in a submission.

- **[S] Sarter, N. B., & Woods, D. D. (1997). *Automation Surprises.*** In *Handbook
  of Human Factors and Ergonomics*. → not used by any of the twelve missions (this
  aircraft has no autopilot); retained because it is the obvious next manipulation if
  the simulator gains one.

---

## 4. EEG and mental workload

- **[S] Borghini, G., et al. (2014). *Measuring neurophysiological signals in
  aircraft pilots and car drivers…* Neurosci. Biobehav. Rev., 44, 58–75.**
  <https://pubmed.ncbi.nlm.nih.gov/23116991/>
- **[S] Gevins, A., & Smith, M. E. (2000). *Neurophysiological measures of working
  memory and individual differences…* Cerebral Cortex, 10(9), 829–839.**
  <https://pubmed.ncbi.nlm.nih.gov/10982744/>
  → frontal-midline theta ↑ with working-memory load, parietal alpha ↓ with load.
  The core EEG prediction throughout.

- **[S] Pope, A. T., Bogart, E. H., & Bartolome, D. S. (1995). *Biocybernetic system
  evaluates indices of operator engagement.* Biological Psychology, 40, 187–195.**
  → the engagement index β/(α+θ).

- **[S] Raufi, B., & Longo, L. (2022). *Evaluation of EEG alpha-to-theta and
  theta-to-alpha band ratios as indexes of mental workload.* Front. Neuroinformatics
  16:861967.** <https://pmc.ncbi.nlm.nih.gov/articles/PMC9149374/>
  → ratio indices outperform single bands.

- **[S] Dehais, F., et al. (2020). *A Neuroergonomics Approach to Mental Workload,
  Engagement and Performance.* Front. Neuroscience 14:268.**
  <https://pmc.ncbi.nlm.nih.gov/articles/PMC7154497/>

- **[V] Zhou, Xu & Zhang (2025). *Cognitive load recognition in simulated flight
  missions: an EEG study.* Front. Human Neuroscience 19:1542774.**
  <https://www.frontiersin.org/articles/10.3389/fnhum.2025.1542774/full>
  → **the single most useful methodological source here.** Read directly. Three
  workload levels, **5.5 min per condition**, order randomised within blocks, a
  **5-minute eyes-open resting baseline watching a static screen** (no eyes-closed
  condition), 64 channels at 2 kHz downsampled to 250 Hz, 0.1–70 Hz band-pass with a
  notch, **2 s epochs**, PSD across five bands. Reported within-subject three-class
  accuracy ~77–83% (shallow CNN) — and **cross-session accuracy collapsing to ~35%,
  i.e. chance.** Used for: trial-length choice, the eyes-open primary baseline, the
  2 s window, and the hard warning against pooling sessions.

- **[S] Hernández-Sabaté, A., et al. (2024). *EEG Dataset Collection for Mental
  Workload Predictions in a Flight-Deck Environment.* Sensors 24(4):1174.**
  <https://pmc.ncbi.nlm.nih.gov/articles/PMC10891818/>
  → scripted, time-stamped in-sim events (traffic, engine failure, windshear) as the
  workload manipulation; a direct precedent for this design.

- **[S] Taheri Gorji, H., et al. (2023). *Using machine learning methods and EEG to
  discriminate aircraft pilot cognitive workload during flight.* Sci. Reports
  13:2507.** <https://doi.org/10.1038/s41598-023-29647-0>

- **[S] Hamann, A., & Carstengerdes, N. (2022). *Assessing the development of mental
  fatigue… cortical oxygenation and frontal theta in simulated flight.* Sci. Reports
  12:6449.** <https://doi.org/10.1038/s41598-022-10044-y>
  → graded n-back loading in an A321 simulator; ≥10 min per level; the dose-response
  validation approach.

- **[S] Detection of Pilot's Mental Workload Using a Wireless EEG Headset in Airfield
  Traffic Pattern Tasks.** <https://pmc.ncbi.nlm.nih.gov/articles/PMC10378707/>

- **[?] Rodríguez-Bermúdez, G., et al. (2025). *P300 to determine pilot cognitive
  states.* Sensors 25(19):6201.** <https://pmc.ncbi.nlm.nih.gov/articles/PMC12526825/>
  → cited for the P300/spare-capacity rationale behind the secondary probes. Not read
  in full for this work.

- **[?] Kramer, A. F., et al. (1988); Gevins et al. (1997).** Page-level details
  unverified — check before submission.

---

## 5. Machine learning — leakage and evaluation

- **[V] *Data leakage in deep learning studies of translational EEG.***
  <https://www.ncbi.nlm.nih.gov/pmc/articles/PMC11099244/>
  → the canonical statement of the problem: randomly assigning segments to train and
  test puts the same subject on both sides, models learn subject-specific features
  rather than the construct, and performance is overestimated.

- **[V] *Impact of Trial-wise and Test Data Leakage on EEG-Based Emotion
  Classification.*** <https://ceur-ws.org/Vol-4115/paper7.pdf>
  → as overlap between training and test windows increases, apparent performance
  inflates.

- **[V] *The role of data partitioning on the performance of EEG-based deep learning
  models in supervised cross-subject analysis.*** *Computers in Biology and Medicine.*
  <https://www.sciencedirect.com/science/article/pii/S001048252500959X>
  → partitioning strategy materially changes reported accuracy; subject-level
  separation is the honest default.

  Together these three are why `EEG_INTEGRATION.md` §5 mandates **leave-one-subject-out**
  and forbids window-level random splits.

---

## 6. A correction carried forward

An earlier version of the project's scenario plan quoted a "NASA-TLX by phase of
flight" table (values 74.6 / 70.7 / 68.1 / 52.6). **Those numbers were traced to a
search-summary artifact and do not appear in any cited source. Do not use them.**
For the phase-workload claim use Yuan et al. (2025) for the physiological evidence
and Boeing (2025) for accident exposure, as this document does.

---

## 7. Honest status of this bibliography

- Everything marked **[V]** was read during this work and the specific claim checked.
- Everything marked **[S]** is a standard result taken from secondary sources and
  should be verified at page level before a thesis or paper submission.
- Everything marked **[?]** has a known gap and must be checked.
- No result in this repository is empirical. Every EEG and workload statement in the
  documentation is a **prediction from the literature**, not a finding from this
  simulator, because no participant has yet flown it.
