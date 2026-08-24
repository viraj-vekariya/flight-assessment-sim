// SystemsInput — the pilot's switches for the abnormal-procedure missions.
//
// The abnormal missions are only meaningful if the pilot can actually DO the
// memory items. Before this, the sim had flight controls but no systems controls,
// so a checklist could only ever be acknowledged, never performed. These four keys
// are the minimum set the twelve missions need:
//
//     H   CARBURETTOR HEAT      on / off      (fixes induction icing)
//     J   FUEL SELECTOR         BOTH -> L -> R -> BOTH
//     K   ELECTRICAL LOAD SHED  on / off      (buys battery time)
//     L   ALTERNATE STATIC      open / closed (fixes a blocked static source)
//
// Every press goes through AircraftSystems.Set*, which raises OnConfigChange, which
// ScenarioEngine logs as a CONFIGURATION_CHANGE marker. So the switch actions are
// timestamped to the same clock as the EEG markers, and "when did the pilot apply
// carb heat" is answerable to the millisecond without asking them.
//
// Deliberately NOT bound: anything the flight model does not simulate (magnetos,
// mixture, primer, master switch). A key that pretends to do something the sim does
// not model would produce fake behavioural data.

using UnityEngine;

public class SystemsInput : MonoBehaviour
{
    AircraftSystems sys;

    void Awake() => sys = GetComponent<AircraftSystems>();

    void Update()
    {
        if (sys == null) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Flying) return;

        if (Input.GetKeyDown(KeyCode.H)) sys.SetCarbHeat(!sys.CarbHeatOn);

        if (Input.GetKeyDown(KeyCode.J))
        {
            var next = sys.Selector == FuelSelector.Both ? FuelSelector.Left
                     : sys.Selector == FuelSelector.Left ? FuelSelector.Right
                                                         : FuelSelector.Both;
            sys.SetSelector(next);
        }

        if (Input.GetKeyDown(KeyCode.K)) sys.SetLoadShed(!sys.LoadShed);
        if (Input.GetKeyDown(KeyCode.L)) sys.SetAlternateStatic(!sys.AlternateStaticOpen);
    }
}
