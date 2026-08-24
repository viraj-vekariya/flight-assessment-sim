# Cognitive‑Load Flight Scenario Plan (research‑grounded)

**Project:** EEG‑based measurement of pilot mental workload in a Cessna VR flight simulator.
**Purpose of this document:** a complete, literature‑backed catalogue of flight scenarios —
from before engine start to after shutdown — organised by where and how a pilot's cognitive
load is triggered. Each scenario cites the research that justifies it. This is the master list;
we will implement a mission for each, and grade them into low / medium / high load tiers.

> Scope note: numbers and tiers are deliberately **not** fixed here. This is the "what and why"
> — the full space of load‑relevant scenarios — so the experiment can be built on solid ground.

---

## 1. Why we design scenarios this way (measurement → design)

**Mental workload = task demand vs. operator capacity.** Both *overload* and *underload*
degrade performance (EUROCONTROL/Flight Safety Foundation OGHFA, "Workload"). We measure it
three ways and triangulate (O'Donnell & Eggemeier, 1986): **subjective** (NASA‑TLX; Hart &
Staveland, 1988 — six dimensions: mental, physical, temporal demand, effort, performance,
frustration), **performance** (tracking error, response latency, errors), and **physiological**
(EEG + HRV + pupillometry).

**What EEG can and cannot see — and what that demands of the scenarios:**
- Load shows up as **frontal‑midline theta ↑ and parietal alpha ↓** (Gevins & Smith, 2000;
  Borghini et al., 2014; Dehais et al., 2020), and as ratio indices — the **engagement index**
  β/(α+θ) (Pope, Bogart & Bartolome, 1995) and θ/α ratios that beat single bands by 18–30%
  (Raufi & Longo, 2022). → **Each event must produce a *contrast*, so every scenario pairs a
  calm baseline segment with the load segment.**
- Spectral power needs **multi‑second, artifact‑free windows.** → **events must be isolated and
  long enough**, not smeared together, and with minimal gross body movement.
- For sub‑second stimuli, the **P300** ERP indexes spare capacity (Rodríguez‑Bermúdez et al.,
  2025; Kramer et al., 1988). → **discrete cues must carry exact, logged timestamps** and be
  repeated so they can be averaged.
- EEG does **not** separate workload from arousal/fatigue, and models drift across days
  (within‑session 83% → cross‑session 37%; Zhou et al., 2025). → **collect a fresh resting
  baseline every session, keep events short, and counterbalance order.**

**Design principles adopted (from workload‑EEG experimental literature):**
1. **Isolate one stressor per low scenario; compound only at high tiers** (Pontiggia et al.,
   2024; Rodríguez‑Bermúdez et al., 2025).
2. **Grade difficulty monotonically and validate the dose‑response** across TLX + performance +
   physiology (Hamann & Carstengerdes, 2022, graded 0–3‑back in an A321 sim). For task‑battery
   loading, quantify the manipulation in **event rate** — ≈3 events/min (low) to ≈20+/min (high)
   (Pontiggia et al., 2024).
3. **Structure every run baseline → task → recovery**, with an explicit calm resting baseline
   (Hernández‑Sabaté et al., 2024 used a 10‑min relaxing‑video rest; Taheri Gorji et al., 2023
   took pre‑flight baselines).
4. **Script discrete, time‑stamped events with synchronized markers** for event‑locked analysis
   (Rodríguez‑Bermúdez et al., 2025 timed stimuli to the second; Hernández‑Sabaté et al., 2024
   inserted scripted traffic / engine‑failure / windshear events).
5. **Within‑subject, counterbalanced/pseudo‑randomised order, ≥10 min per level** (Hamann &
   Carstengerdes, 2022; Pontiggia et al., 2024).
6. **Ground high‑load scenarios in realistic operations (LOFT‑style)** for ecological validity,
   while keeping event timing/identity scripted and identical across participants.

---

## 2. The flight spine and its workload profile (the backbone)

Phases and definitions follow the **CAST/ICAO Common Taxonomy Team (CICTT), "Phase of Flight."**
The normal‑operations arc, before‑flight → after‑landing:

> **Standing (engines off → start) → Pushback/Tow → Taxi‑out → Takeoff → Initial Climb →
> En Route (Climb → Cruise → Descent) → Approach → Landing (flare → rollout) → Taxi‑in →
> Standing (shutdown).** Contingency any time on arrival: **Go‑around / Missed approach.**

Workload is **twin‑peaked ("bathtub"): high at departure, lowest at cruise, rising through
descent to a maximum at approach/landing** — established physiologically (Yuan et al., 2025:
landing = highest HR / lowest HRV / lowest performance; cruise = the trough) and by accident
exposure (Boeing, 2025: **landing ≈1% of flight time but 35% of fatal accidents; approach+landing
≈48%; cruise ≈10%**; FSF/Werfelman, 2020). Two design consequences:

- **Cruise is the clean canvas** — low, stable intrinsic load → the place to inject single,
  isolated stressors and read the sharpest EEG contrast. Cruise carries the *core experimental
  manipulations.*
- **Takeoff, approach, landing are intrinsic peaks** — realistic high‑load conditions in their
  own right, and the place for combined/emergency stressors.

| Phase | Intrinsic baseline load | Main sources of load (task analysis) |
|---|---|---|
| Standing / pre‑flight | Low (non‑zero) | clearances, FMS/setup, before‑start checklists |
| Pushback / tow | Low | ground‑crew coordination, engine start monitoring |
| Taxi‑out | **Moderate** (underestimated) | surface navigation, R/T, checklists, runway‑incursion risk |
| Takeoff roll / rotation | **High** | precise thrust/track control, V‑speed & abort decision |
| Initial climb | High | gear/flap retraction, departure R/T, level‑off |
| Climb to cruise | Low–Moderate | monitoring, occasional ATC |
| **Cruise** | **Low (trough)** | monitoring few parameters — *vigilance/underload risk* |
| Descent / arrival | Moderate–High | approach brief, energy management, crossing restrictions |
| **Approach** | **High (2nd peak)** | deceleration, configuration, glidepath, ATC, go‑around readiness |
| **Landing / flare / rollout** | **Highest** | most information processing + effort of any phase |
| Go‑around / missed | **High, error‑prone** | rapid reconfiguration + decision under startle |
| Taxi‑in / shutdown | Low–Moderate | post‑landing checklist, surface nav (fatigue‑modulated) |

---

## 3. Cognitive‑load trigger taxonomy (the stressors)

Every scenario in §4 injects one (or, at high tiers, several) of these. Each is evidence‑based.

| # | Trigger | Mechanism (why load rises) | Peak load | Key source(s) |
|---|---|---|---|---|
| T1 | **Startle & surprise** (unexpected event) | involuntary reflex + expectation mismatch; ~30–60 s impaired processing | HIGH | Landman et al. 2017; EASA 2018; Martin et al. 2016 |
| T2 | **Time pressure / urgency** | truncated info‑gathering, rushed decisions; accuracy collapses | HIGH | Wu et al. (IJIE); Frontiers 2025 |
| T3 | **Task saturation / multitasking** | limited attention exceeded → tunnelling, missed channels | HIGH | AIAA SciTech 2024; FAA/AOPA |
| T4 | **Automation surprise / mode confusion** | mental‑model mismatch with automation state | MED–HIGH | Sarter & Woods 1997 |
| T5 | **System / instrument failure** | ambiguous indications → diagnose + fly + procedure at once | HIGH | NASA 2017 (unreliable airspeed / AF447) |
| T6 | **Traffic conflict / TCAS RA** | sudden time‑critical maneuver, may conflict with ATC | MED–HIGH | IATA TCAS guidance; ICAO RASG‑PA |
| T7 | **Weather** — turbulence, low‑vis/IMC, windshear, crosswind | degraded control + loss of external cues; windshear = sharpest spike | MED–HIGH (windshear HIGH) | Vivaldi 2004; Sci Rep 2025; FSF |
| T8 | **Go‑around / balked landing** | rarely practiced rapid reconfiguration + decision | HIGH | Blajev & Curtis 2017 (FSF) |
| T9 | **Communication / ATC re‑clearance** | serial verbal working‑memory load; congestion, read‑backs | MED | ICRAT 2018; SKYbrary |
| T10 | **Emergency / abnormal under stress** | infrequent procedures + acute‑stress WM impairment | HIGH | NASA/TM‑2015‑218930; Frontiers 2023 |
| T11 | **Vigilance / underload** (long quiet monitoring) | sustained‑attention decrement; out‑of‑the‑loop | LOW‑but‑degrading | OGHFA "Workload" |
| T12 | **Spatial disorientation / illusions** (IMC, somatogravic) | vestibular–visual conflict | HIGH | SKYbrary; FSF |

Cross‑cutting **modulators** (not events, but design dimensions): **fatigue / time‑on‑task**
(lowers the ceiling on every trigger), **single‑pilot vs. crew** (a failure can jump from
manageable to unacceptable as crew is cut — NASA 2017), and the **startle refractory window**
(measure the ~30–60 s *after* a startle, not just the instant).

---

## 4. Master scenario catalogue (before‑flight → after‑landing)

Each scenario: **ID · name — trigger(s) · expected load · what the pilot must do · source.**
Every phase gets a **calm baseline** (essential EEG reference) plus its load variants.

### A. Standing / pre‑flight (engines off → start)
- **A1 Normal pre‑flight & startup** — baseline · LOW · run checklists, program FMS, copy clearance · *CICTT; OGHFA.*
- **A2 Start/systems abnormality** — T5 · LOW–MED · notice a start or systems fault, work the checklist · *NASA 2017.*
- **A3 Last‑minute clearance/route change** — T9,T2 · MED · re‑program and re‑brief under a departure slot · *ICRAT 2018.*

### B. Taxi‑out
- **B1 Normal taxi to runway** — baseline · LOW–MED · surface nav + R/T + checklist · *CICTT.*
- **B2 Complex/amended taxi routing** — T3,T9 · MED · re‑route while taxiing, avoid incursion · *AIAA 2024; CICTT (RI risk).*
- **B3 Hold‑short / crossing‑traffic decision** — T2,T6 · MED · stop/continue decision with crossing traffic · *SKYbrary.*

### C. Takeoff roll & rotation (intrinsic HIGH)
- **C1 Normal takeoff** — baseline(high‑intrinsic) · HIGH · thrust set, track centreline, rotate at Vr · *Yuan 2025; Boeing 2025.*
- **C2 Rejected takeoff (RTO)** — T1,T2,T5 · HIGH · abort decision at/near V1 on a warning · *EASA 2018; NASA/TM‑2015‑218930.*
- **C3 Crosswind takeoff** — T7 · MED–HIGH · directional control against crosswind/gusts · *Vivaldi 2004.*
- **C4 Engine failure after V1 (continue)** — T5,T1 · HIGH · fly the failure, maintain control & climb · *NASA 2017.* *(high tier)*

### D. Initial climb (HIGH)
- **D1 Normal initial climb** — baseline · MED–HIGH · retract config, hold path, level‑off · *Yuan 2025.*
- **D2 Configuration + comms saturation** — T3 · MED–HIGH · gear/flaps + departure R/T + level‑off together · *AIAA 2024; Wickens 2008.*
- **D3 Interruption on climb** — T1(mild) · MED · acknowledge a cabin/ATC call, keep flying · *Landman 2017.*
- **D4 Departure re‑clearance** — T9 · MED · new heading/altitude from ATC on climb · *ICRAT 2018.*

### E. Climb to cruise
- **E1 Normal climb to cruise** — baseline · LOW–MED · monitor, occasional R/T · *CICTT.*
- **E2 TCAS RA in climb** — T6,T1 · MED–HIGH · fly the RA, resolve vs. ATC clearance · *IATA.*
- **E3 Weather deviation on climb** — T7,T9 · MED · request/execute a deviation around build‑ups · *FSF.*

### F. Cruise (trough) — **the experimental core** (isolated single stressors + graded loading)
- **F1 Calm cruise** — baseline / resting reference · LOW · hold altitude & heading in calm air · *Yuan 2025.*
- **F2 Long vigilance leg** — T11 · LOW (underload) · sustained quiet monitoring, catch a rare change · *OGHFA.*
- **F3 Instrument/system fault** — T5 · MED · notice a frozen gauge/warning, acknowledge, keep holding · *NASA 2017.*
- **F4 Turbulence patch** — T7 · MED · ride out light turbulence, hold altitude · *Vivaldi 2004.*
- **F5 Peripheral distraction / oddball probe** — T3,T9 · MED · respond to a peripheral cue (good for P300) · *Rodríguez‑Bermúdez 2025.*
- **F6 Timed decision under uncertainty** — T2 · MED · divert/continue decision within a window · *Wu et al.*
- **F7 ATC re‑route (heading + altitude + freq)** — T9,T3 · MED · execute a multi‑part re‑clearance · *ICRAT 2018.*
- **F8 Automation surprise** — T4 · MED–HIGH · detect an unexpected autopilot mode/behaviour, recover · *Sarter & Woods 1997.*
- **F9 Startle event (loud warning in quiet cruise)** — T1 · HIGH (transient) · the cleanest startle contrast against a quiet baseline · *EASA 2018; Landman 2017.*
- **F10 Graded dual‑task loading** — T3 (scalable) · LOW→HIGH · concurrent tracking + monitoring + comms/arithmetic at controlled **event rates** (~3 → ~20+/min) · *Pontiggia 2024; Hamann & Carstengerdes 2022; Wickens 2008.*

### G. Descent / arrival (rising)
- **G1 Normal descent & arrival setup** — baseline · MED · approach brief, energy plan, FMS · *Yuan 2025.*
- **G2 Crossing restriction / re‑clearance** — T2,T9 · MED–HIGH · make a crossing altitude, manage energy · *Wu et al.; ICRAT.*
- **G3 TCAS RA in descent** — T6,T1 · MED–HIGH · fly the RA during a busy descent · *IATA.*

### H. Approach (2nd peak, intrinsic HIGH)
- **H1 Normal stabilized approach** — baseline(high‑intrinsic) · HIGH · configure, track glidepath, decelerate · *Yuan 2025.*
- **H2 Crosswind / gusty approach** — T7 · HIGH · hold centreline against crosswind/gusts · *Vivaldi 2004.*
- **H3 Low‑visibility / IMC approach** — T7,T12 · HIGH · full instrument reliance, guard disorientation · *Sci Rep 2025; SKYbrary.*
- **H4 Windshear on approach** — T7,T1 · HIGH · recognise and execute windshear escape · *FSF.*
- **H5 Unstable approach → go‑around decision** — T8,T2 · HIGH · decide and initiate a go‑around · *Blajev & Curtis 2017.*
- **H6 Late runway change / side‑step** — T9,T2 · HIGH · re‑set the approach under time pressure · *ICRAT; FSF.*
- **H7 Interruption on approach** — T3,T1 · HIGH · absorb a distraction on top of high intrinsic load · *AIAA 2024.*

### I. Landing / flare / rollout (highest)
- **I1 Normal landing** — baseline(highest‑intrinsic) · HIGH · flare, touchdown, track, decelerate · *Yuan 2025; Boeing 2025.*
- **I2 Traffic on runway → balked landing** — T8,T1 · HIGH · reject the landing, go around · *Blajev & Curtis 2017.*
- **I3 System trouble on short final → forced landing** — T5,T10,T1 · HIGH · commit and put it down safely · *NASA 2017; NASA/TM‑2015‑218930.* *(emergency)*
- **I4 Crosswind / gusty landing** — T7 · HIGH · de‑crab and touch down on centreline · *Vivaldi 2004.*
- **I5 Bounce / hard‑landing recovery** — T1,T2 · HIGH · decide go‑around vs. recover after a bounce · *Blajev & Curtis 2017.*

### J. Go‑around / missed approach (contingency)
- **J1 Standard (planned) go‑around** — T8 · HIGH · full power, clean up, fly the missed procedure · *Blajev & Curtis 2017.*
- **J2 Low/late go‑around near minimums** — T8,T1 · HIGH · startle‑driven late go‑around, avoid lock‑up · *Blajev & Curtis; "cognitive lockup".*

### K. Taxi‑in / shutdown (fatigue‑modulated)
- **K1 Normal taxi‑in & shutdown** — baseline · LOW–MED · after‑landing checklist, surface nav · *CICTT.*
- **K2 Congested apron / hotspot taxi** — T3,T9 · MED · navigate a busy apron with hold‑shorts · *AIAA 2024.*

### L. Post‑flight
- **L1 Shutdown & securing** — baseline · LOW · securing checklist (clean end‑of‑session baseline) · *CICTT.*

**≈40 scenarios spanning the whole flight.** Each maps to a concrete, scriptable sim event
(turbulence, gauge‑freeze, alarm, distraction/decision cue, ATC heading/altitude change,
traffic/go‑around) — most of which the current `ScenarioEngine` already supports.

---

## 5. Mapping the catalogue to load tiers (Low / Medium / High)

Tiers are defined by **intensity, concurrency, and phase**, not by which situation — the same
situation can appear at different tiers. Per the design principles (§1) we validate that each
tier steps up TLX + performance + physiology.

- **LOW** — calm baselines + a *single, mild, isolated* stressor, mostly in low‑intrinsic phases
  (cruise, climb). One event, generous response window, low intensity. *(e.g., F1–F7 mild, D3,
  B1, K1.)*
- **MEDIUM** — a *single stronger* stressor, or a mild stressor placed in a *high‑intrinsic*
  phase, or *two mild concurrent* demands. Shorter windows, higher turbulence intensity, higher
  event rate. *(e.g., F8, F9, G2, H2, D2, E2.)*
- **HIGH** — *compounded/simultaneous* stressors, emergencies, startle in busy phases, go‑around
  and engine‑out, high event‑rate multitasking (≈20+/min). *(e.g., C2/C4, H4/H5, I2/I3, J2, F10‑high.)*

Cross‑cutting: run a **resting baseline each session**, keep single‑stressor events **isolated
and time‑stamped**, present tiers **within‑subject in counterbalanced order**, and log **EEG +
HRV + pupil markers against the same event timestamps**.

---

## 6. References (verified)

**Workload theory & measurement**
- Hart, S.G., & Staveland, L.E. (1988). *Development of NASA‑TLX.* In Hancock & Meshkati (Eds.),
  Human Mental Workload, 139–183. NASA‑TLX manual: <https://ntrs.nasa.gov/api/citations/20000021488/downloads/20000021488.pdf>
- Comstock, J.R., & Arnegard, R.J. (1992). *The Multi‑Attribute Task Battery.* NASA TM‑104174.
  Santiago‑Espada, Y. et al. (2011). *MATB‑II User's Guide.* NASA/TM‑2011‑217164: <https://ntrs.nasa.gov/api/citations/20110014456/downloads/20110014456.pdf>
- Wickens, C.D. (2008). *Multiple Resources and Mental Workload.* Human Factors, 50(3), 449–455. <https://journals.sagepub.com/doi/10.1518/001872008X288394>
- O'Donnell, R.D., & Eggemeier, F.T. (1986). *Workload assessment methodology.* In Handbook of Perception & Human Performance, Vol. II.
- Paas, F. (1992). *Cognitive‑load mental‑effort scale.* J. Educational Psychology, 84(4), 429–434.
- Klepsch, M., Schmitz, F., & Seufert, T. (2017). *Measuring intrinsic/extraneous/germane load.* Front. Psychology, 8:1997. <https://www.frontiersin.org/articles/10.3389/fpsyg.2017.01997/full>
- EUROCONTROL/FSF OGHFA, *Workload*: <https://skybrary.aero/articles/workload-oghfa-bn>

**EEG / physiological indices**
- Borghini, G. et al. (2014). *Measuring neurophysiological signals in pilots/drivers…* Neurosci. Biobehav. Rev., 44, 58–75. <https://pubmed.ncbi.nlm.nih.gov/23116991/>
- Gevins, A., & Smith, M.E. (2000). *Neurophysiological measures of working memory…* Cerebral Cortex, 10(9), 829–839. <https://pubmed.ncbi.nlm.nih.gov/10982744/>
- Dehais, F. et al. (2020). *A Neuroergonomics Approach to Mental Workload, Engagement and Performance.* Front. Neuroscience, 14:268. <https://pmc.ncbi.nlm.nih.gov/articles/PMC7154497/>
- Pope, A.T., Bogart, E.H., & Bartolome, D.S. (1995). *Biocybernetic system… engagement index.* Biological Psychology, 40, 187–195.
- Raufi, B., & Longo, L. (2022). *EEG alpha‑to‑theta / theta‑to‑alpha ratios as workload indexes.* Front. Neuroinformatics, 16:861967. <https://pmc.ncbi.nlm.nih.gov/articles/PMC9149374/>
- Rodríguez‑Bermúdez, G. et al. (2025). *P300 to determine pilot cognitive states.* Sensors, 25(19):6201. <https://pmc.ncbi.nlm.nih.gov/articles/PMC12526825/>
- Zhou, Xu, & Zhang (2025). *Cognitive load recognition in simulated flight (EEG, MATB).* Front. Human Neuroscience, 19:1542774. <https://www.frontiersin.org/articles/10.3389/fnhum.2025.1542774/full>
- Parasuraman, R., & Rizzo, M. (2007). *Neuroergonomics: The Brain at Work.* Oxford Univ. Press. (Mehta & Parasuraman 2013, Front. Hum. Neurosci. 7:889.)

**Phases of flight & workload distribution**
- CAST/ICAO CICTT, *Phase of Flight — Definitions and Usage Notes.* ICAO/ECCAIRS: <https://www.icao.int/sites/default/files/airnavigation/AIG/ECCAIRS-Aviation-1.3.0.12-VL-for-AttrID-391-Event-Phases.pdf> · overview: <https://skybrary.aero/articles/casticao-common-taxonomy-team-cictt>
- Boeing (2025). *Statistical Summary of Commercial Jet Airplane Accidents, 1959–2024.* <https://www.boeing.com/content/dam/boeing/v2/safety/statsum.pdf>
- Yuan et al. (2025). *Pilot mental workload in the A320 traffic pattern (HRV).* Front. Neuroergonomics, 6:1672492. <https://doi.org/10.3389/fnrgo.2025.1672492>
- Werfelman, L. (2020). *Reversal.* Flight Safety Foundation, AeroSafety World. <https://flightsafety.org/asw-article/reversal-2/>

**Acute triggers / stressors**
- Landman, A. et al. (2017). *Dealing With Unexpected Events on the Flight Deck… Startle and Surprise.* Human Factors. <https://journals.sagepub.com/doi/10.1177/0018720817723428>
- EASA (2018). *Startle Effect Management*, Final Report EASA_REP_RESEA_2015_3. <https://www.easa.europa.eu/sites/default/files/dfu/EASA_Research_Startle_Effect_Managements_Final_Report.pdf>
- Martin, W.L. et al. (2016). *Impairment effects of startle on pilots during unexpected critical events.* Aviation Psychology & Applied Human Factors.
- Sarter, N.B., & Woods, D.D. (1997). *Automation Surprises.* In Handbook of Human Factors & Ergonomics.
- NASA (2017). *Pilot Contribution to Flight Safety During an In‑Flight [Unreliable Airspeed] Failure.* NTRS 20170005471. <https://ntrs.nasa.gov/api/citations/20170005471/downloads/20170005471.pdf>
- IATA, *Assessment of Pilot Compliance to TCAS.* <https://www.iata.org/contentassets/582fbe33f31240938bcf9f33d4b3d0a1/iata_guidance_assessment_of_pilot_compliance_to_tcas.pdf>
- Vivaldi, B.E. (2004). *Effect of Crosswind and Turbulence on Mental Workload and Tracking.* MS thesis, Embry‑Riddle. <https://commons.erau.edu/db-theses/207/>
- Blajev, T., & Curtis, W. (2017). *Go‑Around Decision‑Making and Execution Project.* Flight Safety Foundation. <https://flightsafety.org/wp-content/uploads/2017/03/Go-around-study_final.pdf>
- ICRAT (2018). *In Search of the Upper Limit to ATC Communication.* <https://bpb-us-e1.wpmucdn.com/blog.umd.edu/dist/9/604/files/2019/02/ICRAT-2018-PAPER-upper-limit-1joq1m6.pdf>
- NASA/TM‑2015‑218930. *Effects of Acute Stress on Aircrew Performance.*

**Experimental scenario design**
- Pontiggia, A. et al. (2024). *Examining mental workload with the MATB technique (review).* <https://pmc.ncbi.nlm.nih.gov/articles/PMC11300324/>
- Hamann, A., & Carstengerdes, N. (2022). *Mental‑workload changes in cortical oxygenation & frontal theta during simulated flights.* Sci. Reports, 12:6449. <https://doi.org/10.1038/s41598-022-10044-y>
- Hernández‑Sabaté, A. et al. (2024). *EEG dataset for mental‑workload prediction in a flight‑deck environment.* Sensors, 24(4):1174. <https://pmc.ncbi.nlm.nih.gov/articles/PMC10891818/>
- Taheri Gorji, H. et al. (2023). *ML + EEG to discriminate pilot cognitive workload during flight.* Sci. Reports, 13:2507. <https://doi.org/10.1038/s41598-023-29647-0>
- Line‑Oriented Flight Training (LOFT): <https://skybrary.aero/articles/line-oriented-flight-training>

> **Verification notes for the paper:** a few items the research flagged as *unverified at
> page/author level* — confirm before final submission: the exact page numbers of Kramer et al.
> (1988) and Gevins et al. (1997); the venue of Pontiggia et al. (2024); the author list of
> NASA/TM‑2015‑218930. **Do not** cite any "NASA‑TLX‑by‑phase" numeric table (e.g.
> 74.6/70.7/68.1/52.6) — that figure was traced to a search‑summary artifact and does not appear
> in the source; use Yuan et al. (2025) HRV/performance data and Boeing (2025) accident shares
> for the phase‑workload claim instead.
