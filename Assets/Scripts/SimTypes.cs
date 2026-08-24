// Shared enums used across the simulator.

/// <summary>Top-level app state. Baseline was added for the cognitive-load
/// experiment: a resting block is not "flying" (the aircraft is frozen and the
/// controls are inert) but it is not a menu either — it is recorded data.</summary>
public enum GameState { Menu, Flying, Results, Baseline }

public enum FlightMode
{
    FreeFlight,   // sandbox: take off, fly anywhere, R to reset
    L0_Baseline,  // calm straight & level (already trimmed in the air)
    L1_Takeoff,   // start on runway, take off, climb to target altitude
    L2_LevelHold  // hold assigned altitude & heading for a duration
}

public enum GaugeType { Airspeed, Altimeter, Heading, VerticalSpeed, TurnRate }
