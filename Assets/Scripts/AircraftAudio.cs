using UnityEngine;

/// <summary>
/// Procedural aircraft sound layer — every clip is synthesised in C# at runtime (no
/// audio asset files), the same approach as StallHorn. Continuous ENGINE + WIND +
/// GROUND-ROLL loops whose pitch/volume track the live flight state, plus one-shot
/// TOUCHDOWN, CRASH and scenario-ALARM sounds. All 2D so they're heard in any camera
/// view. Only audible while Flying. (The stall horn is a separate component.)
/// </summary>
public class AircraftAudio : MonoBehaviour
{
    public CessnaPhysics phys;

    AudioSource engine, wind, ground, oneShot;
    AudioClip touchdownClip, crashClip, alarmClip;
    bool wasGrounded, wasCrashed, wasAlarm;

    const int SR = 44100;

    void Start()
    {
        engine = MakeLoop("Engine", EngineClip());
        wind   = MakeLoop("Wind",   WindClip());
        ground = MakeLoop("Ground", GroundClip());

        var os = new GameObject("SfxOneShot");
        os.transform.SetParent(transform);
        os.transform.localPosition = Vector3.zero;
        oneShot = os.AddComponent<AudioSource>();
        oneShot.spatialBlend = 0f;
        oneShot.playOnAwake = false;

        touchdownClip = TouchdownClip();
        crashClip     = CrashClip();
        alarmClip     = AlarmClip();
    }

    AudioSource MakeLoop(string name, AudioClip clip)
    {
        var go = new GameObject("Sfx" + name);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.spatialBlend = 0f;
        s.playOnAwake = false; s.volume = 0f;
        s.Play();   // always running; volume gates audibility (no start/stop pops)
        return s;
    }

    void Update()
    {
        if (phys == null) return;
        bool inFlight = GameManager.Instance != null && GameManager.Instance.State == GameState.Flying;
        bool flying = inFlight && !phys.Crashed;

        // duck the engine/wind/ground to 35% while a voice callout is speaking, so it's clear
        float duck = (VoiceCallouts.Instance != null && VoiceCallouts.Instance.Speaking) ? 0.35f : 1f;

        // engine — pitch + volume rise with throttle; a little idle even at 0
        float thr = phys.Throttle01;
        if (engine)
        {
            engine.pitch  = Mathf.Lerp(0.75f, 1.5f, thr);
            // This line used to read `engine.volume = 0f;  // (per request)`, a
            // development convenience that would have shipped. Engine sound is a
            // WORKLOAD-RELEVANT CUE — rough running and partial power loss are heard
            // before they are seen, and two missions depend on exactly that — so
            // whether it is audible is now a declared, recorded condition rather than a
            // hard-coded zero. See AudioPolicy.cs.
            engine.volume = Mathf.Lerp(0.10f, 0.42f, thr) * duck * AudioPolicy.EngineGain;
        }

        // wind — swells with airspeed
        float spd = phys.AirspeedMs;
        if (wind) wind.volume = flying ? Mathf.Clamp01(spd / 75f) * 0.35f * duck * AudioPolicy.WindGain : 0f;

        // ground roll — rumble while the wheels are down and moving
        if (ground)
        {
            bool rolling = flying && phys.Grounded && spd > 1.5f;
            float k = Mathf.Clamp01(spd / 40f);
            ground.volume = rolling ? k * 0.4f * duck * AudioPolicy.GroundGain : 0f;
            ground.pitch  = Mathf.Lerp(0.8f, 1.3f, k);
        }

        // touchdown — gear meets the ground (rising edge), louder with sink rate
        if (phys.Grounded && !wasGrounded && inFlight && oneShot && touchdownClip)
        {
            float v = Mathf.Clamp01(phys.TouchdownSink / 4f);
            oneShot.PlayOneShot(touchdownClip, (0.4f + v * 0.5f) * AudioPolicy.WarningGain);
        }
        wasGrounded = phys.Grounded;

        // crash bang (rising edge)
        if (phys.Crashed && !wasCrashed && oneShot && crashClip)
            oneShot.PlayOneShot(crashClip, 0.9f * AudioPolicy.WarningGain);
        wasCrashed = phys.Crashed;

        // scenario master-caution beep when an alarm appears (rising edge)
        var eng = GameManager.Instance != null ? GameManager.Instance.ScenarioRunner : null;
        bool alarm = eng != null && eng.Active && eng.AlarmActive;
        if (alarm && !wasAlarm && oneShot && alarmClip) oneShot.PlayOneShot(alarmClip, 0.6f * AudioPolicy.WarningGain);
        wasAlarm = alarm;
    }

    // ---- procedurally-synthesised clips ----

    static AudioClip EngineClip()
    {
        int len = SR;                          // 1 s, integer cycles -> seamless loop
        var d = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            // stacked sawtooths ≈ a buzzy prop engine (70 Hz fundamental + harmonics)
            d[i] = (0.5f * Saw(70f, t) + 0.3f * Saw(140f, t) + 0.18f * Saw(210f, t)) * 0.5f;
        }
        return Clip("engine", d);
    }

    static AudioClip WindClip()
    {
        int len = SR;
        var d = new float[len];
        float prev = 0f;
        var rng = new System.Random(12345);
        for (int i = 0; i < len; i++)
        {
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = Mathf.Lerp(prev, n, 0.15f);   // low-pass -> airy hiss
            d[i] = prev * 0.6f;
        }
        return Clip("wind", d);
    }

    static AudioClip GroundClip()
    {
        int len = SR;
        var d = new float[len];
        float prev = 0f;
        var rng = new System.Random(777);
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = Mathf.Lerp(prev, n, 0.05f);   // heavy low-pass -> rumble
            d[i] = (prev * 0.7f + Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.4f) * 0.6f;
        }
        return Clip("ground", d);
    }

    static AudioClip TouchdownClip()
    {
        int len = SR / 3;                        // ~0.33 s
        var d = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Exp(-t * 14f);
            float thud = Mathf.Sin(2f * Mathf.PI * 60f * t);
            float squeak = Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.2f;   // tyre chirp
            d[i] = (thud + squeak) * env * 0.8f;
        }
        return Clip("touchdown", d);
    }

    static AudioClip CrashClip()
    {
        int len = (int)(SR * 0.6f);
        var d = new float[len];
        var rng = new System.Random(9);
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Exp(-t * 6f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            d[i] = (noise * 0.7f + Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.5f) * env;
        }
        return Clip("crash", d);
    }

    static AudioClip AlarmClip()
    {
        int len = (int)(SR * 0.5f);              // a short double-beep
        var d = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float beat = (t < 0.12f || (t > 0.20f && t < 0.32f)) ? 1f : 0f;
            d[i] = Mathf.Sin(2f * Mathf.PI * 1000f * t) * 0.3f * beat;
        }
        return Clip("alarm", d);
    }

    static float Saw(float f, float t) { float x = t * f; return 2f * (x - Mathf.Floor(x + 0.5f)); }

    static AudioClip Clip(string name, float[] data)
    {
        var c = AudioClip.Create(name, data.Length, 1, SR, false);
        c.SetData(data, 0);
        return c;
    }
}
